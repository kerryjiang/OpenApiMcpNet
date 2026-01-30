using System.Text.Json;

namespace OpenApiMcpNet;

/// <summary>
/// Provides extension methods for <see cref="JsonElement"/>.
/// </summary>
public static class JsonElementExtensions
{
    /// <summary>
    /// Converts a <see cref="JsonElement"/> to its string representation.
    /// For strings, returns the unquoted value. For other types, returns the JSON representation.
    /// </summary>
    /// <param name="element">The JSON element to convert.</param>
    /// <returns>A string representation of the JSON element.</returns>
    public static string ToJsonString(this JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => element.GetRawText()
        };
    }
}