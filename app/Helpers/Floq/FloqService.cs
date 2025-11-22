using System.Globalization;
using System.Net.Http.Headers;

using Floq;

public static class FloqService
{
    private static readonly HttpClient Client = new()
                                                {
                                                    BaseAddress = new Uri("https://api-prod.floq.no"),
                                                };

    internal static async Task GetCurrentEmployees(bool showquitters, ConsoleAppContext ctx, CancellationToken token)
    {
        var session = (UserSession) ctx.State!;
        var folqClient = CreateFolqClientForUser(session);
        var res = await folqClient.GetEmployees(token);
        var steadies = res
            .Where(e => e.ActivelyEmployeed() || showquitters)
            .OrderBy(e => e.Id)
            .ToArray();
        for (var index = 0; index < steadies.Length; index++)
        {
            var emp = steadies[index];
            Console.MarkupLine($"({index+1}/{steadies.Length}) " + Format(emp));
        }
    }

    private static string Format(Employee emp)
    {
        var color = emp.ActivelyEmployeed() ? "white" : "grey dim";
        return $"[{color}]{emp.First_Name} {emp.Last_Name}[/] [[id:{emp.Id}]]";
    }

    private static string Format(RpcProjectsForEmployeeeForDateResponse proj)
    {
        return $"[dim]{proj.Id}[/] {proj.Project} ";
    }


