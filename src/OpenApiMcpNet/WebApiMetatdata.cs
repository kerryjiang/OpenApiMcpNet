using Microsoft.OpenApi.Models;

namespace OpenApiMcpNet;

/// <summary>
/// Contains metadata about a web API endpoint derived from an OpenAPI specification.
/// </summary>
public class WebApiMetadata
{
    /// <summary>
    /// Gets the base URL of the API.
    /// </summary>
    public string BaseUrl { get; }

    /// <summary>
    /// Gets the path of the API endpoint (e.g., "/users/{id}").
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the HTTP operation type (GET, POST, PUT, DELETE, etc.).
    /// </summary>
    public OperationType OperationType { get; }

    /// <summary>
    /// Gets the OpenAPI operation definition containing parameter and response schemas.
    /// </summary>
    public OpenApiOperation Operation { get; }

    /// <summary>
    /// Creates a new instance of <see cref="WebApiMetadata"/>.
    /// </summary>
    /// <param name="baseUrl">The base URL of the API.</param>
    /// <param name="path">The path of the API endpoint.</param>
    /// <param name="operationType">The HTTP operation type.</param>
    /// <param name="operation">The OpenAPI operation definition.</param>
    public WebApiMetadata(string baseUrl, string path, OperationType operationType, OpenApiOperation operation)
    {
        BaseUrl = baseUrl;
        Path = path;
        OperationType = operationType;
        Operation = operation;
    }
}