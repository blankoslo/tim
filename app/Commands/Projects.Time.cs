using System.Globalization;
using Spectre.Console.Extensions;

internal partial class Projects
{
    /// <summary>Lister ansatte som har ført timer på et prosjekt</summary>
    /// <param name="projectId">ProsjektID i Floq, f.eks. "ANE1006"</param>
    /// <param name="range">-r, Hvilken periode. SingleDay,CurrentWeek,PreviousWeek,CurrentMonth,PreviousMonth"</param>
    [Command("time")]
    public async Task<int> TimeReport(ConsoleAppContext ctx,
        [Argument] string? projectId = null,
        SelectedRange range = SelectedRange.CurrentWeek,
        CancellationToken token = default)
    {
        var session = ctx.UserSession;
        var projectIds = new List<string>();

        if(System.Console.IsInputRedirected)
        {
            try
            {
                ctx.StandardInput(line =>
                {
                    if(!string.IsNullOrWhiteSpace(line.line))
                    {
                        if(line.line.Length != 7)
                        {
                            throw new Exception("BooM! Invalid project ID length from piped input.");
                        }

                        projectIds.Add(line.line.Trim());
                    }
                });
            }
            catch(Exception)
            {
                Console.MarkupLine(
                    $"[red]✗[/] Ugyldig prosjektID mottatt fra piped input. Sjekk at hver linje inneholder en gyldig prosjektID på 7 tegn.");
                return -1;
            }
        }

        var client = HttpClientFactory.CreateFloqClientForUser(session);
        // If no piped input, use projectId argument
        if(!projectIds.Any())
        {
            if(!string.IsNullOrWhiteSpace(projectId))
            {
                projectIds.Add(projectId);
            }
            else
            {
                var defaultProject = await UserSecretsManager.GetDefaultProject(token);

                if(defaultProject is not null)
                {
                    projectIds.Add(defaultProject.Id);
                }
                else
                {
                    Console.Markup(
                        "[red]✗[/] Mangler prosjektID. Bruk argumentet [purple]projectId[/], eller sett et default-prosjekt");
                    return -1;
                }
            }
        }


        var dates = GetAllWeekDays(range);
        if(range is SelectedRange.CurrentMonth or SelectedRange.PreviousMonth)
        {
            dates = GetAllMonthDays(range);
        }
        else if(range == SelectedRange.SingleDay)
        {
            dates = new[]
                    {
                        DateOnly.FromDateTime(DateTime.UtcNow)
                    };
        }

        var multipleProjects = projectIds.Count > 1;


        foreach(var projId in projectIds)
        {
            await ProcessProject(projId, range, dates, client, multipleProjects, token);
        }

        return 0;
    }

    private static async Task ProcessProject(string projectId,
        SelectedRange range,
        DateOnly[] dates,
        FloqClient client,
        bool multipleProjects,
        CancellationToken ct)
    {

        var report = await CreateProjectReport(projectId, range, dates, client, ct)
            .Spinner();

        if(report != null && report.HasTimeEntries())
        {
            Table table = new();
            var caption = GetProjectCaption(report, dates);
            table.Caption = new TableTitle(caption, new Style(Color.FromHex("#58C6FF")));
            table.Border = TableBorder.SimpleHeavy;
            RenderProjectTable(table, report);
            Console.Write(table);
        }
        else
        {
            if(!multipleProjects)
            {
                Console.MarkupLine($"[red]Ingen førte timer[/] for prosjekt [purple]{projectId}[/] i perioden");
            }
        }
    }

    private static string GetProjectCaption(ProjectTimeReport report, DateOnly[] dates)
    {
        var dateInRange = dates.First();

        if(!report.IsMonthly())
        {
            var weekNo = ISOWeekShim.GetWeekOfYear(dateInRange);
            return $"Prosjekt {report.ProjectId} – Uke {weekNo}";
        }

        var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(dateInRange.Month);
        return $"Prosjekt {report.ProjectId} – {monthName} {dateInRange.Year}";
    }

