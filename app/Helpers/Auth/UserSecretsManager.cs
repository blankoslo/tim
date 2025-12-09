using System.Globalization;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration.UserSecrets;
public class UserSecretsManager
{
    private static readonly HttpClient AuthClient = new()
    {
        BaseAddress = new Uri("https://inni.blank.no"),
    };

    public static async Task WriteCodeExchange(GoogleCodeExchange exchange, CancellationToken token)
    {
        var secretsPath = GetAppDataPath();
        Directory.CreateDirectory(Path.GetDirectoryName(secretsPath)!);
        var decoded = JwtHelper.Decode(exchange.IdToken);

        // Read existing secrets to preserve other fields (upsert operation)
        var secrets = await ReadAsDictionary(token) ?? new Dictionary<string, string>();

        // Update only Google-related fields
        secrets["Google:AccessToken"] = exchange.AccessToken;
        secrets["Google:RefreshToken"] = exchange.RefreshToken;
        secrets["Google:IdToken"] = exchange.IdToken;
        secrets["Google:ExpiresAt"] = exchange.ExpiresAt.ToString("O");
        secrets["Google:Name"] = decoded.Name;
        secrets["Google:Email"] = decoded.Email;

        var secretsJson = ToJson(secrets);
        await File.WriteAllTextAsync(secretsPath, secretsJson, token);
    }

    public static async Task WriteImplicitData(ImplicitCallbackData data, Employee? employee, CancellationToken token)
    {
        var secretsPath = GetAppDataPath();
        Directory.CreateDirectory(Path.GetDirectoryName(secretsPath)!);

        var secrets = await ReadAsDictionary(token) ?? new Dictionary<string, string>();

        secrets["Floq:AccessToken"] = data.AccessToken;
        secrets["Floq:RefreshToken"] = data.RefreshToken;
        secrets["Floq:ExpiresAt"] = data.ExpireDate;
        secrets["Floq:Email"] = data.UserEmail;

        if (employee != null)
        {
            secrets["Employee:Id"] = employee.Id.ToString();
            secrets["Employee:Name"] = $"{employee.First_Name} {employee.Last_Name}";
        }

        var secretsJson = ToJson(secrets);
        await File.WriteAllTextAsync(secretsPath, secretsJson, token);
    }

