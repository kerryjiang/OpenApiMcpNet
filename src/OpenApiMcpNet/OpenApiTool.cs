using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi.Models;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;
using Microsoft.Extensions.Logging;

namespace OpenApiMcpNet;

internal class OpenApiTool : McpServerTool
{
    private readonly WebApiMetadata _webApiMetadata;
    private readonly IWebApiCaller _webApiCaller;

    public override Tool ProtocolTool { get; }
    
    public override IReadOnlyList<object> Metadata { get; }

    private readonly ILogger _logger;

    public OpenApiTool(string name, string description, WebApiMetadata webApiMetadata, IWebApiCaller webApiCaller, ILogger logger)
    {
        _webApiMetadata = webApiMetadata;
        _webApiCaller = webApiCaller;
        _logger = logger;
        
        // Set up the protocol tool
        ProtocolTool = new Tool
        {
            Name = name,
            Description = description,
            InputSchema = BuildInputSchema()
        };
        
        // Build metadata from OpenAPI operation
        Metadata = BuildMetadata();
    }

    private IReadOnlyList<object> BuildMetadata()
    {
        var metadata = new List<object>();
        var operation = _webApiMetadata.Operation;

        // Add operation metadata
        var operationMetadata = new Dictionary<string, object>
        {
            ["path"] = _webApiMetadata.Path,
            ["method"] = _webApiMetadata.OperationType.ToString().ToUpperInvariant()
        };

        if (operation.OperationId != null)
        {
            operationMetadata["operationId"] = operation.OperationId;
        }

        if (operation.Deprecated)
        {
            operationMetadata["deprecated"] = true;
        }

        if (operation.Tags?.Count > 0)
        {
            operationMetadata["tags"] = operation.Tags.Select(t => t.Name).ToList();
        }

        if (operation.ExternalDocs != null)
        {
            operationMetadata["externalDocs"] = new Dictionary<string, string>
            {
                ["url"] = operation.ExternalDocs.Url?.ToString() ?? string.Empty,
                ["description"] = operation.ExternalDocs.Description ?? string.Empty
            };
        }

        if (operation.Responses?.Count > 0)
        {
            var responses = new Dictionary<string, string>();
            foreach (var response in operation.Responses)
            {
                responses[response.Key] = response.Value.Description ?? string.Empty;
            }
            operationMetadata["responses"] = responses;
        }

        // Add OpenAPI extensions if present
        if (operation.Extensions?.Count > 0)
        {
            var extensions = new Dictionary<string, object>();
            foreach (var ext in operation.Extensions)
            {
                // Extensions are typically x-* properties
                try
                {
                    extensions[ext.Key] = ext.Value?.ToString() ?? string.Empty;
                }
                catch
                {
                    // Skip extensions that can't be serialized
                }
            }
            if (extensions.Count > 0)
            {
                operationMetadata["extensions"] = extensions;
            }
        }

        metadata.Add(operationMetadata);
        return metadata;
    }

    public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
    {
        var arguments = request.Params?.Arguments ?? new Dictionary<string, JsonElement>();
        return await InvokeAsync(arguments, cancellationToken);
    }

