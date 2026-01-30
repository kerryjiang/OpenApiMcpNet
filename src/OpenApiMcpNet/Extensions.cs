using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using ModelContextProtocol.Server;

namespace OpenApiMcpNet;

public static class Extensions
{
    /// <summary>
    /// Registers MCP tools from an OpenAPI specification.
    /// </summary>
    /// <param name="builder">The MCP server builder to add the tools to.</param>
    /// <param name="openApiSpec">The OpenAPI specification as a string (JSON or YAML).</param>
    /// <param name="baseUrl">The base URL for the API.</param>
    /// <returns>The builder for chaining.</returns>
    public static IMcpServerBuilder WithToolsFromOpenApi(this IMcpServerBuilder builder, string openApiSpec, string baseUrl)
    {
        var reader = new OpenApiStringReader();
        var openApiDocument = reader.Read(openApiSpec, out var diagnostic);

        if (diagnostic.Errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Failed to parse OpenAPI spec: {string.Join(", ", diagnostic.Errors.Select(e => e.Message))}");
        }

        return WithToolsFromOpenApi(builder, openApiDocument, baseUrl);
    }

    /// <summary>
    /// Registers MCP tools from an OpenAPI specification.
    /// </summary>
    /// <param name="builder">The MCP server builder to add the tools to.</param>
    /// <param name="openApiSpecStream">The OpenAPI specification as a stream (JSON or YAML).</param>
    /// <param name="baseUrl">The base URL for the API.</param>
    /// <returns>The builder for chaining.</returns>
    public static IMcpServerBuilder WithToolsFromOpenApi(this IMcpServerBuilder builder, Stream openApiSpecStream, string baseUrl)
    {
        var reader = new OpenApiStreamReader();
        var openApiDocument = reader.Read(openApiSpecStream, out var diagnostic);

        if (diagnostic.Errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Failed to parse OpenAPI spec: {string.Join(", ", diagnostic.Errors.Select(e => e.Message))}");
        }

        return WithToolsFromOpenApi(builder, openApiDocument, baseUrl);
    }

    /// <summary>
    /// Registers MCP tools from an OpenAPI document.
    /// </summary>
    /// <param name="builder">The MCP server builder to add the tools to.</param>
    /// <param name="openApiDocument">The parsed OpenAPI document.</param>
    /// <param name="baseUrl">The base URL for the API.</param>
    /// <param name="webApiCaller">The web API caller to use for making HTTP requests.</param>
    /// <returns>The builder for chaining.</returns>
    public static IMcpServerBuilder WithToolsFromOpenApi(this IMcpServerBuilder builder, OpenApiDocument openApiDocument, string baseUrl)
    {
        builder.Services.TryAddSingleton<IWebApiCaller, WebApiCaller>();

        foreach (var path in openApiDocument.Paths)
        {
            foreach (var operation in path.Value.Operations)
            {
                builder.Services.AddSingleton(sp =>
                {
                    var webApiCaller = sp.GetRequiredService<IWebApiCaller>();
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    return CreateToolFromOperation(baseUrl, path.Key, operation.Key, operation.Value, webApiCaller, loggerFactory);
                });
            }
        }

        return builder;
    }

    private static McpServerTool CreateToolFromOperation(string baseUrl, string path, OperationType operationType, OpenApiOperation operation, IWebApiCaller webApiCaller, ILoggerFactory loggerFactory)
    {
        var rawToolName = operation.OperationId ?? $"{operationType}_{path.Replace("/", "_").TrimStart('_')}";
        var toolName = SanitizeToolName(rawToolName);
        var description = operation.Summary ?? operation.Description ?? $"{operationType} {path}";
        var webApiMetadata = new WebApiMetadata(baseUrl, path, operationType, operation);
        
        return new OpenApiTool(toolName, description, webApiMetadata, webApiCaller, loggerFactory.CreateLogger(toolName));
    }

    /// <summary>
    /// Sanitizes a tool name to comply with MCP tool name requirements.
    /// MCP tool names must match: ^[a-zA-Z0-9_-]{1,64}$
    /// </summary>
    /// <param name="name">The raw tool name.</param>
    /// <returns>A sanitized tool name that complies with MCP requirements.</returns>
    private static string SanitizeToolName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "unnamed_tool";
        }

        // Replace invalid characters with underscores
        var sanitized = new System.Text.StringBuilder(name.Length);
        
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
            {
                sanitized.Append(c);
            }
            else
            {
                sanitized.Append('_');
            }
        }

        var result = sanitized.ToString();

        // Ensure it doesn't start with a digit (some systems have issues with this)
        if (result.Length > 0 && char.IsDigit(result[0]))
        {
            result = "_" + result;
        }

        // Trim consecutive underscores
        while (result.Contains("__"))
        {
            result = result.Replace("__", "_");
        }

        // Trim leading/trailing underscores
        result = result.Trim('_');

        // Ensure minimum length
        if (string.IsNullOrEmpty(result))
        {
            result = "unnamed_tool";
        }

        // Truncate to max 64 characters
        if (result.Length > 64)
        {
            result = result.Substring(0, 64).TrimEnd('_', '-');
        }

        return result;
    }
}
