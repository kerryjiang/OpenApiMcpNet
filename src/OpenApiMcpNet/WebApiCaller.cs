using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

namespace OpenApiMcpNet;

/// <summary>
/// Default implementation of <see cref="IWebApiCaller"/> that makes HTTP requests to web APIs.
/// </summary>
public class WebApiCaller : IWebApiCaller
{
    private readonly HttpClient _httpClient;

    private readonly IAuthenticationHandler _authenticationHandler;

    private readonly ILogger<WebApiCaller> _logger;

    /// <summary>
    /// Creates a new WebApiCaller with the specified HTTP client and authentication handler.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for making requests.</param>
    /// <param name="authenticationHandler">The authentication handler. If null, no authentication is applied.</param>
    /// <param name="logger">The logger to use for logging.</param>
    public WebApiCaller(HttpClient httpClient, IAuthenticationHandler authenticationHandler, ILogger<WebApiCaller> logger)
    {
        _httpClient = httpClient;
        _authenticationHandler = authenticationHandler;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<JsonElement> CallApiAsync(WebApiMetadata apiMetadata, IDictionary<string, JsonElement> parameters, CancellationToken cancellationToken)
    {
        var operation = apiMetadata.Operation;
        var path = apiMetadata.Path;
        var queryParams = new List<KeyValuePair<string, string>>();
        var headerParams = new Dictionary<string, string>();
        var bodyParams = new Dictionary<string, JsonElement>();

        // Categorize parameters by location (path, query, header, cookie)
        if (operation.Parameters != null)
        {
            foreach (var paramDef in operation.Parameters)
            {
                if (!parameters.TryGetValue(paramDef.Name, out var paramValue))
                {
                    continue;
                }

                var stringValue = paramValue.ToJsonString();

                switch (paramDef.In)
                {
                    case ParameterLocation.Path:
                        path = path.Replace($"{{{paramDef.Name}}}", Uri.EscapeDataString(stringValue));
                        break;
                    case ParameterLocation.Query:
                        queryParams.Add(new KeyValuePair<string, string>(paramDef.Name, stringValue));
                        break;
                    case ParameterLocation.Header:
                        headerParams[paramDef.Name] = stringValue;
                        break;
                    case ParameterLocation.Cookie:
                        // Cookies are handled via headers
                        if (headerParams.TryGetValue("Cookie", out var existingCookies))
                        {
                            headerParams["Cookie"] = $"{existingCookies}; {paramDef.Name}={stringValue}";
                        }
                        else
                        {
                            headerParams["Cookie"] = $"{paramDef.Name}={stringValue}";
                        }
                        break;
                }
            }
        }

        // Collect body parameters (parameters not defined in operation.Parameters)
        var definedParamNames = operation.Parameters?.Select(p => p.Name).ToHashSet() ?? new HashSet<string>();
        
        foreach (var param in parameters)
        {
            if (!definedParamNames.Contains(param.Key))
            {
                bodyParams[param.Key] = param.Value;
            }
        }

        // Build the full URL
        var urlBuilder = new StringBuilder(apiMetadata.BaseUrl.TrimEnd('/'));
        urlBuilder.Append('/');
        urlBuilder.Append(path.TrimStart('/'));

        if (queryParams.Count > 0)
        {
            urlBuilder.Append('?');
            urlBuilder.Append(string.Join("&", queryParams.Select(kvp => 
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}")));
        }

        var url = urlBuilder.ToString();

        // Create the HTTP request
        var httpMethod = GetHttpMethod(apiMetadata.OperationType);
        var request = new HttpRequestMessage(httpMethod, url);

        // Add headers
        foreach (var header in headerParams)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Add request body if applicable
        if (bodyParams.Count > 0 && SupportsRequestBody(apiMetadata.OperationType))
        {
            object bodyContent;

            // If there's a single "body" parameter, use it directly
            if (bodyParams.Count == 1 && bodyParams.TryGetValue("body", out var bodyValue))
            {
                bodyContent = bodyValue;
            }
            else
            {
                // Otherwise, send all body parameters as an object
                bodyContent = bodyParams;
            }

            request.Content = new StringContent(
                JsonSerializer.Serialize(bodyContent),
                Encoding.UTF8,
                "application/json");
        }

        // Authenticate the request
        _authenticationHandler.AuthenticateRequest(request, queryParams, bodyParams);

        _logger.LogInformation("Sending {Method} request to {Url}", httpMethod, url);

        // Send the request
        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
          {      
            //var failureResponseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogError("API call to {Url} failed with status code {StatusCode}", url, response.StatusCode);

            throw new HttpRequestException(
                message: $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}",
                inner: null,
                statusCode: response.StatusCode);
        }

        // Read the response
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        // Parse response as JSON
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return JsonSerializer.SerializeToElement(new { success = true, statusCode = (int)response.StatusCode });
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(responseContent);
        }
        catch (JsonException)
        {
            // If response is not valid JSON, wrap it in a JSON object
            return JsonSerializer.SerializeToElement(new { content = responseContent, statusCode = (int)response.StatusCode });
        }
    }

    private static HttpMethod GetHttpMethod(OperationType operationType)
    {
        return operationType switch
        {
            OperationType.Get => HttpMethod.Get,
            OperationType.Post => HttpMethod.Post,
            OperationType.Put => HttpMethod.Put,
            OperationType.Delete => HttpMethod.Delete,
            OperationType.Patch => HttpMethod.Patch,
            OperationType.Head => HttpMethod.Head,
            OperationType.Options => HttpMethod.Options,
            OperationType.Trace => HttpMethod.Trace,
            _ => throw new ArgumentOutOfRangeException(nameof(operationType), $"Unsupported operation type: {operationType}")
        };
    }

    private static bool SupportsRequestBody(OperationType operationType)
    {
        return operationType switch
        {
            OperationType.Post => true,
            OperationType.Put => true,
            OperationType.Patch => true,
            _ => false
        };
    }
}
