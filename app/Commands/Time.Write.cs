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
        SelectedRange range = SelectedRange.Current,
        decimal? hours = 7.5m,
        CancellationToken token = default) => await WriteLogEntries(range, project, hours, ctx, token);


    internal static async Task WriteLogEntries(SelectedRange week, string? project, decimal? hours, ConsoleAppContext ctx, CancellationToken cancellationToken = default)
    {
        var session = ctx.GetUserSession();
        await ListWeek(ctx,week, ct: cancellationToken);

        var dates = GetDates(week);

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

        var isConfirmed = AnsiConsole.Prompt(
            new ConfirmationPrompt($"Timeføre " +
                                   $"[bold]{hoursFriendlyStr}t[/] på " +
                                   $"[purple]{targetProjectCode}[/] " +
                                   $"[white][[{dates.First():dd.MM}-{dates.Last():dd.MM}]][/]")
                .ShowDefaultValue(true));

        if (!isConfirmed)
        {
            Console.MarkupLine("[yellow]Timeføring avbrutt.[/]");
            return;
        }

        // Console write what values we are doing, what week and what values we are logging:
        AnsiConsole.MarkupLine($"Timefører " +
                               $"[bold]{hoursFriendlyStr}t[/] på " +
                               $"[purple]{targetProjectCode}[/] " +
                               $"[white][[{dates.First():dd.MM}-{dates.Last():dd.MM}]][/]");

        var folqClient = HttpClientFactory.CreateFolqClientForUser(session);
        foreach (var day in dates)
        {
            var loggedHoursForDay = await folqClient.GetRpcProjectsForEmployeeForDate(session.EmployeeId, day, cancellationToken);
            var loggedHoursForDayAndProject = loggedHoursForDay.SingleOrDefault(h => h.Id == targetProjectCode);
            if (loggedHoursForDayAndProject is { Minutes: > 0 })
            {
                var minutesDiffTowardsTarget = minutesPerDay - loggedHoursForDayAndProject.Minutes;
                if (minutesDiffTowardsTarget == 0)
                {
                    AnsiConsole.MarkupLine($"[grey]‍️[[SKIPPED]]{targetProjectCode} har allerede {hoursFriendlyStr}t {day:dd.MM}[/]");
                    continue;
                }
                AnsiConsole.MarkupLine($"[yellow]⚠️ {targetProjectCode} har {Formatting.MinutesToHours(loggedHoursForDayAndProject.Minutes)}t {day:dddd dd. MMMM}[/]");
                var overwrite = AnsiConsole.Prompt(
                    new ConfirmationPrompt(
                        $"Endre fra {Formatting.MinutesToHours(loggedHoursForDayAndProject.Minutes)} til {hoursFriendlyStr} timer?"));

                if (overwrite)
                {
                    var timeEntryRequest = new TimeEntryRequest(session.EmployeeId, day, session.EmployeeId, minutesDiffTowardsTarget, targetProjectCode);
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
                var timeEntryRequest = new TimeEntryRequest(session.EmployeeId, day, session.EmployeeId, minutesPerDay, targetProjectCode);
                await folqClient.AddTimeEntry(timeEntryRequest, cancellationToken);
                AnsiConsole.MarkupLine($"[green]✅ {hoursFriendlyStr} {targetProjectCode} {day:dddd dd. MMMM}[/]");
            }
        }

        await Time.ListWeek(ctx, week, ct: cancellationToken);

    }
}