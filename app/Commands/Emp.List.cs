internal partial class Emp
{
    /// <summary>Hent alle ansatte fra Floq</summary>
    /// <param name="includeInactive"></param>
    /// <param name="customer">-c, Kundenavn, f.eks. "Aneo Mobility"</param>
    /// <param name="ids">Bare gi ut id'ene, slik at det kan pipes til 'tim ls'</param>
    [Command("list|emp ls")]
    public async Task List(ConsoleAppContext ctx, bool includeInactive = false, string? customer = null,
        bool ids = false, CancellationToken token = default)
    {
        var session = ctx.GetUserSession();
        var client = HttpClientFactory.CreateFloqClientForUser(session);

        if(customer == null)
        {
            var res = await client.GetEmployees(token);
            var steadies = res
                .Where(e => e.ActivelyEmployeed() || includeInactive)
                .OrderBy(e => e.Id)
                .ToArray();
            for(var index = 0; index < steadies.Length; index++)
            {
                var emp = steadies[index];
                if(ids)
                {
                    Console.WriteLine(emp.Id);
                }
                else
                {
                    Console.MarkupLine($"({index + 1}/{steadies.Length}) " + Formatting.FormatOther(emp));
                }
            }
        }
        else
        {
            var allEmployees = (await client.GetEmployees(token)).ToList();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var from = today.AddDays(-20);
            var platform = HttpClientFactory.CreatePlatformClientForUser(session);
            var atClients = await platform.GetBillableCustomers(from, today, token);
            var empsAtCustomers = atClients
                .Where(c => c.CustomerName == customer)
                .OrderBy(e => e.EmployeeId)
                .GroupBy(e => e.CustomerId);

            foreach(var empsAtCustomer in empsAtCustomers)
            {
                foreach(var placement in empsAtCustomer.ToList())
                {
                    var emp = allEmployees.FirstOrDefault(e => e.Id == placement.EmployeeId);
                    if(emp == null || (!includeInactive && !emp.ActivelyEmployeed()))
                    {
                        continue;
                    }

                    if(ids)
                    {
                        Console.WriteLine(emp.Id);
                    }
                    else
                    {
                        Console.MarkupLine($"{Formatting.FormatEmpOnProj(placement, emp)}");
                    }
                }
            }
        }
    }
}
