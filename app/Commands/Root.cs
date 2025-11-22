[RegisterCommands]
internal class Root
{
    /// <summary>Logger inn via browser</summary>
    [Command("login")]
    public async Task Login(ConsoleAppContext ctx, CancellationToken token = default) => await AuthCommands.FolqLogin(ctx, token);

    /// <summary>Logger deg ut lokalt</summary>
    [Command("logout")]
    public async Task Logout(ConsoleAppContext ctx, CancellationToken token = default) => await AuthCommands.FolqLogout(ctx, token);

    /// <summary></summary>
    /// <param name="debug" hidden>-d, Vis ALT du har på meg</param>
    [ConsoleAppFilter<AuthenticationFilter>]
    [Hidden]
    public async Task WhoamI(ConsoleAppContext ctx, bool debug = false, CancellationToken token = default) => await AuthCommands.WhoAmI(ctx, token);

    /// <summary>
    /// curl '/employees?id=eq.1'
    /// </summary>
    /// <param name="uri">subpath: f.eks. `/employees`</param>
    /// <param name="data">-d, json body: f.eks. `{}`</param>
    /// <param name="x">-X, method: f.eks. `GET|POST|PUT`</param>
    /// <param name="h">-H, Headere f.eks. 'Accept: application/json'</param>
    [ConsoleAppFilter<AuthenticationFilter>]
    [Hidden]
    public async Task Curl(ConsoleAppContext ctx, [Argument] string uri, string x = "GET", string? data = null, string[]? h = null, CancellationToken token = default) =>
        await FloqService.Curl(ctx, x, uri, data, h, token);
}
