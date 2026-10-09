using System.Net.Http.Headers;


public class HttpClientFactory
{
    private static HttpClient FloqClient()
    {
        var handler = new SocketsHttpHandler { MaxConnectionsPerServer = 6 };
        return new HttpClient(handler) { BaseAddress = new Uri("https://api-prod.floq.no") };
    }

    private static HttpClient PlatformClient()
    {
        var handler = new SocketsHttpHandler { MaxConnectionsPerServer = 6 };
        return new HttpClient(handler) { BaseAddress = new Uri("https://api.platform.floq.no") };
    }

    public static FloqClient CreateFloqClientForUser(UserSession session)
    {
        return CreateFloqClientForUser(session.AccessToken, session.EmployeeId);
    }

    public static FloqPlatformClient CreatePlatformClientForUser(UserSession session)
    {
        return SetupHttpClient<FloqPlatformClient>(PlatformClient(), session.AccessToken,
                session.EmployeeId, c => new FloqPlatformClient(c));
    }

    public static FloqClient CreateFloqClientForUser(string accessToken, int? employeeId = null)
    {
        return SetupHttpClient<FloqClient>(FloqClient(), accessToken, employeeId, http => new FloqClient(http));
    }

    private static T SetupHttpClient<T>(HttpClient client, string accessToken, int? employeeId,
        Func<HttpClient, T> createType)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if(employeeId is { } empId)
        {
            client.DefaultRequestHeaders.Add("User-Agent", [
                $"tim/{AppInfoHelper.App.MajorMinorPatch}",
                $"floq-employee/{empId}"
            ]);
        }
        else
        {
            client.DefaultRequestHeaders.Add("User-Agent", [
                $"tim/{AppInfoHelper.App.MajorMinorPatch}"
            ]);
        }

        return createType(client);
    }
}
