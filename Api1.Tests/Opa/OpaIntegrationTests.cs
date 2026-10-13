using Api1.Tests.Transfers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Moq;

using OperationAuthorization;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api1.Tests.Opa;

public sealed class OpaIntegrationTests(OpaApiFixture fixture) : IClassFixture<OpaApiFixture>
{
    [Theory]
    [InlineData("cashier", "transfers", 1000, HttpStatusCode.OK)]
    [InlineData("cashier", "transfers", 1001, HttpStatusCode.Forbidden)]
    [InlineData("supervisor", "openid transfers", 2000, HttpStatusCode.OK)]
    [InlineData("viewer", "transfers", 100, HttpStatusCode.Forbidden)]
    [InlineData("unknown", "transfers", 100, HttpStatusCode.Forbidden)]
    [InlineData("cashier", "", 100, HttpStatusCode.Forbidden)]
    [InlineData("cashier", "transfers", 0, HttpStatusCode.BadRequest)]
    public async Task Api_and_real_opa_enforce_transfer_authorization(
        string role, string scope, int amount, HttpStatusCode expected)
    {
        foreach (var path in new[] { "/transfers", "/transfers/opa", "/transfers/opa-centralized" })
        {
            using var response = await Send(ApiFactory.Token(role, scope), amount, path);
            Assert.Equal(expected, response.StatusCode);
            if (expected == HttpStatusCode.OK)
            {
                var transfer = await response.Content.ReadFromJsonAsync<Api1.Transfers.TransferRequest>(TestContext.Current.CancellationToken);
                Assert.Equal(amount, transfer!.Amount);
            }
        }
    }

    [Fact]
    public async Task Fractional_amount_above_standard_limit_is_denied()
    {
        using var response = await Send(ApiFactory.Token("cashier"), 1000.01m);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("audience")]
    [InlineData("issuer")]
    [InlineData("expired")]
    public async Task Invalid_credentials_are_rejected_before_the_opa_decision(string invalid)
    {
        var token = invalid switch
        {
            "missing" => null,
            "audience" => ApiFactory.Token(audience: "another-api"),
            "issuer" => ApiFactory.ServiceToken(),
            _ => ApiFactory.Token(expired: true)
        };
        using var response = await Send(token, 100);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Rego_policy_tests_pass_in_the_same_container()
    {
        var result = await fixture.Opa.ExecAsync(["/opa", "test", "/policies", "--verbose", "--fail-on-empty"],
            TestContext.Current.CancellationToken);
        Assert.True(result.ExitCode == 0, result.Stdout + result.Stderr);
    }

    [Fact]
    public async Task Centralized_endpoint_does_not_consult_local_role_or_task_resolution()
    {
        var roles = new Mock<IRoleDefinitionStore>(MockBehavior.Strict);
        var tasks = new Mock<ITaskCatalog>(MockBehavior.Strict);
        tasks.Setup(catalog => catalog.GetDefinitions()).Returns(new TaskCatalog().GetDefinitions());
        using var api = fixture.Api.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRoleDefinitionStore>();
            services.RemoveAll<ITaskCatalog>();
            services.AddSingleton(roles.Object);
            services.AddSingleton(tasks.Object);
        }));
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.Token("supervisor"));
        using var response = await client.PostAsJsonAsync("/transfers/opa-centralized", new { amount = 2000 }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        roles.VerifyNoOtherCalls();
        tasks.Verify(catalog => catalog.GetDefinitions(), Times.Once);
        tasks.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Changing_central_role_data_changes_the_decision_without_changing_the_api()
    {
        using var opa = new HttpClient { BaseAddress = new Uri($"http://{fixture.Opa.Hostname}:{fixture.Opa.GetMappedPublicPort(8181)}/") };
        opa.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            Api1.Opa.OpaServiceToken.Create(OpaApiFixture.SigningKey, "iam"));
        const string path = "v1/data/authorization/services/api1/roles/cashier";
        var original = await opa.GetFromJsonAsync<JsonElement>(path, TestContext.Current.CancellationToken);
        using var denied = await Send(ApiFactory.Token("cashier"), 2000, "/transfers/opa-centralized");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        try
        {
            using var update = await opa.PutAsJsonAsync(path, new[] { "api1:make-transfer-higher-limit" }, TestContext.Current.CancellationToken);
            update.EnsureSuccessStatusCode();
            using var allowed = await Send(ApiFactory.Token("cashier"), 2000, "/transfers/opa-centralized");
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }
        finally
        {
            using var restore = await opa.PutAsJsonAsync(path, original.GetProperty("result"), TestContext.Current.CancellationToken);
            restore.EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Request_body_cannot_replace_the_authenticated_roles_or_grant_tasks()
    {
        using var client = fixture.Api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.Token("viewer"));
        using var response = await client.PostAsJsonAsync("/transfers/opa-centralized", new
        {
            amount = 2000,
            roles = new[] { "supervisor" },
            allowedTasks = new[] { "api1:make-transfer-higher-limit" }
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<HttpResponseMessage> Send(string? token, decimal amount, string path = "/transfers/opa")
    {
        using var client = fixture.Api.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { amount })
        };
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
