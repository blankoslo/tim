internal partial class Authentications
{
    /// <summary>Logger deg ut lokalt</summary>
    [Command("logout")]
    public async Task Logout(ConsoleAppContext ctx, CancellationToken token = default)
    {
        await UserSecretsManager.RemoveFolqSession(token);
        Console.WriteLine("kthxbye!");
    }
}