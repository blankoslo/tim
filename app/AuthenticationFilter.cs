internal class AuthenticationFilter(ConsoleAppFilter next) : ConsoleAppFilter(next)
{
    public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken)
    {
        // setup new state to context
        var session = await UserSecretsManager.GetFolqSession(cancellationToken);
        if (session is { IsExpired: false })
        {
            var authedContext = context with { State = session };
            await Next.InvokeAsync(authedContext, cancellationToken);
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Login required.[/]");
        }
    }
}