    public static async Task<UserSession?> GetFloqSession(CancellationToken token)
    {
        var secrets = await ReadAsDictionary(token);
        if (secrets == null)
        {
            return null;
        }

        if (!secrets.TryGetValue("Floq:AccessToken", out var accessToken) || string.IsNullOrEmpty(accessToken))
        {
            return null;
        }

        if (!secrets.TryGetValue("Employee:Name", out var empName) || string.IsNullOrEmpty(empName))
        {
            empName = "<no-name-ansatt>";
        }
        else
        {
            empName = secrets["Employee:Name"];
        }
        if (!secrets.TryGetValue("Employee:Id", out var empId) || string.IsNullOrEmpty(empId))
        {
            throw new Exception("Logg inn på nytt");
        }

        if (!secrets.TryGetValue("Floq:RefreshToken", out var refreshToken) || string.IsNullOrEmpty(refreshToken))
        {
            throw new Exception("Logg inn på nytt");
        }

        DateTime dateTimeInUtc = DateTime.Parse(secrets["Floq:ExpiresAt"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var session = new UserSession(empName, secrets["Floq:Email"], accessToken, refreshToken, int.Parse(empId), dateTimeInUtc);


        return session;
    }

    public static async Task StoreDefaultProject(UserDefaultedProject? value, CancellationToken token)
    {

        var secrets = await ReadAsDictionary(token) ?? new Dictionary<string, string>();
        if (value is null)
        {
            secrets.Remove("DefaultProject");
        }
        else
        {
            secrets["DefaultProject"] = JsonSerializer.Serialize(value);
        }

        var secretsJson = ToJson(secrets);
        var secretsPath = GetAppDataPath();
        await File.WriteAllTextAsync(secretsPath, secretsJson, token);
    }

    public static async Task<UserDefaultedProject?> GetDefaultProject(CancellationToken token)
    {
        var secrets = await ReadAsDictionary(token);
        if (secrets == null || !secrets.TryGetValue("DefaultProject", out var projectJson))
        {

            return null;
        }

        try
        {
            if (JsonSerializer.Deserialize<UserDefaultedProject>(projectJson) is { } project)
            {
                return project;
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async Task<UserSession?> GetGoogleSession(CancellationToken token)
    {
        var secrets = await ReadAsDictionary(token);
        if (secrets == null)
        {
            return null;
        }


        if (!secrets.TryGetValue("Google:IdToken", out var idToken) || string.IsNullOrEmpty(idToken))
        {
            return null;
        }

        var (name, email) = JwtHelper.Decode(idToken);
        var dateTimestr = secrets["Google:ExpiresAt"];
        var expiryUtc = DateTimeOffset.Parse(dateTimestr, CultureInfo.InvariantCulture).UtcDateTime;
        var googleToken = secrets["Google:AccessToken"];

        // empFetch not implemented for Google, deprecated
        throw new NotImplementedException("Implement emp fetch here");
        //return new UserSession(name, email, googleToken, empId, expiryUtc);
    }

    private static async Task<Dictionary<string, string>?> ReadAsDictionary(CancellationToken token)
    {
        var secretsJson = await ReadJson(token);
        if (secretsJson == null)
            return null;
        return FromJson(secretsJson);
    }

    public static async Task<string?> ReadJson(CancellationToken token)
    {
        var secretsPath = GetAppDataPath();
        if (!File.Exists(secretsPath))
        {
            return null;
        }
        return await File.ReadAllTextAsync(secretsPath, token);
    }

    public static string GetAppDataPath()
    {
        var path= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "blank",
            "tim",
            Assembly.GetExecutingAssembly().GetCustomAttribute<UserSecretsIdAttribute>()!.UserSecretsId,
            "secrets.json");

        return path;
    }

    private static string ToJson(Dictionary<string, string> secrets)
    {
        var jsonLines = new List<string> { "{" };
        var count = 0;
        foreach (var kvp in secrets)
        {
            count++;
            var comma = count < secrets.Count ? "," : "";
            jsonLines.Add($"  \"{kvp.Key}\": \"{EscapeJsonString(kvp.Value)}\"{comma}");
        }
        jsonLines.Add("}");
        return string.Join(Environment.NewLine, jsonLines);
    }

    private static Dictionary<string, string> FromJson(string json)
    {
        var dict = new Dictionary<string, string>();
        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.GetString() ?? "";
        }

        return dict;
    }

    private static string EscapeJsonString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    public static async Task<UserSession?> RefreshFloqSession(string refreshToken, CancellationToken token)
    {
        Console.WriteLine("Refreshing session");
        var payload = new { refresh_token = refreshToken };
        var response = await AuthClient.PostAsJsonAsync("/login/oauth/refresh", payload, token);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("unable to refresh session");
            return null;
        }

        var refreshResponse = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>(token);
        if (refreshResponse == null)
        {
            return null;
        }

        // Store the new tokens
        var secretsPath = GetAppDataPath();
        Directory.CreateDirectory(Path.GetDirectoryName(secretsPath)!);

        var secrets = await ReadAsDictionary(token) ?? new Dictionary<string, string>();

        secrets["Floq:AccessToken"] = refreshResponse.AccessToken;
        secrets["Floq:ExpiresAt"] = refreshResponse.ExpiryDate;

        var secretsJson = ToJson(secrets);
        await File.WriteAllTextAsync(secretsPath, secretsJson, token);

        // Re-read the full session to return
        return await GetFloqSession(token);
    }

    private record RefreshTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expiry_date")] string ExpiryDate);

    public static async Task RemoveFloqSession(CancellationToken token)
    {
        var secretsPath = GetAppDataPath();
        if (File.Exists(secretsPath))
        {
            var secrets = await ReadAsDictionary(token);
            if (secrets != null)
            {
                secrets.Remove("Floq:AccessToken");
                secrets.Remove("Floq:RefreshToken");
                secrets.Remove("Floq:ExpiresAt");
                secrets.Remove("Floq:Email");
                secrets.Remove("Employee:Id");
                secrets.Remove("Employee:Name");
                var secretsJson = ToJson(secrets);
                await File.WriteAllTextAsync(secretsPath, secretsJson, token);
            }
        }
    }
}

public record UserSession(string Name, string Email, string AccessToken, string RefreshToken, int EmployeeId, DateTime ExpiresAtUtc)
{
    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    private TimeSpan CalcExpireIn => ExpiresAtUtc - DateTime.UtcNow;

    public string ExpireInFriendly
    {
        get
        {
            var span = CalcExpireIn;
            var prefix = span.TotalMinutes < 0 ? "For " : "Om " ;
            var postfix = span.TotalMinutes < 0 ? " siden" : "";
            return Math.Abs(span.TotalMinutes) switch
            {
                >= 1440 => $"{prefix}{(int)Math.Abs(span.TotalDays)}d {(int)(Math.Abs(span.TotalHours) % 24)}h{postfix}",
                >= 60 => $"{prefix}{(int)Math.Abs(span.TotalHours)}h {Math.Abs(span.Minutes)}m{postfix}" ,
                >= 1 => $"{prefix}{(int)Math.Abs(span.TotalMinutes)}m {Math.Abs(span.Seconds)}s{postfix}",
                _ => $"{prefix}{(int)Math.Abs(span.TotalSeconds)}s{postfix}"
            };
        }
    }
}

public record UserDefaultedProject(string Id, string Project, string Customer);
