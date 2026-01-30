using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Readers;
using Xunit;

namespace OpenApiMcpNet.Tests.EndToEnd;

public class WebApiCallerEndToEndTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _httpClient;

    public WebApiCallerEndToEndTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task CallApiAsync_GetAllUsers_ReturnsUserList()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var openApiSpec = await GetOpenApiSpecAsync();
        var webApiCaller = new WebApiCaller(_httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());
        
        var metadata = CreateMetadataFromSpec(openApiSpec, "/api/Users", "get");

        // Act
        var result = await webApiCaller.CallApiAsync(metadata, new Dictionary<string, JsonElement>(), CancellationToken.None);

        // Assert
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.True(result.GetArrayLength() > 0);
        
        var firstUser = result[0];
        Assert.True(firstUser.TryGetProperty("id", out _));
        Assert.True(firstUser.TryGetProperty("name", out _));
        Assert.True(firstUser.TryGetProperty("email", out _));
    }

    [Fact]
    public async Task CallApiAsync_GetUserById_ReturnsUser()
    {
        // Arrange
        var openApiSpec = await GetOpenApiSpecAsync();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var webApiCaller = new WebApiCaller(_httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());
        
        var metadata = CreateMetadataFromSpec(openApiSpec, "/api/Users/{id}", "get");
        var parameters = new Dictionary<string, JsonElement>
        {
            ["id"] = JsonSerializer.SerializeToElement(1)
        };

        // Act
        var result = await webApiCaller.CallApiAsync(metadata, parameters, CancellationToken.None);

        // Assert
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal(1, result.GetProperty("id").GetInt32());
        Assert.Equal("Alice", result.GetProperty("name").GetString());
    }

    [Fact]
    public async Task CallApiAsync_SearchUsers_WithQueryParameters_ReturnsFilteredResults()
    {
        // Arrange
        var openApiSpec = await GetOpenApiSpecAsync();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var webApiCaller = new WebApiCaller(_httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());  
        
        var metadata = CreateMetadataFromSpec(openApiSpec, "/api/Users/search", "get");
        var parameters = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("Alice"),
            ["limit"] = JsonSerializer.SerializeToElement(5)
        };

        // Act
        var result = await webApiCaller.CallApiAsync(metadata, parameters, CancellationToken.None);

        // Assert
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
        Assert.True(result.GetArrayLength() >= 1);
        
        foreach (var user in result.EnumerateArray())
        {
            Assert.Contains("Alice", user.GetProperty("name").GetString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task CallApiAsync_CreateUser_ReturnsCreatedUser()
    {
        // Arrange
        var openApiSpec = await GetOpenApiSpecAsync();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var webApiCaller = new WebApiCaller(_httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());
        
        var metadata = CreateMetadataFromSpec(openApiSpec, "/api/Users", "post");
        var parameters = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("NewUser"),
            ["email"] = JsonSerializer.SerializeToElement("newuser@example.com")
        };

        // Act
        var result = await webApiCaller.CallApiAsync(metadata, parameters, CancellationToken.None);

        // Assert
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.True(result.GetProperty("id").GetInt32() > 0);
        Assert.Equal("NewUser", result.GetProperty("name").GetString());
        Assert.Equal("newuser@example.com", result.GetProperty("email").GetString());
    }

    [Fact]
    public async Task CallApiAsync_UpdateUser_ReturnsUpdatedUser()
    {
        // Arrange
        var openApiSpec = await GetOpenApiSpecAsync();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var webApiCaller = new WebApiCaller(_httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());
        
        var metadata = CreateMetadataFromSpec(openApiSpec, "/api/Users/{id}", "put");
        var parameters = new Dictionary<string, JsonElement>
        {
            ["id"] = JsonSerializer.SerializeToElement(2),
            ["name"] = JsonSerializer.SerializeToElement("UpdatedBob"),
            ["email"] = JsonSerializer.SerializeToElement("updatedbob@example.com")
        };

        // Act
        var result = await webApiCaller.CallApiAsync(metadata, parameters, CancellationToken.None);

        // Assert
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal(2, result.GetProperty("id").GetInt32());
        Assert.Equal("UpdatedBob", result.GetProperty("name").GetString());
    }

    [Fact]
    public async Task CallApiAsync_GetNonExistentUser_ThrowsHttpRequestException()
    {
        // Arrange
        var openApiSpec = await GetOpenApiSpecAsync();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var webApiCaller = new WebApiCaller(_httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());
        
        var metadata = CreateMetadataFromSpec(openApiSpec, "/api/Users/{id}", "get");
        var parameters = new Dictionary<string, JsonElement>
        {
            ["id"] = JsonSerializer.SerializeToElement(99999)
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            webApiCaller.CallApiAsync(metadata, parameters, CancellationToken.None));
        
        Assert.Contains("404", exception.Message);
    }

    private async Task<Microsoft.OpenApi.Models.OpenApiDocument> GetOpenApiSpecAsync()
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

    private WebApiMetadata CreateMetadataFromSpec(Microsoft.OpenApi.Models.OpenApiDocument document, string path, string method)
    {
        var pathItem = document.Paths[path];
        var operationType = Enum.Parse<Microsoft.OpenApi.Models.OperationType>(method, ignoreCase: true);
        var operation = pathItem.Operations[operationType];
        
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";
        
        return new WebApiMetadata(baseUrl, path, operationType, operation);
    }
}
