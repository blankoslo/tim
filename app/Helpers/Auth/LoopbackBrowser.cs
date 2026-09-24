using System.Diagnostics;
using System.Net;
using System.Text;
using Duende.IdentityModel.OidcClient.Browser;

// The IBrowser Duende.IdentityModel.OidcClient calls into to drive the login flow:
// opens the system browser to the authorize URL it hands us, then waits for Floq's
// authorization server to redirect back to a local HttpListener on the loopback
// address (RFC 8252, "OAuth 2.0 for Native Apps").
public class LoopbackBrowser(string redirectUri) : IBrowser
{
    public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
    {
        using var http = new HttpListener();
        http.Prefixes.Add(redirectUri);
        http.Start();

        Process.Start(new ProcessStartInfo
        {
            FileName = options.StartUrl,
            UseShellExecute = true
        });
        Console.MarkupLineInterpolated($"Åpner innlogging i browser. Hvis ikke, klikk her: [link={options.StartUrl}]{options.StartUrl}[/]");

        var context = await http.GetContextAsync().WaitAsync(cancellationToken);

        var failed = context.Request.QueryString["error"] != null;
        var html = Html.LayoutHtml.Replace("{{InnerHtml}}", failed ? Html.ErrorInnerHtml : Html.SuccessInnerHtml);
        var buffer = Encoding.UTF8.GetBytes(html);
        await context.Response.OutputStream.WriteAsync(buffer, cancellationToken);
        context.Response.OutputStream.Close();

        return new BrowserResult
        {
            ResultType = BrowserResultType.Success,
            Response = context.Request.Url!.ToString()
        };
    }
}
