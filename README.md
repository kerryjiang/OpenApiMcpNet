# OpenApiMcpNet

A .NET library that automatically generates [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) tools from OpenAPI specifications. This allows AI assistants and LLMs to interact with any REST API that has an OpenAPI (Swagger) specification.

## Features

- **Automatic Tool Generation**: Converts OpenAPI operations into MCP-compatible tools automatically
- **Full OpenAPI Support**: Handles path, query, header, and cookie parameters, as well as request bodies
- **Authentication Support**: Built-in support for OAuth 1.0a and OAuth 2.0 (client credentials flow)
- **Extensible Architecture**: Custom authentication handlers and web API callers can be injected
- **Fluent API**: Simple, chainable configuration via `IMcpServerBuilder` extensions

## Installation

```bash
dotnet add package OpenApiMcpNet
```

## Quick Start

### Basic Usage

```csharp
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using OpenApiMcpNet;

var builder = Host.CreateApplicationBuilder(args);

// Add MCP server with tools from OpenAPI spec
builder.Services.AddMcpServer()
    .WithToolsFromOpenApi(openApiSpecJson, "https://api.example.com");

var host = builder.Build();
await host.RunAsync();
```

### Loading OpenAPI Spec from File or URL

```csharp
// From a string
builder.Services.AddMcpServer()
    .WithToolsFromOpenApi(openApiSpecJsonOrYaml, "https://api.example.com");

// From a stream
using var stream = File.OpenRead("openapi.yaml");
builder.Services.AddMcpServer()
    .WithToolsFromOpenApi(stream, "https://api.example.com");

// From an OpenApiDocument
var reader = new OpenApiStringReader();
var document = reader.Read(openApiSpec, out var diagnostic);
builder.Services.AddMcpServer()
    .WithToolsFromOpenApi(document, "https://api.example.com");
```

## Authentication

### OAuth 2.0 (Client Credentials)

```csharp
var authHandler = new OAuth2AuthenticationHandler(
    httpClient,
    tokenEndpoint: "https://auth.example.com/oauth/token",
    consumerKey: "your-client-id",
    consumerSecret: "your-client-secret",
    scope: "read write" // optional
);

// Authenticate before making requests
await authHandler.AuthenticateAsync();

// Register with DI
builder.Services.AddSingleton<IAuthenticationHandler>(authHandler);
```

### OAuth 1.0a

```csharp
var authHandler = new OAuth1AuthenticationHandler(
    httpClient,
    requestTokenUrl: "https://api.example.com/oauth/request_token",
    accessTokenUrl: "https://api.example.com/oauth/access_token",
    consumerKey: "your-consumer-key",
    consumerSecret: "your-consumer-secret",
    signatureMethod: "HMAC-SHA1"
);

await authHandler.AuthenticateAsync();

builder.Services.AddSingleton<IAuthenticationHandler>(authHandler);
```

### Custom Authentication

Implement `IAuthenticationHandler` or `IRequestAuthenticationHandler` for custom authentication:

```csharp
public class ApiKeyAuthenticationHandler : IAuthenticationHandler
{
    private readonly string _apiKey;

    public bool IsAuthenticated => true;

    public ApiKeyAuthenticationHandler(string apiKey)
    {
        _apiKey = apiKey;
    }

    public Task AuthenticateAsync() => Task.CompletedTask;

    public void AuthenticateRequest(
        HttpRequestMessage request,
        IEnumerable<KeyValuePair<string, string>> queryParameters,
        IEnumerable<KeyValuePair<string, JsonElement>> bodyParameters)
    {
        request.Headers.Add("X-API-Key", _apiKey);
    }
}
```

## How It Works

1. **Parse OpenAPI Spec**: The library reads your OpenAPI specification (JSON or YAML)
2. **Generate Tools**: Each operation in the spec becomes an MCP tool with:
   - **Name**: Derived from `operationId` or generated from method + path
   - **Description**: From `summary` or `description` in the spec
   - **Input Schema**: Auto-generated from parameters and request body schemas
3. **Handle Requests**: When an AI calls a tool, the library:
   - Maps parameters to the correct location (path, query, header, body)
   - Applies authentication
   - Makes the HTTP request
   - Returns the response as structured JSON

## API Reference

### Extension Methods

#### `WithToolsFromOpenApi(string openApiSpec, string baseUrl)`

Registers MCP tools from an OpenAPI specification string.

#### `WithToolsFromOpenApi(Stream openApiSpecStream, string baseUrl)`

Registers MCP tools from an OpenAPI specification stream.

#### `WithToolsFromOpenApi(OpenApiDocument openApiDocument, string baseUrl)`

Registers MCP tools from a parsed OpenAPI document.

### Interfaces

#### `IAuthenticationHandler`

Interface for authentication handlers that need to authenticate before making requests.

```csharp
public interface IAuthenticationHandler
{
    bool IsAuthenticated { get; }
    Task AuthenticateAsync();
    void AuthenticateRequest(HttpRequestMessage request, ...);
}
```

#### `IWebApiCaller`

Interface for making HTTP requests to web APIs.

```csharp
public interface IWebApiCaller
{
    Task<JsonElement> CallApiAsync(WebApiMetadata apiMetadata, IDictionary<string, JsonElement> parameters, CancellationToken cancellationToken);
}
```

## Example

Given this OpenAPI operation:

```yaml
paths:
  /users/{id}:
    get:
      operationId: GetUser
      summary: Gets a user by ID
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: integer
      responses:
        200:
          description: The user
```

The library generates an MCP tool:

- **Name**: `GetUser`
- **Description**: `Gets a user by ID`
- **Input Schema**: `{ "id": { "type": "integer" } }`

When called with `{ "id": 123 }`, it makes a `GET` request to `/users/123`.

## Requirements

- .NET 8.0 or later
- [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) 0.5.0-preview.1 or later
- [Microsoft.OpenApi.Readers](https://www.nuget.org/packages/Microsoft.OpenApi.Readers) 1.6.28 or later

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
