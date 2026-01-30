using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Readers;
using ModelContextProtocol.Server;
using Xunit;

namespace OpenApiMcpNet.Tests.EndToEnd;

public class ExtensionsEndToEndTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _httpClient;

    public ExtensionsEndToEndTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task WithToolsFromOpenApi_CreatesToolsFromOpenApiSpec()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var openApiSpecJson = await _httpClient.GetStringAsync("/swagger/v1/swagger.json");
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";
        var webApiCaller = new WebApiCaller(_httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());

        var reader = new OpenApiStringReader();
        var openApiDocument = reader.Read(openApiSpecJson, out _);

        // Act - directly create tools using the same logic as the extension method
        var tools = new List<McpServerTool>();
        foreach (var path in openApiDocument.Paths)
        {
            foreach (var operation in path.Value.Operations)
            {
                var toolName = operation.Value.OperationId ?? $"{operation.Key}_{path.Key.Replace("/", "_").TrimStart('_')}";
                var description = operation.Value.Summary ?? operation.Value.Description ?? $"{operation.Key} {path.Key}";
                var webApiMetadata = new WebApiMetadata(baseUrl, path.Key, operation.Key, operation.Value);
                var tool = new OpenApiTool(toolName, description, webApiMetadata, webApiCaller, loggerFactory.CreateLogger<OpenApiTool>());
                tools.Add(tool);
            }
        }

        // Assert
        Assert.NotEmpty(tools);
        
        // Check that we have tools for the Users endpoints
        var toolNames = tools.Select(t => t.ProtocolTool.Name).ToList();
        
        // The API should have GET, POST endpoints for /api/Users
        Assert.Contains(tools, t => t.ProtocolTool.Name.Contains("Users", StringComparison.OrdinalIgnoreCase) || 
                                    t.ProtocolTool.Name.Contains("User", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WithToolsFromOpenApi_ToolsAreExecutable()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var openApiSpecJson = await _httpClient.GetStringAsync("/swagger/v1/swagger.json");
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";
        var webApiCaller = new WebApiCaller(_httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());

        var reader = new OpenApiStringReader();
        var openApiDocument = reader.Read(openApiSpecJson, out _);

        // Create tools
        var tools = new List<McpServerTool>();
        foreach (var path in openApiDocument.Paths)
        {
            foreach (var operation in path.Value.Operations)
            {
                var toolName = operation.Value.OperationId ?? $"{operation.Key}_{path.Key.Replace("/", "_").TrimStart('_')}";
                var description = operation.Value.Summary ?? operation.Value.Description ?? $"{operation.Key} {path.Key}";
                var webApiMetadata = new WebApiMetadata(baseUrl, path.Key, operation.Key, operation.Value);
                var tool = new OpenApiTool(toolName, description, webApiMetadata, webApiCaller, loggerFactory.CreateLogger<OpenApiTool>());
                tools.Add(tool);
            }
        }

        // Find the GET /api/Users tool
        var getUsersTool = tools.FirstOrDefault(t => 
            t.ProtocolTool.Name.Contains("Users", StringComparison.OrdinalIgnoreCase) &&
            ((Dictionary<string, object>)t.Metadata[0])["method"].ToString() == "GET" &&
            ((Dictionary<string, object>)t.Metadata[0])["path"].ToString() == "/api/Users");

        Assert.NotNull(getUsersTool);

        // Act - Execute the tool (cast to OpenApiTool to access the testable InvokeAsync overload)
        var openApiTool = (OpenApiTool)getUsersTool;
        var result = await openApiTool.InvokeAsync(new Dictionary<string, JsonElement>(), CancellationToken.None);

        // Assert
        Assert.NotEqual(true, result.IsError);
        var textContent = result.Content.First() as ModelContextProtocol.Protocol.TextContentBlock;
        Assert.NotNull(textContent);
        
        var users = JsonSerializer.Deserialize<JsonElement>(textContent.Text);
        Assert.Equal(JsonValueKind.Array, users.ValueKind);
    }
}
