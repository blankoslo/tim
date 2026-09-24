using System.Net;
using System.Net.Sockets;

internal partial class Authentications
{
    /// <summary>Logger inn via browser</summary>
    [Command("login")]
    public async Task Login(ConsoleAppContext ctx, CancellationToken token = default)
    {
        await LoginImpl(token);
    }

    [Command("refresh")]
    [Hidden]
    public async Task Refresh(ConsoleAppContext ctx, CancellationToken token = default)
    {
        await UserSecretsManager.RefreshFloqSession(token);
    }

    public static async Task LoginImpl(CancellationToken token)
    {
        await Console.Status()
            .AutoRefresh(true)
            .Spinner(Spinner.Known.Star)
            .StartAsync("Starter login-flyt…", async ctx1 =>
            {
                var port = FindFirstAvailablePort();
                var redirectUrl = $"http://localhost:{port}/";
                ctx1.Status = "Venter på at du skal fullføre innlogging i browser...";

                var loginResult = await OidcAuthClient.LoginAsync(redirectUrl, new LoopbackBrowser(redirectUrl), token);
                if(loginResult.IsError)
                {
                    ctx1.Status = ":/";
                    Console.MarkupLine($"[red]Innlogging feilet[/].\n[yellow]{loginResult.Error}: {loginResult.ErrorDescription}[/]");
                    return;
                }

                ctx1.Status = "Sjekker ansatt-basen.";
                var email = loginResult.User.FindFirst("email")?.Value;
                if(email == null)
                {
                    Console.MarkupLine($"[red]Innlogging feilet[/].\n[yellow]Fant ingen e-post i innloggingen.[/]");
                    return;
                }

                var client = HttpClientFactory.CreateFloqClientForUser(loginResult.AccessToken);
                var emp = await client.GetEmployeeByEmail(email, token);
                if(emp == null)
                {
                    Console.MarkupLine($"[red]Innlogging feilet[/].\n" +
                                       $"[yellow]Ingen ansatt med e-post: [bold white]{email}[/][/].");
                    return;
                }

                ctx1.Status = "Ansatt-match funnet";
                await UserSecretsManager.WriteTokenData(loginResult, email, emp, token);
                ctx1.Status = "Innlogging fullført";
                Console.MarkupLine($"Innlogget som [green]{emp.First_Name} [dim]({emp.Email})[/][/]");
            });
    }

    private static int FindFirstAvailablePort()
    {
        int port;
        var listener = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            listener.Start();
            port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }

        return port;
    }
}
