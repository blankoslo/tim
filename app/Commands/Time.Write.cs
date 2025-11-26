internal partial class Time
{
    /// <summary>Registrerer nye timer</summary>
    /// <param name="range">-w, Hvilken uke som skal timeføres. Gyldige: "Current|Previous"</param>
    /// <param name="project">-p, Prosjektkoden til prosjektet. Bruker global default-prosjekt hvis ikke angitt</param>
    /// <param name="hours">-h, Antall timer som skal føres</param>
    [Command("write")]
    public async Task Write(
        ConsoleAppContext ctx,
        [HideDefaultValue, Argument]string? project = null,
        SelectedRange range = SelectedRange.Today,
        decimal? hours = 7.5m,
        CancellationToken token = default) => await WriteLogEntries(ctx, range, project, hours, token);

    private const int minutesPerDay = 450;

    private static async Task WriteLogEntries(ConsoleAppContext ctx,
        SelectedRange? mode = null,
        string? project = null,
        decimal? hours = null,
        CancellationToken cancellationToken = default)
    {
        var session = ctx.GetUserSession();
        SelectedRange displayList = mode switch
        {
            SelectedRange.Today => SelectedRange.CurrentWeek,
            null => SelectedRange.CurrentWeek,
            _ => mode.Value
        };
        await ListPeriod(ctx, displayList, ct: cancellationToken);

        var datesToWrite = GetDatesToWrite(mode);


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
                Console.MarkupLine("[red]❌ Ingen prosjektkode angitt og ingen default prosjekt funnet.[/]");
                return;
            }
        }

        var isConfirmed = Console.Prompt(
            new ConfirmationPrompt($"Timeføre " +
                                   $"[bold]{hours}t[/] på " +
                                   $"[purple]{targetProjectCode}[/] " +
                                   $"[white][[{TimeforingDisplayStr(datesToWrite)}][/]")
                .ShowDefaultValue(true));

        if (!isConfirmed)
        {
            Console.MarkupLine("[yellow]Timeføring avbrutt.[/]");
            return;
        }

        await WriteEntriesForDates(targetProjectCode, datesToWrite, session, hours, cancellationToken);

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
        UserSession session, decimal? hours = null, CancellationToken cancellationToken = default)
    {
        hours ??= 7.5m;

        // Console write what values we are doing, what week and what values we are logging:
        Console.MarkupLine($"Timefører " +
                           $"[bold]{hours}t[/] på " +
                           $"[purple]{targetProjectCode}[/] " +
                           $"[white][[{TimeforingDisplayStr(dates)}][/]");

        var client = HttpClientFactory.CreateFloqClientForUser(session);

        foreach (var day in dates)
        {
            await WriteEntryForDay(client, session, day, targetProjectCode,  hours, cancellationToken);
        }
    }

    private static async Task WriteEntryForDay(FloqClient client, UserSession session, DateOnly day, string targetProjectCode, decimal? hours = null, CancellationToken cancellationToken = default)
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
                Console.MarkupLine($"[grey]‍️[[SKIPPED]]{targetProjectCode} har allerede {hoursFriendlyStr}t {day:dd.MM}[/]");
                return;
            }
            Console.MarkupLine($"[yellow]⚠️ {targetProjectCode} har {Formatting.MinutesToHours(loggedHoursForDayAndProject.Minutes)}t {day:dddd dd. MMMM}[/]");
            var overwrite = Console.Prompt(
                new ConfirmationPrompt(
                    $"Endre fra {Formatting.MinutesToHours(loggedHoursForDayAndProject.Minutes)} til {hoursFriendlyStr} timer?"));

            if (overwrite)
            {
                var timeEntryRequest = new TimeEntryRequest(session.EmployeeId, day, session.EmployeeId, minutesDiffTowardsTarget, targetProjectCode);
                await client.AddTimeEntry(timeEntryRequest, cancellationToken);
                Console.MarkupLine($"[green]✅ Endret til {hoursFriendlyStr}t {targetProjectCode} {day:dddd dd. MMMM}[/]");
            }
            else
            {
                Console.MarkupLine($"[white]Beholdt {hoursFriendlyStr} på {targetProjectCode} den {day:dddd dd. MMMM}[/]");
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
            Console.MarkupLine($"[green]✅ {hoursFriendlyStr} {targetProjectCode} {day:dddd dd. MMMM}[/]");
        }
    }
}