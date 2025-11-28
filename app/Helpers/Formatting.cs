using System.Globalization;

public static class Formatting
{
    // 450 => 7.5
    public static string MinutesToHours(int? projectLogMinutes)
    {
        if (projectLogMinutes == null)
            return "0";

        return (projectLogMinutes.Value / 60m).ToString("F1");
    }

    public static string Format(UserDefaultedProject proj)
    {
        return $"[purple]{proj.Id}[/] {proj.Project} ";
    }

    public static string Format(RpcProjectsForEmployeeeForDateResponse proj)
    {
        return $"[purple]{proj.Id}[/] {proj.Project} ";
    }

    public static string FormatOther(Employee emp)
    {
        var color = emp.ActivelyEmployeed() ? "white" : "grey dim";
        return $"[{color}]{emp.First_Name} {emp.Last_Name}[/] [[id:{emp.Id}]]";
    }

    public static string FormatEmpOnProj(RpcEmployeesOnProjectsResponse emp)
    {
        return $"[white]{emp.Customer_Name}[/] {emp.First_Name} {emp.Last_Name} [dim][[id:{emp.Id}]][/]";
    }

    public static string ToNorwegianDateString(this DateOnly date)
    {
        return date.ToString("dd. MMMM", CultureInfo.GetCultureInfo("nb-NO"));
    }
}