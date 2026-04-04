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
    public async Task<int> Write(
        ConsoleAppContext ctx,
        [HideDefaultValue] string? project = null,
        [HideDefaultValue] SelectedRange range = SelectedRange.SingleDay,
        [Argument] decimal? hours = 7.5m,
        [HideDefaultValue] string? date = null,
        [HideDefaultValue] bool? yes = false,
        CancellationToken token = default)
    {
        return await WriteLogEntries(ctx, range, project, hours, date, yes, token);
    }

    private const int minutesPerDay = 450;

    private async Task<int> WriteLogEntries(ConsoleAppContext ctx,
        SelectedRange? mode = null,
        string? project = null,
        decimal? hours = null,
        string? userDefinedDateStr = null,
        bool? skipConfirmations = null,
        CancellationToken token = default)
    {
        var session = ctx.GetUserSession();

        DateOnly? specificDate = null;

        if(userDefinedDateStr is not null)
        {
            var currentYear = DateTime.UtcNow.Year;
            if(DateOnly.TryParseExact($"{currentYear}.{userDefinedDateStr}", "yyyy.dd.MM", out var parsedDate))
            {
                specificDate = parsedDate;
                mode = SelectedRange.SingleDay;
            }
            else
            {
                Console.MarkupLine(
                    $"[red]errorz![/] Kunne ikke tolke datoen du oppga: '{userDefinedDateStr}'. Bruk format dd.MM, for eksempel 15.03 for 15. mars.");
                return 1;
            }
        }

        var displayRange = mode switch
        {
            SelectedRange.SingleDay or null => SelectedRange.CurrentWeek,
            _ => mode.Value
        };

        var datesToWrite = GetDatesToWrite(mode, specificDate);
        var hoursToWrite = hours ?? 7.5m;

        string projectToWriteOn;
        if(project != null)
        {
            // project arg provided
            var client = HttpClientFactory.CreateFloqClientForUser(session);
            var allProjects = await client.GetAllProjectsWithCustomer(token);
            var projectExists = allProjects.Any(p =>
                string.Compare(p.Id, project, StringComparison.CurrentCultureIgnoreCase) == 0);
            if(!projectExists)
            {
                Console.MarkupLine($"[red]❌ Fant ingen prosjekt med kode '{project}'[/]");
                var availableProjects = allProjects?
                    .Where(p => p.Active &&
                                p.Id.StartsWith(project.Length > 2 ? project.ToUpper()[..2] : project.ToUpper()))
                    .OrderByDescending(p => p.Id)
                    .Select(p => $"{p.Id} - {p.Name} ({p.Customer?.Name})")
                    .ToList();
                if(availableProjects is { Count: > 0 })
                {
                    Console.MarkupLine("Mente du noen av disse?");
                    foreach(var proj in availableProjects.Take(5))
                    {
                        Console.MarkupLine($" - {proj}");
                    }
                }

                return 1;
            }

            projectToWriteOn = project.ToUpper();
        }
        else
        {
            // project arg not provided
            var defaultProj = await UserSecretsManager.GetDefaultProject(token);
            if(defaultProj == null)
            {
                if(skipConfirmations is true)
                {
                    Console.Console.MarkupLine("[red]❌ Ingen default prosjekt satt og du bruker -y for å unnvike interaktivitet. Avbryter![/]");
                    return 1;
                }

                Console.MarkupLine("[yellow]⚠️ Ingen prosjektkode angitt && ingen default prosjekt satt.[/]");
                Console.WriteLine();
                Console.MarkupLine($"Hjelp:\n" +
                                   $"- Angi et prosjekt med [green]`tim write -p|--project [purple]<PROSJEKTKODE>[/]`[/]\n" +
                                   $"- Sett et default prosjekt med [green]`tim set-default [purple]<PROSJEKTKODE>[/]`[/] og rekjør [green]`tim write`[/].");
                Console.WriteLine();

                var chooseProject = Console.Prompt(
                    new ConfirmationPrompt(
                        $"\nVil du sette et default-prosjekt og timeføre {hoursToWrite} på dette nå?"));
                if(chooseProject)
                {
                    await SetDefault(ctx, null, token);
                    var newDefault = await UserSecretsManager.GetDefaultProject(token);
                    if(newDefault is not null)
                    {
                        projectToWriteOn = newDefault.Id;
                    }
                    else
                    {
                        Console.MarkupLine("[red]❌ Ingen default prosjekt satt. Avbryter.[/]");
                        return 1;
                    }
                }
                else
                {
                    return 1;
                }
            }
            else
            {
                projectToWriteOn = defaultProj.Id;
            }

            Console.MarkupLine($"Bruker default prosjektkode: [purple]{projectToWriteOn}[/]");
        }


        await WriteEntriesForDates(projectToWriteOn, datesToWrite, session, hoursToWrite, skipConfirmations ?? true,
            token);

        await ListPeriod(ctx, displayRange, specificDate, ct: token);
        if(datesToWrite.Length == 1)
        {
            Console.WriteLine($"Førte {hoursToWrite} på {projectToWriteOn} den {datesToWrite[0]:dd.MM} ");
        }
        else
        {
            Console.WriteLine(
                $"Førte {hoursToWrite} på {projectToWriteOn} [{datesToWrite[0]:dd.MM}-{datesToWrite[^1]:dd.MM}] ");
        }
        return 0;
    }

    private static async Task WriteEntriesForDates(string targetProjectCode, DateOnly[] datesToWrite,
        UserSession session, decimal hours, bool yes,
        CancellationToken cancellationToken = default)
    {
        var client = HttpClientFactory.CreateFloqClientForUser(session);

        foreach(var day in datesToWrite)
        {
            await WriteEntryForDay(client, session, datesToWrite.Length, day, targetProjectCode, yes, hours,
                cancellationToken);
        }
    }

    private static async Task WriteEntryForDay(FloqClient client, UserSession session, int totalDaysToWrite,
        DateOnly day,
        string targetProjectCode, bool skipConfirm, decimal? hours = null,
        CancellationToken cancellationToken = default)
    {
        var minutesToLog = minutesPerDay;

        if(hours.HasValue)
        {
            minutesToLog = (int)(hours.Value * 60);
        }

        var hoursFriendlyStr = minutesToLog > 0 ? $"{minutesToLog / 60m:F1}" : "0";

        var loggedHoursForDay =
            await client.GetRpcProjectsForEmployeeForDate(session.EmployeeId, day, cancellationToken);
        var loggedHoursForDayAndProject = loggedHoursForDay.SingleOrDefault(h => h.Id == targetProjectCode);

        var hoursDiff = (loggedHoursForDayAndProject?.Minutes - (hours * 60)) / 60m;

        var changeTxt = hoursDiff switch
        {
            < 0 =>
                $"{Formatting.MinutesToHours(loggedHoursForDayAndProject?.Minutes)}[green]+{Math.Abs(hoursDiff.Value):F1}[/] => {hoursFriendlyStr}",
            > 0 =>
                $"{Formatting.MinutesToHours(loggedHoursForDayAndProject?.Minutes)}[red]-{Math.Abs(hoursDiff.Value):F1}[/] => {hoursFriendlyStr}",
            0 => $"[grey]Hadde allerede {hoursFriendlyStr} timer. Ingen endring.[/]",
            null => $"0[green]+{hoursFriendlyStr}[/] => {hoursFriendlyStr}"
        };
        var log = $"[purple]{targetProjectCode}[/] [[{day:dd.MM}]]  {changeTxt}";

        if(loggedHoursForDayAndProject is { Minutes: > 0 } existinglog)
        {
            var minutesDiffTowardsTarget = minutesToLog - existinglog.Minutes;


            if(totalDaysToWrite == 1 || skipConfirm)
            {
                var timeEntryRequest = new TimeEntryRequest(session.EmployeeId, day, session.EmployeeId,
                    minutesDiffTowardsTarget, targetProjectCode);
                await client.AddTimeEntry(timeEntryRequest, cancellationToken);
                Console.MarkupLine(log);
            }
            else
            {
                var overwrite = Console.Prompt(new ConfirmationPrompt(log));

                if(overwrite)
                {
                    var timeEntryRequest = new TimeEntryRequest(session.EmployeeId, day, session.EmployeeId,
                        minutesDiffTowardsTarget, targetProjectCode);
                    await client.AddTimeEntry(timeEntryRequest, cancellationToken);
                }
            }
        }
        else
        {
            var minutesDiffTowardsTarget = minutesPerDay - loggedHoursForDayAndProject?.Minutes;
            if(minutesDiffTowardsTarget != 0)
            {
                var timeEntryRequest = new TimeEntryRequest(session.EmployeeId, day, session.EmployeeId, minutesToLog,
                    targetProjectCode);
                await client.AddTimeEntry(timeEntryRequest, cancellationToken);
            }

            Console.MarkupLine(log);
        }
    }
}
