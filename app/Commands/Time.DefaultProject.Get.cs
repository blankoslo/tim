internal partial class Time
{
    /// <summary>Henter default-prosjektet ditt</summary>
    public async Task<int> GetDefault(ConsoleAppContext ctx, bool ids = false, CancellationToken token = default)
    {
        var defaultProj = await UserSecretsManager.GetDefaultProject(token);
        if(defaultProj != null)
        {
            if(ids)
            {
                Console.WriteLine(defaultProj.Id);
            }
            else
            {
                AnsiConsole.MarkupLine(Formatting.Format(defaultProj));
            }

            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine("[red]❌ Ingen default funnet[/]");
            return -1;
        }
    }
}
