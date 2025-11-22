using System.Diagnostics;
using System.Net;

public class AuthCommands
{
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

    internal static async Task FolqLogin(ConsoleAppContext ctx, CancellationToken token = default)
    {
        await Console.Status()
            .Spinner(Spinner.Known.Star)
            .StartAsync("Starter login-flyt…", async ctx =>
            {
                using var http = new HttpListener();
                http.Prefixes.Add(GoogleOAuthConfig.Instance.RedirectUri);
                http.Start();
                ctx.Status = "Web-server started for å motta callback";
                ctx.Refresh();
                Process.Start(new ProcessStartInfo { FileName = $"https://inni.blank.no/login/oauth?to={TimServerConfig.LocalUrl}", UseShellExecute = true });
                ctx.Status = "Browser åpnet for innlogging...";
                ctx.Refresh();
                var callback = await http.GetContextAsync().WaitAsync(token);
                ctx.Status = "Callback mottatt!";
                ctx.Refresh();

                var data = await GoogleOAuthHelpers.HandleImplicitFlowCallback(callback, token);
                if (data == null)
                {
                    ctx.Status = "Innlogging feilet (ingen implicit flow fra front-kanal)";
                    ctx.Refresh();
                }
                else
                {
                    ctx.Status = "Flott. Data mottatt!";
                    ctx.Refresh();
                    var emp = await FloqService.GetEmployee(data.AccessToken, data.UserEmail, token);
                    ctx.Status = "Ansatt-data hentet!";
                    ctx.Refresh();
                    await UserSecretsManager.WriteImplicitData(data, emp, token);
                    ctx.Status = "Innlogging fullført";
                    ctx.Refresh();
                }
            });
        Console.MarkupLine($" ✅ Innlogget.");
    }

    internal static async Task WhoAmI(ConsoleAppContext ctx, CancellationToken token)
    {
        AnsiConsole.MarkupLine($"[green]🔍 Sjekker hvem du er...[/]");
        UserSession session = (ctx.State as UserSession)!;
        var json = await UserSecretsManager.ReadJson(token);
        Console.MarkupLine("\n----- secrets.json -----");
        Console.WriteLine(json ?? "<ingen secrets.json funnet>");
        Console.WriteLine("\n----- session -----");
        Console.WriteLine(session.ToString().Replace(",", "\n").Replace("{", "\n{\n").Replace("}", "\n}"));
        Console.WriteLine("----------");
    }

    internal static async Task FolqLogout(ConsoleAppContext ctx, CancellationToken token)
    {
        await UserSecretsManager.RemoveFolqSession(token);
    }
}
