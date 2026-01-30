using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Moq;
using Xunit;

namespace OpenApiMcpNet.Tests.Unit;

public class WebApiCallerTests
{
    [Fact]
    public async Task CallApiAsync_Get_BuildsCorrectUrl()
    {
        // Arrange
        var handlerMock = new MockHttpMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Equal("https://api.example.com/users", req.RequestUri?.ToString());
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        var httpClient = new HttpClient(handlerMock);
        var webApiCaller = new WebApiCaller(httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());

        var metadata = new WebApiMetadata(
            "https://api.example.com",
            "/users",
            OperationType.Get,
            new OpenApiOperation());

        // Act
        var result = await webApiCaller.CallApiAsync(metadata, new Dictionary<string, JsonElement>(), CancellationToken.None);

        // Assert
        Assert.Equal(JsonValueKind.Array, result.ValueKind);
    }

    [Fact]
    public async Task CallApiAsync_Get_WithPathParameter_ReplacesPathPlaceholder()
    {
        // Arrange
        string? capturedUrl = null;
        var handlerMock = new MockHttpMessageHandler(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":123}", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handlerMock);
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var webApiCaller = new WebApiCaller(httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());

        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "id",
                    In = ParameterLocation.Path,
                    Schema = new OpenApiSchema { Type = "integer" }
                }
            }
        };

        var metadata = new WebApiMetadata(
            "https://api.example.com",
            "/users/{id}",
            OperationType.Get,
            operation);

        var parameters = new Dictionary<string, JsonElement>
        {
            ["id"] = JsonSerializer.SerializeToElement(123)
        };

        // Act
        await webApiCaller.CallApiAsync(metadata, parameters, CancellationToken.None);

        // Assert
        Assert.Equal("https://api.example.com/users/123", capturedUrl);
    }

    [Fact]
    public async Task CallApiAsync_Get_WithQueryParameters_AppendsToUrl()
    {
        // Arrange
        string? capturedUrl = null;
        var handlerMock = new MockHttpMessageHandler(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handlerMock);
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var webApiCaller = new WebApiCaller(httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());

        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "name",
                    In = ParameterLocation.Query,
                    Schema = new OpenApiSchema { Type = "string" }
                },
                new OpenApiParameter
                {
                    Name = "limit",
                    In = ParameterLocation.Query,
                    Schema = new OpenApiSchema { Type = "integer" }
                }
            }
        };

        var metadata = new WebApiMetadata(
            "https://api.example.com",
            "/users/search",
            OperationType.Get,
            operation);

        var parameters = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("Alice"),
            ["limit"] = JsonSerializer.SerializeToElement(10)
        };

        // Act
        await webApiCaller.CallApiAsync(metadata, parameters, CancellationToken.None);

        // Assert
        Assert.Contains("name=Alice", capturedUrl);
        Assert.Contains("limit=10", capturedUrl);
    }

    [Fact]
    public async Task CallApiAsync_Post_SendsBodyAsJson()
    {
        // Arrange
        string? capturedBody = null;
        var handlerMock = new MockHttpMessageHandler(async req =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(System.Net.HttpStatusCode.Created)
            {
                Content = new StringContent("{\"id\":1,\"name\":\"Test\"}", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handlerMock);
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var webApiCaller = new WebApiCaller(httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());

        var metadata = new WebApiMetadata(
            "https://api.example.com",
            "/users",
            OperationType.Post,
            new OpenApiOperation());

        var parameters = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("Test"),
            ["email"] = JsonSerializer.SerializeToElement("test@example.com")
        };

        // Act
        await webApiCaller.CallApiAsync(metadata, parameters, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedBody);
        var bodyJson = JsonSerializer.Deserialize<JsonElement>(capturedBody);
        Assert.Equal("Test", bodyJson.GetProperty("name").GetString());
        Assert.Equal("test@example.com", bodyJson.GetProperty("email").GetString());
    }

    [Fact]
    public async Task CallApiAsync_WithHeader_AddsHeaderToRequest()
    {
        // Arrange
        string? capturedHeader = null;
        var handlerMock = new MockHttpMessageHandler(req =>
        {
            if (req.Headers.TryGetValues("X-Api-Key", out var values))
            {
                capturedHeader = values.First();
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handlerMock);
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var webApiCaller = new WebApiCaller(httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());

        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "X-Api-Key",
                    In = ParameterLocation.Header,
                    Schema = new OpenApiSchema { Type = "string" }
                }
            }
        };

        var metadata = new WebApiMetadata(
            "https://api.example.com",
            "/protected",
            OperationType.Get,
            operation);

        var parameters = new Dictionary<string, JsonElement>
        {
            ["X-Api-Key"] = JsonSerializer.SerializeToElement("secret-key-123")
        };

        // Act
        await webApiCaller.CallApiAsync(metadata, parameters, CancellationToken.None);

        // Assert
        Assert.Equal("secret-key-123", capturedHeader);
    }

    [Fact]
    public async Task CallApiAsync_NonSuccessStatus_ThrowsHttpRequestException()
    {
        // Arrange
        var handlerMock = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            {
                Content = new StringContent("Not found", System.Text.Encoding.UTF8, "text/plain")
            };
        });

        var httpClient = new HttpClient(handlerMock);
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var webApiCaller = new WebApiCaller(httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());

        var metadata = new WebApiMetadata(
            "https://api.example.com",
            "/users/999",
            OperationType.Get,
            new OpenApiOperation());

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            webApiCaller.CallApiAsync(metadata, new Dictionary<string, JsonElement>(), CancellationToken.None));
    }

    [Fact]
    public async Task CallApiAsync_EmptyResponse_ReturnsSuccessObject()
    {
        // Arrange
        var handlerMock = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.NoContent)
            {
                Content = new StringContent("", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handlerMock);
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var webApiCaller = new WebApiCaller(httpClient, new NoOpAuthenticationHandler(), loggerFactory.CreateLogger<WebApiCaller>());

        var metadata = new WebApiMetadata(
            "https://api.example.com",
            "/users/1",
            OperationType.Delete,
            new OpenApiOperation());

        // Act
        var result = await webApiCaller.CallApiAsync(metadata, new Dictionary<string, JsonElement>(), CancellationToken.None);

        // Assert
        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(204, result.GetProperty("statusCode").GetInt32());
    }
}

/// <summary>
/// A mock HTTP message handler for testing
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handlerFunc;

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handlerFunc = req => Task.FromResult(handler(req));
    }

    public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        _handlerFunc = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _handlerFunc(request);
    }
}
