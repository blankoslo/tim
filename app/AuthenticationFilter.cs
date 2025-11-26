using System.Reflection;

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
                var newState = existingState with { Session = session };
                await Next.InvokeAsync(context with { State = newState}, cancellationToken);
            }
            else
            {
                await Next.InvokeAsync(context with { State = new GlobalState(session)}, cancellationToken);
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Login required.[/]");
        }
    }
}

public record GlobalState(UserSession? Session = null, string[]? StdIn = null);


