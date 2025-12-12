using System.Net.Http.Headers;


public class HttpClientFactory
{
    private static FloqClient? _clientSingletion;
    private static FloqReportsApiClient? _clientReportsSingletion;

    private static readonly HttpClient FloqClient = new() { BaseAddress = new Uri("https://api-prod.floq.no") };

    private static readonly HttpClient ReportsClient = new()
    {
        BaseAddress = new Uri("https://reports-api-prod.floq.no")
    };

    public static FloqClient CreateFloqClientForUser(UserSession session)
    {
        if(_clientSingletion == null)
        {
            _clientSingletion = CreateFloqClientForUser(session.AccessToken, session.EmployeeId);
        }

        return _clientSingletion;
    }

    public static FloqReportsApiClient CreateReportsClientForUser(UserSession session)
    {
        if(_clientReportsSingletion == null)
        {
            _clientReportsSingletion = SetupHttpClient<FloqReportsApiClient>(ReportsClient, session.AccessToken,
                session.EmployeeId, c => new FloqReportsApiClient(c));
        }

        return _clientReportsSingletion;
    }

    public static FloqClient CreateFloqClientForUser(string accessToken, int? employeeId = null)
    {
        return SetupHttpClient<FloqClient>(FloqClient, accessToken, employeeId, http => new FloqClient(http));
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
