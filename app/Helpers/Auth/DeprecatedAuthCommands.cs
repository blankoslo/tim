using System.Diagnostics;
using System.Net;

public class DeprecatedAuthCommands
{
    // Beholder koden i tilfelle vi skal gjøre noe med Floq auth senere
    [Obsolete("Bruk floq login istedet")]
    public static async Task GoogleLogin(CancellationToken token)
    {
        Console.WriteLine("Åpner nettleser for å logge inn direkte mot Google");
        Process.Start(new ProcessStartInfo { FileName = GoogleOAuthHelpers.GoogleAuthRequest(), UseShellExecute = true });

        using var http = new HttpListener();
        http.Prefixes.Add(GoogleOAuthConfig.Instance.RedirectUri);
        http.Start();
        var callback = await http.GetContextAsync().WaitAsync(token);

        var code = await GoogleOAuthHelpers.HandleAuthCodeCallback(callback, token);
        if (code == null)
        {
            Console.MarkupLine("❌ Innlogging feilet (ingen code).");
            return;
        }

        var exchange = await GoogleOAuthHelpers.ExchangeCodeForTokens(code, token);
        if (exchange == null)
        {
            Console.WriteLine("❌ Innlogging feilet (kode-utveksling!) det er ikke du - det er meg :/).");
            return;
        }

        await UserSecretsManager.WriteCodeExchange(exchange, token);
        var decoded = JwtHelper.Decode(exchange.IdToken);
        Console.WriteLine($"✅ Innlogget som {decoded.Name} ({decoded.Email})!");
    }
}
