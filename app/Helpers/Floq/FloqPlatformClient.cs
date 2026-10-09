public class FloqPlatformClient(HttpClient client)
{
    // https://api.platform.floq.no/reports/project-employee-hours.csv?projectId=ANE1006&from=2025-11-01&to=2025-11-30
    public async Task<Stream> GetProjectEmployeeHoursCsvStream(DateOnly startDate, DateOnly endDate,
        string projectId, CancellationToken token)
    {
        return await client.GetStreamAsync(
            $"/reports/project-employee-hours.csv?projectId={Uri.EscapeDataString(projectId)}&from={startDate:yyyy-MM-dd}&to={endDate:yyyy-MM-dd}",
            token);
    }
}
