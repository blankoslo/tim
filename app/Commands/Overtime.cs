/// <summary>Registrer overtidstimer for utbetaling</summary>
[RegisterCommands("overtime")]
[ConsoleAppFilter<AuthenticationFilter>]
[ConsoleAppFilter<AddStdinToContext>]
internal class Overtime
{
    /// <summary>Registrer overtidstimer</summary>
    /// <param name="hours">Antall timer overtid</param>
    /// <param name="comment">Beskrivelse av overtidsarbeidet</param>
    [Command("")]
    public async Task Register(
        ConsoleAppContext ctx,
        [Argument] decimal hours,
        [Argument] string comment,
        CancellationToken token = default)
    {
        var session = ctx.GetUserSession();
        var client = HttpClientFactory.CreateFloqClientForUser(session);

        var minutes = (int)(hours * 60);

        var request = new PaidOvertimeRequest(
            session.EmployeeId,
            minutes,
            comment
        );

        var success = await client.PostPaidOvertime(request, token);

        if(success)
        {
            Console.MarkupLine($"[green]✓[/] Registrerte [bold]{hours}[/] timer overtid");
            Console.MarkupLine($"  [dim]Kommentar: {comment}[/]");
        }
        else
        {
            Console.MarkupLine($"[red]✗[/] Kunne ikke registrere overtid. Prøv igjen senere.");
        }
    }

    /// <summary>List dine registrerte overtidstimer</summary>
    [Command("list|overtime ls")]
    public async Task List(
        ConsoleAppContext ctx,
        CancellationToken token = default)
    {
        var session = ctx.GetUserSession();
        var client = HttpClientFactory.CreateFloqClientForUser(session);

        var entries = await client.GetPaidOvertime(session.EmployeeId, token);

        if(!entries.Any())
        {
            Console.MarkupLine("[dim]Ingen registrerte overtidstimer funnet.[/]");
            return;
        }

        var table = new Table();
        table.AddColumn(new TableColumn("ID").RightAligned());
        table.AddColumn("Utbetalt dato");
        table.AddColumn("Timer");
        table.AddColumn("Kommentar");
        table.AddColumn(new TableColumn("Registrert").RightAligned());

        var sorted = entries
            .OrderByDescending(e => e.Paid_Date == null)
            .ThenByDescending(e => e.Paid_Date)
            .Take(5);

        foreach(var entry in sorted)
        {
            var hours = entry.Minutes / 60m;
            var paidDateStr = entry.Paid_Date?.ToString("dd.MM.yyyy") ?? "-";
            var regDateStr = entry.Registered_Date?.ToString("dd.MM.yyyy") ?? "-";

            table.AddRow(
                $"[dim]{entry.Id}[/]",
                paidDateStr,
                $"{hours:0.##}",
                entry.Comment ?? "",
                $"[dim]{regDateStr}[/]"
            );
        }

        Console.Write(table);
    }

    /// <summary>Slett en overtidsregistrering</summary>
    /// <param name="id">ID til overtidsregistreringen som skal slettes</param>
    [Command("delete|overtime rm")]
    public async Task Delete(
        ConsoleAppContext ctx,
        [Argument] int id,
        CancellationToken token = default)
    {
        var session = ctx.GetUserSession();
        var client = HttpClientFactory.CreateFloqClientForUser(session);

        var entries = await client.GetPaidOvertime(session.EmployeeId, token);
        var entry = entries.FirstOrDefault(e => e.Id == id);

        if(entry == null)
        {
            Console.MarkupLine($"[red]✗[/] Fant ingen overtidsregistrering med ID {id}.");
            return;
        }

        if(entry.Paid_Date != null)
        {
            Console.MarkupLine(
                $"[red]✗[/] Kan ikke slette overtid som allerede er utbetalt (utbetalt {entry.Paid_Date:dd.MM.yyyy}).");
            return;
        }

        var hours = entry.Minutes / 60m;
        Console.MarkupLine($"Sletter: [bold]{hours:0.##}[/] timer - {entry.Comment}");

        var success = await client.DeletePaidOvertime(id, token);

        if(success)
        {
            Console.MarkupLine($"[green]✓[/] Slettet overtidsregistrering.");
        }
        else
        {
            Console.MarkupLine($"[red]✗[/] Kunne ikke slette overtidsregistrering. Prøv igjen senere.");
        }
    }
}
