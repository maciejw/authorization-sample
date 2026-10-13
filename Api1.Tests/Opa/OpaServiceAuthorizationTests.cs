using Api1.Opa;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Api1.Tests.Transfers;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Api1.Tests.Opa;

public sealed class OpaServiceAuthorizationTests(OpaApiFixture fixture) : IClassFixture<OpaApiFixture>
{
    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    public async Task Api1_can_evaluate_only_its_allowed_decisions(string version)
    {
        using var client = Client(OpaServiceToken.Create(OpaApiFixture.SigningKey));
        object input = version == "v1"
            ? new { allowedTasks = new[] { TaskCatalog.Tasks.MakeTransfer }, resource = new { amount = 100 } }
            : new { roles = new[] { "cashier" }, resource = new { amount = 100 } };
        using var response = await client.PostAsJsonAsync(
            $"v1/data/api1/make_transfer_higher_limit_{version}/allow", new { input }, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var decision = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(decision.GetProperty("result").GetBoolean());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("signature")]
    [InlineData("expired")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("other-service")]
    [InlineData("iam")]
    [InlineData("algorithm")]
    [InlineData("missing-expiry")]
    [InlineData("not-yet-valid")]
    public async Task Invalid_or_unauthorized_service_tokens_cannot_evaluate(string invalid)
    {
        var token = invalid switch
        {
            "missing" => null,
            "malformed" => "not-a-jwt",
            "signature" => OpaServiceToken.Create("different-test-only-signing-key-at-least-32-bytes"),
            "expired" => OpaServiceToken.Create(OpaApiFixture.SigningKey, expired: true),
            "issuer" => OpaServiceToken.Create(OpaApiFixture.SigningKey, issuer: "other-issuer"),
            "audience" => OpaServiceToken.Create(OpaApiFixture.SigningKey, audience: "api1"),
            "other-service" => OpaServiceToken.Create(OpaApiFixture.SigningKey, "api2"),
            "iam" => OpaServiceToken.Create(OpaApiFixture.SigningKey, "iam"),
            _ => CustomToken(invalid)
        };
        using var client = Client(token);
        using var response = await client.PostAsJsonAsync("v1/data/api1/make_transfer_higher_limit_v2/allow",
            new { input = new { roles = new[] { "supervisor" }, resource = new { amount = 2000 } } },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "v1/data/api2/make_transfer_higher_limit_v2/allow")]
    [InlineData("POST", "v1/data/api1/unlisted/allow")]
    [InlineData("GET", "v1/data/api1/make_transfer_higher_limit_v2/allow")]
    [InlineData("GET", "v1/data/authorization")]
    [InlineData("PUT", "v1/data/authorization/services/api1/roles/cashier")]
    [InlineData("PUT", "v1/policies/arbitrary")]
    public async Task Api1_cannot_query_other_paths_or_modify_policy_data(string method, string path)
    {
        using var client = Client(OpaServiceToken.Create(OpaApiFixture.SigningKey));
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = new StringContent("{}")
        };
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Api_denies_transfer_when_opa_rejects_the_service_token()
    {
        using var app = fixture.Api.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HttpClients:Opa:SigningKey"] = "different-test-only-signing-key-at-least-32-bytes"
            })));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.Token("cashier"));

        using var response = await client.PostAsJsonAsync("/transfers/opa-centralized", new { amount = 100 },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient Client(string? token)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri($"http://{fixture.Opa.Hostname}:{fixture.Opa.GetMappedPublicPort(8181)}/")
        };
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string CustomToken(string invalid)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken("authorization-sample-services", "opa",
            [new Claim("client_id", "api1")],
            invalid == "not-yet-valid" ? now.AddMinutes(2) : now.AddMinutes(-1),
            invalid == "missing-expiry" ? null : now.AddMinutes(5),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(OpaApiFixture.SigningKey)),
                invalid == "algorithm" ? SecurityAlgorithms.HmacSha384 : SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
