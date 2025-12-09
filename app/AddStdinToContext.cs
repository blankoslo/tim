internal class AddStdinToContext(ConsoleAppFilter next) : ConsoleAppFilter(next)
{
    public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken)
    {
        if (System.Console.IsInputRedirected)
        {
            string[] stdinArray = [];
            while (System.Console.ReadLine() is { } line)
            {
                stdinArray = stdinArray.Append(line).ToArray();
            }

            if (context.State is GlobalState existingState)
            {
                GlobalState newState = existingState with { StdIn = stdinArray };
                await Next.InvokeAsync(context with { State = newState}, cancellationToken);
            }
            else
            {
                await Next.InvokeAsync(context with { State = new GlobalState(StdIn:stdinArray)}, cancellationToken);
            }
        }
        else
        {
            await Next.InvokeAsync(context, CancellationToken.None);
        }
    }
}