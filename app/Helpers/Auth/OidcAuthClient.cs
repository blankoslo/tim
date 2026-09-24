using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.Browser;
using Duende.IdentityModel.OidcClient.Results;

// Floq's own authorization server (an OpenID Connect provider with discovery at
// https://inni.blank.no/.well-known/openid-configuration). tim is a registered
// public client, so it authenticates with PKCE (S256) instead of a client secret.
// Duende.IdentityModel.OidcClient handles PKCE, CSRF state, building the authorize
// URL and the token/userinfo exchange for us, discovering the actual endpoints from
// the discovery document rather than us hardcoding them here.
public static class OidcAuthClient
{
    private const string Authority = "https://inni.blank.no";
    private const string ClientId = "cf3589d756aebd342dce402c648e5296";
    private const string Scope = "openid email offline_access role:employee";

    public static Task<LoginResult> LoginAsync(string redirectUri, IBrowser browser, CancellationToken token)
    {
        var client = new OidcClient(BuildOptions(redirectUri, browser));
        return client.LoginAsync(new LoginRequest(), token);
    }

    public static Task<RefreshTokenResult> RefreshAsync(string refreshToken, CancellationToken token)
    {
        // No browser/redirect involved in a refresh, so it's left at its default.
        var client = new OidcClient(BuildOptions());
        return client.RefreshTokenAsync(refreshToken, cancellationToken: token);
    }

    private static OidcClientOptions BuildOptions(string redirectUri = "", IBrowser? browser = null) => new()
    {
        Authority = Authority,
        ClientId = ClientId,
        Scope = Scope,
        RedirectUri = redirectUri,
        Browser = browser!,
        // Floq's id_token carries the email claim, so we read it off LoginResult.User
        // instead of making an extra round-trip to the userinfo endpoint.
        LoadProfile = false
    };
}
