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
    public async Task List(ConsoleAppContext ctx,
        SelectedRange range = SelectedRange.CurrentWeek,
        [Argument] int? emp = null,
        string? customer = null,
        CancellationToken token = default)
    {
        await ListPeriod(ctx, range, emp, customer, token);
    }

    internal static async Task ListPeriod(ConsoleAppContext consoleCtx,
        SelectedRange range,
        int? employeeId = null,
        string? customer = null,
        CancellationToken ct = default)
    {
        var session = consoleCtx.UserSession;
        var employeeIds = new List<int>();

        if(System.Console.IsInputRedirected)
        {
            consoleCtx.StandardInput(line =>
            {
                if(line.IsInteger(out var empIdFromStdIn))
                {
                    employeeIds.Add(empIdFromStdIn);
                }
            });
        }

        if(System.Console.IsInputRedirected && !employeeIds.Any())
        {
            Console.Markup("[red]ERR![/] stdin må være rene tall (int)");
            return;
        }

        // 2) If no piped input, use employeeId argument or fallback to logged-in
        if(!employeeIds.Any())
        {
            if(employeeId != null)
            {
                employeeIds.Add(employeeId.Value); // argument
            }
            else
            {
                employeeIds.Add(session.EmployeeId); // fallback
            }
        }

        var client = HttpClientFactory.CreateFloqClientForUser(session);
        foreach(var empId in employeeIds)
        {
            if(employeeIds.Count > 1)
            {
                Console.WriteLine();
            }

            await ProcessEmployee(range, empId, customer, ct, session, client, employeeIds.Count > 1);

            if(employeeIds.Count > 1)
            {
                Console.WriteLine();
            }
        }
    }

    private static async Task ProcessEmployee(SelectedRange range, int? employeeId, string? customer,
        CancellationToken ct,
        UserSession session, FloqClient client, bool multipleEmployeeOutput)
    {
        var dates = GetDatesToWrite(range);
        var empId = employeeId ?? session.EmployeeId;

        var report = await CreateReport(range, dates, client, empId, customer, ct);
        if(report != null && report.HasTimeEntries())
        {
            Table table = new();
            var caption = GetCaption(report, dates, session, multipleEmployeeOutput);
            table.Caption = new TableTitle(caption ?? "?", new Style(Color.FromHex("#58C6FF")));
            table.Border = TableBorder.SimpleHeavy;
            RenderTableLive(table, report);
            Console.Write(table);
        }
        else
        {
            var employee = await client.GetEmployee(empId, ct);
            var empStr = employee is not null ? Formatting.FormatOther(employee) : empId.ToString();
            Console.MarkupLine($"[red]Ikke bemannet[/],  {empStr}");
        }
    }

    private static string? GetCaption(WeeklyTimeforingReport report, DateOnly[] dates, UserSession session,
        bool multipleEmployeeOutput)
    {
        var empStr = $" – {report.Employee.First_Name} {report.Employee.Last_Name}";
        if(session.EmployeeId == report.Employee.Id && !multipleEmployeeOutput)
        {
            empStr = "";
        }

        var dateInRange = dates.First();

        if(!report.IsMonthly())
        {
            var weekNo = ISOWeekShim.GetWeekOfYear(dateInRange);
            return $"Uke {weekNo}{empStr}";
        }

        if(report.IsMonthly())
        {
            var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(dateInRange.Month);
            return $"{monthName} {dateInRange.Year}{empStr}";
        }

        return null;
    }

    private static async Task<WeeklyTimeforingReport?> CreateReport(SelectedRange range, DateOnly[] dates,
        FloqClient client,
        int employeeId,
        string? customer,
        CancellationToken ct)
    {
        var emp = await client.GetEmployee(employeeId, ct);
        if(emp is null)
        {
            return null;
        }

        Dictionary<DateOnly, Task<IEnumerable<RpcProjectsForEmployeeeForDateResponse>>> allTasks = new();
        foreach(var singleDay in dates)
        {
            var t = client.GetRpcProjectsForEmployeeForDate(employeeId, singleDay, ct);
            allTasks.Add(singleDay, t);
        }

        await Task.WhenAll(allTasks.Values);

        Dictionary<DateOnly, IEnumerable<RpcProjectsForEmployeeeForDateResponse>> entriesByDay = new();
        foreach(var kvp in allTasks)
        {
            var entries = await kvp.Value;
            if(customer is not null)
            {
                entriesByDay[kvp.Key] =
                    entries.Where(e => e.Customer.Equals(customer, StringComparison.CurrentCultureIgnoreCase));
            }
            else
            {
                entriesByDay[kvp.Key] = entries;
            }
        }

        var projects = entriesByDay.SelectMany(kvp => kvp.Value)
            .GroupBy(p => p.Id)
            .Select(g => g.First())
            .OrderBy(p => p.Id)
            .ToList().Select(p => new Project(p.Id, p.Project)).ToList();


        Dictionary<ProjectDay, Timeforing> timerPrProsjekt = new();

        foreach(var project in projects)
        {
            foreach(var day in dates)
            {
                var projectEntriesOnDay = entriesByDay[day].FirstOrDefault(e => e.Id == project.Id);

                timerPrProsjekt.Add(new ProjectDay(project.Id, day),
                    projectEntriesOnDay is not null
                        ? new Timeforing(day, projectEntriesOnDay.Minutes, projectEntriesOnDay.Percentage_Staffed)
                        : new Timeforing(day, 0, 0));
            }
        }

        WeeklyTimeforingReport report = new(range, dates, projects, timerPrProsjekt, emp);
        return report;
    }

    private record WeeklyTimeforingReport(
        SelectedRange Range,
        DateOnly[] Days,
        List<Project> Projects,
        Dictionary<ProjectDay, Timeforing> ProjectTimeforing,
        Employee Employee)
    {
        public bool HasTimeEntries()
        {
            return ProjectTimeforing.Any();
        }

        public Timeforing GetEntry(ProjectDay proj)
        {
            return ProjectTimeforing[proj];
        }

        public decimal GetDailyHoursSum(DateOnly projectDay)
        {
            return ProjectTimeforing
                .Where(kvp => kvp.Key.Day == projectDay)
                .Sum(kvp => kvp.Value.Minutes) / 60m;
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

    private record Timeforing(DateOnly Day, int Minutes, int PercentageStaffed);

    private record Project(string Id, string Name);

    private record ProjectDay(string ProjectId, DateOnly Day);

    private static void RenderTableLive(Table table,
        WeeklyTimeforingReport report)
    {
        table.AddColumn("");

        table.Columns[0].Width(30);

        foreach(var day in report.Days)
        {
            var day1 = day;
            table.AddColumn("", c =>
            {
                c.Alignment = Justify.Center;
                c.Header = new Markup($"{day1:dd.MM}");
            });

            if(report.IsMonthly() && day.DayOfWeek == DayOfWeek.Friday)
            {
                table.AddColumn("");
            }
        }

        foreach(var proj in report.Projects)
        {
            List<string> row = new() { $"[purple]{proj.Id}[/] {Shorten(proj.Name)}" };
            foreach(var day in report.Days)
            {
                var entry = report.GetEntry(new ProjectDay(proj.Id, day));
                var item = (proj.Id, entry) switch
                {
                    (_, { Minutes: > 0 }) => $"[white]{Formatting.MinutesToHours(entry.Minutes)}[/]",
                    ("AVS", { PercentageStaffed: > 0 }) => $"[purple]A[/]",
                    ("FER1000", { PercentageStaffed: > 0 }) => $"[purple]F[/]",
                    _ => "[dim]-[/]"
                };
                row.Add(item);

                if(report.IsMonthly() && day.DayOfWeek == DayOfWeek.Friday)
                {
                    row.Add("");
                }
            }

            table.AddRow(row.ToArray());
        }

        List<string> sumRow = new() { "[]Daglig sum[/]" };
        var dailyTotals = new Dictionary<DateOnly, decimal>();

        foreach(var day in report.Days)
        {
            var sum = report.GetDailyHoursSum(day);
            dailyTotals[day] = sum;

            if(sum > 0)
            {
                sumRow.Add($"[{(sum < 7.5m ? "yellow" : "green")}]{sum}[/]");
            }
            else
            {
                sumRow.Add($"[dim]-[/]");
            }


            if(report.IsMonthly() && day.DayOfWeek == DayOfWeek.Friday)
            {
                sumRow.Add("");
            }
        }

        table.AddRow(sumRow.ToArray());

        List<string> cumulativeRow = new() { "[dim]Ukesum[/]" };
        decimal runningTotal = 0;
        foreach(var day in report.Days)
        {
            var minutes = dailyTotals[day];
            runningTotal += minutes;
            var color = "";
            if(day.DayOfWeek == DayOfWeek.Friday)
            {
                color = runningTotal < 37.5m ? "red" : "green";
            }

            if(day.DayOfWeek == DayOfWeek.Friday && runningTotal > 0)
            {
                cumulativeRow.Add($"[{color}]{runningTotal}[/]");
            }
            else
            {
                cumulativeRow.Add($"[dim][/]");
            }


            if(report.IsMonthly() && day.DayOfWeek == DayOfWeek.Friday)
            {
                runningTotal = 0;
                cumulativeRow.Add("");
            }
        }


        table.AddRow(cumulativeRow.ToArray());
    }

    private static DateOnly[] GetDatesToWrite(SelectedRange? range, DateOnly? date = null)
    {
        return (date, range) switch
        {
            (not null, _) => [date.Value],
            (_, SelectedRange.SingleDay) => [DateOnly.FromDateTime(DateTime.UtcNow)],
            (_, SelectedRange.CurrentWeek or SelectedRange.PreviousWeek) => GetWeekDays(range),
            (_, SelectedRange.CurrentMonth or SelectedRange.PreviousMonth) => GetMonthDays(range),
            _ => throw new Exception(
                "Ugyldig kommando, du må enten velge en dato eller en range. Range:'{range}', dato:'{date}'")
        };
    }

    public static DateOnly[] GetWeekDays(SelectedRange? week)
    {
        var dateInWeek = DateOnly.FromDateTime(DateTime.UtcNow);

        if(week == SelectedRange.PreviousWeek)
        {
            dateInWeek = dateInWeek.AddDays(-7);
        }

        var year = ISOWeekShim.GetYear(dateInWeek);
        var weekNo = ISOWeekShim.GetWeekOfYear(dateInWeek);

        var weekDaysForRange = Enumerable.Range(1, 5)
            .Select(dayOfWeek => DateOnly.FromDateTime(ISOWeek.ToDateTime(year, weekNo, (DayOfWeek)dayOfWeek)))
            .ToArray();

        return weekDaysForRange;
    }

    public static DateOnly[] GetMonthDays(SelectedRange? week)
    {
        var dateInMonth = DateOnly.FromDateTime(DateTime.UtcNow);
        if(week == SelectedRange.PreviousMonth)
        {
            dateInMonth = dateInMonth.AddMonths(-1);
        }

        var year = dateInMonth.Year;
        var month = dateInMonth.Month;

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
