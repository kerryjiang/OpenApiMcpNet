using System.Text.Json;

namespace OpenApiMcpNet;

public interface IWebApiCaller
{
    Task<JsonElement> CallApiAsync(WebApiMetadata apiMetadata, IDictionary<string, JsonElement> parameters, CancellationToken cancellationToken);
}