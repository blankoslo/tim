using System.Net.Http.Json;
using System.Text.Json.Serialization;

// floq-platform (https://api.platform.floq.no): the reads that replaced floq-db's RPC functions.
// Same OIDC token as PostgREST, but camelCase JSON and inclusive date ranges.
public class FloqPlatformClient(HttpClient client)
{
    // Replaces /rpc/projects_for_employee_for_date: one call for the whole range instead of one per day.
    public async Task<IReadOnlyList<EmployeeDay>> GetEmployeeDays(IEnumerable<int> employeeIds, DateOnly from,
        DateOnly to, CancellationToken token)
    {
        var res = await client.GetFromJsonAsync(
            $"/reports/employee-days?employeeIds={string.Join(',', employeeIds)}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            FloqPlatformJsonSerializerContext.Default.IReadOnlyListEmployeeDay, token);
        return res ?? [];
    }

    // Replaces /rpc/employees_on_projects. Ids only — names come from /employees.
    public async Task<IReadOnlyList<BillablePlacement>> GetBillableCustomers(DateOnly from, DateOnly to,
        CancellationToken token)
    {
        var res = await client.GetFromJsonAsync(
            $"/staffing/billable-customers?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            FloqPlatformJsonSerializerContext.Default.IReadOnlyListBillablePlacement, token);
        return res ?? [];
    }

    public async Task<TimesheetPage> GetHours(int employeeId, DateOnly from, DateOnly to, CancellationToken token)
    {
        var res = await client.GetFromJsonAsync(
            $"/timesheet/hours?employeeId={employeeId}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            FloqPlatformJsonSerializerContext.Default.TimesheetPage, token);
        return res ?? new TimesheetPage([]);
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage msg, CancellationToken token)
    {
        return client.SendAsync(msg, token);
    }
}

// CustomerId/CustomerName are null for AVS, which is no project.
public record EmployeeDay(
    int EmployeeId,
    DateOnly Date,
    string Code,
    string? Name,
    string? CustomerId,
    string? CustomerName,
    int Minutes,
    int StaffedPercentage,
    int AbsencePercentage);

public record BillablePlacement(int EmployeeId, string CustomerId, string? CustomerName);

public record TimesheetPage(IReadOnlyList<TimesheetEntry> Entries);

public record TimesheetEntry(DateOnly Date, string Code, int Minutes);

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase
)]
[JsonSerializable(typeof(IReadOnlyList<EmployeeDay>))]
[JsonSerializable(typeof(IReadOnlyList<BillablePlacement>))]
[JsonSerializable(typeof(TimesheetPage))]
internal partial class FloqPlatformJsonSerializerContext : JsonSerializerContext
{
}
