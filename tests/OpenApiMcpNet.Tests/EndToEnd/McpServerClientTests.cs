using System.IO.Pipelines;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Readers;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace OpenApiMcpNet.Tests.EndToEnd;

/// <summary>
/// End-to-end tests using an MCP server hosting OpenAPI tools and MCP client to test against it.
/// </summary>
public class McpServerClientTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _httpClient;

    public McpServerClientTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task McpClient_CanListToolsFromServer()
    {
        // Arrange
        var openApiSpecJson = await _httpClient.GetStringAsync("/swagger/v1/swagger.json");
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";

        await using var serverAndClient = await CreateServerAndClientAsync(openApiSpecJson, baseUrl);
        var client = serverAndClient.Client;

        // Act
        var tools = await client.ListToolsAsync();

        // Assert
        Assert.NotEmpty(tools);
        
        // Should have tools for Users API
        Assert.Contains(tools, t => t.Name.Contains("Users", StringComparison.OrdinalIgnoreCase) ||
                                    t.Name.Contains("User", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task McpClient_CanCallGetUsersTool()
    {
        // Arrange
        var openApiSpecJson = await _httpClient.GetStringAsync("/swagger/v1/swagger.json");
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";

        await using var serverAndClient = await CreateServerAndClientAsync(openApiSpecJson, baseUrl);
        var client = serverAndClient.Client;

        var tools = await client.ListToolsAsync();
        var getUsersTool = tools.FirstOrDefault(t =>
            t.Name.Equals("GetUsers", StringComparison.OrdinalIgnoreCase) ||
            (t.Name.Contains("User", StringComparison.OrdinalIgnoreCase) &&
             t.Name.Contains("Get", StringComparison.OrdinalIgnoreCase) &&
             !t.Name.Contains("Id", StringComparison.OrdinalIgnoreCase)));

        Assert.NotNull(getUsersTool);

        // Act
        var result = await client.CallToolAsync(getUsersTool.Name, new Dictionary<string, object?>());

        // Assert
        Assert.NotEqual(true, result.IsError);
        Assert.NotEmpty(result.Content);

        var textContent = result.Content.First() as TextContentBlock;
        Assert.NotNull(textContent);

        var users = JsonSerializer.Deserialize<JsonElement>(textContent.Text);
        Assert.Equal(JsonValueKind.Array, users.ValueKind);
        Assert.True(users.GetArrayLength() > 0);
    }

    [Fact]
    public async Task McpClient_CanCallGetUserByIdTool()
    {
        // Arrange
        var openApiSpecJson = await _httpClient.GetStringAsync("/swagger/v1/swagger.json");
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";

        await using var serverAndClient = await CreateServerAndClientAsync(openApiSpecJson, baseUrl);
        var client = serverAndClient.Client;

        var tools = await client.ListToolsAsync();
        
        // The method is named "GetUser" in the controller (not "GetUserById")
        var getUserTool = tools.FirstOrDefault(t =>
            t.Name.Equals("GetUser", StringComparison.OrdinalIgnoreCase) ||
            (t.Name.Contains("get", StringComparison.OrdinalIgnoreCase) &&
             t.Name.Contains("user", StringComparison.OrdinalIgnoreCase) &&
             t.Name.Contains("id", StringComparison.OrdinalIgnoreCase)));

        Assert.NotNull(getUserTool);

        // Act
        var result = await client.CallToolAsync(getUserTool.Name, new Dictionary<string, object?>
        {
            ["id"] = 1
        });

        // Assert
        Assert.NotEqual(true, result.IsError);
        Assert.NotEmpty(result.Content);

        var textContent = result.Content.First() as TextContentBlock;
        Assert.NotNull(textContent);

        var user = JsonSerializer.Deserialize<JsonElement>(textContent.Text);
        Assert.Equal(1, user.GetProperty("id").GetInt32());
        Assert.Equal("Alice", user.GetProperty("name").GetString());
    }

    [Fact]
    public async Task McpClient_CanCallCreateUserTool()
    {
        // Arrange
        var openApiSpecJson = await _httpClient.GetStringAsync("/swagger/v1/swagger.json");
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";

        await using var serverAndClient = await CreateServerAndClientAsync(openApiSpecJson, baseUrl);
        var client = serverAndClient.Client;

        var tools = await client.ListToolsAsync();
        
        // The method is named "CreateUser" in the controller
        var createUserTool = tools.FirstOrDefault(t =>
            t.Name.Equals("CreateUser", StringComparison.OrdinalIgnoreCase) ||
            (t.Name.Contains("post", StringComparison.OrdinalIgnoreCase) &&
             t.Name.Contains("user", StringComparison.OrdinalIgnoreCase)));

        Assert.NotNull(createUserTool);

        // The POST body parameter for CreateUser expects a CreateUserRequest with name and email
        // The request body should be passed as the body parameter
        var result = await client.CallToolAsync(createUserTool.Name, new Dictionary<string, object?>
        {
            ["body"] = new Dictionary<string, object?>
            {
                ["name"] = "McpTestUser",
                ["email"] = "mcptest@example.com"
            }
        });

        // Assert
        Assert.NotEqual(true, result.IsError);
        Assert.NotEmpty(result.Content);

        var textContent = result.Content.First() as TextContentBlock;
        Assert.NotNull(textContent);

        var user = JsonSerializer.Deserialize<JsonElement>(textContent.Text);
        Assert.True(user.GetProperty("id").GetInt32() > 0);
        Assert.Equal("McpTestUser", user.GetProperty("name").GetString());
        Assert.Equal("mcptest@example.com", user.GetProperty("email").GetString());
    }

    [Fact]
    public async Task McpClient_ToolCallReturnsErrorForNonExistentUser()
    {
        // Arrange
        var openApiSpecJson = await _httpClient.GetStringAsync("/swagger/v1/swagger.json");
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";

        await using var serverAndClient = await CreateServerAndClientAsync(openApiSpecJson, baseUrl);
        var client = serverAndClient.Client;

        var tools = await client.ListToolsAsync();
        var getUserByIdTool = tools.FirstOrDefault(t =>
            t.Name.Equals("GetUserById", StringComparison.OrdinalIgnoreCase) ||
            (t.Name.Contains("User", StringComparison.OrdinalIgnoreCase) &&
             t.Name.Contains("id", StringComparison.OrdinalIgnoreCase)));

        Assert.NotNull(getUserByIdTool);

        // Act
        var result = await client.CallToolAsync(getUserByIdTool.Name, new Dictionary<string, object?>
        {
            ["id"] = 99999
        });

        // Assert
        Assert.True(result.IsError);
        
        var textContent = result.Content.First() as TextContentBlock;
        Assert.NotNull(textContent);
        Assert.Contains("404", textContent.Text);
    }

    [Fact]
    public async Task McpClient_ToolsHaveCorrectInputSchema()
    {
        // Arrange
        var openApiSpecJson = await _httpClient.GetStringAsync("/swagger/v1/swagger.json");
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";

        await using var serverAndClient = await CreateServerAndClientAsync(openApiSpecJson, baseUrl);
        var client = serverAndClient.Client;

        // Act
        var tools = await client.ListToolsAsync();
        var getUserByIdTool = tools.FirstOrDefault(t =>
            t.Name.Equals("GetUserById", StringComparison.OrdinalIgnoreCase) ||
            (t.Name.Contains("User", StringComparison.OrdinalIgnoreCase) &&
             t.Name.Contains("id", StringComparison.OrdinalIgnoreCase)));

        Assert.NotNull(getUserByIdTool);

        // Assert - use JsonSchema property instead of InputSchema
        Assert.Equal("object", getUserByIdTool.JsonSchema.GetProperty("type").GetString());
        
        var properties = getUserByIdTool.JsonSchema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("id", out var idProp));
        Assert.Equal("integer", idProp.GetProperty("type").GetString());
    }

    [Fact]
    public async Task McpClient_ToolsHaveDescriptions()
    {
        // Arrange
        var openApiSpecJson = await _httpClient.GetStringAsync("/swagger/v1/swagger.json");
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";

        await using var serverAndClient = await CreateServerAndClientAsync(openApiSpecJson, baseUrl);
        var client = serverAndClient.Client;

        // Act
        var tools = await client.ListToolsAsync();

        // Assert
        foreach (var tool in tools)
        {
            Assert.False(string.IsNullOrEmpty(tool.Description), $"Tool {tool.Name} should have a description");
        }
    }

    private async Task<ServerClientPair> CreateServerAndClientAsync(string openApiSpec, string baseUrl)
    {
        // Create bidirectional pipes for server/client communication
        var serverToClientPipe = new Pipe();
        var clientToServerPipe = new Pipe();

        // Server reads from clientToServer, writes to serverToClient
        var serverInputStream = clientToServerPipe.Reader.AsStream();
        var serverOutputStream = serverToClientPipe.Writer.AsStream();

        // Parse the OpenAPI spec to get tools
        var reader = new OpenApiStringReader();
        var openApiDocument = reader.Read(openApiSpec, out _);

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        // Create the web API caller
        var webApiCaller = new WebApiCaller(_httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());

        // Create tools from the OpenAPI operations
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

        // Create server transport
        var serverTransport = new StreamServerTransport(serverInputStream, serverOutputStream);

        // Create tool collection
        var toolCollection = new McpServerPrimitiveCollection<McpServerTool>();
        foreach (var tool in tools)
        {
            toolCollection.TryAdd(tool);
        }

        // Create MCP server options with tools
        var serverOptions = new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = "TestServer",
                Version = "1.0.0"
            },
            Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability()
            },
            ToolCollection = toolCollection
        };

        // Create the MCP server directly with tools
        var server = McpServer.Create(serverTransport, serverOptions);

        var serverCts = new CancellationTokenSource();
        var serverTask = Task.Run(async () =>
        {
            try
            {
                await server.RunAsync(serverCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when disposing
            }
            catch (IOException)
            {
                // Expected when pipes are closed
            }
        });

        // Create client transport and connect
        // Client writes to clientToServerPipe.Writer (server input)
        // Client reads from serverToClientPipe.Reader (server output)
        var clientTransport = new StreamClientTransport(
            clientToServerPipe.Writer.AsStream(),
            serverToClientPipe.Reader.AsStream());

        var client = await McpClient.CreateAsync(clientTransport, new McpClientOptions
        {
            ClientInfo = new Implementation
            {
                Name = "TestClient",
                Version = "1.0.0"
            }
        });

        return new ServerClientPair(client, server, serverCts, serverTask, 
            serverToClientPipe, clientToServerPipe);
    }
}

/// <summary>
/// Wrapper for server and client pair that handles disposal
/// </summary>
public class ServerClientPair : IAsyncDisposable
{
    public McpClient Client { get; }
    private readonly McpServer _server;
    private readonly CancellationTokenSource _serverCts;
    private readonly Task _serverTask;
    private readonly Pipe _serverToClientPipe;
    private readonly Pipe _clientToServerPipe;

    public ServerClientPair(
        McpClient client, 
        McpServer server, 
        CancellationTokenSource serverCts,
        Task serverTask,
        Pipe serverToClientPipe,
        Pipe clientToServerPipe)
    {
        Client = client;
        _server = server;
        _serverCts = serverCts;
        _serverTask = serverTask;
        _serverToClientPipe = serverToClientPipe;
        _clientToServerPipe = clientToServerPipe;
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        
        // Complete the pipes to signal shutdown
        await _serverToClientPipe.Writer.CompleteAsync();
        await _clientToServerPipe.Writer.CompleteAsync();
        
        _serverCts.Cancel();
        
        try
        {
            await _serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            // Server didn't stop in time, continue with disposal
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        
        await _server.DisposeAsync();
        _serverCts.Dispose();
    }
}
