using System.Reflection;

public class AppInfoHelper
{
    static AppInfoHelper()
    {
        App = GetApp();
    }

    public static AppVersionInfo App { get; }

    private static AppVersionInfo GetApp()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        var version = entryAssembly?.GetName().Version;
        var majorMinorPatch = $"{version?.Major}.{version?.Minor}.{version?.Build}";
        var informationalVersion = entryAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        string? sha = null;
        if (!string.IsNullOrEmpty(informationalVersion))
        {
            if (informationalVersion.Contains(".Sha.", StringComparison.Ordinal))
            {
                // Handle deployed format: extract SHA after ".Sha."
                var shaIndex = informationalVersion.LastIndexOf(".Sha.", StringComparison.Ordinal) + 5;
                if (shaIndex < informationalVersion.Length)
                {
                    sha = informationalVersion.Substring(shaIndex);
                }
            }
            else if (informationalVersion.Contains("+"))
            {
                // Handle local format: extract SHA after "+"
                sha = informationalVersion.Split('+').Last();
            }
            else
            {
                // Fallback to original logic
                sha = informationalVersion.Split(".").Last();
            }
        }

        return new AppVersionInfo(majorMinorPatch, informationalVersion, sha);
    }

    public record AppVersionInfo(string MajorMinorPatch, string? Informational, string? Sha);
}
