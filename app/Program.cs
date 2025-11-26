var app = ConsoleApp.Create();
app.Add("", ThisWeekOrLoginRequriredCommand);
await app.RunAsync(args);

async Task ThisWeekOrLoginRequriredCommand (ConsoleAppContext ctx, CancellationToken token)
{
    if (ctx.Arguments.Length > 0)
    {
        Console.MarkupLine($"""
                            [red]Wah?[/] Forstod ikke '{string.Join(" ",ctx.Arguments)}`
                            Prøv [green]`tim -h|--help[/]`.
                            """);
        return;
    }

    var session = await UserSecretsManager.GetFloqSession(token);

    if (session is { IsExpired: true })
    {
        Console.MarkupLine($"[red] Sesjon utløpt.[/] Vennligst logg inn på nytt med [green]`tim login`.[/]");
        return;
    }

    if (session is { IsExpired: false })
    {
        var newCtx = ctx with { State = session };
        await Time.ListPeriod(newCtx, SelectedRange.CurrentWeek, ct: token);
        return;
    }

    // no-session, show welcome message:

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
    var wantToLogin = new ConfirmationPrompt("Logge inn?").ShowDefaultValue(true);
    var yesPlz = Console.Prompt(wantToLogin);
    if (yesPlz)
    {
        await Authentications.LoginImpl(token);
    }
    else
    {
        Console.MarkupLine("[dim]np, tar det senere[/]");
    }

}
