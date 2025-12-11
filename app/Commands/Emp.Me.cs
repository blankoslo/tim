internal partial class Emp
{
    /// <summary>Hent mine ansatt-detaljer</summary>
    public async Task Me(ConsoleAppContext ctx, CancellationToken token)
    {
        var session = ctx.GetUserSession();
        var client = HttpClientFactory.CreateFloqClientForUser(session);
        var emp = await client.GetEmployeeByEmail(session.Email, token);
        if (emp != null)
        {
            Console.MarkupLine(Formatting.FormatOther(emp));
        }
        else
        {
            Console.MarkupLine($"Fant deg ikke i Floq på epost {session.Email}");
        }
    }
}