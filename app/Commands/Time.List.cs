using System.Globalization;

internal partial class Time
{
    /// <summary>Lister førte timer</summary>
    /// <param name="weekRange">-r, Hvilken periode.  Gyldige: "Current|Previous"</param>
    [Command("list|ls")]
    [ConsoleAppFilter<AuthenticationFilter>]
    public async Task List(ConsoleAppContext ctx, SelectedWeekRange weekRange = SelectedWeekRange.Current, CancellationToken token = default) => await ListWeek(ctx, weekRange, token);

    internal static async Task ListWeek(ConsoleAppContext consoleCtx, SelectedWeekRange? week = null, CancellationToken ct = default)
    {
        var session = consoleCtx.GetUserSession();

        FloqClient folqClient = HttpClientFactory.CreateFolqClientForUser(session);

        var ( dates, weekNo) = GetWeek(week);

        Table table = new()
                      {
                          Caption = new ($"[dim] uke {weekNo}[/]"),
                          Border = TableBorder.Rounded,
                      };

        await Console.Live(table)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Top)
            .StartAsync(async liveCtx =>
            {
                var report = await CreateReport(dates, folqClient, session.EmployeeId, ct);
                RenderTableLive(table, report, liveCtx);
            });
    }

    private static async Task<WeeklyTimeforingReport> CreateReport(DateOnly[] dates, FloqClient folqClient,
        int employeeId, CancellationToken ct)
    {
        // Fetch all logged hours for each day
        Dictionary<DateOnly, Task<IEnumerable<RpcProjectsForEmployeeeForDateResponse>>> allTasks = new();
        foreach (DateOnly singleDay in dates)
        {
            var t = folqClient.GetRpcProjectsForEmployeeForDate(employeeId, singleDay, ct);
            allTasks.Add(singleDay, t);
        }

        var allTimeEntriesThisWeek = await Task.WhenAll(allTasks.Values);

        // Build a dictionary: day -> entries
        Dictionary<DateOnly, IEnumerable<RpcProjectsForEmployeeeForDateResponse>> entriesByDay = new();
        foreach (var kvp in allTasks)
        {
            entriesByDay[kvp.Key] = await kvp.Value;
        }

        // Build a dictionary: projectId -> project info
        List<RpcProjectsForEmployeeeForDateResponse> allEntriesFlattened = allTimeEntriesThisWeek.SelectMany(x => x).ToList();
        List<Project> projects = allEntriesFlattened
            .GroupBy(p => p.Id)
            .Select(g => g.First())
            .OrderBy(p => p.Id)
            .ToList().Select(p => new Project(p.Id, p.Project)).ToList();


        Dictionary<ProjectDay, Timeforing> timerPrProsjekt = new();

        foreach (var project in projects)
        {
            foreach (var day in dates)
            {
                var minutes = entriesByDay[day].Where(e => e.Id == project.Id).Sum(e => e.Minutes);
                timerPrProsjekt.Add(new ProjectDay(project.Id, day), new Timeforing(day, minutes));
            }
        }

        WeeklyTimeforingReport report = new(dates, projects, timerPrProsjekt);
        return report;
    }

    record WeeklyTimeforingReport(DateOnly[] Days, List<Project> Projects, Dictionary<ProjectDay, Timeforing> ProjectTimeforing)
    {
        public bool HasTimeEntries() => ProjectTimeforing.Any();

        public Timeforing? GetEntry(ProjectDay proj)
        {
            return ProjectTimeforing[proj];

        }

        public decimal GetDailyHoursSum(DateOnly projectDay)
        {
            return (ProjectTimeforing
                .Where(kvp => kvp.Key.Day == projectDay)
                .Sum(kvp => kvp.Value.Minutes))/60m;
        }
    }

    record Timeforing(DateOnly Day, int Minutes);

    record Project(string Id, string Name);

    record ProjectDay(string ProjectId, DateOnly Day);

    private static void RenderTableLive(Table table,
        WeeklyTimeforingReport report,
        LiveDisplayContext liveCtx)
    {
        if (!report.HasTimeEntries())
        {
            AnsiConsole.MarkupLine("[red]\nIngen prosjekter satt opp for timeføring funnet for denne uka[/]");
            return;
        }

        table.AddColumn("");
        foreach (DateOnly day in report.Days)
        {
            table.AddColumn(day.ToString("dd.MM"), c =>
            {
                c.Alignment = Justify.Center;
            });
            liveCtx.Refresh();
        }

        foreach (var proj in report.Projects)
        {
            List<string> row = new() { $"[purple]{proj.Id}[/] {Shorten(proj.Name)}" };
            foreach (DateOnly day in report.Days)
            {
                var entry = report.GetEntry(new ProjectDay(proj.Id, day));
                row.Add(entry is { Minutes: > 0 }
                    ? $"[white]{Formatting.MinutesToHours(entry.Minutes)}[/]"
                    : "[gray]0[/]");
            }

            table.AddRow(row.ToArray());
            liveCtx.Refresh();
        }

        // add a sum row here
        List<string> sumRow = new() { "[]Daglig sum[/]" };
        List<decimal> dailyTotals = new();

        foreach (var day in report.Days)
        {
            decimal sum = report.GetDailyHoursSum(day);
            dailyTotals.Add(sum);
            if (sum < 7.5m)
            {
                sumRow.Add($"[yellow]{sum}[/]");
            }
            else
            {
                sumRow.Add($"[green]{sum}[/]");
            }
        }

        table.AddEmptyRow();
        table.AddRow(sumRow.ToArray());


        // add a cumulative sum row with running totals
        List<string> cumulativeRow = new() { "[dim]Kumulativ[/]" };
        decimal runningTotal = 0;
        for (int dayIndex = 0; dayIndex < dailyTotals.Count; dayIndex++)
        {
            decimal minutes = dailyTotals[dayIndex];
            runningTotal += minutes;
            string color = "";
            if (dayIndex == dailyTotals.Count - 1)
            {
                color = runningTotal < 37.5m ? "red" : "green";
            }

            cumulativeRow.Add($"[{color}]{runningTotal}[/]");
        }


        table.AddRow(cumulativeRow.ToArray());
        liveCtx.Refresh();
    }

    private static (DateOnly[] Dates, int WeekNo) GetWeek(SelectedWeekRange? week)
    {
        DateOnly dateInWeek = DateOnly.FromDateTime(DateTime.UtcNow);
        if (week == SelectedWeekRange.Previous)
        {
            dateInWeek = dateInWeek.AddDays(-7);
        }

        int year = ISOWeek.GetYear(dateInWeek);
        int weekNo = ISOWeek.GetWeekOfYear(dateInWeek);

        var weekDaysForRange = Enumerable.Range(1, 5)
            .Select(dayOfWeek => DateOnly.FromDateTime(ISOWeek.ToDateTime(year, weekNo, (DayOfWeek)dayOfWeek)))
            .ToArray();

        return (weekDaysForRange, weekNo);
    }

    private static string Shorten(string someString, int defaultLength = 20)
    {
        return someString.Length > defaultLength ? someString[..defaultLength] + "…" : someString;
    }
}