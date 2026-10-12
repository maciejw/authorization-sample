using Microsoft.AspNetCore.Authentication.JwtBearer;

using System.Security.Claims;

namespace OperationAuthorization.AspNetCore;

public sealed class ScopeNormalizationEvents : JwtBearerEvents
{
    public override Task TokenValidated(TokenValidatedContext context)
    {
        // Normalize validated JWT scopes for RequireClaim("scope", ...).
        foreach (var identity in context.Principal!.Identities)
        {
            foreach (var claim in identity.FindAll("scope").ToArray())
            {
                foreach (var scope in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!identity.HasClaim("scope", scope))
                        identity.AddClaim(new Claim("scope", scope, claim.ValueType,
                            claim.Issuer, claim.OriginalIssuer));
                }
            }
        }
        return Task.CompletedTask;
    }
}
