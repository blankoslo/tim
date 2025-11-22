using System.IdentityModel.Tokens.Jwt;

public record IdToken(string Name, string Email);

public static class JwtHelper
{
    public static IdToken Decode(string idToken)
    {
        var jwt = new JwtSecurityToken(idToken);
        var name = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? "";
        var email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value ?? "";
        return new IdToken(name, email);
    }
}
