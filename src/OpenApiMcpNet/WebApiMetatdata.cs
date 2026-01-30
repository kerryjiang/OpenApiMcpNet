using Microsoft.OpenApi.Models;

namespace OpenApiMcpNet;

public class WebApiMetadata
{
    public string BaseUrl { get; }

    public string Path { get; }

    public OperationType OperationType { get; }

    public OpenApiOperation Operation { get; }

    public WebApiMetadata(string baseUrl, string path, OperationType operationType, OpenApiOperation operation)
    {
        BaseUrl = baseUrl;
        Path = path;
        OperationType = operationType;
        Operation = operation;
    }
}