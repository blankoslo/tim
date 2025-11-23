using System.Net.Http.Json;
using System.Text.Json;

public class FloqClient(HttpClient client)
{
    public async Task<IEnumerable<Employee>> GetEmployees(CancellationToken token)
    {
        var res = await client.GetFromJsonAsync<IEnumerable<Employee>>("/employees", token);
        res ??= [];
        return res.OrderBy(x => x.Id);
    }

    public async Task<Employee?> GetEmployee(int employeeId, CancellationToken token)
    {
        var res = await client.GetFromJsonAsync<IEnumerable<Employee>>($"/employees?select=*&id=eq.{employeeId}", token);
        return res?.FirstOrDefault();
    }

    public async Task<Employee?> GetEmployeeByEmail(string email, CancellationToken token)
    {
        var res = await client.GetFromJsonAsync<IEnumerable<Employee>>($"/employees?select=*&email=eq.{email}", token);
        return res?.FirstOrDefault();
    }

    private record RpcProjectsForEmployeeeForDateRequest(int employee_id, string date);
    private record RpcEmployeesOnProjectsRequest(DateOnly from_date, DateOnly to_date);

    // RPC: projects_for_employee_for_date
    // Denne funksjonen returnerer prosjekter fordi noe/noen sørger for at
    // prosjekter finnes for ansatt & dato (med null|0-verdier).
    public async Task<IEnumerable<RpcProjectsForEmployeeeForDateResponse>> GetRpcProjectsForEmployeeForDate(int employeeId, DateOnly date, CancellationToken token)
    {
        var reqPayload = new RpcProjectsForEmployeeeForDateRequest(employeeId, date.ToString("yyyy-MM-dd"));
        var res = await client.PostAsJsonAsync("/rpc/projects_for_employee_for_date", reqPayload, JsonSerializerOptions.Web, token);
        if (res.IsSuccessStatusCode)
        {
            return await res.Content.ReadFromJsonAsync<IEnumerable<RpcProjectsForEmployeeeForDateResponse>>(token) ?? [];
        }
        return [];
    }

    // RPC: employees_on_projects
    // Pass på helger! Bruk et ukes-spenn for bedre resultat.
    public async Task<IEnumerable<RpcEmployeesOnProjectsResponse>> GetRpcEmployeesOnProjects(DateOnly fromDate, DateOnly toDate, CancellationToken token)
    {
        var reqPayload = new RpcEmployeesOnProjectsRequest(fromDate, toDate);
        var res = await client.PostAsJsonAsync("/rpc/employees_on_projects", reqPayload, JsonSerializerOptions.Web, token);
        if (res.IsSuccessStatusCode)
        {
            return await res.Content.ReadFromJsonAsync<IEnumerable<RpcEmployeesOnProjectsResponse>>(token) ?? [];
        }
        return [];
    }

    public async Task<bool> AddTimeEntry(TimeEntryRequest request, CancellationToken token)
    {
        var res = await client.PostAsJsonAsync("/time_entry", request, JsonSerializerOptions.Web, token);
        return res.IsSuccessStatusCode;
    }
}
public record RpcProjectsForEmployeeeForDateResponse(string Id, string Project, string Customer, int Minutes, int PercentageStaffed);
public record RpcEmployeesOnProjectsResponse(string Customer_Id, string Customer_Name, string First_Name, string Last_Name, int Id, string Emoji);


public record Employee(int Id,
    string Email,
    string Title,
    DateOnly Date_Of_Employment,
    bool Has_Permanent_Position,
    DateOnly? Termination_Date,
    string First_Name,
    string Last_Name,
    DateOnly? Birth_Date)
{

    public Today CheckToday()
    {
        var utcTime = DateTime.UtcNow;
        TimeZoneInfo norwegianTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        DateTimeOffset today = TimeZoneInfo.ConvertTimeFromUtc(utcTime, norwegianTimeZone);


        if (ActivelyEmployeed() && RelativeTo(Birth_Date) is { IsToday: true } birthday)
            return new Today(birthday.Years, TodayType.Birthday);

        if (ActivelyEmployeed() && RelativeTo(Date_Of_Employment) is { Years: 0, IsToday: true } firstDay)
            return new Today(firstDay.Years, TodayType.FirstDay);

        if (ActivelyEmployeed() && RelativeTo(Date_Of_Employment) is { Years: >0, IsToday: true } jubileum)
            return new Today(jubileum.Years, TodayType.WorkAnniversary);

        if (ActivelyEmployeed() && RelativeTo(Termination_Date) is { IsToday: true } exit)
            return new Today(exit.Years, TodayType.Exit);

        return Today.Nope();

        (bool IsToday, int Years) RelativeTo(DateOnly? dateOnly)
        {
            return dateOnly == null ? (false, 0) : (dateOnly.Value.Month == today.Month && dateOnly.Value.Day == today.Day, today.Year - dateOnly.Value.Year);
        }
    }

    public bool ActivelyEmployeed()
    {
        if(Termination_Date == null)
            return true;

        return Termination_Date > DateOnly.FromDateTime(DateTime.Now);
    }

    public int? Age()
    {
        if (Birth_Date == null)
            return null;

        var today = DateOnly.FromDateTime(DateTime.Now);
        int age = today.Year - Birth_Date.Value.Year; // Calculate the difference in years

        if (Birth_Date.Value > today.AddYears(-age)) // Adjust the age if the birthday hasn't occurred yet this year
        {
            age--;
        }

        return age;
    }
}

public record Today(int Years, TodayType TodayType)
{
    public static Today Nope()
    {
        return new Today(0, TodayType.None);
    }
}

public enum TodayType
{
    None = 0,
    Birthday = 1,
    FirstDay = 2,
    WorkAnniversary = 3,
    Exit = 4,
}

public record TimeEntryRequest(int creator, DateOnly date, int employee, int minutes, string project);
