using Microsoft.IdentityModel.Tokens;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Api1.Opa;

public static class OpaServiceToken
{
    public static string Create(string signingKey, string clientId = "api1",
        string issuer = "authorization-sample-services", string audience = "opa", bool expired = false)
    {
        var now = DateTime.UtcNow;
        DateTime expires = expired ? now.AddSeconds(-1) : now.AddMinutes(5);
        var token = new JwtSecurityToken(issuer, audience, [new Claim("client_id", clientId)], now.AddMinutes(-1), expires,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
