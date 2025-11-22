using System.Net;
using System.Text;
using System.Text.Json;

public record GoogleCodeExchange(string AccessToken, string RefreshToken, string IdToken, DateTime ExpiresAt);

public static class GoogleOAuthHelpers
{
    public static async Task<GoogleCodeExchange?> ExchangeCodeForTokens(string code, CancellationToken token)
    {
        var config = GoogleOAuthConfig.Instance;
        using var client = new HttpClient();
        var tokenRequest = new Dictionary<string, string>
                           {
                               {"code", code},
                               {"redirect_uri", config.RedirectUri},
                               {"client_id", config.ClientId},
                               {"client_secret", config.ClientSecret},
                               {"grant_type", "authorization_code"},
                               {"code_verifier", config.CodeVerifier}
                           };

        var content = new FormUrlEncodedContent(tokenRequest);
        var tokenResponse = await client.PostAsync(config.TokenEndpoint, content, token);
        var responseText = await tokenResponse.Content.ReadAsStringAsync(token);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            Console.WriteLine($"‼️{tokenResponse.StatusCode} ({tokenResponse.ReasonPhrase}) \n{responseText}");
            return null;
        }

        var tokenData = JsonDocument.Parse(responseText);
        var root = tokenData.RootElement;

        var accessToken = root.GetProperty("access_token").GetString() ?? "";
        var refreshToken = root.GetProperty("refresh_token").GetString() ?? "";
        var idToken = root.GetProperty("id_token").GetString() ?? "";
        var expiresIn = root.GetProperty("expires_in").GetInt32();

        var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn);

        return new GoogleCodeExchange(accessToken, refreshToken, idToken, expiresAt);
    }

    public static string GoogleAuthRequest()
    {
        var config = GoogleOAuthConfig.Instance;
        return $"{config.AuthorizationEndpoint}?" +
               $"response_type=code&" +
               $"scope={Uri.EscapeDataString(config.Scope)}&" +
               $"redirect_uri={Uri.EscapeDataString(config.RedirectUri)}&" +
               $"client_id={config.ClientId}&" +
               $"state={config.State}&" +
               $"code_challenge={config.CodeChallenge}&" +
               $"code_challenge_method=S256&" +
               $"login_hint=@blank.no&" +
               $"hd=blank.no";
    }

    public static async Task<string?> HandleAuthCodeCallback(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var query = context.Request.QueryString;
        var html = Html.LayoutHtml.Replace("{{InnerHtml}}", Html.SuccessInnerHtml);

        if (query["error"] != null)
        {

            html = Html.LayoutHtml.Replace("{{InnerHtml}}", Html.ErrorInnerHtml);
        }

        var code = query["code"];
        if (code == null)
        {
            html = Html.LayoutHtml.Replace("{{InnerHtml}}", Html.ErrorInnerHtml);
        }

        var buffer = Encoding.UTF8.GetBytes(html);
        await context.Response.OutputStream.WriteAsync(buffer, cancellationToken);
        context.Response.OutputStream.Close();
        return code;
    }

    public static async Task<ImplicitCallbackData?> HandleImplicitFlowCallback(HttpListenerContext context, CancellationToken token)
    {
        var query = context.Request.QueryString;

        var html = Html.LayoutHtml.Replace("{{InnerHtml}}", Html.SuccessInnerHtml);

        if (query["error"] != null)
        {
            html = Html.LayoutHtml.Replace("{{InnerHtml}}", Html.ErrorInnerHtml);
        }

        var buffer = Encoding.UTF8.GetBytes(html);
        await context.Response.OutputStream.WriteAsync(buffer, token);
        context.Response.OutputStream.Close();

        var accessToken = query["access_token"];
        var expiryDate = query["expiry_date"];
        var refreshToken = query["refresh_token"];
        var userEmail = query["user_email"];
        ImplicitCallbackData? data = null;

        if (accessToken is not null && expiryDate is not null && refreshToken is not null && userEmail is not null)
        {
            data = new ImplicitCallbackData(accessToken, expiryDate, refreshToken, userEmail);
        }

        return data;
    }
}

public record ImplicitCallbackData(string AccessToken, string ExpireDate, string RefreshToken, string UserEmail);
