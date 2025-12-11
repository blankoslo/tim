[RegisterCommands("reports")]
[ConsoleAppFilter<AuthenticationFilter>]
[ConsoleAppFilter<AddStdinToContext>]
internal class Reports
{
    /// <param name="range">-r, Range: CurrentMonth, Previousmonth</param>
    /// <param name="projectId">-p, ProsjektID, f.eks ANE1006</param>
    /// <param name="outputPath">-o, mappe for å lagre csv-filene</param>
    [Command("project-employee-hours")]
    public async Task<int> ProjectEmployeHoursReport(ConsoleAppContext ctx, SelectedRange range, [Argument] string? projectId = null, string? outputPath = null,  CancellationToken token = default)
    {
        var session = ctx.GetUserSession();
        var client = HttpClientFactory.CreateReportsClientForUser(session);

        var projectIds = new List<string>();
        if (System.Console.IsInputRedirected)
        {
            ctx.StandardInput(line =>
            {
                if (!string.IsNullOrWhiteSpace(line.line))
                {
                    if (line.line.Length != 7)
                    {
                        throw new Exception("BooM! Invalid project ID from piped input.");
                    }
                    projectIds.Add(line.line.Trim());
                }
            });
        }
        else if (!string.IsNullOrWhiteSpace(projectId))
        {
            projectIds.Add(projectId);
        }

        if (projectIds.Count == 0)
        {
            Console.MarkupLine("[red]❌ ProsjektID må oppgis som argument eller via stdin[/]");
            return -1;
        }

        DateOnly from, to;
        try
        {
            (from,  to) = range switch
            {
                SelectedRange.CurrentMonth => GetMonthRange(DateTime.Today),
                SelectedRange.PreviousMonth => GetMonthRange(DateTime.Today.AddMonths(-1)),
                _ => throw new ArgumentOutOfRangeException(nameof(range), range, null)
            };
        }
        catch (Exception)
        {
            Console.MarkupLine("[red]❌ Ugyldig range valgt[/]");
            return -1;
        }
        var tasks = new List<Task>();
        foreach (var pid in projectIds)
        {
            var t = Download(client, from, to, pid, outputPath, token);
            tasks.Add(t);
        }
        await Task.WhenAll(tasks);
        Console.MarkupLine($"[green]✓[/] Ferdig med nedlasting av rapporter.");
        return 0;
    }

    private static async Task Download(FloqReportsApiClient client, DateOnly from, DateOnly to,
        string projectId, string? outputFolder, CancellationToken token)
    {
        await using var stream = await client.GetProjectsEmployeeHoursStream(from, to, projectId.ToUpper(), token);

        var defaultFileName = $"report_{projectId.ToLower()}_{from:yyyy-MM-dd}_{to:yyyy-MM-dd}.csv";

        var folder = string.IsNullOrWhiteSpace(outputFolder) ? "." : outputFolder;

        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var fileName = Path.Combine(folder, defaultFileName);

        await using var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
        await stream.CopyToAsync(fileStream, token);

        Console.MarkupLine($"[dim]Report saved {Path.GetFullPath(fileName)}[/]");
    }

    private static (DateOnly From, DateOnly To) GetMonthRange(DateTime date)
    {
        DateOnly firstOfMonth = new(date.Year, date.Month, 1);
        return (firstOfMonth, firstOfMonth.AddMonths(1).AddDays(-1));
    }
}