    private static async Task<ProjectTimeReport?> CreateProjectReport(
        string projectId,
        SelectedRange range,
        DateOnly[] dates,
        FloqClient client,
        CancellationToken ct)
    {
        // First, find all employees who have worked on projects in this date range
        var fromDate = dates.Min();
        var toDate = dates.Max();
        var employeesOnProjects = (await client.GetRpcEmployeesOnProjects(fromDate, toDate, ct)).ToList();

        // Get unique employee IDs
        var employeeIds = employeesOnProjects
            .Select(e => e.Id)
            .Distinct()
            .ToList();

        if(!employeeIds.Any())
        {
            return null;
        }

        const int MaxConcurrency = 6;

        using var semaphore = new SemaphoreSlim(MaxConcurrency);

        var allTasks = new Dictionary<(int, DateOnly), Task<IEnumerable<RpcProjectsForEmployeeeForDateResponse>>>();

        foreach (var empId in employeeIds)
        {
            foreach (var day in dates)
            {
                var t= GetWithThrottle(client, empId, day, semaphore, ct);
                allTasks.Add((empId, day), t);
            }
        }

        await Task.WhenAll(allTasks.Values);

        Dictionary<int, EmployeeInfo> employeesWithHours = new();
        Dictionary<EmployeeDay, ProjectTimeforing> timerPrAnsatt = new();

        foreach(var kvp in allTasks)
        {
            var (empId, day) = kvp.Key;
            var entries = await kvp.Value;
            var projectEntry = entries.FirstOrDefault(e => e.Id.Equals(projectId, StringComparison.OrdinalIgnoreCase));

            if(projectEntry != null && projectEntry.Minutes > 0)
            {
                // Add employee to our list if not already there
                if(!employeesWithHours.ContainsKey(empId))
                {
                    var empOnProj = employeesOnProjects.FirstOrDefault(e => e.Id == empId);
                    if(empOnProj != null)
                    {
                        employeesWithHours[empId] = new EmployeeInfo(empId, empOnProj.First_Name, empOnProj.Last_Name);
                    }
                }

                timerPrAnsatt[new EmployeeDay(empId, day)] =
                    new ProjectTimeforing(day, projectEntry.Minutes, projectEntry.Percentage_Staffed);
            }
        }

        if(!employeesWithHours.Any())
        {
            return null;
        }

        foreach(var emp in employeesWithHours.Values)
        {
            foreach(var day in dates)
            {
                var key = new EmployeeDay(emp.Id, day);
                if(!timerPrAnsatt.ContainsKey(key))
                {
                    timerPrAnsatt[key] = new ProjectTimeforing(day, 0, 0);
                }
            }
        }

        return new ProjectTimeReport(
            projectId,
            range,
            dates,
            employeesWithHours.Values.OrderBy(e => e.LastName).ThenBy(e => e.FirstName).ToList(),
            timerPrAnsatt);
    }

