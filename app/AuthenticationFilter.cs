internal class AuthenticationFilter(ConsoleAppFilter next) : ConsoleAppFilter(next)
{
    public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken)
    {
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

internal static class ConsoleAppContextExtensions
{
    public static UserSession GetUserSession(this ConsoleAppContext ctx)
    {
        UserSession? session =  (UserSession?) ctx.State;
        if (session is null or { IsExpired: true })
        {
            throw new Exception("Tried to fetch session, but session was not present or expired.");
        }

        return session;
    }
}

