internal class AuthenticationFilter(ConsoleAppFilter next) : ConsoleAppFilter(next)
{
    public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken)
    {
        var session = await UserSecretsManager.GetFloqSession(cancellationToken);

        if (session is { IsExpired: true })
        {
            session = await UserSecretsManager.RefreshFloqSession(cancellationToken);
            if (session is not { IsExpired: false })
            {
                Console.MarkupLine("[red]Failed.[/] Logg inn på ny med [green]`tim login`[/] :/");
                return;
            }
        }

        if (session is { IsExpired: false })
        {
            if (context.State is GlobalState { } existingState)
            {
                GlobalState newState = existingState with { Session = session };
                await Next.InvokeAsync(context with { State = newState}, cancellationToken);
            }
            else
            {
                // Console.WriteLine("[auth] no existing state found, creating one (filter is first");
                await Next.InvokeAsync(context with { State = new GlobalState(session)}, cancellationToken);
            }
        }
        else
        {
            Console.MarkupLine("[red]Plz login.[/]");
        }
    }
}

public record GlobalState(UserSession? Session = null, string[]? StdIn = null);


