using System.ComponentModel;

internal partial class Time
{
    /// <summary>Registrerer nye timer</summary>
    /// <param name="range">-r, Hvilken uke som skal timeføres. Gyldige: "Current|Previous"</param>
    /// <param name="project">-p, Prosjektkoden til prosjektet. Bruker global default-prosjekt hvis ikke angitt</param>
    /// <param name="hours">-h, Antall timer som skal føres</param>
    /// <param name="date">-d, Dato som skal føres, dd.MM Default dagens dato.</param>
    /// <param name="yes">-y, Bare kjørr, ikke spør om bekreftelser.</param>
    [Command("write")]
    [ConsoleAppFilter<AddStdinToContext>]
    public async Task Write(
        ConsoleAppContext ctx,
        [HideDefaultValue] string? project = null,
        [HideDefaultValue] SelectedRange range = SelectedRange.SingleDay,
         [Argument] decimal? hours = 7.5m,
         [HideDefaultValue] string? date = null,
         [HideDefaultValue] bool? yes = false,
        CancellationToken token = default) => await WriteLogEntries(ctx, range, project, hours, date, yes, token);

    private const int minutesPerDay = 450;

    private async Task WriteLogEntries(ConsoleAppContext ctx,
        SelectedRange? mode = null,
        string? project = null,
        decimal? hours = null,
        string? userDefinedDateStr = null,
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

        DateOnly? specificDate = null;

        if (userDefinedDateStr is {})
        {
            var couldParse = DateOnly.TryParseExact($"{currentYear}.{userDefinedDateStr}", "yyyy.dd.MM", out var parsedDate);
            if (couldParse)
            {
                specificDate = parsedDate;
            }
            else
            {
                Console.MarkupLine($"[red]errorz![/] Kunne ikke tolke datoen du oppga: '{userDefinedDateStr}'. Bruk format dd.MM, for eksempel 15.03 for 15. mars.");
                return;
            }
        }

        var datesToWrite = GetDatesToWrite(mode, specificDate);
        decimal hoursToWrite = hours ?? 7.5m;

        string projectToWriteOn;
        if (project != null)
        {
            var client = HttpClientFactory.CreateFloqClientForUser(session);
            var allProjects = await client.GetAllProjectsWithCustomer();
            var projectExists = allProjects?.Any(p => string.Compare(p.Id,  project, StringComparison.CurrentCultureIgnoreCase) == 0);
            if (projectExists is false)
            {
                Console.MarkupLine($"[red]❌ Fant ingen prosjekt med kode '{project}'[/]");
                var availableProjects = allProjects?
                    .Where(p => p.Active && p.Id.StartsWith(project.Length > 2 ? project.ToUpper()[..2] : project.ToUpper()))
                    .OrderByDescending(p => p.Id)
                    .Select(p => $"{p.Id} - {p.Name} ({p.Customer?.Name})")
                    .ToList();
                if (availableProjects is { Count: > 0 })
                {
                    Console.MarkupLine("Mente du noen av disse?");
                    foreach (var proj in availableProjects.Take(5))
                    {
                        Console.MarkupLine($" - {proj}");
                    }
                }
                return;
            }
            projectToWriteOn = project.ToUpper();
        }
        else
        {
            // fetch default project:
            var defaultProj = await UserSecretsManager.GetDefaultProject(cancellationToken);
            if (defaultProj == null)
            {
                Console.MarkupLine("[red]❌ Ingen prosjektkode angitt og ingen default prosjekt funnet.[/]");
                Console.WriteLine();
                Console.MarkupLine($"Hjelp:\n" +
                                   $"- Angi et prosjekt med [green]`tim write -p|--project [purple]<PROSJEKTKODE>[/]`[/]\n" +
                                   $"- Sett et default prosjekt med [green]`tim set-default [purple]<PROSJEKTKODE>[/]`[/] og rekjør [green]`tim write`[/].");
                Console.WriteLine();
                var chooseProject = Console.Prompt(
                    new ConfirmationPrompt($"\nVil du sette et default-prosjekt og timeføre {hoursToWrite} på dette nå?"));
                if (chooseProject)
                {
                    await SetDefault(ctx, null, cancellationToken);
                    var newDefault = await UserSecretsManager.GetDefaultProject(cancellationToken);
                    if (newDefault is { })
                    {
                        projectToWriteOn = newDefault.Id;
                    }
                    else
                    {
                        Console.WriteLine($"[red]❌ Ingen default prosjekt satt. Avbryter.[/]");
                        return;
                    }

                }
                else
                {
                    return;
                }
            }
            else
            {
                projectToWriteOn = defaultProj.Id;
            }

            Console.MarkupLine($"Bruker default prosjektkode: [purple]{projectToWriteOn}[/]");
        }



        await WriteEntriesForDates(projectToWriteOn, datesToWrite, session, hoursToWrite, skipConfirmations ?? true, cancellationToken);

        await ListPeriod(ctx, displayList, ct: cancellationToken);
        if (datesToWrite.Length == 1)
        {
            Console.WriteLine($"Førte {hoursToWrite} på {projectToWriteOn} den {datesToWrite[0]:dd.MM} ");
        }
        else
        {
            Console.WriteLine($"Førte {hoursToWrite} på {projectToWriteOn} [{datesToWrite[0]:dd.MM}-{datesToWrite[^1]:dd.MM}] ");
        }

    }

    private static async Task WriteEntriesForDates(string targetProjectCode, DateOnly[] datesToWrite,
        UserSession session, decimal hours, bool yes,
        CancellationToken cancellationToken = default)
    {
        var client = HttpClientFactory.CreateFloqClientForUser(session);

        foreach (var day in datesToWrite)
        {
            await WriteEntryForDay(client, session, totalDaysToWrite:datesToWrite.Length, day, targetProjectCode, yes, hours, cancellationToken);
        }
    }

    private static async Task WriteEntryForDay(FloqClient client, UserSession session, int totalDaysToWrite, DateOnly day,
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

        decimal? hoursDiff = (loggedHoursForDayAndProject?.Minutes - hours * 60) / 60m;

        var changeTxt = hoursDiff switch
        {
            < 0 => $"{Formatting.MinutesToHours(loggedHoursForDayAndProject?.Minutes)}[green]+{Math.Abs(hoursDiff.Value):F1}[/] => {hoursFriendlyStr}",
            > 0 => $"{Formatting.MinutesToHours(loggedHoursForDayAndProject?.Minutes)}[red]-{Math.Abs(hoursDiff.Value):F1}[/] => {hoursFriendlyStr}",
            0 => $"[grey]Hadde allerede {hoursFriendlyStr} timer. Ingen endring.[/]",
            null => $"0[green]+{hoursFriendlyStr}[/[ => {hoursFriendlyStr}"
        };
        string log = $"[purple]{targetProjectCode}[/] [[{day:dd.MM}]]  {changeTxt}";

        if (loggedHoursForDayAndProject is { Minutes: > 0 } existinglog)
        {
            var minutesDiffTowardsTarget = minutesToLog - existinglog.Minutes;


            if (totalDaysToWrite == 1 || skipConfirm)
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
            if (minutesDiffTowardsTarget != 0)
            {
                var timeEntryRequest = new TimeEntryRequest(session.EmployeeId, day, session.EmployeeId, minutesToLog, targetProjectCode);
                await client.AddTimeEntry(timeEntryRequest, cancellationToken);
            }

            Console.MarkupLine(log);
        }
    }
}
