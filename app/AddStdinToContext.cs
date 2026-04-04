internal class AddStdinToContext(ConsoleAppFilter next) : ConsoleAppFilter(next)
{
    public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken)
    {
        if(StdinBuffer.Lines is { } stdinLines)
        {
            var newState = context.State is GlobalState existing
                ? existing with { StdIn = stdinLines }
                : new GlobalState(StdIn: stdinLines);
            await Next.InvokeAsync(context with { State = newState }, cancellationToken);
        }
        else
        {
            await Next.InvokeAsync(context, cancellationToken);
        }
    }
}
