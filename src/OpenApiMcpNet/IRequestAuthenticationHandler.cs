using System.Text.Json;

namespace OpenApiMcpNet;

public interface IRequestAuthenticationHandler
{
    void Authenticate(HttpRequestMessage request, IEnumerable<KeyValuePair<string, string>> queryParameters, IEnumerable<KeyValuePair<string, JsonElement>> bodyParameters);
}