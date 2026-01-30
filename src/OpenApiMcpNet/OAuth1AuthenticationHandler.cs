using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;

namespace OpenApiMcpNet;

/// <summary>
/// OAuth 1.0a authentication handler.
/// Implements the OAuth 1.0a flow to obtain access tokens.
/// </summary>
public class OAuth1AuthenticationHandler : IAuthenticationHandler
{
    private readonly HttpClient _httpClient;
    private readonly string _requestTokenUrl;
    private readonly string _accessTokenUrl;
    private readonly string _consumerKey;
    private readonly string _consumerSecret;
    private string? _accessToken;
    private string? _accessTokenSecret;
    private readonly string _signatureMethod;

    /// <inheritdoc/>
    public bool IsAuthenticated { get; private set; } = false;

    protected const string KeyOAuthVersion = "1.0";

    protected const string KeyOAuthCallback = "oauth_callback";
    protected const string OAuthCallbackOOB = "oob"; // Out-of-band for server-to-server
    protected const string KeyHmacSha1SignatureMethod = "HMAC-SHA1";
    protected const string KeyHmacSha256SignatureMethod = "HMAC-SHA256";
    protected const string KeyPlainTextSignatureMethod = "PLAINTEXT";
    protected const string KeyOAuthSignature = "oauth_signature";

