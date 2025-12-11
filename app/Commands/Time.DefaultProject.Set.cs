using System.Globalization;

using static Spectre.Console.AnsiConsole;

internal partial class Time
{
    [Hidden]
    public async Task SetDefaultNull(ConsoleAppContext ctx, CancellationToken token = default)
    {
        await UserSecretsManager.StoreDefaultProject(null, token);
        MarkupLine($"[green]✅ Slettet default prosjekt[/]");
    }

    /// <summary>Setter et prosjekt som default til timeføring</summary>
    /// <param name="project">-p, Prosjektets kode. Eks: 'ANE1006'</param>
    public async Task SetDefault(ConsoleAppContext ctx, [Argument, HideDefaultValue] string? project = null,  CancellationToken token = default)
    {
        var session = ctx.GetUserSession();
        var client = HttpClientFactory.CreateFloqClientForUser(session);


        var currentDefaultProject = await UserSecretsManager.GetDefaultProject(token);
        if(currentDefaultProject is not null)
        {
            await SelectFromSameCustomerAsCurrentDefaultProject(token, client, currentDefaultProject);
            return;
        }

        List<GetAllProjectsIncludeCustomer> allProjectsForTopMinutedCustomer = await GetProjectsForMostActiveProject(token, client, session);

        if (allProjectsForTopMinutedCustomer.Count == 0)
        {
            List<string> choices = allProjectsForTopMinutedCustomer.OrderByDescending(p => p.Id).Select(Formatting.Format).ToList();

            var prompt = new SelectionPrompt<string>
                         {
                             Title = "Velg et nylig prosjekt å sette som [green]standard[/]:",
                             PageSize = 10,
                             MoreChoicesText =
                                 "[grey](Bla opp og ned for å se flere prosjekter)[/]"
                         };
            prompt.AddChoices(choices.ToArray());

            string selected = Prompt(prompt);

            var selectedProject = allProjectsForTopMinutedCustomer[choices.IndexOf(selected)];
            MarkupLine($"{Formatting.Format(selectedProject)}");
            await UserSecretsManager.StoreDefaultProject(new UserDefaultedProject(selectedProject.Id, selectedProject.Name, selectedProject.Customer.Name, selectedProject.Customer.Id), token);
        }
        else
        {
            var projects = await client.GetAllProjectsWithCustomer(token);
            var projectChoices = projects.Where(p => p is { Billable: "billable", Active: true, Customer.Id: not "NAV" }).OrderBy(p => p.Id).ToArray();
            var choices = projectChoices.Select(Formatting.Format).ToArray();;

            var prompt = new SelectionPrompt<string>
                         {
                             Title = "Velg et prosjekt blant alle å sette som [green]standard[/]:",
                             PageSize = 10,
                             MoreChoicesText =
                                 "[grey](Bla opp og ned for å se flere prosjekter)[/]"
                         };
            prompt.AddChoices(choices.ToArray());

            string selected = Prompt(prompt);

            var selectedProject = projectChoices[choices.IndexOf(selected)];
            MarkupLine($"{Formatting.Format(selectedProject)}");
            await UserSecretsManager.StoreDefaultProject(new UserDefaultedProject(selectedProject.Id, selectedProject.Name, selectedProject.Customer.Name, selectedProject.Customer.Id), token);
        }
    }

    private static async Task<List<GetAllProjectsIncludeCustomer>> GetProjectsForMostActiveProject(CancellationToken token, FloqClient client,
        UserSession session)
    {
        DateOnly[] datesLastTwoWeeks = new DateOnly[14];

        var allTasks = new List<Task<IEnumerable<RpcProjectsForEmployeeeForDateResponse>>>();

        foreach (var date in datesLastTwoWeeks)
        {
            var task = client.GetRpcProjectsForEmployeeForDate(session.EmployeeId, date, token);
            allTasks.Add(task);
        }

        var projectsLastWeeks = await Task.WhenAll(allTasks);
        var recentTimeforing = projectsLastWeeks.SelectMany(p => p).ToList();
        var recentTimeforingGroupedByProject = recentTimeforing.GroupBy(p => p.Id);

        RpcProjectsForEmployeeeForDateResponse timeforingForTopProject = recentTimeforingGroupedByProject
            .OrderByDescending(g => g.Sum(p => p.Minutes))
            .Select(g => g.First())
            .First();

        var allProjectsWithCustomer = await client.GetAllProjectsWithCustomer(token);
        var allProjectsForTopMinutedCustomer = allProjectsWithCustomer.Where(p => p.Customer.Name == timeforingForTopProject.Customer).ToList();
        return allProjectsForTopMinutedCustomer;
    }

    private static async Task SelectFromSameCustomerAsCurrentDefaultProject(CancellationToken token, FloqClient client,
        UserDefaultedProject currentDefaultProject)
    {
        var allProjects = await client.GetAllProjectsWithCustomer(token);
        var defaultProjectDetails = allProjects.First(p => p.Id == currentDefaultProject.Id);
        MarkupLine($"Nåværende default-prosjekt er hos {Formatting.Format(defaultProjectDetails.Customer)}");
        var choices = allProjects
            .Where(p => p.Customer.Id == defaultProjectDetails.Customer.Id)
            .OrderByDescending(p => p.Id)
            .Select(Formatting.Format)
            .ToList();
        var prompt = new SelectionPrompt<string>
                     {
                         Title = "Velg et prosjekt å sette som [green]standard[/]:",
                         PageSize = 10,
                         MoreChoicesText =
                             "[grey](Bla opp og ned for å se flere prosjekter)[/]"
                     };
        prompt.AddChoices(choices.ToArray());
        string selected = Prompt(prompt);
        var selectedProject = allProjects.First(p => Formatting.Format(p) == selected);
        MarkupLine($"{Formatting.Format(selectedProject)}");
        var customerForProject = await client.GetCustomers(token);
        var customer = customerForProject.First(c => c.Id == selectedProject.Customer.Id);
        await UserSecretsManager.StoreDefaultProject(new UserDefaultedProject(selectedProject.Id, selectedProject.Name, selectedProject.Customer.Name, customer.Id), token);
    }
}