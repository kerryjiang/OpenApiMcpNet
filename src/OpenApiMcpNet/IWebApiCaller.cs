using System.Text.Json;

namespace OpenApiMcpNet;

/// <summary>
/// Defines a web API caller that can make HTTP requests based on API metadata.
/// </summary>
public interface IWebApiCaller
{
    /// <summary>
    /// Calls a web API using the specified metadata and parameters.
    /// </summary>
    /// <param name="apiMetadata">The metadata describing the API endpoint to call.</param>
    /// <param name="parameters">The parameters to send with the request.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation, containing the JSON response.</returns>
    Task<JsonElement> CallApiAsync(WebApiMetadata apiMetadata, IDictionary<string, JsonElement> parameters, CancellationToken cancellationToken);
}