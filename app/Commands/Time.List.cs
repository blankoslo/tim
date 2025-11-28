using System.Globalization;

using Spectre.Console.Rendering;

internal partial class Time
{
    /// <summary>Lister førte timer</summary>
    /// <param name="range">-r, Hvilken periode. SingleDay,CurrentWeek,PreviousWeek,CurrentMonth,PreviousMonth"</param>
    /// <param name="emp">-e, Hvem sine timer. EmployeeID i Floq.  Default: innlogget ansatt"</param>
    /// <param name="customer">-c, Filtrer på kunde. Kundekode i Floq. Eks "ANE" for Aneo Mobility.</param>
    [Command("list|ls")]
    [ConsoleAppFilter<AuthenticationFilter>]
    [ConsoleAppFilter<AddStdinToContext>]
    public async Task List(ConsoleAppContext ctx,
        SelectedRange range = SelectedRange.CurrentWeek,
        [Argument] int? emp = null,
        string? customer = null,
        CancellationToken token = default) => await ListPeriod(ctx, range, emp, customer, token);

    internal static async Task ListPeriod(ConsoleAppContext consoleCtx,
        SelectedRange range,
        int? employeeId = null,
        string? customer = null,
        CancellationToken ct = default)
    {
        var session = consoleCtx.UserSession;
        var employeeIds = new List<int>();

        if (System.Console.IsInputRedirected)
        {
            consoleCtx.StandardInput(line =>
            {
                if (line.IsInteger(out var empIdFromStdIn))
                {
                    employeeIds.Add(empIdFromStdIn);
                }
            });
        }

        // 2) If no piped input, use employeeId argument or fallback to logged-in
        if (!employeeIds.Any())
        {
            if (employeeId != null)
                employeeIds.Add(employeeId.Value); // argument
            else
                employeeIds.Add(session.EmployeeId); // fallback
        }

        var client = HttpClientFactory.CreateFloqClientForUser(session);
        foreach (var id in employeeIds)
        {
            await ProcessEmployee(range, id, customer, ct, session, client);
        }
    }

