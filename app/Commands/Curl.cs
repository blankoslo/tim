
[RegisterCommands]
internal class CurlCommand
{
    /// <summary>
    /// curl '/employees?id=eq.1'
    /// </summary>
    /// <param name="uri">subpath: f.eks. `/employees`</param>
    /// <param name="data">-d, json body: f.eks. `{}`</param>
    /// <param name="x">-X, method: f.eks. `GET|POST|PUT`</param>
    /// <param name="h">-H, Headere f.eks. 'Accept: application/json'</param>
    [ConsoleAppFilter<AuthenticationFilter>]
    [Command("curl")]
    public async Task<int> Curl(
        ConsoleAppContext ctx,
        [Argument] string uri,
        string x = "GET",
        string? data = null,
        string[]? h = null,
        CancellationToken token = default)
    {

        var session = ctx.GetUserSession();
        HttpClientFactory.CreateFolqClientForUser(session);

        HttpMethod httpMethod = HttpMethod.Parse(x);
        var msg = new HttpRequestMessage(httpMethod, uri);
        if (data is not null &&
            (httpMethod == HttpMethod.Post ||
             httpMethod == HttpMethod.Put ||
             httpMethod == HttpMethod.Patch))
        {
            msg.Content = new StringContent(data, System.Text.Encoding.UTF8, "application/json");
        }

        if (h is not null)
        {
            foreach (var header in h)
            {
                var split = header.Split(":", 2);
                if (split.Length == 2)
                {
                    var headerName = split[0].Trim();
                    var headerValue = split[1].Trim();
                    msg.Headers.Add(headerName, headerValue);
                }
            }
        }

        var response = await HttpClientFactory.Client.SendAsync(msg, token);
        if (response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(token);
            Console.WriteLine($"{responseBody}");
            return 0;
        }
        else
        {
            var responseBody = await response.Content.ReadAsStringAsync(token);
            Console.WriteLine($"{(int)response.StatusCode} {response.ReasonPhrase}\n{responseBody}");
            return (int) response.StatusCode;
        }
    }
}