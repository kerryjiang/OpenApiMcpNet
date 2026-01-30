using System.Text.Json;

namespace OpenApiMcpNet;

/// <summary>
/// A no-operation authentication handler that does nothing.
/// Use this when the API does not require authentication.
/// </summary>
public class NoOpAuthenticationHandler : IAuthenticationHandler
{
    /// <summary>
    /// Singleton instance of the no-op handler.
    /// </summary>
    public static readonly NoOpAuthenticationHandler Instance = new();

    public bool IsAuthenticated => true;


    /// <inheritdoc/>
    public Task AuthenticateAsync()
    {
        // No-op: does nothing
        return Task.CompletedTask;
    }

    public void AuthenticateRequest(HttpRequestMessage request, IEnumerable<KeyValuePair<string, string>> queryParameters, IEnumerable<KeyValuePair<string, JsonElement>> bodyParameters)
    {
        // No-op: does nothing
    }
}
