[RegisterCommands("emp")]
[ConsoleAppFilter<AuthenticationFilter>]
class EmployeeCommands
{
    /// <summary>Hent alle ansatte fra Floq</summary>
    [Command("list|emp ls")]
    [Hidden]
    public async Task List(ConsoleAppContext ctx, bool includeInactive = false, CancellationToken token = default) => await FloqService.GetCurrentEmployees(includeInactive, ctx, token);

    /// <summary>Hent mine ansatt-detaljer</summary>
    [Hidden]
    public async Task Me(ConsoleAppContext ctx, CancellationToken token) => await FloqService.GetMe(ctx, token);

    /// <summary>Hent en spesifikk ansatts detaljer</summary>
    [Command("")]
    [Hidden]
    public async Task Get([Argument] int employeeId, ConsoleAppContext ctx, CancellationToken token) => await FloqService.GetEmployee(employeeId, ctx, token);
}
