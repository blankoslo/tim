internal class AuthenticationFilter(ConsoleAppFilter next) : ConsoleAppFilter(next)
{
    public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken)
    {
        var session = await UserSecretsManager.GetFloqSession(cancellationToken);
        if (session is { IsExpired: false })
        {
            var existingState = context.State as GlobalState;
            if (existingState is { })
            {
                // Console.WriteLine("[auth] adding global state with session");
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
            Console.MarkupLine("[red]Login required.[/]");
        }
    }
}

public record GlobalState(UserSession? Session = null, string[]? StdIn = null);


