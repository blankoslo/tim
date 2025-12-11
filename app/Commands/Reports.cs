[RegisterCommands("reports")]
[ConsoleAppFilter<AuthenticationFilter>]
[ConsoleAppFilter<AddStdinToContext>]
internal class Reports
{
    /// <param name="range">-r, Range: CurrentMonth, Previousmonth</param>
    /// <param name="projectId">-p, ProsjektID, f.eks ANE1006</param>
    /// <param name="outputPath">-o, et sted å lagre csv-filen</param>
    [Command("project-employee-hours")]
    public async Task<int> ProjectEmployeHoursReport(ConsoleAppContext ctx, SelectedRange range, [Argument] string projectId, string? outputPath = null,  CancellationToken token = default)
    {
        var session = ctx.GetUserSession();
        var client = HttpClientFactory.CreateReportsClientForUser(session);
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


        await using var stream = await client.GetProjectsEmployeeHoursStream(from, to, projectId.ToUpper(), token);

        var defaultFileName = $"report_{projectId}_{from:yyyy-MM-dd}_{to:yyyy-MM-dd}.csv";

        string fileName;
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            fileName = defaultFileName;
        }
        else if (Directory.Exists(outputPath))
        {
            fileName = Path.Combine(outputPath, defaultFileName);
        }
        else
        {
            fileName = outputPath;
        }

        await using var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
        await stream.CopyToAsync(fileStream, token);

        Console.WriteLine($"Report saved to: {Path.GetFullPath(fileName)}");
        return 0;
    }

    private static (DateOnly From, DateOnly To) GetMonthRange(DateTime date)
    {
        DateOnly firstOfMonth = new(date.Year, date.Month, 1);
        return (firstOfMonth, firstOfMonth.AddMonths(1).AddDays(-1));
    }
}