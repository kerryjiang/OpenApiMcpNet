using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OpenApiMcpNet;

/// <summary>
/// OAuth 2.0 Bearer token authentication handler.
/// Uses client credentials grant flow to obtain access tokens.
/// </summary>
public class OAuth2AuthenticationHandler : IAuthenticationHandler
{
    private readonly HttpClient _httpClient;
    private readonly string _tokenEndpoint;
    private readonly string _consumerKey;
    private readonly string _consumerSecret;
    private readonly string? _scope;
    private string? _accessToken;

    /// <inheritdoc/>
    public bool IsAuthenticated { get; private set; } = false;

    /// <summary>
    /// Creates an OAuth 2.0 authentication handler using client credentials grant.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to obtain access tokens.</param>
    /// <param name="tokenEndpoint">The OAuth 2.0 token endpoint URL.</param>
    /// <param name="consumerKey">The OAuth 2.0 client ID (consumer key).</param>
    /// <param name="consumerSecret">The OAuth 2.0 client secret (consumer secret).</param>
    /// <param name="scope">Optional scope for the access token request.</param>
    public OAuth2AuthenticationHandler(
        HttpClient httpClient,
        string tokenEndpoint,
        string consumerKey,
        string consumerSecret,
        string? scope = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _tokenEndpoint = tokenEndpoint ?? throw new ArgumentNullException(nameof(tokenEndpoint));
        _consumerKey = consumerKey ?? throw new ArgumentNullException(nameof(consumerKey));
        _consumerSecret = consumerSecret ?? throw new ArgumentNullException(nameof(consumerSecret));
        _scope = scope;
    }

    /// <inheritdoc/>
    public async Task AuthenticateAsync()
    {
        _accessToken = await GetAccessTokenAsync();
        IsAuthenticated = true;
    }

    private async Task<string> GetAccessTokenAsync()
    {
        // Build the request content for client credentials grant
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "client_credentials")
        };

        if (!string.IsNullOrEmpty(_scope))
        {
            parameters.Add(new("scope", _scope));
        }

        var request = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(parameters)
        };

        // Add Basic authentication header with client credentials
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_consumerKey}:{_consumerSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var response = await _httpClient.SendAsync(request);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();

            throw new HttpRequestException(
                statusCode: response.StatusCode,
                message: $"Failed to obtain OAuth 2.0 access token. Status: {response.StatusCode}, Response: {errorContent}",
                inner: null);
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

        if (!tokenResponse.TryGetProperty("access_token", out var accessTokenElement))
        {
            throw new InvalidOperationException(
                $"OAuth 2.0 token response does not contain 'access_token'. Response: {responseContent}");
        }

        var accessToken = accessTokenElement.GetString();
        
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new InvalidOperationException("OAuth 2.0 access token is null or empty.");
        }

        return accessToken;
    }

    /// <inheritdoc/>
    public void AuthenticateRequest(HttpRequestMessage request, IEnumerable<KeyValuePair<string, string>> queryParameters, IEnumerable<KeyValuePair<string, JsonElement>> bodyParameters)
    {
        if (!IsAuthenticated)
        {
            return;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    }
}