    private static async Task<IEnumerable<RpcProjectsForEmployeeeForDateResponse>> GetWithThrottle(
        FloqClient client,
        int empId,
        DateOnly day,
        SemaphoreSlim semaphore,
        CancellationToken ct)
    {
        await semaphore.WaitAsync(ct);
        try
        {
            return await client.GetRpcProjectsForEmployeeForDate(empId, day, ct);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static void RenderProjectTable(Table table, ProjectTimeReport report)
    {
        table.AddColumn("");
        table.Columns[0].Width(25);

        foreach(var day in report.Days)
        {
            var day1 = day;
            var isWeekend = day1.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            table.AddColumn("", c =>
            {
                c.Alignment = Justify.Center;
                c.Header = isWeekend
                    ? new Markup($"[dim]{day1:dd.MM}[/]")
                    : new Markup($"{day1:dd.MM}");
            });

            if(report.IsMonthly() && day.DayOfWeek == DayOfWeek.Sunday)
            {
                table.AddColumn("");
            }
        }

        foreach(var emp in report.Employees)
        {
            List<string> row = new() { $"[white]{emp.FirstName} {Shorten(emp.LastName, 10)}[/]" };

            foreach(var day in report.Days)
            {
                var entry = report.GetEntry(new EmployeeDay(emp.Id, day));
                var item = entry switch
                {
                    { Minutes: > 0 } => $"[white]{Formatting.MinutesToHours(entry.Minutes)}[/]",
                    _ => "[dim]-[/]"
                };
                row.Add(item);

                if(report.IsMonthly() && day.DayOfWeek == DayOfWeek.Sunday)
                {
                    row.Add("");
                }
            }

            table.AddRow(row.ToArray());
        }

        // Daily sum row
        List<string> sumRow = new() { "[green]Daglig sum[/]" };
        var dailyTotals = new Dictionary<DateOnly, decimal>();

        foreach(var day in report.Days)
        {
            var sum = report.GetDailyHoursSum(day);
            dailyTotals[day] = sum;

            if(sum > 0)
            {
                sumRow.Add($"[green]{sum:F1}[/]");
            }
            else
            {
                sumRow.Add("[dim]-[/]");
            }

            if(report.IsMonthly() && day.DayOfWeek == DayOfWeek.Sunday)
            {
                sumRow.Add("");
            }
        }

        table.AddRow(sumRow.ToArray());

        // Weekly sum row (cumulative)
        List<string> cumulativeRow = new() { "[dim]Ukesum[/]" };
        decimal runningTotal = 0;

        foreach(var day in report.Days)
        {
            var hours = dailyTotals[day];
            runningTotal += hours;

            if(day.DayOfWeek == DayOfWeek.Sunday && runningTotal > 0)
            {
                cumulativeRow.Add($"[blue]{runningTotal:F1}[/]");
            }
            else
            {
                cumulativeRow.Add("[dim][/]");
            }

            if(report.IsMonthly() && day.DayOfWeek == DayOfWeek.Sunday)
            {
                runningTotal = 0;
                cumulativeRow.Add("");
            }
        }

        table.AddRow(cumulativeRow.ToArray());

        // Total row (grand total for all employees)
        var grandTotal = dailyTotals.Values.Sum();
        List<string> totalRow = new() { "[bold yellow]Total[/]" };

        foreach(var day in report.Days)
        {
            totalRow.Add("[dim][/]");

            if(report.IsMonthly() && day.DayOfWeek == DayOfWeek.Sunday)
            {
                totalRow.Add("");
            }
        }

        // Replace the last non-empty cell with the grand total
        if(grandTotal > 0)
        {
            // Find the last index that is not a separator
            var lastIndex = totalRow.Count - 1;
            while(lastIndex > 0 && totalRow[lastIndex] == "")
            {
                lastIndex--;
            }

            totalRow[lastIndex] = $"[bold yellow]{grandTotal:F1}[/]";
        }

        table.AddRow(totalRow.ToArray());
    }

    private static string Shorten(string someString, int defaultLength = 20)
    {
        return someString.Length > defaultLength ? someString[..defaultLength] + "…" : someString;
    }

    /// <summary>Gets all 7 days of the week including Saturday and Sunday</summary>
    private static DateOnly[] GetAllWeekDays(SelectedRange? week)
    {
        var dateInWeek = DateOnly.FromDateTime(DateTime.UtcNow);

        if(week == SelectedRange.PreviousWeek)
        {
            dateInWeek = dateInWeek.AddDays(-7);
        }

        var year = ISOWeekShim.GetYear(dateInWeek);
        var weekNo = ISOWeekShim.GetWeekOfYear(dateInWeek);

        // Include all 7 days: Monday(1) through Sunday(0)
        var weekDaysForRange = Enumerable.Range(1, 7)
            .Select(dayOfWeek => DateOnly.FromDateTime(ISOWeek.ToDateTime(year, weekNo, (DayOfWeek)(dayOfWeek % 7))))
            .ToArray();

        return weekDaysForRange;
    }

    private static DateOnly[] GetAllMonthDays(SelectedRange? week)
    {
        var dateInMonth = DateOnly.FromDateTime(DateTime.UtcNow);
        if(week == SelectedRange.PreviousMonth)
        {
            dateInMonth = dateInMonth.AddMonths(-1);
        }

        var year = dateInMonth.Year;
        var month = dateInMonth.Month;

        var daysInMonth = DateTime.DaysInMonth(year, month);

        // Include all days including weekends
        var monthDaysForRange = Enumerable.Range(1, daysInMonth)
            .Select(day => new DateOnly(year, month, day))
            .ToArray();

        return monthDaysForRange;
    }

    private record ProjectTimeReport(
        string ProjectId,
        SelectedRange Range,
        DateOnly[] Days,
        List<EmployeeInfo> Employees,
        Dictionary<EmployeeDay, ProjectTimeforing> EmployeeTimeforing)
    {
        public bool HasTimeEntries()
        {
            return EmployeeTimeforing.Any(e => e.Value.Minutes > 0);
        }

        public ProjectTimeforing GetEntry(EmployeeDay key)
        {
            return EmployeeTimeforing.TryGetValue(key, out var entry) ? entry : new ProjectTimeforing(key.Day, 0, 0);
        }

        public decimal GetDailyHoursSum(DateOnly day)
        {
            return EmployeeTimeforing
                .Where(kvp => kvp.Key.Day == day)
                .Sum(kvp => kvp.Value.Minutes) / 60m;
        }

        public bool IsMonthly()
        {
            return Range is SelectedRange.CurrentMonth or SelectedRange.PreviousMonth;
        }
    }

    private record EmployeeInfo(int Id, string FirstName, string LastName);

    private record EmployeeDay(int EmployeeId, DateOnly Day);

    private record ProjectTimeforing(DateOnly Day, int Minutes, int PercentageStaffed);
}
