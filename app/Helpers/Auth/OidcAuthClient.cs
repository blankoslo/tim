using System.Buffers.Text;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

// Floq's own authorization server (an OpenID Connect provider with discovery at
// https://inni.blank.no/.well-known/openid-configuration). tim is a registered
// public client, so it authenticates with PKCE (S256) instead of a client secret.
public static class OidcAuthClient
{
    private const string Issuer = "https://inni.blank.no";
    private const string ClientId = "cf3589d756aebd342dce402c648e5296";
    private const string Scope = "openid email profile offline_access role:employee";

    private static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri(Issuer),
        Timeout = TimeSpan.FromSeconds(10)
    };

    public static (string Verifier, string Challenge) GeneratePkce()
    {
        var verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    public static string GenerateState() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

    public static string BuildAuthorizationUrl(string redirectUri, string state, string codeChallenge)
    {
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = ClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = Scope,
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        var qs = string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return $"{Issuer}/login/oauth/as/authorize?{qs}";
    }

    public static async Task<TokenResponse?> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri,
        CancellationToken token)
    {
        var response = await Client.PostAsync("/login/oauth/as/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = ClientId,
                ["code_verifier"] = codeVerifier
            }), token);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync(AuthJsonSerializerContext.Default.TokenResponse, token)
            : null;
    }

    public static async Task<TokenResponse?> RefreshAsync(string refreshToken, CancellationToken token)
    {
        var response = await Client.PostAsync("/login/oauth/as/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = ClientId
            }), token);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync(AuthJsonSerializerContext.Default.TokenResponse, token)
            : null;
    }

    public static async Task<UserInfoResponse?> GetUserInfoAsync(string accessToken, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/login/oauth/as/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await Client.SendAsync(request, token);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync(AuthJsonSerializerContext.Default.UserInfoResponse, token)
            : null;
    }
}

public record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

public record UserInfoResponse(
    [property: JsonPropertyName("email")] string Email);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(TokenResponse))]
[JsonSerializable(typeof(UserInfoResponse))]
internal partial class AuthJsonSerializerContext : JsonSerializerContext
{
}
