using System.Diagnostics;
using System.Net;

internal partial class Authentications
{
    /// <summary>Logger inn via browser</summary>
    [Command("login")]
    public async Task Login(ConsoleAppContext ctx, CancellationToken token = default)
    {
        await LoginImpl(token);
    }

    public static async Task LoginImpl(CancellationToken token)
    {
        await Console.Status()
            .AutoRefresh(true)
            .Spinner(Spinner.Known.Star)
            .StartAsync("Starter login-flyt…", async ctx1 =>
            {
                using var http = new HttpListener();
                http.Prefixes.Add(GoogleOAuthConfig.Instance.RedirectUri);
                http.Start();
                ctx1.Status = "Web-server started for å motta callback";
                Process.Start(new ProcessStartInfo { FileName = $"https://inni.blank.no/login/oauth?to={TimServerConfig.LocalUrl}", UseShellExecute = true });
                ctx1.Status = "Venter på at du skal skal fulløre innlogging i browser...";
                var callback = await http.GetContextAsync().WaitAsync(token);
                ctx1.Status = "Callback mottatt!";

                var data = await GoogleOAuthHelpers.HandleImplicitFlowCallback(callback, token);
                if (data == null)
                {
                    ctx1.Status = ":/";
                    Console.MarkupLine($"[red]Innlogging feilet[/].\nPrøv igjen.");
                    return;
                }

                ctx1.Status = "Sjekker ansatt-basen.";
                var client = HttpClientFactory.CreateFloqClientForUser(data.AccessToken);
                var emp = await client.GetEmployeeByEmail(data.UserEmail, token);
                if (emp == null)
                {
                    Console.MarkupLine($"[red]Innlogging feilet[/].\n" +
                                       $"[yellow]Ingen ansatt med e-post: [bold white]{data.UserEmail}[/][/].");
                    return;
                }

                ctx1.Status = "Ansatt-match funnet";
                await UserSecretsManager.WriteImplicitData(data, emp, token);
                ctx1.Status = "Innlogging fullført";
                Console.MarkupLine($"Innlogget som [green]{emp.First_Name} [dim]({emp.Email})[/][/]");
            });
    }
}