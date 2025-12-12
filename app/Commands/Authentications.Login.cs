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
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"https://inni.blank.no/login/oauth?to={redirectUrl}",
                    UseShellExecute = true
                });
                ctx1.Status = "Venter på at du skal skal fulløre innlogging i browser...";
                var callback = await http.GetContextAsync().WaitAsync(token);
                ctx1.Status = "Callback mottatt!";

                var data = await HandleImplicitFlowCallback(callback, token);
                if(data == null)
                {
                    ctx1.Status = ":/";
                    Console.MarkupLine($"[red]Innlogging feilet[/].\nPrøv igjen.");
                    return;
                }

                ctx1.Status = "Sjekker ansatt-basen.";
                var client = HttpClientFactory.CreateFloqClientForUser(data.AccessToken);
                var emp = await client.GetEmployeeByEmail(data.UserEmail, token);
                if(emp == null)
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

    private static async Task<ImplicitCallbackData?> HandleImplicitFlowCallback(HttpListenerContext context,
        CancellationToken token)
    {
        var query = context.Request.QueryString;

        var html = Html.LayoutHtml.Replace("{{InnerHtml}}", Html.SuccessInnerHtml);

        if(query["error"] != null)
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

        if(accessToken is not null && expiryDate is not null && refreshToken is not null && userEmail is not null)
        {
            data = new ImplicitCallbackData(accessToken, expiryDate, refreshToken, userEmail);
        }

        return data;
    }
}
