using System.Globalization;

CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("nb-NO");
CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("nb-NO");

var app = ConsoleApp.Create();
await app.RunAsync(args);
