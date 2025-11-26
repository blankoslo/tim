using System.ComponentModel;

internal partial class Time
{
    /// <summary>Registrerer nye timer</summary>
    /// <param name="range">-w, Hvilken uke som skal timeføres. Gyldige: "Current|Previous"</param>
    /// <param name="project">-p, Prosjektkoden til prosjektet. Bruker global default-prosjekt hvis ikke angitt</param>
    /// <param name="hours">-h, Antall timer som skal føres</param>
    /// <param name="date">-d, Dato som skal føres, MM.dd. Default dagens dato.</param>
    [Command("write")]
    public async Task Write(
        ConsoleAppContext ctx,
        [HideDefaultValue, Argument] string? project = null,
        SelectedRange range = SelectedRange.SingleDay,
        decimal? hours = 7.5m,
        string? date = null,
        bool? yes = false,
        CancellationToken token = default) => await WriteLogEntries(ctx, range, project, hours, date, yes, token);

    private const int minutesPerDay = 450;

    private static async Task WriteLogEntries(ConsoleAppContext ctx,
        SelectedRange? mode = null,
        string? project = null,
        decimal? hours = null,
        string? dateStr = null,
        bool? skipConfirmations = null,
        CancellationToken cancellationToken = default)
    {
        var session = ctx.GetUserSession();
        SelectedRange displayList = mode switch
        {
            SelectedRange.SingleDay => SelectedRange.CurrentWeek,
            null => SelectedRange.CurrentWeek,
            _ => mode.Value
        };
        await ListPeriod(ctx, displayList, ct: cancellationToken);

        var currentYear = DateTime.UtcNow.Year;
        if (dateStr is not null)
        {
            var couldParse = DateOnly.TryParseExact($"{currentYear}.{dateStr}", "yyyy.dd.MM", out var date);
            if (!couldParse)
            {
                Console.WriteLine($"Kunne ikke tolke datoen du oppga: '{dateStr}'. Bruk format dd.MM, for eksempel 15.03 for 15. mars.");
                return;
            }
        }


        var datesToWrite = GetDatesToWrite(mode);

        string projectToWriteOn;
        if (project != null)
        {
            projectToWriteOn = project;
        }
        else
        {
            // fetch default project:
            var defaultProj = await UserSecretsManager.GetDefaultProject(cancellationToken);
            if (defaultProj != null)
            {
                projectToWriteOn = defaultProj.Id;
            }
            else
            {
                Console.MarkupLine("[red]❌ Ingen prosjektkode angitt og ingen default prosjekt funnet.[/]");
                return;
            }
        }

        decimal hoursToWrite = hours ?? 7.5m;



        if (datesToWrite.Length == 1 && skipConfirmations is null or false)
        {
            var selectionPrompt = new ConfirmationPrompt($"Timeføre " +
                                                         $"[bold]{hoursToWrite}t[/] på " +
                                                         $"[purple]{projectToWriteOn}[/] " +
                                                         $"[white][[{TimeforingDisplayStr(datesToWrite)}][/]");
            var confirmed = Console.Prompt(selectionPrompt);
            if (!confirmed)
            {
                Console.MarkupLine("[yellow]Ok! Timeføring avbrutt.[/]");
                return;
            }
        }


        await WriteEntriesForDates(projectToWriteOn, datesToWrite, session, hoursToWrite, skipConfirmations ?? true, cancellationToken);

        await ListPeriod(ctx, displayList, ct: cancellationToken);
    }


    private static string TimeforingDisplayStr(DateOnly[] datesToWrite)
    {
        var timeStr = datesToWrite switch
        {
            { Length: > 1 } => $"{datesToWrite.First():dd.MM}-{datesToWrite.Last():dd.MM}]",
            { Length: 1 } => $"{datesToWrite[0]:dd.MM}]",
            { Length: 0 } => "Ingen datoer angitt",
        };
        return timeStr;
    }

    private static async Task WriteEntriesForDates(string targetProjectCode, DateOnly[] dates,
        UserSession session, decimal hours, bool yes,
        CancellationToken cancellationToken = default)
    {
        var client = HttpClientFactory.CreateFloqClientForUser(session);

        foreach (var day in dates)
        {
            await WriteEntryForDay(client, session, day, targetProjectCode, yes, hours, cancellationToken);
        }
    }

    private static async Task WriteEntryForDay(FloqClient client, UserSession session, DateOnly day,
        string targetProjectCode, bool skipConfirm, decimal? hours = null,
        CancellationToken cancellationToken = default)
    {
        var minutesToLog = minutesPerDay;

        if (hours.HasValue)
        {
            minutesToLog = (int)(hours.Value * 60);
        }

        var hoursFriendlyStr = minutesToLog > 0 ? $"{minutesToLog / 60m:F1}" : "0";

        var loggedHoursForDay = await client.GetRpcProjectsForEmployeeForDate(session.EmployeeId, day, cancellationToken);
        var loggedHoursForDayAndProject = loggedHoursForDay.SingleOrDefault(h => h.Id == targetProjectCode);
        if (loggedHoursForDayAndProject is { Minutes: > 0 })
        {
            var minutesDiffTowardsTarget = minutesToLog - loggedHoursForDayAndProject.Minutes;
            if (minutesDiffTowardsTarget == 0)
            {
                // Console.MarkupLine($"[grey]‍️[[SKIPPED]]{targetProjectCode} har allerede {hoursFriendlyStr}t {day:dd.MM}[/]");
                return;
            }

            var hoursDiff = (loggedHoursForDayAndProject.Minutes - hours * 60) / 60m;

            var changeTxt = hoursDiff switch
            {
                < 0 => $"{Formatting.MinutesToHours(loggedHoursForDayAndProject.Minutes)}[green]+{Math.Abs(hoursDiff.Value):F1}[/] => {hoursFriendlyStr}",
                > 0 => $"{Formatting.MinutesToHours(loggedHoursForDayAndProject.Minutes)}[red]-{Math.Abs(hoursDiff.Value):F1}[/] => {hoursFriendlyStr}",
                _ => "[grey]0[/]"
            };
            string log = $"[purple]{targetProjectCode}[/] [[{day:dd.MM}]]  {changeTxt}";

            if (skipConfirm)
            {
                var timeEntryRequest = new TimeEntryRequest(session.EmployeeId, day, session.EmployeeId, minutesDiffTowardsTarget, targetProjectCode);
                await client.AddTimeEntry(timeEntryRequest, cancellationToken);
                Console.MarkupLine(log);
            }
            else
            {

                var overwrite = Console.Prompt(new ConfirmationPrompt(log));

                if (overwrite)
                {
                    var timeEntryRequest = new TimeEntryRequest(session.EmployeeId, day, session.EmployeeId, minutesDiffTowardsTarget, targetProjectCode);
                    await client.AddTimeEntry(timeEntryRequest, cancellationToken);
                }
            }
        }
        else
        {
            var minutesDiffTowardsTarget = minutesPerDay - loggedHoursForDayAndProject?.Minutes;
            if (minutesDiffTowardsTarget == 0)
            {
                Console.MarkupLine($"[grey]‍️[[SKIPPED]]{targetProjectCode} har allerede {hoursFriendlyStr} {day:dd.MM}[/]");
                return;
            }
            var timeEntryRequest = new TimeEntryRequest(session.EmployeeId, day, session.EmployeeId, minutesToLog, targetProjectCode);
            await client.AddTimeEntry(timeEntryRequest, cancellationToken);
            Console.MarkupLine($"[purple]{targetProjectCode}[/] {day:dd. MM}  {hoursFriendlyStr}t");
        }
    }
}