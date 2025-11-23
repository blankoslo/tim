using System.Net.Http.Headers;


public class HttpClientFactory
{
    public static readonly HttpClient Client = new()
                                               {
                                                   BaseAddress = new Uri("https://api-prod.floq.no"),
                                               };

    public static FloqClient CreateFolqClientForUser(UserSession session)
    {
        return CreateFolqClientForUser(session.AccessToken, session.EmployeeId);
    }

    public static FloqClient CreateFolqClientForUser(string accessToken, int? employeeId = null)
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (employeeId is {} empId )
        {
            Client.DefaultRequestHeaders.Add("User-Agent", [
                $"tim/{AppInfoHelper.App.MajorMinorPatch}",
                $"folq-employee/{empId}"
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