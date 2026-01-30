using System.Text.Json;

namespace OpenApiMcpNet;

/// <summary>
/// Defines a simple request authentication handler that applies authentication directly to HTTP requests.
/// Unlike <see cref="IAuthenticationHandler"/>, this interface does not require a separate authentication step.
/// </summary>
public interface IRequestAuthenticationHandler
{
    /// <summary>
    /// Applies authentication to an HTTP request.
    /// </summary>
    /// <param name="request">The HTTP request to authenticate.</param>
    /// <param name="queryParameters">The query parameters of the request.</param>
    /// <param name="bodyParameters">The body parameters of the request.</param>
    void Authenticate(HttpRequestMessage request, IEnumerable<KeyValuePair<string, string>> queryParameters, IEnumerable<KeyValuePair<string, JsonElement>> bodyParameters);
}