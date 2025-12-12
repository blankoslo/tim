public class FloqReportsApiClient(HttpClient client)
{
    // https://reports-api-prod.floq.no/project_employee_hours?start_date=2025-11-01&end_date=2025-11-30&project_id=ANE1006
    public async Task<Stream> GetProjectsEmployeeHoursStream(DateOnly startDate, DateOnly endDate, string projectId,
        CancellationToken token)
    {
        return await client.GetStreamAsync(
            $"/project_employee_hours?start_date={startDate:yyyy-MM-dd}&end_date={endDate:yyyy-MM-dd}&project_id={projectId}",
            token);
    }
}
