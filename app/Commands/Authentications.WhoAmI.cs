internal partial class Authentications
{
    /// <summary></summary>
    /// <param name="debug" hidden>-d, Vis ALT du har på meg</param>
    [Hidden]
    [Command("whoami")]
    [ConsoleAppFilter<AuthenticationFilter>]
    public async Task WhoamI(ConsoleAppContext ctx, bool debug = false, CancellationToken token = default)
    {
        Console.MarkupLine($"[dim]{UserSecretsManager.GetAppDataPath()}[/]:");
        var session = ctx.GetUserSession();
        var json = await UserSecretsManager.ReadJson(token);
        Console.WriteLine(json ?? "<ingen secrets.json funnet>");
        Console.WriteLine("\n----- session deserialized:-----");
        Console.WriteLine(Pretty(session));
        return;
        string Pretty(UserSession s) => s.ToString().Replace(",", "\n").Replace("{", "\n{\n").Replace("}", "\n}");
    }
}