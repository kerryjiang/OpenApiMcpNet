using System.Text.Json;

namespace OpenApiMcpNet;

public interface IAuthenticationHandler
{
    public bool IsAuthenticated { get; }

    Task AuthenticateAsync();

    void AuthenticateRequest(HttpRequestMessage request, IEnumerable<KeyValuePair<string, string>> queryParameters, IEnumerable<KeyValuePair<string, JsonElement>> bodyParameters);
}