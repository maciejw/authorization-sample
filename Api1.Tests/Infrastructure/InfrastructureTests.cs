using OperationAuthorization.AspNetCore;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Api1.Tests;

public sealed class InfrastructureTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("authorization.tasks.read")]
    [InlineData("other authorization.tasks.read")]
    public async Task Iam_service_can_export_all_task_names_without_user_roles(string scope)
    {
        using var response = await Send(ApiFactory.InfrastructureToken(scope));
        response.EnsureSuccessStatusCode();
        var catalog = await response.Content.ReadFromJsonAsync<TaskCatalogResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(catalog);
        Assert.Equal("api1", catalog.Service);
        Assert.Equal(1, catalog.Version);
        Assert.Equal(["make-transfer", "make-transfer-higher-limit", "view-transfers"],
            catalog.Tasks.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("transfers.reconcile")]
    [InlineData("authorization.tasks.read-other")]
    [InlineData("iam.tasks.read")]
    public async Task Other_service_scopes_do_not_grant_task_export(string scope)
    {
        using var response = await Send(ApiFactory.InfrastructureToken(scope));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task User_issuer_is_rejected_even_with_iam_scope_and_supervisor_role()
    {
        using var response = await Send(ApiFactory.Token("supervisor", "authorization.tasks.read", audience: "authorization-catalog"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Missing_token_is_unauthorized()
    {
        using var response = await Send(null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Iam_token_does_not_grant_reconciliation_access()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/transfers/reconcile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.InfrastructureToken());
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("api1")]
    [InlineData("another-catalog")]
    public async Task Infrastructure_requires_its_own_audience_even_with_correct_issuer_and_scope(string audience)
    {
        using var response = await Send(ApiFactory.InfrastructureToken(audience: audience));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("signature")]
    [InlineData("expired")]
    public async Task Infrastructure_validates_issuer_signature_and_lifetime(string invalid)
    {
        var token = invalid switch
        {
            "issuer" => ApiFactory.InfrastructureToken(issuer: "another-issuer"),
            "signature" => ApiFactory.InfrastructureToken(key: new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(new byte[32])),
            _ => ApiFactory.InfrastructureToken(expired: true)
        };
        using var response = await Send(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpResponseMessage> Send(string? token)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/infrastructure/tasks");
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
