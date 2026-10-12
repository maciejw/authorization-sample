using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using System.Security.Claims;

namespace Api1.Tests;

public sealed class ScopeNormalizationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Splits_scopes_without_duplicating_existing_claims_on_repeated_calls()
    {
        var identity = new ClaimsIdentity(
            [new Claim("scope", "openid  transfers transfers other", ClaimValueTypes.String, "issuer"),
             new Claim("scope", "transfers", ClaimValueTypes.String, "issuer")], "test");
        var context = CreateContext(identity);

        await Normalize(context);
        await Normalize(context);

        Assert.Single(identity.FindAll("scope"), claim => claim.Value == "transfers");
        Assert.Single(identity.FindAll("scope"), claim => claim.Value == "openid");
        Assert.Single(identity.FindAll("scope"), claim => claim.Value == "other");
        Assert.All(identity.FindAll("scope"), claim => Assert.Equal("issuer", claim.Issuer));
    }

    [Fact]
    public async Task Does_not_add_scopes_when_none_are_present()
    {
        var identity = new ClaimsIdentity([new Claim("sub", "alice")], "test");
        var context = CreateContext(identity);
        await Normalize(context);
        Assert.Empty(identity.FindAll("scope"));
    }

    private async Task Normalize(TokenValidatedContext context)
    {
        using var scope = factory.Services.CreateScope();
        var events = (JwtBearerEvents)scope.ServiceProvider.GetRequiredService(context.Options.EventsType!);
        await events.TokenValidated(context);
    }

    private TokenValidatedContext CreateContext(ClaimsIdentity identity)
    {
        var options = factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        return new TokenValidatedContext(new DefaultHttpContext(),
            new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler)),
            options)
        {
            Principal = new ClaimsPrincipal(identity)
        };
    }
}
