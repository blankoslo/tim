public class Formatting
{
    // 450 => 7.5
    public static string MinutesToHours(int projectLogMinutes)
    {
        return (projectLogMinutes / 60m).ToString("F1");
    }

    public static string Format(UserDefaultedProject proj)
    {
        return $"[dim]{proj.Id}[/] {proj.Project} ";
    }

    public static string Format(RpcProjectsForEmployeeeForDateResponse proj)
    {
        return $"[dim]{proj.Id}[/] {proj.Project} ";
    }

    public static string FormatOther(Employee emp)
    {
        var color = emp.ActivelyEmployeed() ? "white" : "grey dim";
        return $"[{color}]{emp.First_Name} {emp.Last_Name}[/] [[id:{emp.Id}]]";
    }

    public static string FormatEmpOnProj(RpcEmployeesOnProjectsResponse emp)
    {
        return $"[white] {emp.Customer_Name} {emp.First_Name} {emp.Last_Name}[/] [[id:{emp.Id}]]";
    }
}