    /// <summary>
    /// Creates an OAuth 1.0a authentication handler for obtaining tokens via the OAuth flow.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to make OAuth requests.</param>
    /// <param name="requestTokenUrl">The request token endpoint URL.</param>
    /// <param name="accessTokenUrl">The access token endpoint URL.</param>
    /// <param name="consumerKey">The consumer key (API key).</param>
    /// <param name="consumerSecret">The consumer secret (API secret).</param>
    /// <param name="signatureMethod">The signature method. Defaults to HMAC-SHA1.</param>
    public OAuth1AuthenticationHandler(
        HttpClient httpClient,
        string requestTokenUrl,
        string accessTokenUrl,
        string consumerKey,
        string consumerSecret,
        string signatureMethod)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _requestTokenUrl = requestTokenUrl ?? throw new ArgumentNullException(nameof(requestTokenUrl));
        _accessTokenUrl = accessTokenUrl ?? throw new ArgumentNullException(nameof(accessTokenUrl));
        _consumerKey = consumerKey ?? throw new ArgumentNullException(nameof(consumerKey));
        _consumerSecret = consumerSecret ?? throw new ArgumentNullException(nameof(consumerSecret));
        _signatureMethod = signatureMethod;
    }

    /// <inheritdoc/>
    public async Task AuthenticateAsync()
    {
        // Step 1: Get request token
        var (requestToken, requestTokenSecret) = await GetRequestTokenInternalAsync();

        // Step 2: Exchange request token for access token
        // Note: In a full 3-legged OAuth flow, there would be a user authorization step here
        // For 2-legged OAuth or server-to-server, we go directly to access token
        var (accessToken, accessTokenSecret) = await GetAccessTokenAsync(requestToken, requestTokenSecret);
        SetAuthenticationResult(accessToken, accessTokenSecret);
    }

    protected async Task<(string token, string tokenSecret)> GetRequestTokenInternalAsync()
    {
        var timestamp = GetTimestamp();
        var nonce = GetNonce();

        var oauthParams = GetOAuthParameters(null);

        oauthParams[KeyOAuthCallback] = OAuthCallbackOOB; // Out-of-band for server-to-server

        // Generate signature (no token secret yet)
        var signature = GenerateSignature("POST", new Uri(_requestTokenUrl), oauthParams, string.Empty);
        oauthParams[KeyOAuthSignature] = signature;
        var request = new HttpRequestMessage(HttpMethod.Post, _requestTokenUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", BuildAuthorizationHeader(oauthParams));

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Failed to obtain OAuth 1.0a request token. Status: {response.StatusCode}, Response: {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        return ParseTokenResponse(responseContent);
    }

    protected virtual async Task<(string token, string tokenSecret)> GetAccessTokenAsync(string requestToken, string requestTokenSecret)
    {
        var timestamp = GetTimestamp();
        var nonce = GetNonce();

        var oauthParams = GetOAuthParameters(requestToken);

        // Generate signature with request token secret
        var signature = GenerateSignature("POST", new Uri(_accessTokenUrl), oauthParams, requestTokenSecret);
        oauthParams[KeyOAuthSignature] = signature;

        var request = new HttpRequestMessage(HttpMethod.Post, _accessTokenUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", BuildAuthorizationHeader(oauthParams));

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Failed to obtain OAuth 1.0a access token. Status: {response.StatusCode}, Response: {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        return ParseTokenResponse(responseContent);
    }

    /// <inheritdoc/>
    public void AuthenticateRequest(HttpRequestMessage request, IEnumerable<KeyValuePair<string, string>> queryParameters, IEnumerable<KeyValuePair<string, JsonElement>> bodyParameters)
    {
        if (!IsAuthenticated)
        {
            return;
        }
        
        var oauthParams = GetOAuthParameters(_accessToken);

        // Collect all parameters for signature base string
        var allParams = new SortedDictionary<string, string>(oauthParams);

        // Add query parameters
        foreach (var param in queryParameters)
        {
            allParams[param.Key] = param.Value;
        }

        // Add body parameters (only for application/x-www-form-urlencoded)
        if (request.Content?.Headers.ContentType?.MediaType == "application/x-www-form-urlencoded")
        {
            foreach (var param in bodyParameters)
            {
                allParams[param.Key] = param.Value.ToJsonString();
            }
        }

        // Generate signature
        var signature = GenerateSignature(request.Method.Method, request.RequestUri!, allParams, _accessTokenSecret!);
        oauthParams[KeyOAuthSignature] = signature;

        // Build Authorization header
        var authHeader = BuildAuthorizationHeader(oauthParams);
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", authHeader);
    }

    protected (string Token, string TokenSecret) ParseTokenResponse(string response)
    {
        var parameters = HttpUtility.ParseQueryString(response);
        
        var token = parameters["oauth_token"];
        var tokenSecret = parameters["oauth_token_secret"];

        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException($"OAuth response does not contain 'oauth_token'. Response: {response}");
        }

        if (string.IsNullOrEmpty(tokenSecret))
        {
            throw new InvalidOperationException($"OAuth response does not contain 'oauth_token_secret'. Response: {response}");
        }

        return (token, tokenSecret);
    }

    protected SortedDictionary<string, string> GetOAuthParameters(string? token)
    {
        var oauthParams = new SortedDictionary<string, string>
        {
            ["oauth_consumer_key"] = _consumerKey,
            ["oauth_nonce"] = GetNonce(),
            ["oauth_signature_method"] = _signatureMethod,
            ["oauth_timestamp"] = GetTimestamp(),
            ["oauth_token"] = token ?? string.Empty,
            ["oauth_version"] = "1.0"
        };

        if (!string.IsNullOrEmpty(token))
        {
            oauthParams["oauth_token"] = token;
        }

        return oauthParams;
    }

    protected string GenerateSignature(string httpMethod, Uri requestUri, SortedDictionary<string, string> allParams, string tokenSecret)
    {
        // Create parameter string
        var parameterString = string.Join("&",
            allParams.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        // Create base URL (without query string)
        var baseUrl = $"{requestUri.Scheme}://{requestUri.Host}";
        if (!requestUri.IsDefaultPort)
        {
            baseUrl += $":{requestUri.Port}";
        }
        baseUrl += requestUri.AbsolutePath;

        // Create signature base string
        var signatureBaseString = $"{httpMethod.ToUpperInvariant()}&{Uri.EscapeDataString(baseUrl)}&{Uri.EscapeDataString(parameterString)}";

        // Create signing key
        var signingKey = $"{Uri.EscapeDataString(_consumerSecret)}&{Uri.EscapeDataString(tokenSecret)}";

        // Generate signature
        return _signatureMethod switch
        {
            "HMAC-SHA1" => GenerateHmacSha1Signature(signatureBaseString, signingKey),
            "HMAC-SHA256" => GenerateHmacSha256Signature(signatureBaseString, signingKey),
            "PLAINTEXT" => signingKey,
            _ => throw new NotSupportedException($"Signature method '{_signatureMethod}' is not supported.")
        };
    }

    protected internal void SetAuthenticationResult(string accessToken, string accessTokenSecret)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new ArgumentNullException(nameof(accessToken));
        }

        if (string.IsNullOrEmpty(accessTokenSecret))
        {
            throw new ArgumentNullException(nameof(accessTokenSecret));
        }

        _accessToken = accessToken;
        _accessTokenSecret = accessTokenSecret;
        IsAuthenticated = true;
    }

    private static string GenerateHmacSha1Signature(string data, string key)
    {
        using var hmac = new HMACSHA1(Encoding.ASCII.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.ASCII.GetBytes(data));
        return Convert.ToBase64String(hash);
    }

    private static string GenerateHmacSha256Signature(string data, string key)
    {
        using var hmac = new HMACSHA256(Encoding.ASCII.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.ASCII.GetBytes(data));
        return Convert.ToBase64String(hash);
    }

    protected string BuildAuthorizationHeader(IDictionary<string, string> oauthParams)
    {
        return string.Join(", ",
            oauthParams.Select(p => $"{Uri.EscapeDataString(p.Key)}=\"{Uri.EscapeDataString(p.Value)}\""));
    }

    protected string GetTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
    }

    protected string GetNonce()
    {
        return Guid.NewGuid().ToString("N");
    }
}
