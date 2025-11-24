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
        var client = HttpClientFactory.CreateFloqClientForUser(session);

        if (customer == null)
        {
            var res = await client.GetEmployees(token);
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
            var allEmployees = (await client.GetEmployees(token)).ToList();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var from = today.AddDays(-20);
            var atClients = await client.GetRpcEmployeesOnProjects(from, today, token);
            var empsAtCustomers = atClients
                .Where(c => c.Customer_Name == customer)
                .GroupBy(e => e.Customer_Id);

            foreach (var empsAtCustomer in empsAtCustomers)
            {
                foreach (var emp in empsAtCustomer.ToList())
                {
                    if (includeInactive)
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
                    else
                    {
                        var allDetails = allEmployees.FirstOrDefault(e => e.ActivelyEmployeed() && e.Id == emp.Id);
                        if(allDetails == null)
                            continue;

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
    }



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

    /// <summary>Hent en spesifikk ansatts detaljer</summary>
    /// <param name="employeeId">-e, EmployeeID i Floq.</param>
    [Command("")]
    public async Task Get(ConsoleAppContext ctx, [Argument] int employeeId, CancellationToken token = default)
    {
        var session = ctx.GetUserSession();
        var client = HttpClientFactory.CreateFloqClientForUser(session);
        var emp = await client.GetEmployee(employeeId, token);
        if (emp != null)
        {
            Console.MarkupLine(Formatting.FormatOther(emp));
        }
        else
        {
            Console.MarkupLine($"Fant ikke ansatt {employeeId} i Floq");
        }
    }
}
