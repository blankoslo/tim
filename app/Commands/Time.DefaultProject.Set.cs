internal partial class Time
{
    /// <summary>Setter et prosjekt som default til timeføring</summary>
    /// <param name="project">-p, Prosjektets kode. Eks: 'ANE1006'</param>
    public async Task SetDefault(ConsoleAppContext ctx, [Argument, HideDefaultValue] string? project = null,  CancellationToken token = default)
    {
        var session = ctx.GetUserSession();
        var folqClient = HttpClientFactory.CreateFolqClientForUser(session);

        DateOnly oneWeekAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        if (oneWeekAgo.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            oneWeekAgo = oneWeekAgo.AddDays(-3);
        var hoursLoggedOnProjectsLastWeek =
            await folqClient.GetRpcProjectsForEmployeeForDate(session.EmployeeId, oneWeekAgo, token);
        var projectList = hoursLoggedOnProjectsLastWeek.ToList();

        if (projectList.Count == 0)
        {
            AnsiConsole.MarkupLine("lol, du har [red]ingen[/] timeføringer på prosjekter forrige uke, " +
                                   "så her burde jeg ha gitt deg et annet relevant utvalg å velge i. " +
                                   "Meeeeen [bold]DET[/] har jeg ikke støtte for ennå." +
                                   "Jeg må be Dem bruke --project|-p for å angi prosjekt istedet.");
            return;
        }

        List<string> choices = projectList.Select(Formatting.Format).ToList();

        var prompt = new SelectionPrompt<string>
                     {
                         Title = "Velg et prosjekt å sette som [green]standard[/]:",
                         PageSize = 10,
                         MoreChoicesText =
                             "[grey](Bla opp og ned for å se flere prosjekter)[/]"
                     };
        prompt.AddChoices(choices.ToArray());
        prompt.HighlightStyle(new Style(background: Color.Purple, decoration: Decoration.Dim));

        string selected = AnsiConsole.Prompt(prompt);

        var selectedProject = projectList[choices.IndexOf(selected)];
        AnsiConsole.MarkupLine($"{Formatting.Format(selectedProject)}");
        await UserSecretsManager.StoreDefaultProject(new UserDefaultedProject(selectedProject.Id, selectedProject.Project, selectedProject.Customer), token);
    }
}