    /// <summary>
    /// Invokes the tool with the given arguments. This method is public for testing purposes.
    /// </summary>
    public async ValueTask<CallToolResult> InvokeAsync(IDictionary<string, JsonElement> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _webApiCaller.CallApiAsync(_webApiMetadata, arguments, cancellationToken);
            
            var result = new CallToolResult
            {
                Content = new[]
                {
                    new TextContentBlock
                    {
                        Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            };
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking OpenApiTool {ToolName}", ProtocolTool.Name);

            return new CallToolResult
            {
                IsError = true,
                Content = new[]
                {
                    new TextContentBlock
                    {
                        Text = $"Error calling API: {ex.Message}"
                    }
                }
            };
        }
    }

    private JsonElement BuildInputSchema()
    {
        var operation = _webApiMetadata.Operation;
        var schemaObj = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject()
        };

        var properties = schemaObj["properties"]!.AsObject();
        var requiredProperties = new List<string>();

        // Add parameters from path, query, header, and cookie
        if (operation.Parameters != null)
        {
            foreach (var parameter in operation.Parameters)
            {
                if (parameter.Schema != null)
                {
                    var propertySchema = ConvertOpenApiSchemaToJsonNode(parameter.Schema);
                    if (propertySchema is JsonObject propObj && !string.IsNullOrEmpty(parameter.Description ?? parameter.Schema.Description))
                    {
                        propObj["description"] = parameter.Description ?? parameter.Schema.Description;
                    }
                    
                    properties[parameter.Name] = propertySchema;

                    if (parameter.Required)
                    {
                        requiredProperties.Add(parameter.Name);
                    }
                }
            }
        }

        // Add request body if present
        if (operation.RequestBody != null)
        {
            var jsonContent = operation.RequestBody.Content.FirstOrDefault(c => 
                c.Key.Contains("json", StringComparison.OrdinalIgnoreCase)).Value;

            if (jsonContent?.Schema != null)
            {
                // For request body, we can either flatten the schema or add it as a single "body" parameter
                var bodySchema = ConvertOpenApiSchemaToJsonNode(jsonContent.Schema);
                
                if (jsonContent.Schema.Type == "object" && jsonContent.Schema.Properties?.Count > 0)
                {
                    // Flatten object properties into the main schema
                    foreach (var prop in jsonContent.Schema.Properties)
                    {
                        properties[prop.Key] = ConvertOpenApiSchemaToJsonNode(prop.Value);
                        
                        if (jsonContent.Schema.Required?.Contains(prop.Key) == true)
                        {
                            requiredProperties.Add(prop.Key);
                        }
                    }
                }
                else
                {
                    properties["body"] = bodySchema;
                    if (operation.RequestBody.Required)
                    {
                        requiredProperties.Add("body");
                    }
                }
            }
        }

        if (requiredProperties.Count > 0)
        {
            schemaObj["required"] = new JsonArray(requiredProperties.Select(r => JsonValue.Create(r)).ToArray()!);
        }

        return JsonSerializer.SerializeToElement(schemaObj);
    }

    private JsonNode ConvertOpenApiSchemaToJsonNode(OpenApiSchema openApiSchema)
    {
        var schema = new JsonObject();

        if (!string.IsNullOrEmpty(openApiSchema.Description))
        {
            schema["description"] = openApiSchema.Description;
        }

        // Map OpenAPI types to JSON schema types
        var type = openApiSchema.Type switch
        {
            "string" => "string",
            "integer" => "integer",
            "number" => "number",
            "boolean" => "boolean",
            "array" => "array",
            "object" => "object",
            _ => openApiSchema.Type
        };

        if (!string.IsNullOrEmpty(type))
        {
            schema["type"] = type;
        }

        // Handle enum values
        if (openApiSchema.Enum?.Count > 0)
        {
            schema["enum"] = new JsonArray(openApiSchema.Enum.Select(e => JsonValue.Create(e.ToString())).ToArray()!);
        }

        // Handle array items
        if (openApiSchema.Type == "array" && openApiSchema.Items != null)
        {
            schema["items"] = ConvertOpenApiSchemaToJsonNode(openApiSchema.Items);
        }

        // Handle object properties
        if (openApiSchema.Type == "object" && openApiSchema.Properties?.Count > 0)
        {
            var properties = new JsonObject();
            foreach (var prop in openApiSchema.Properties)
            {
                properties[prop.Key] = ConvertOpenApiSchemaToJsonNode(prop.Value);
            }
            schema["properties"] = properties;

            if (openApiSchema.Required?.Count > 0)
            {
                schema["required"] = new JsonArray(openApiSchema.Required.Select(r => JsonValue.Create(r)).ToArray()!);
            }
        }

        // Add format if present
        if (!string.IsNullOrEmpty(openApiSchema.Format))
        {
            schema["format"] = openApiSchema.Format;
        }

        // Add constraints
        if (openApiSchema.Minimum.HasValue)
        {
            schema["minimum"] = openApiSchema.Minimum.Value;
        }

        if (openApiSchema.Maximum.HasValue)
        {
            schema["maximum"] = openApiSchema.Maximum.Value;
        }

        if (openApiSchema.MinLength.HasValue)
        {
            schema["minLength"] = openApiSchema.MinLength.Value;
        }

        if (openApiSchema.MaxLength.HasValue)
        {
            schema["maxLength"] = openApiSchema.MaxLength.Value;
        }

        if (!string.IsNullOrEmpty(openApiSchema.Pattern))
        {
            schema["pattern"] = openApiSchema.Pattern;
        }

        return schema;
    }
}
