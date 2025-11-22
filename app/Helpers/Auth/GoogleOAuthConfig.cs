using System.Security.Cryptography;
using System.Text;

public class GoogleOAuthConfig
{
    private GoogleOAuthConfig()
    {
        RedirectUri = TimServerConfig.LocalUrl;
        ClientId = "1085640931155-1dbno0man652b3fgf4edoersdavsdasm.apps.googleusercontent.com";
        ClientSecret = "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"; // this is okay since PKCE is used
        Scope = "openid profile email";
        AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        TokenEndpoint = "https://oauth2.googleapis.com/token";
        State = Guid.NewGuid().ToString("N");
        CodeVerifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        using var sha256 = SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(CodeVerifier));
        CodeChallenge = Convert.ToBase64String(challengeBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static GoogleOAuthConfig Instance { get; } = new();

    public string RedirectUri { get; }
    public string ClientId { get; }
    public string ClientSecret { get; }
    public string Scope { get; }
    public string AuthorizationEndpoint { get; }
    public string TokenEndpoint { get; }
    public string State { get; }
    public string CodeVerifier { get; }
    public string CodeChallenge { get; }
}
