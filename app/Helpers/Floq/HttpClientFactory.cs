using System.Net.Http.Headers;


public class HttpClientFactory
{
    private static FloqClient? _clientSingletion;

    private static readonly HttpClient Client = new()
                                                {
                                                    BaseAddress = new Uri("https://api-prod.floq.no"),
                                                };

    public static FloqClient CreateFloqClientForUser(UserSession session)
    {
        if (_clientSingletion == null)
        {
            _clientSingletion = CreateFloqClientForUser(session.AccessToken, session.EmployeeId);
        }

        return _clientSingletion;
    }

    public static FloqClient CreateFloqClientForUser(string accessToken, int? employeeId = null)
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (employeeId is {} empId )
        {
            Client.DefaultRequestHeaders.Add("User-Agent", [
                $"tim/{AppInfoHelper.App.MajorMinorPatch}",
                $"floq-employee/{empId}"
            ]);
        }
        else
        {
            Client.DefaultRequestHeaders.Add("User-Agent", [
                $"tim/{AppInfoHelper.App.MajorMinorPatch}",
            ]);
        }

        return new FloqClient(Client);
    }
}