[RegisterCommands("emp")]
[ConsoleAppFilter<AuthenticationFilter>]
class EmployeeCommands
{
    /// <summary>Hent alle ansatte fra Floq</summary>
    [Command("list|emp ls")]
    [Hidden]
    public async Task List(ConsoleAppContext ctx, bool includeInactive = false, CancellationToken token = default)
    {
        var session = (UserSession) ctx.State!;
        var folqClient = HttpClientFactory.CreateFolqClientForUser(session);
        var res = await folqClient.GetEmployees(token);
        var steadies = res
            .Where(e => e.ActivelyEmployeed() || includeInactive)
            .OrderBy(e => e.Id)
            .ToArray();
        for (var index = 0; index < steadies.Length; index++)
        {
            var emp = steadies[index];
            Console.MarkupLine($"({index+1}/{steadies.Length}) " + Format(emp));
        }
    }

    /// <summary>Hent mine ansatt-detaljer</summary>
    [Hidden]
    public async Task Me(ConsoleAppContext ctx, CancellationToken token)
    {
        var session = (UserSession)ctx.State!;
        var folqClient = HttpClientFactory.CreateFolqClientForUser(session);
        var emp = await folqClient.GetEmployeeByEmail(session.Email, token);
        if (emp != null)
        {
            Console.MarkupLine(Format(emp));
        }
        else
        {
            Console.MarkupLine($"Fant deg ikke i Folq på epost {session.Email}");
        }
    }

    /// <summary>Hent en spesifikk ansatts detaljer</summary>
    [Command("")]
    [Hidden]
    public async Task Get([Argument] int employeeId, ConsoleAppContext ctx, CancellationToken token)
    {
        var session = (UserSession) ctx.State!;
        var folqClient = HttpClientFactory.CreateFolqClientForUser(session);
        var emp = await folqClient.GetEmployee(employeeId, token);
        if (emp != null)
        {
            Console.MarkupLine(Format(emp));
        }
        else
        {
            Console.MarkupLine($"Fant ikke ansatt {employeeId} i Folq");
        }
    }

    private static string Format(Employee emp)
    {
        var color = emp.ActivelyEmployeed() ? "white" : "grey dim";
        return $"[{color}]{emp.First_Name} {emp.Last_Name}[/] [[id:{emp.Id}]]";
    }
}
