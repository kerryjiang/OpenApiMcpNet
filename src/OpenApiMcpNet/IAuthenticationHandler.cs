using System.Text.Json;

namespace OpenApiMcpNet;

/// <summary>
/// Defines an authentication handler that can authenticate and apply authentication to HTTP requests.
/// </summary>
public interface IAuthenticationHandler
{
    /// <summary>
    /// Gets a value indicating whether the handler has been authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Performs the authentication process to obtain credentials.
    /// </summary>
    /// <returns>A task representing the asynchronous authentication operation.</returns>
    Task AuthenticateAsync();

    /// <summary>
    /// Applies authentication to an HTTP request.
    /// </summary>
    /// <param name="request">The HTTP request to authenticate.</param>
    /// <param name="queryParameters">The query parameters of the request.</param>
    /// <param name="bodyParameters">The body parameters of the request.</param>
    void AuthenticateRequest(HttpRequestMessage request, IEnumerable<KeyValuePair<string, string>> queryParameters, IEnumerable<KeyValuePair<string, JsonElement>> bodyParameters);
}