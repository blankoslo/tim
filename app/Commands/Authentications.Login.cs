using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

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
                using var http = new HttpListener();
                http.Prefixes.Add(redirectUrl);
                http.Start();
                ctx1.Status = "Web-server started for å motta callback";

                var (codeVerifier, codeChallenge) = OidcAuthClient.GeneratePkce();
                var state = OidcAuthClient.GenerateState();
                var loginUrl = OidcAuthClient.BuildAuthorizationUrl(redirectUrl, state, codeChallenge);

                Process.Start(new ProcessStartInfo
                {
                    FileName = loginUrl,
                    UseShellExecute = true
                });
                ctx1.Status = "Venter på at du skal fulløre innlogging i browser...";
                Console.MarkupLineInterpolated($"Åpner innlogging i browser. Hvis ikke, klikk her: [link={loginUrl}]{loginUrl}[/]");
                var callback = await http.GetContextAsync().WaitAsync(token);
                ctx1.Status = "Callback mottatt!";

                var code = await HandleAuthorizationCallback(callback, state, token);
                if(code == null)
                {
                    ctx1.Status = ":/";
                    Console.MarkupLine($"[red]Innlogging feilet[/].\nPrøv igjen.");
                    return;
                }

                ctx1.Status = "Bytter kode mot token…";
                var tokenResponse = await OidcAuthClient.ExchangeCodeAsync(code, codeVerifier, redirectUrl, token);
                if(tokenResponse == null)
                {
                    Console.MarkupLine($"[red]Innlogging feilet[/].\n[yellow]Klarte ikke å hente token.[/]");
                    return;
                }

                ctx1.Status = "Henter brukerinfo";
                var userInfo = await OidcAuthClient.GetUserInfoAsync(tokenResponse.AccessToken, token);
                if(userInfo == null)
                {
                    Console.MarkupLine($"[red]Innlogging feilet[/].\n[yellow]Klarte ikke å hente brukerinfo.[/]");
                    return;
                }

                ctx1.Status = "Sjekker ansatt-basen.";
                var client = HttpClientFactory.CreateFloqClientForUser(tokenResponse.AccessToken);
                var emp = await client.GetEmployeeByEmail(userInfo.Email, token);
                if(emp == null)
                {
                    Console.MarkupLine($"[red]Innlogging feilet[/].\n" +
                                       $"[yellow]Ingen ansatt med e-post: [bold white]{userInfo.Email}[/][/].");
                    return;
                }

                ctx1.Status = "Ansatt-match funnet";
                await UserSecretsManager.WriteTokenData(tokenResponse, userInfo.Email, emp, token);
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

    private static async Task<string?> HandleAuthorizationCallback(HttpListenerContext context, string expectedState,
        CancellationToken token)
    {
        var query = context.Request.QueryString;
        var failed = query["error"] != null || query["state"] != expectedState || query["code"] == null;

        var html = Html.LayoutHtml.Replace("{{InnerHtml}}", failed ? Html.ErrorInnerHtml : Html.SuccessInnerHtml);
        var buffer = Encoding.UTF8.GetBytes(html);
        await context.Response.OutputStream.WriteAsync(buffer, token);
        context.Response.OutputStream.Close();

        return failed ? null : query["code"];
    }
}
