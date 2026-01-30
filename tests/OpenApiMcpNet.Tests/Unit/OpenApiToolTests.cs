using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;
using Xunit;

namespace OpenApiMcpNet.Tests.Unit;

public class OpenApiToolTests
{
    [Fact]
    public void Constructor_SetsProtocolToolProperties()
    {
        // Arrange
        var name = "getUsers";
        var description = "Gets all users";
        var webApiMetadata = new WebApiMetadata(
            "https://api.example.com",
            "/users",
            OperationType.Get,
            new OpenApiOperation());
        var webApiCaller = Mock.Of<IWebApiCaller>();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        // Act
        var tool = new OpenApiTool(name, description, webApiMetadata, webApiCaller, loggerFactory.CreateLogger<OpenApiTool>());

        // Assert
        Assert.Equal(name, tool.ProtocolTool.Name);
        Assert.Equal(description, tool.ProtocolTool.Description);
    }

    [Fact]
    public void Constructor_BuildsInputSchemaFromParameters()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "id",
                    Required = true,
                    In = ParameterLocation.Path,
                    Schema = new OpenApiSchema { Type = "integer", Description = "The user ID" }
                },
                new OpenApiParameter
                {
                    Name = "include",
                    Required = false,
                    In = ParameterLocation.Query,
                    Schema = new OpenApiSchema { Type = "string" }
                }
            }
        };

        var webApiMetadata = new WebApiMetadata(
            "https://api.example.com",
            "/users/{id}",
            OperationType.Get,
            operation);
        var webApiCaller = Mock.Of<IWebApiCaller>();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        // Act
        var tool = new OpenApiTool("getUser", "Get a user", webApiMetadata, webApiCaller, loggerFactory.CreateLogger<OpenApiTool>());

        // Assert
        var schema = tool.ProtocolTool.InputSchema;
        Assert.Equal("object", schema.GetProperty("type").GetString());
        
        var properties = schema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("id", out _));
        Assert.True(properties.TryGetProperty("include", out _));
        
        var required = schema.GetProperty("required");
        Assert.Contains("id", required.EnumerateArray().Select(e => e.GetString()));
        Assert.DoesNotContain("include", required.EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void Constructor_BuildsInputSchemaFromRequestBody()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["name"] = new OpenApiSchema { Type = "string" },
                                ["email"] = new OpenApiSchema { Type = "string" }
                            },
                            Required = new HashSet<string> { "name", "email" }
                        }
                    }
                }
            }
        };

        var webApiMetadata = new WebApiMetadata(
            "https://api.example.com",
            "/users",
            OperationType.Post,
            operation);
        var webApiCaller = Mock.Of<IWebApiCaller>();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        // Act
        var tool = new OpenApiTool("createUser", "Create a user", webApiMetadata, webApiCaller, loggerFactory.CreateLogger<OpenApiTool>());

        // Assert
        var schema = tool.ProtocolTool.InputSchema;
        var properties = schema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("name", out _));
        Assert.True(properties.TryGetProperty("email", out _));
        
        var required = schema.GetProperty("required");
        Assert.Contains("name", required.EnumerateArray().Select(e => e.GetString()));
        Assert.Contains("email", required.EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void Constructor_BuildsMetadataFromOperation()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            OperationId = "getUsers",
            Tags = new List<OpenApiTag> { new OpenApiTag { Name = "Users" } },
            Deprecated = true,
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "Success" },
                ["404"] = new OpenApiResponse { Description = "Not found" }
            }
        };

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        var webApiMetadata = new WebApiMetadata(
            "https://api.example.com",
            "/users",
            OperationType.Get,
            operation);
        var webApiCaller = Mock.Of<IWebApiCaller>();

        // Act
        var tool = new OpenApiTool("getUsers", "Get users", webApiMetadata, webApiCaller, loggerFactory.CreateLogger<OpenApiTool>());

        // Assert
        Assert.NotEmpty(tool.Metadata);
        var metadata = tool.Metadata[0] as Dictionary<string, object>;
        Assert.NotNull(metadata);
        Assert.Equal("/users", metadata["path"]);
        Assert.Equal("GET", metadata["method"]);
        Assert.Equal("getUsers", metadata["operationId"]);
        Assert.True((bool)metadata["deprecated"]);
    }

    [Fact]
    public async Task InvokeAsync_CallsWebApiCaller()
    {
        // Arrange
        var expectedResponse = JsonSerializer.SerializeToElement(new { id = 1, name = "Test" });
        
        var webApiCallerMock = new Mock<IWebApiCaller>();
        webApiCallerMock
            .Setup(x => x.CallApiAsync(
                It.IsAny<WebApiMetadata>(),
                It.IsAny<IDictionary<string, JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var webApiMetadata = new WebApiMetadata(
            "https://api.example.com",
            "/users/1",
            OperationType.Get,
            new OpenApiOperation());

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var tool = new OpenApiTool("getUser", "Get user", webApiMetadata, webApiCallerMock.Object, loggerFactory.CreateLogger<OpenApiTool>());

        var arguments = new Dictionary<string, JsonElement>
        {
            ["id"] = JsonSerializer.SerializeToElement(1)
        };

        // Act
        var result = await tool.InvokeAsync(arguments, CancellationToken.None);

        // Assert
        Assert.NotEqual(true, result.IsError);
        Assert.Single(result.Content);
        
        webApiCallerMock.Verify(x => x.CallApiAsync(
            webApiMetadata,
            It.Is<IDictionary<string, JsonElement>>(d => d.ContainsKey("id")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsErrorOnException()
    {
        // Arrange
        var webApiCallerMock = new Mock<IWebApiCaller>();
        webApiCallerMock
            .Setup(x => x.CallApiAsync(
                It.IsAny<WebApiMetadata>(),
                It.IsAny<IDictionary<string, JsonElement>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var webApiMetadata = new WebApiMetadata(
            "https://api.example.com",
            "/users",
            OperationType.Get,
            new OpenApiOperation());

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var tool = new OpenApiTool("getUsers", "Get users", webApiMetadata, webApiCallerMock.Object, loggerFactory.CreateLogger<OpenApiTool>());

        // Act
        var result = await tool.InvokeAsync(new Dictionary<string, JsonElement>(), CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("Connection refused", ((TextContentBlock)result.Content.First()).Text);
    }
}
