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
        CancellationToken token = default)
    {
        await ListPeriod(ctx, range, employeeId: emp, customer: customer, ct: token);
    }

    internal static async Task ListPeriod(ConsoleAppContext consoleCtx,
        SelectedRange range,
        DateOnly? specificDate = null,
        int? employeeId = null,
        string? customer = null,
        CancellationToken ct = default)
    {
        var dates = specificDate.HasValue
            ? GetWeekDaysForDate(specificDate.Value)
            : GetDatesToWrite(range);
        await ListPeriod(consoleCtx, dates, employeeId, customer, ct);
    }

    private static DateOnly[] GetWeekDaysForDate(DateOnly date)
    {
        var year = ISOWeekShim.GetYear(date);
        var weekNo = ISOWeekShim.GetWeekOfYear(date);
        return Enumerable.Range(1, 5)
            .Select(d => DateOnly.FromDateTime(ISOWeek.ToDateTime(year, weekNo, (DayOfWeek)d)))
            .ToArray();
    }

    private static async Task ListPeriod(ConsoleAppContext consoleCtx,
        DateOnly[] dates,
        int? employeeId = null,
        string? customer = null,
        CancellationToken ct = default,
        SelectedRange range = SelectedRange.CurrentWeek)
    {
        var session = consoleCtx.UserSession;
        var employeeIds = new List<int>();

        consoleCtx.StandardInput(line =>
        {
            if(line.IsInteger(out var empIdFromStdIn))
            {
                employeeIds.Add(empIdFromStdIn);
            }
        });

        if(!employeeIds.Any())
        {
            if(employeeId != null)
            {
                employeeIds.Add(employeeId.Value);
            }
            else
            {
                employeeIds.Add(session.EmployeeId);
            }
        }

        var client = HttpClientFactory.CreateFloqClientForUser(session);
        var platform = HttpClientFactory.CreatePlatformClientForUser(session);
        foreach(var empId in employeeIds)
        {
            if(employeeIds.Count > 1)
            {
                Console.WriteLine();
            }

            await ProcessEmployee(dates, empId, customer, ct, session, client, platform, employeeIds.Count > 1, range);

            if(employeeIds.Count > 1)
            {
                Console.WriteLine();
            }
        }
    }

    private static async Task ProcessEmployee(DateOnly[] dates, int? employeeId, string? customer,
        CancellationToken ct,
        UserSession session, FloqClient client, FloqPlatformClient platform, bool multipleEmployeeOutput,
        SelectedRange range = SelectedRange.CurrentWeek)
    {
        var empId = employeeId ?? session.EmployeeId;

        var report = await CreateReport(range, dates, client, platform, empId, customer, ct);
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
        FloqPlatformClient platform,
        int employeeId,
        string? customer,
        CancellationToken ct)
    {
        var emp = await client.GetEmployee(employeeId, ct);
        if(emp is null)
        {
            return null;
        }

        var days = await platform.GetEmployeeDays([employeeId], dates.Min(), dates.Max(), ct);

        // The doc promises a customer code ("ANE"); the RPC only knew the name, so accept both.
        var entriesByDay = dates.ToDictionary(day => day, day => days
            .Where(e => e.Date == day)
            .Where(e => customer is null
                        || string.Equals(e.CustomerId, customer, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(e.CustomerName, customer, StringComparison.CurrentCultureIgnoreCase))
            .ToList());

        var projects = entriesByDay.SelectMany(kvp => kvp.Value)
            .GroupBy(p => p.Code)
            .Select(g => g.First())
            .OrderBy(p => p.Code)
            .ToList().Select(p => new Project(p.Code, p.Name ?? "")).ToList();


        Dictionary<ProjectDay, Timeforing> timerPrProsjekt = new();

        foreach(var project in projects)
        {
            foreach(var day in dates)
            {
                var projectEntriesOnDay = entriesByDay[day].FirstOrDefault(e => e.Code == project.Id);

                timerPrProsjekt.Add(new ProjectDay(project.Id, day),
                    projectEntriesOnDay is not null
                        ? new Timeforing(day, projectEntriesOnDay.Minutes,
                            projectEntriesOnDay.StaffedPercentage + projectEntriesOnDay.AbsencePercentage)
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
            dateInWeek = dateInWeek.AddDays(-7);
        return GetWeekDaysForDate(dateInWeek);
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