    internal static async Task GetMe(ConsoleAppContext ctx, CancellationToken token)
    {
        var session = (UserSession)ctx.State!;
        var folqClient = CreateFolqClientForUser(session);
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

    internal static async Task GetEmployee(int employeeId, ConsoleAppContext ctx, CancellationToken token)
    {
        var session = (UserSession) ctx.State!;
        var folqClient = CreateFolqClientForUser(session);
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

    public static async Task<Employee?> GetEmployee(string accessToken, string eMail, CancellationToken token)
    {
        var folqClient = CreateFolqClientForUser(accessToken);
        return await folqClient.GetEmployeeByEmail(eMail, token);
    }

    internal static async Task GetLoggedHours(ConsoleAppContext ctx, Range? week = null, CancellationToken token = default)
    {
        var session = (UserSession)ctx.State!;

        var folqClient = CreateFolqClientForUser(session);
        if (session.EmployeeId != null)
        {
            var dateInWeek = DateOnly.FromDateTime(DateTime.UtcNow);
            if (week == Range.PrevWeek)
            {
                dateInWeek = dateInWeek.AddDays(-7);
            }

            var weekNo = ISOWeek.GetWeekOfYear(dateInWeek);
            var dayInRange = Enumerable.Range(0, 5)
                .Select(offset => dateInWeek.AddDays(offset - (int)dateInWeek.DayOfWeek + 1))
                .ToArray();

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.Caption = new TableTitle($"[dim] uke {weekNo}[/]");



            await AnsiConsole.Live(table)
                .Overflow(VerticalOverflow.Ellipsis)
                .Cropping(VerticalOverflowCropping.Top)
                .StartAsync(async ctx =>
            {
                ctx.Refresh();


                // Fetch all logged hours for each day
                var allTasks = new List<Task<IEnumerable<RpcProjectsForEmployeeeForDateResponse>>>();
                foreach (var singleDay in dayInRange)
                {
                    var task = folqClient.GetLoggedHours(session.EmployeeId.Value, singleDay, token);
                    allTasks.Add(task);
                }

                IEnumerable<RpcProjectsForEmployeeeForDateResponse>[] allLogged = await Task.WhenAll(allTasks);

                // Build a dictionary: projectId -> project info
                var allStaffed = allLogged.SelectMany(x => x).ToList();
                var uniqueProjects = allStaffed
                    .GroupBy(p => p.Id)
                    .Select(g => g.First())
                    .OrderBy(p => p.Id)
                    .ToList();

                if (uniqueProjects.Any())
                {
                    table.AddColumn("");
                    foreach (var day in dayInRange)
                    {
                        table.AddColumn(day.ToString("dd.MM"), c =>
                        {
                            c.Alignment = Justify.Center;
                        });
                        ctx.Refresh();
                    }

                    foreach (var proj in uniqueProjects)
                    {
                        var row = new List<string> { $"[purple]{proj.Id}[/] {Shorten(proj.Project)}" };
                        foreach (var day in dayInRange)
                        {
                            var entry = allLogged[dayInRange.ToList().IndexOf(day)].FirstOrDefault(x => x.Id == proj.Id);
                            if (entry is { Minutes: > 0 })
                            {
                                row.Add($"[white]{entry.Minutes / 60.0:F1}[/]");
                            }
                            else
                            {
                                row.Add("[gray]0[/]");
                            }
                        }
                        await Task.Delay(20, token);
                        table.AddRow(row.ToArray());
                        ctx.Refresh();
                    }

                    // add a sum row here
                    var sumRow = new List<string> { "[]Daglig sum[/]" };
                    var dailyTotals = new List<int>();
                    for (int i = 0; i < dayInRange.Count(); i++)
                    {
                        var dayTotalMinutes = allLogged[i].Sum(x => x.Minutes);
                        dailyTotals.Add(dayTotalMinutes);
                        if (dayTotalMinutes < 7.5*60)
                        {
                            sumRow.Add($"[yellow]{MinutesToHours(dayTotalMinutes)}[/]");
                        }
                        else
                        {
                            sumRow.Add($"[green]{MinutesToHours(dayTotalMinutes)}[/]");
                        }
                    }
                    table.AddEmptyRow();
                    table.AddRow(sumRow.ToArray());


                    // add a cumulative sum row with running totals
                    var cumulativeRow = new List<string> { "[dim]Kumulativ[/]" };
                    int runningTotal = 0;
                    for (var dayIndex = 0; dayIndex < dailyTotals.Count; dayIndex++)
                    {
                        var minutes = dailyTotals[dayIndex];
                        runningTotal += minutes;
                        var color = "";
                        if (dayIndex == dailyTotals.Count - 1)
                        {
                            color = runningTotal < (37.5*60) ? "red" : "green";
                        }

                        cumulativeRow.Add($"[{color}]{MinutesToHours(runningTotal)}[/]");
                    }


                    table.AddRow(cumulativeRow.ToArray());
                    ctx.Refresh();


                }
                else
                {
                    AnsiConsole.MarkupLine("[red]\nIngen bemannede prosjekter funnet for denne uka[/]");
                }
            });
        }
        else
        {
            AnsiConsole.MarkupLine("[red]❌ Din bruker er ikke koblet til en ansatt i Folq, så jeg kan ikke hente bemannede prosjekter for deg :/[/]");
        }
    }

    private static string Shorten(string someString, int defaultLength = 20)
    {
        return someString.Length > defaultLength ? someString[..defaultLength] + "…" : someString;
    }

    internal static async Task<int> Curl(ConsoleAppContext ctx,
        string method,
        string uri,
        string? inputBody = null,
        string[]? otherHeaders = null,
        CancellationToken token = default)
    {
        var session = (UserSession)ctx.State!;
        CreateFolqClientForUser(session);
        HttpMethod httpMethod = HttpMethod.Parse(method);

        var msg = new HttpRequestMessage(httpMethod, uri);
        if (inputBody is not null &&
            (httpMethod == HttpMethod.Post ||
             httpMethod == HttpMethod.Put ||
             httpMethod == HttpMethod.Patch))
        {
            msg.Content = new StringContent(inputBody);
            msg.Headers.Add("Content-Type", "application/json");
        }

        if (otherHeaders is not null)
        {
            foreach (var header in otherHeaders)
            {
                var split = header.Split(":", 2);
                if (split.Length == 2)
                {
                    var headerName = split[0].Trim();
                    var headerValue = split[1].Trim();
                    msg.Headers.Add(headerName, headerValue);
                }
            }
        }

        var response = await Client.SendAsync(msg, token);
        if (response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(token);
            Console.WriteLine($"{responseBody}");
            return 0;
        }
        else
        {
            var responseBody = await response.Content.ReadAsStringAsync(token);
            Console.WriteLine($"{(int)response.StatusCode} {response.ReasonPhrase}\n{responseBody}");
            return (int)response.StatusCode;
        }
    }

    private static FloqClient CreateFolqClientForUser(UserSession session)
    {
        return CreateFolqClientForUser(session.AccessToken, session.EmployeeId);
    }

    private static FloqClient CreateFolqClientForUser(string accessToken, int? employeeId = null)
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (employeeId is {} empId )
        {
            Client.DefaultRequestHeaders.Add("User-Agent", [
                $"tim/{AppInfoHelper.App.MajorMinorPatch}",
                $"folq-employee/{empId}"
            ]);
        }
        else
        {
            Client.DefaultRequestHeaders.Add("User-Agent", [
                $"tim/{AppInfoHelper.App.MajorMinorPatch}",
            ]);
        }

        return new FloqClient(Client);
    }

    internal static async Task SetOrSelectDefaultProject(ConsoleAppContext ctx, string? projectId,
        CancellationToken token = default)
    {
        var session = (UserSession)ctx.State!;

        var folqClient = CreateFolqClientForUser(session);
        if (session.EmployeeId != null)
        {
            DateOnly oneWeekAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
            if(oneWeekAgo.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                oneWeekAgo = oneWeekAgo.AddDays(-3);
            var hoursLoggedOnProjectsLastWeek = await folqClient.GetLoggedHours(session.EmployeeId.Value, oneWeekAgo, token);
            var projectList = hoursLoggedOnProjectsLastWeek.ToList();
            if (projectList.Count == 0)
            {
                AnsiConsole.MarkupLine("lol, du har [red]ingen[/] timeføringer på prosjekter forrige uke, " +
                                       "så her burde jeg ha gitt deg et annet relevant utvalg å velge i. " +
                                       "Meeeeen [bold]DET[/] gidder jeg ikke." +
                                       "Jeg må be Dem bruke --project|-p for å angi prosjekt istedet.");

                return;
            }

            var choices = projectList.Select(Format).ToList();

            var prompt = new SelectionPrompt<string>
                         {
                             Title = "Velg et prosjekt å sette som [green]standard[/]:",
                             PageSize = 10,
                             MoreChoicesText = "[grey](Bla opp og ned for å se flere prosjekter)[/]"
                         };
            prompt.AddChoices(choices.ToArray());
            prompt.HighlightStyle(new Style(background:Color.Purple, decoration:Decoration.Dim));

            var selected = AnsiConsole.Prompt(prompt);

            var selectedProject = projectList[choices.IndexOf(selected)];
            AnsiConsole.MarkupLine($"{Format(selectedProject)}");
            await UserSecretsManager.StoreDefaultProject(selectedProject, token);
        }
        else
        {
            AnsiConsole.MarkupLine(
                "[red]❌ Din bruker er ikke koblet til en ansatt i Folq, så jeg kan ikke hente prosjekter for deg :/[/]");
        }
    }

    internal static async Task GetDefault(ConsoleAppContext ctx, CancellationToken token = default)
    {
        var defaultProj = await UserSecretsManager.GetDefaultProject(token);
        if (defaultProj != null)
        {
            AnsiConsole.MarkupLine(Format(defaultProj));
        }
        else
        {
            AnsiConsole.MarkupLine("[red]❌ Ingen default funnet[/]");
        }
    }

    internal static async Task WriteLogEntries(Range? week, string? project, decimal? hours, ConsoleAppContext ctx, CancellationToken cancellationToken = default)
    {
        await GetLoggedHours(ctx,week, cancellationToken);

        var session = (UserSession)ctx.State!;

        if (session.EmployeeId == null)
        {
            AnsiConsole.MarkupLine("[red]❌ Innlogget sesjon er ikke koblet til en ansatt i Folq, så jeg kan ikke logge tid for deg :/[/]");
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly targetMonday;
        if (week == Range.PrevWeek)
        {
            targetMonday = today.AddDays(-7 - (int)today.DayOfWeek + 1);
        }
        else
        {
            targetMonday = today.AddDays(-(int)today.DayOfWeek + 1);
        }

        DateOnly[] daysToLog = Enumerable.Range(0, 5)
            .Select(offset => targetMonday.AddDays(offset))
            .ToArray();

        var minutesPerDay = 450;
        if (hours.HasValue)
        {
            minutesPerDay = (int)(hours.Value * 60);
        }

        var hoursFriendlyStr= minutesPerDay > 0 ? $"{minutesPerDay / 60m:F1}" : "0";

        string? targetProjectCode;
        if (project != null)
        {
            targetProjectCode = project;
        }
        else
        {
            // fetch default project:
            var defaultProj = await UserSecretsManager.GetDefaultProject(cancellationToken);
            if (defaultProj != null)
            {
                targetProjectCode = defaultProj.Id;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]❌ Ingen prosjektkode angitt, og ingen default prosjekt funnet.[/]");
                return;
            }
        }

        // Console write what values we are doing, what week and what values we are logging:
        AnsiConsole.MarkupLine($"Timefører " +
                               $"[bold]{hoursFriendlyStr}t[/] på " +
                               $"[purple]{targetProjectCode}[/] " +
                               $"[white][[{targetMonday:dd.MM}-{daysToLog.Last():dd.MM}]][/]");

        var folqClient = CreateFolqClientForUser(session);
        foreach (var day in daysToLog)
        {
            var loggedHoursForDay = await folqClient.GetLoggedHours(session.EmployeeId.Value, day, cancellationToken);
            var loggedHoursForDayAndProject = loggedHoursForDay.SingleOrDefault(h => h.Id == targetProjectCode);
            if (loggedHoursForDayAndProject is { Minutes: > 0 })
            {
                var minutesDiffTowardsTarget = minutesPerDay - loggedHoursForDayAndProject.Minutes;
                if (minutesDiffTowardsTarget == 0)
                {
                    AnsiConsole.MarkupLine($"[grey]‍️[[SKIPPED]]{targetProjectCode} har allerede {hoursFriendlyStr}t {day:dd.MM}[/]");
                    continue;
                }
                AnsiConsole.MarkupLine($"[yellow]⚠️ {targetProjectCode} har {MinutesToHours(loggedHoursForDayAndProject.Minutes)}t {day:dddd dd. MMMM}[/]");
                var overwrite = AnsiConsole.Prompt(
                    new ConfirmationPrompt(
                        $"Endre fra {MinutesToHours(loggedHoursForDayAndProject.Minutes)} til {hoursFriendlyStr} timer?"));

                if (overwrite)
                {
                    var timeEntryRequest = new TimeEntryRequest(session.EmployeeId.Value, day, session.EmployeeId.Value, minutesDiffTowardsTarget, targetProjectCode);
                    await folqClient.AddTimeEntry(timeEntryRequest, cancellationToken);
                    AnsiConsole.MarkupLine($"[green]✅ Endret til {hoursFriendlyStr}t {targetProjectCode} {day:dddd dd. MMMM}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[white]Beholdt {hoursFriendlyStr} på {targetProjectCode} den {day:dddd dd. MMMM}[/]");
                }
            }
            else
            {
                var minutesDiffTowardsTarget = minutesPerDay - loggedHoursForDayAndProject?.Minutes;
                if (minutesDiffTowardsTarget == 0)
                {
                    AnsiConsole.MarkupLine($"[grey]‍️[[SKIPPED]]{targetProjectCode} har allerede {hoursFriendlyStr} {day:dd.MM}[/]");
                    continue;
                }
                var timeEntryRequest = new TimeEntryRequest(session.EmployeeId.Value, day, session.EmployeeId.Value, minutesPerDay, targetProjectCode);
                await folqClient.AddTimeEntry(timeEntryRequest, cancellationToken);
                AnsiConsole.MarkupLine($"[green]✅ {hoursFriendlyStr} {targetProjectCode} {day:dddd dd. MMMM}[/]");
            }
        }

        await GetLoggedHours(ctx, week, cancellationToken);

    }

    static string MinutesToHours(int projectLogMinutes)
    {
        return (projectLogMinutes / 60m).ToString("F1");
    }
}

public enum Range
{
    Week,
    PrevWeek
}
