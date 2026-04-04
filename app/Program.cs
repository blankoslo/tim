using System.Globalization;

CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("nb-NO");
CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("nb-NO");

// `-` signals "read from stdin". Strip it before ConsoleAppFramework parses args
// so it doesn't get assigned to a positional parameter (e.g. hours).
if (args.Contains("-"))
{
    args = args.Where(a => a != "-").ToArray();
    var lines = new List<string>();
    while (System.Console.ReadLine() is { } line)
        lines.Add(line);
    StdinBuffer.Lines = [.. lines];
}

var app = ConsoleApp.Create();
await app.RunAsync(args);
