using System.Text.Json;
using Microsoft.OpenApi.Models;
using Xunit;

namespace OpenApiMcpNet.Tests.Unit;

public class WebApiMetadataTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var baseUrl = "https://api.example.com";
        var path = "/users/{id}";
        var operationType = OperationType.Get;
        var operation = new OpenApiOperation
        {
            OperationId = "getUser",
            Summary = "Get a user by ID"
        };

        // Act
        var metadata = new WebApiMetadata(baseUrl, path, operationType, operation);

        // Assert
        Assert.Equal(baseUrl, metadata.BaseUrl);
        Assert.Equal(path, metadata.Path);
        Assert.Equal(operationType, metadata.OperationType);
        Assert.Same(operation, metadata.Operation);
    }

    [Theory]
    [InlineData(OperationType.Get)]
    [InlineData(OperationType.Post)]
    [InlineData(OperationType.Put)]
    [InlineData(OperationType.Delete)]
    [InlineData(OperationType.Patch)]
    public void Constructor_SupportsAllOperationTypes(OperationType operationType)
    {
        // Arrange & Act
        var metadata = new WebApiMetadata(
            "https://api.example.com",
            "/resource",
            operationType,
            new OpenApiOperation());

        // Assert
        Assert.Equal(operationType, metadata.OperationType);
    }
}
