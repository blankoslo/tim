[RegisterCommands("projects")]
[ConsoleAppFilter<AuthenticationFilter>]
[ConsoleAppFilter<AddStdinToContext>]
internal partial class Projects
{
    /// <param name="customer">-c, kundenavn, f.eks. "Aneo Mobility</param>
    /// <param name="ids">Gi ut kun prosjekt-IDer</param>
    [Command("")]
    public async Task<int> ListProjects(ConsoleAppContext ctx, string? customer = null, bool ids = false,
        CancellationToken token = default)
    {
        var session = ctx.GetUserSession();
        var client = HttpClientFactory.CreateFloqClientForUser(session);
        var projects = await client.GetAllProjectsWithCustomer(token);
        projects = projects.Where(p => p.Active);

        if (!string.IsNullOrEmpty(customer))
        {
            projects = projects.Where(p => p.Customer.Name.Contains(customer, StringComparison.OrdinalIgnoreCase));
        }

        projects = projects
            .OrderBy(p => p.Billable != "billable")
            .ThenBy(p => p.Id)
            .ThenBy(p => p.Name);

        if (ids)
        {
            Console.WriteLine(string.Join(Environment.NewLine, projects.Select(p => p.Id)));
            return -1;
        }

        if (projects.Count() == 0)
        {
            Console.MarkupLine("[yellow]⚠️ Ingen prosjekter funnet[/]");
            return -1;
        }

        var table = new Table().Border(TableBorder.SimpleHeavy)
            .AddColumn("Prosjekt-id")
            .AddColumn("Kunde-id")
            .AddColumn("Kunde")
            .AddColumn("Prosjekt")
            .AddColumn("Fakturerbart");

        foreach (var proj in projects)
        {
            table.AddRow(
                $"[purple]{proj.Id}[/]",
                proj.Customer.Id,
                proj.Customer.Name,
                proj.Name,
                proj.Billable == "billable" ? "✔️" : ""
            );
        }

        Console.Write(table);
        return 0;
    }
}