    private static async Task ProcessEmployee(SelectedRange range, int? employeeId, string? customer, CancellationToken ct,
        UserSession session, FloqClient client)
    {
        var dates = GetDatesToWrite(range);

        Table table = new();

        var empId = employeeId ?? session.EmployeeId;

        await Console.Live(table)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Top)
            .StartAsync(async liveCtx =>
            {
                var report = await CreateReport(range, dates, client, empId, customer, session, ct);
                if (report != null)
                {
                    string? caption = GetCaption(report, dates, range);
                    table.Caption = new TableTitle(caption ?? "?");
                    table.Border = new RoundedTableBorder();
                    RenderTableLive(table, report, liveCtx);
                }
                else
                {
                    Console.MarkupLine($"[red]❌ Kunne ikke hente rapport [/]");
                }
            });
    }

    private static string? GetCaption(WeeklyTimeforingReport report, DateOnly[] dates, SelectedRange? range)
    {
        var empStr = report.Employee != null ?  $" [blue]{report.Employee.Last_Name}[/]": "";
        var dateInRange = dates.First();

        if (!report.IsMonthly())
        {
            int weekNo = ISOWeek.GetWeekOfYear(dateInRange);
            return $"[dim] uke {weekNo}{empStr}[/]";
        }

        if (report.IsMonthly())
        {
            var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(dateInRange.Month);
            return $"[dim] {monthName} {dateInRange.Year}{empStr}[/]";
        }

        return null;
    }

    private static async Task<WeeklyTimeforingReport?> CreateReport(SelectedRange range, DateOnly[] dates,
        FloqClient client,
        int employeeId,
        string? customer,
        UserSession session,
        CancellationToken ct)
    {
        // Fetch all logged hours for each day
        var sessionEmployeeId = session.EmployeeId;
        Employee? emp = null;
        if (sessionEmployeeId != employeeId)
        {
            emp = await client.GetEmployee(employeeId, ct);
        }

        Dictionary<DateOnly, Task<IEnumerable<RpcProjectsForEmployeeeForDateResponse>>> allTasks = new();
        foreach (DateOnly singleDay in dates)
        {
            var t = client.GetRpcProjectsForEmployeeForDate(employeeId, singleDay, ct);
            allTasks.Add(singleDay, t);
        }

        var allTimeEntriesThisWeek = await Task.WhenAll(allTasks.Values);

        // Build a dictionary: day -> entries
        Dictionary<DateOnly, IEnumerable<RpcProjectsForEmployeeeForDateResponse>> entriesByDay = new();
        foreach (var kvp in allTasks)
        {
            IEnumerable<RpcProjectsForEmployeeeForDateResponse> entries = await kvp.Value;
            if (customer is not null)
            {
                entriesByDay[kvp.Key] = entries.Where(e => e.Customer.Equals(customer, StringComparison.CurrentCultureIgnoreCase));
            }
            else
            {
                entriesByDay[kvp.Key] = entries;
            }
        }

        // Build a dictionary: projectId -> project info
        List<Project> projects = entriesByDay.SelectMany(kvp => kvp.Value)
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

        WeeklyTimeforingReport report = new(range, dates, projects, timerPrProsjekt, emp);
        return report;
    }

    record WeeklyTimeforingReport(SelectedRange Range, DateOnly[] Days, List<Project> Projects, Dictionary<ProjectDay, Timeforing> ProjectTimeforing, Employee? Employee)
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

        public bool IsMonthly()
        {
            return Range switch
            {
                SelectedRange.CurrentMonth or SelectedRange.PreviousMonth => true,
                _ => false
            };
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
            Console.MarkupLine("[red]\nIngen prosjekter satt opp for timeføring funnet for denne uka[/]");
            return;
        }

        table.AddColumn("");
        foreach (var day in report.Days)
        {
            var day1 = day;
            table.AddColumn("", c =>
            {
                c.Alignment = Justify.Center;
                c.Header = new Markup($"{day1:dd.MM}");
            });

            if(report.IsMonthly() && day.DayOfWeek == DayOfWeek.Friday)
                table.AddColumn("");

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
                    : "[dim]-[/]");

                if(report.IsMonthly() && day.DayOfWeek == DayOfWeek.Friday)
                    row.Add("");
            }

            table.AddRow(row.ToArray());
            liveCtx.Refresh();
        }

        // add a sum row here
        List<string> sumRow = new() { "[]Daglig sum[/]" };
        var dailyTotals = new Dictionary<DateOnly, decimal>();

        foreach (var day in report.Days)
        {
            decimal sum = report.GetDailyHoursSum(day);
            dailyTotals[day] = sum;

            if (sum > 0)
            {
                sumRow.Add($"[{(sum < 7.5m ? "yellow" : "green")}]{sum}[/]");
            }
            else
            {
                sumRow.Add($"[dim]-[/]");
            }


            if(report.IsMonthly() && day.DayOfWeek == DayOfWeek.Friday)
                sumRow.Add("");
        }

        table.AddRow(sumRow.ToArray());


        // add a cumulative sum row with running totals
        List<string> cumulativeRow = new() { "[dim]Ukesum[/]" };
        decimal runningTotal = 0;
        foreach (var day in  report.Days)
        {
            decimal minutes = dailyTotals[day];
            runningTotal += minutes;
            string color = "";
            if (day.DayOfWeek == DayOfWeek.Friday)
            {
                color = runningTotal < 37.5m ? "red" : "green";
            }

            if (day.DayOfWeek == DayOfWeek.Friday && runningTotal > 0)
            {
                cumulativeRow.Add($"[{color}]{runningTotal}[/]");
            }
            else
            {
                cumulativeRow.Add($"[dim][/]");
            }


            if (report.IsMonthly() && day.DayOfWeek == DayOfWeek.Friday)
            {
                runningTotal = 0;
                cumulativeRow.Add("");
            }
        }


        table.AddRow(cumulativeRow.ToArray());
        liveCtx.Refresh();
    }

    private static DateOnly[] GetDatesToWrite(SelectedRange? range, DateOnly? date = null)
    {
        return (date, range) switch
        {
            (not null, _) => [date.Value],
            (_, SelectedRange.SingleDay) => [DateOnly.FromDateTime(DateTime.UtcNow)],
            (_, SelectedRange.CurrentWeek or SelectedRange.PreviousWeek) => GetWeekDays(range),
            (_, SelectedRange.CurrentMonth or SelectedRange.PreviousMonth) => GetMonthDays(range),
            _ => throw new Exception("Ugyldig kommando, du må enten velge en dato eller en range. Range:'{range}', dato:'{date}'")
        };
    }

    public static DateOnly[] GetWeekDays(SelectedRange? week)
    {
        DateOnly dateInWeek = DateOnly.FromDateTime(DateTime.UtcNow);

        if (week == SelectedRange.PreviousWeek)
        {
            dateInWeek = dateInWeek.AddDays(-7);
        }

        int year = ISOWeek.GetYear(dateInWeek);
        int weekNo = ISOWeek.GetWeekOfYear(dateInWeek);

        var weekDaysForRange = Enumerable.Range(1, 5)
            .Select(dayOfWeek => DateOnly.FromDateTime(ISOWeek.ToDateTime(year, weekNo, (DayOfWeek)dayOfWeek)))
            .ToArray();

        return weekDaysForRange;
    }

    public static DateOnly[] GetMonthDays(SelectedRange? week)
    {
        DateOnly dateInMonth = DateOnly.FromDateTime(DateTime.UtcNow);
        if (week == SelectedRange.PreviousMonth)
        {
            dateInMonth = dateInMonth.AddMonths(-1);
        }

        int year = dateInMonth.Year;
        int month = dateInMonth.Month;

        var daysInMonth = DateTime.DaysInMonth(year, month);

        var monthDaysForRange = Enumerable.Range(1, daysInMonth)
            .Select(day => new DateOnly(year, month, day))
            .Where(d => d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            .ToArray();

        return monthDaysForRange;

    }

    private static string Shorten(string someString, int defaultLength = 20)
    {
        return someString.Length > defaultLength ? someString[..defaultLength] + "…" : someString;
    }
}