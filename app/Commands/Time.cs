[RegisterCommands]
[ConsoleAppFilter<AuthenticationFilter>]
internal class Time
{
    /// <summary>Registrerer nye timer</summary>
    /// <param name="range">-w, Hvilken uke som skal timeføres. Gyldige: "This|Previous"</param>
    /// <param name="project">-p, Prosjektkoden til prosjektet. Bruker global default-prosjekt hvis ikke angitt</param>
    /// <param name="hours">-h, Antall timer som skal føres</param>
    public async Task Write(
        ConsoleAppContext ctx,
        [HideDefaultValue, Argument]string? project = null,
        Range range = Range.Week,
        decimal? hours = 7.5m,

        CancellationToken token = default) => await FloqService.WriteLogEntries(range, project, hours, ctx, token);

    /// <summary>Lister førte timer</summary>
    /// <param name="range">-r, Hvilken periode.  Gyldige: "This|Previous"</param>
    [Command("list|ls")]
    public async Task List(ConsoleAppContext ctx, Range range = Range.Week, CancellationToken token = default) => await FloqService.GetLoggedHours(ctx, range, token);

    /// <summary>Setter et prosjekt som default til timeføring</summary>
    /// <param name="project">-p, Prosjektets kode. Eks: 'ANE1006'</param>
    public async Task SetDefault(ConsoleAppContext ctx, [Argument, HideDefaultValue] string? project = null,  CancellationToken token = default) => await FloqService.SetOrSelectDefaultProject(ctx, project, token);

    /// <summary>Henter default-prosjektet ditt</summary>
    public async Task GetDefault(ConsoleAppContext ctx, CancellationToken token = default) => await FloqService.GetDefault(ctx, token);
}

