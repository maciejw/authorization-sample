using Api1.Transfers;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

using OperationAuthorization.AspNetCore;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;

namespace Api1.Tests.Transfers;

public sealed class TransferTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient client;

    public TransferTests(ApiFactory factory) => client = factory.CreateClient();

    [Theory]
    [InlineData(null, "transfers", 100, HttpStatusCode.Forbidden)]
    [InlineData("unknown", "transfers", 100, HttpStatusCode.Forbidden)]
    [InlineData("viewer", "transfers", 100, HttpStatusCode.Forbidden)]
    [InlineData("cashier", "", 100, HttpStatusCode.Forbidden)]
    [InlineData("supervisor", "transfers-other", 2000, HttpStatusCode.Forbidden)]
    [InlineData("cashier", "transfers", 1000, HttpStatusCode.OK)]
    [InlineData("cashier", "transfers", 1001, HttpStatusCode.Forbidden)]
    [InlineData("supervisor", "transfers", 100, HttpStatusCode.OK)]
    [InlineData("supervisor", "openid transfers other", 2000, HttpStatusCode.OK)]
    [InlineData("cashier", "transfers", 0, HttpStatusCode.BadRequest)]
    public async Task Scope_and_task_requirements_control_transfers(
        string? role, string scope, int amount, HttpStatusCode expected)
    {
        foreach (var path in new[] { "/transfers" })
        {
            using var response = await Send(ApiFactory.Token(role, scope), amount, path);
            Assert.Equal(expected, response.StatusCode);
            if (expected == HttpStatusCode.OK)
                Assert.Equal(amount, (await response.Content.ReadFromJsonAsync<TransferRequest>(TestContext.Current.CancellationToken))!.Amount);
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("audience")]
    [InlineData("issuer")]
    [InlineData("signature")]
    [InlineData("expired")]
    public async Task Invalid_credentials_are_unauthorized(string invalid)
    {
        var token = invalid switch
        {
            "missing" => null,
            "malformed" => "not-a-jwt",
            "audience" => ApiFactory.Token(audience: "another-api"),
            "issuer" => ApiFactory.Token(issuer: "another-issuer"),
            "signature" => ApiFactory.Token(key: new SymmetricSecurityKey(new byte[32])),
            "expired" => ApiFactory.Token(expired: true),
            _ => throw new InvalidOperationException()
        };
        foreach (var path in new[] { "/transfers" })
        {
            using var response = await Send(token, 100, path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Contains(response.Headers.WwwAuthenticate, value => value.Scheme == "Bearer");
        }
    }

    [Fact]
    public async Task Permissions_do_not_leak_between_requests()
    {
        using var first = await Send(ApiFactory.Token("supervisor"), 2000);
        using var second = await Send(ApiFactory.Token("cashier"), 2000);
        using var third = await Send(ApiFactory.Token("supervisor"), 2000);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
    }

    private async Task<HttpResponseMessage> Send(string? token, int amount, string path = "/transfers")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new TransferRequest(amount))
        };
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private static readonly SymmetricSecurityKey SigningKey = new(
        "sample-tests-only-signing-key-32-bytes-minimum"u8.ToArray());

    private static readonly SymmetricSecurityKey ServiceSigningKey = new(
        "service-tests-only-signing-key-32-bytes-minimum"u8.ToArray());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            ConfigureScheme(services, AuthenticationSchemes.Users, "sample-tests", SigningKey);
            ConfigureScheme(services, AuthenticationSchemes.Services, "sample-service-tests", ServiceSigningKey);
            ConfigureScheme(services, OperationAuthorizationInfrastructureDefaults.AuthenticationScheme,
                "sample-service-tests", ServiceSigningKey, OperationAuthorizationInfrastructureDefaults.Audience);
        });
    }

    private static void ConfigureScheme(IServiceCollection services, string scheme, string issuer, SecurityKey key, string audience = "api1")
    {
        services.PostConfigure<JwtBearerOptions>(scheme, options =>
        {
            // Keep real JWT validation with separate local issuers and signing keys.
            options.Authority = null;
            options.ConfigurationManager = null;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                RoleClaimType = "role"
            };
        });
    }

    public static string InfrastructureToken(string scope = "authorization.tasks.read",
        string audience = "authorization-catalog", string issuer = "sample-service-tests",
        SecurityKey? key = null, bool expired = false) =>
        Token(role: null, scope: scope, audience: audience, issuer: issuer, key: key ?? ServiceSigningKey,
            expired: expired, subject: null, clientId: "iam-exporter");

    public static string ServiceToken(string scope = "transfers.reconcile", string audience = "api1",
        string? role = null, string issuer = "sample-service-tests", SecurityKey? key = null, bool expired = false) =>
        Token(role: role, scope: scope, audience: audience, issuer: issuer, key: key ?? ServiceSigningKey,
            expired: expired, subject: null, clientId: "reconciliation-worker");

    public static string Token(string? role = "cashier", string scope = "transfers",
        string audience = "api1", string issuer = "sample-tests",
        SecurityKey? key = null, bool expired = false, string? subject = "alice", string? clientId = null)
    {
        List<Claim> claims = [new("scope", scope)];
        if (subject is not null)
            claims.Add(new("sub", subject));
        if (clientId is not null)
            claims.Add(new("client_id", clientId));

        if (role is not null)
        {
            claims.Add(new("role", role));
        }
        var now = DateTime.UtcNow;

        DateTime expires = expired ? now.AddMinutes(-1) : now.AddMinutes(10);
        SigningCredentials signingCredentials = new(key ?? SigningKey, SecurityAlgorithms.HmacSha256);

        JwtSecurityToken token = new(issuer, audience, claims, now.AddMinutes(-10), expires, signingCredentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
