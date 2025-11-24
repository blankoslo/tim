using System.Runtime.Serialization;

[RegisterCommands("emp")]
[ConsoleAppFilter<AuthenticationFilter>]
class EmployeeCommands
{
    /// <summary>Hent alle ansatte fra Floq</summary>
    /// <param name="includeInactive"></param>
    /// <param name="customer">-c, Kundenavn, f.eks. "Aneo Mobility"</param>
    /// <param name="ids">
    [Command("list|emp ls")]
    public async Task List(ConsoleAppContext ctx, bool includeInactive = false, string? customer = null, bool ids = false, CancellationToken token = default)
    {
        var session = ctx.GetUserSession();
        var folqClient = HttpClientFactory.CreateFolqClientForUser(session);

        if (customer == null)
        {
            var res = await folqClient.GetEmployees(token);
            var steadies = res
                .Where(e => e.ActivelyEmployeed() || includeInactive)
                .OrderBy(e => e.Id)
                .ToArray();
            for (var index = 0; index < steadies.Length; index++)
            {
                var emp = steadies[index];
                Console.MarkupLine($"({index+1}/{steadies.Length}) " + Formatting.FormatOther(emp));
            }
        }
        else
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var from = today.AddDays(-20);
            var atClients = await folqClient.GetRpcEmployeesOnProjects(from, today, token);
            foreach (var empsAtCustomer in atClients.Where(c => c.Customer_Name == customer).GroupBy(e => e.Customer_Id))
            {
                foreach (var emp in empsAtCustomer.ToList())
                {
                    if (ids)
                    {
                        Console.WriteLine(emp.Id);
                    }
                    else
                    {
                        Console.MarkupLine($"{Formatting.FormatEmpOnProj(emp)}");
                    }
                }
            }
        }
    }



    /// <summary>Hent mine ansatt-detaljer</summary>
    public async Task Me(ConsoleAppContext ctx, CancellationToken token)
    {
        var session = ctx.GetUserSession();
        var folqClient = HttpClientFactory.CreateFolqClientForUser(session);
        var emp = await folqClient.GetEmployeeByEmail(session.Email, token);
        if (emp != null)
        {
            Console.MarkupLine(Formatting.FormatOther(emp));
        }
        else
        {
            Console.MarkupLine($"Fant deg ikke i Folq på epost {session.Email}");
        }
    }

    /// <summary>Hent en spesifikk ansatts detaljer</summary>
    /// <param name="employeeId">-e, EmployeeID i Folq.</param>
    [Command("")]
    public async Task Get(ConsoleAppContext ctx, [Argument] int employeeId, CancellationToken token = default)
    {
        var session = ctx.GetUserSession();
        var folqClient = HttpClientFactory.CreateFolqClientForUser(session);
        var emp = await folqClient.GetEmployee(employeeId, token);
        if (emp != null)
        {
            Console.MarkupLine(Formatting.FormatOther(emp));
        }
        else
        {
            Console.MarkupLine($"Fant ikke ansatt {employeeId} i Folq");
        }
    }
}
