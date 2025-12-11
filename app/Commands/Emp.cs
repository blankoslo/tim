[RegisterCommands("emp")]
[ConsoleAppFilter<AuthenticationFilter>]
internal partial class Emp
{
    /// <summary>Hent en spesifikk ansatts detaljer</summary>
    /// <param name="employeeId">-e, EmployeeID i Floq.</param>
    [Command("")]
    public async Task Get(ConsoleAppContext ctx, [Argument] int? employeeId = null, CancellationToken token = default)
    {
        var session = ctx.GetUserSession();
        var client = HttpClientFactory.CreateFloqClientForUser(session);

        if (!employeeId.HasValue)
        {
            await Me(ctx, token);
            return;
        }

        var emp = await client.GetEmployee(employeeId.Value, token);
        Console.MarkupLine(emp != null ? Formatting.FormatOther(emp) : $"Fant ikke ansatt {employeeId} i Floq");
    }
}
