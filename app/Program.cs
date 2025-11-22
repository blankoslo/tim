var app = ConsoleApp.Create();
app.Add("", ThisWeekOrLoginRequriredCommand);
await app.RunAsync(args);

async Task ThisWeekOrLoginRequriredCommand (ConsoleAppContext ctx, CancellationToken token)
{
    var session = await UserSecretsManager.GetFolqSession(token);
    if (session is { IsExpired: false })
    {
        var newCtx = ctx with { State = session };
        await FloqService.GetLoggedHours(newCtx, Range.Week, token);
    }
    else
    {
        if (session is { IsExpired: true })
        {
            Console.MarkupLine($"[red] Sesjon utløpt.[/] Vennligst logg inn på nytt med `tim login`.");
            return;
        }

        Console.Write(
            new FigletText("tim")
                .Color(Color.Purple));

        Console.MarkupLine($"[dim] v{AppInfoHelper.App.MajorMinorPatch}[/]");
        Console.MarkupLine($"[dim] {AppInfoHelper.App.Informational}[/]");
        Console.MarkupLine("""
                           []
                           Velkommen til tim. Kom igang med 
                           - [green] `tim login`[/]. 
                           - [green] `tim -h|--help[/]`.
                           [/]
                           """);
        var wantToLogin = new ConfirmationPrompt("Vil du logge inn");
        var yesPlz = Console.Prompt(wantToLogin);
        if (yesPlz)
        {
            await AuthCommands.FolqLogin(ctx, token);
        }
        else
        {
            Console.MarkupLine("np, tar det senere :)");
        }
    }
}
