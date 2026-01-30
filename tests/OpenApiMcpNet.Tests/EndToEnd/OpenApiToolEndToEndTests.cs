using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using ModelContextProtocol.Protocol;
using Xunit;

namespace OpenApiMcpNet.Tests.EndToEnd;

public class OpenApiToolEndToEndTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _httpClient;

    public OpenApiToolEndToEndTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApiTool_GetUsers_ExecutesSuccessfully()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var openApiSpec = await GetOpenApiSpecAsync();
        var webApiCaller = new WebApiCaller(_httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";

        var pathItem = openApiSpec.Paths["/api/Users"];
        var operation = pathItem.Operations[OperationType.Get];
        var metadata = new WebApiMetadata(baseUrl, "/api/Users", OperationType.Get, operation);
        
        var tool = new OpenApiTool("getUsers", "Get all users", metadata, webApiCaller, loggerFactory.CreateLogger<OpenApiTool>());

        // Act
        var result = await tool.InvokeAsync(new Dictionary<string, JsonElement>(), CancellationToken.None);

        // Assert
        Assert.NotEqual(true, result.IsError);
        Assert.Single(result.Content);

        var textContent = result.Content.First() as TextContentBlock;
        Assert.NotNull(textContent);
        
        var users = JsonSerializer.Deserialize<JsonElement>(textContent.Text);
        Assert.Equal(JsonValueKind.Array, users.ValueKind);
        Assert.True(users.GetArrayLength() > 0);
    }

    [Fact]
    public async Task OpenApiTool_GetUserById_ExecutesSuccessfully()
    {
        // Arrange
        var openApiSpec = await GetOpenApiSpecAsync();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var webApiCaller = new WebApiCaller(_httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";

        var pathItem = openApiSpec.Paths["/api/Users/{id}"];
        var operation = pathItem.Operations[OperationType.Get];
        var metadata = new WebApiMetadata(baseUrl, "/api/Users/{id}", OperationType.Get, operation);
        
        var tool = new OpenApiTool("getUser", "Get a user by ID", metadata, webApiCaller, loggerFactory.CreateLogger<OpenApiTool>());

        var arguments = new Dictionary<string, JsonElement>
        {
            ["id"] = JsonSerializer.SerializeToElement(1)
        };

        // Act
        var result = await tool.InvokeAsync(arguments, CancellationToken.None);

        // Assert
        Assert.NotEqual(true, result.IsError);
        
        var textContent = result.Content.First() as TextContentBlock;
        Assert.NotNull(textContent);
        
        var user = JsonSerializer.Deserialize<JsonElement>(textContent.Text);
        Assert.Equal(1, user.GetProperty("id").GetInt32());
        Assert.Equal("Alice", user.GetProperty("name").GetString());
    }

    [Fact]
    public async Task OpenApiTool_CreateUser_ExecutesSuccessfully()
    {
        // Arrange
        var openApiSpec = await GetOpenApiSpecAsync();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var webApiCaller = new WebApiCaller(_httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";

        var pathItem = openApiSpec.Paths["/api/Users"];
        var operation = pathItem.Operations[OperationType.Post];
        var metadata = new WebApiMetadata(baseUrl, "/api/Users", OperationType.Post, operation);
        
        var tool = new OpenApiTool("createUser", "Create a new user", metadata, webApiCaller, loggerFactory.CreateLogger<OpenApiTool>());

        var arguments = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("TestUser"),
            ["email"] = JsonSerializer.SerializeToElement("testuser@example.com")
        };

        // Act
        var result = await tool.InvokeAsync(arguments, CancellationToken.None);

        // Assert
        Assert.NotEqual(true, result.IsError);
        
        var textContent = result.Content.First() as TextContentBlock;
        Assert.NotNull(textContent);
        
        var user = JsonSerializer.Deserialize<JsonElement>(textContent.Text);
        Assert.True(user.GetProperty("id").GetInt32() > 0);
        Assert.Equal("TestUser", user.GetProperty("name").GetString());
        Assert.Equal("testuser@example.com", user.GetProperty("email").GetString());
    }

    [Fact]
    public async Task OpenApiTool_GetNonExistentUser_ReturnsError()
    {
        // Arrange
        var openApiSpec = await GetOpenApiSpecAsync();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var webApiCaller = new WebApiCaller(_httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";

        var pathItem = openApiSpec.Paths["/api/Users/{id}"];
        var operation = pathItem.Operations[OperationType.Get];
        var metadata = new WebApiMetadata(baseUrl, "/api/Users/{id}", OperationType.Get, operation);
        
        var tool = new OpenApiTool("getUser", "Get a user by ID", metadata, webApiCaller, loggerFactory.CreateLogger<OpenApiTool>());

        var arguments = new Dictionary<string, JsonElement>
        {
            ["id"] = JsonSerializer.SerializeToElement(99999)
        };

        // Act
        var result = await tool.InvokeAsync(arguments, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        
        var textContent = result.Content.First() as TextContentBlock;
        Assert.NotNull(textContent);
        Assert.Contains("404", textContent.Text);
    }

    [Fact]
    public async Task OpenApiTool_HasCorrectInputSchema()
    {
        // Arrange
        var openApiSpec = await GetOpenApiSpecAsync();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var webApiCaller = new WebApiCaller(_httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";

        var pathItem = openApiSpec.Paths["/api/Users/{id}"];
        var operation = pathItem.Operations[OperationType.Get];
        var metadata = new WebApiMetadata(baseUrl, "/api/Users/{id}", OperationType.Get, operation);
        
        var tool = new OpenApiTool("getUser", "Get a user by ID", metadata, webApiCaller, loggerFactory.CreateLogger<OpenApiTool>());

        // Act
        var inputSchema = tool.ProtocolTool.InputSchema;

        // Assert
        Assert.Equal("object", inputSchema.GetProperty("type").GetString());
        
        var properties = inputSchema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("id", out var idProp));
        Assert.Equal("integer", idProp.GetProperty("type").GetString());
    }

    [Fact]
    public async Task OpenApiTool_HasCorrectMetadata()
    {
        // Arrange
        var openApiSpec = await GetOpenApiSpecAsync();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var webApiCaller = new WebApiCaller(_httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";

        var pathItem = openApiSpec.Paths["/api/Users"];
        var operation = pathItem.Operations[OperationType.Get];
        var metadata = new WebApiMetadata(baseUrl, "/api/Users", OperationType.Get, operation);
        
        var tool = new OpenApiTool("GetUsers", "Get all users", metadata, webApiCaller, loggerFactory.CreateLogger<OpenApiTool>());

        // Act & Assert
        Assert.NotEmpty(tool.Metadata);
        var toolMetadata = tool.Metadata[0] as Dictionary<string, object>;
        Assert.NotNull(toolMetadata);
        Assert.Equal("/api/Users", toolMetadata["path"]);
        Assert.Equal("GET", toolMetadata["method"]);
    }

    private async Task<OpenApiDocument> GetOpenApiSpecAsync()
    {
        var response = await _httpClient.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        
        var stream = await response.Content.ReadAsStreamAsync();
        var reader = new OpenApiStreamReader();
        var document = reader.Read(stream, out var diagnostic);
        
        if (diagnostic.Errors.Any())
        {
            throw new InvalidOperationException($"Failed to parse OpenAPI spec: {string.Join(", ", diagnostic.Errors.Select(e => e.Message))}");
        }
        
        return document;
    }
}
