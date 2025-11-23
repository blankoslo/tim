internal partial class Time
{
    /// <summary>Henter default-prosjektet ditt</summary>
    internal async Task GetDefault(ConsoleAppContext ctx, CancellationToken token = default)
    {
        var defaultProj = await UserSecretsManager.GetDefaultProject(token);
        if (defaultProj != null)
        {
            AnsiConsole.MarkupLine(Formatting.Format(defaultProj));
        }
        else
        {
            AnsiConsole.MarkupLine("[red]❌ Ingen default funnet[/]");
        }
    }
}