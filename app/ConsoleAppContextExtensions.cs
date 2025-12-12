internal static class ConsoleAppContextExtensions
{
    extension(ConsoleAppContext ctx)
    {
        public UserSession UserSession => ctx.GetUserSession();

        public UserSession GetUserSession()
        {
            var state = (GlobalState?)ctx.State;
            if(state is null or { Session: null })
            {
                throw new Exception("Tried to fetch session, but session was not present or expired.");
            }

            if(state is { Session.IsExpired: true })
            {
                throw new Exception("Session expired.");
            }

            return state.Session;
        }

        public void StandardInput(Action<StdinLine> linehandler)
        {
            var state = (GlobalState?)ctx.State;
            if(state is null or { StdIn: null })
            {
                return;
            }

            foreach(var line in state.StdIn)
            {
                linehandler(new StdinLine(line));
            }
        }
    }
}

internal record StdinLine(string line)
{
    public bool IsInteger(out int result)
    {
        return int.TryParse(line, out result);
    }
}
