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
}