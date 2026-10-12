using Api1.Transfers;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Api1.Tests;

public sealed class TransferAccessTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("/transfers", "viewer", "transfers", HttpStatusCode.OK)]
    [InlineData("/transfers/1", "viewer", "transfers", HttpStatusCode.OK)]
    [InlineData("/transfers", "cashier", "transfers", HttpStatusCode.OK)]
    [InlineData("/transfers/1", "supervisor", "transfers", HttpStatusCode.OK)]
    [InlineData("/transfers", "viewer", "", HttpStatusCode.Forbidden)]
    [InlineData("/transfers/1", "supervisor", "transfers.reconcile", HttpStatusCode.Forbidden)]
    [InlineData("/transfers", "unknown", "transfers", HttpStatusCode.Forbidden)]
    [InlineData("/transfers/1", null, "transfers", HttpStatusCode.Forbidden)]
    [InlineData("/transfers", "viewer", "openid transfers", HttpStatusCode.OK)]
    [InlineData("/transfers/1", "viewer", "openid transfers", HttpStatusCode.OK)]
    public async Task Read_endpoints_require_both_group_scope_and_operation(
        string path, string? role, string scope, HttpStatusCode expected)
    {
        using var response = await Send(HttpMethod.Get, path, ApiFactory.Token(role, scope));
        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/transfers")]
    [InlineData("GET", "/transfers/1")]
    [InlineData("POST", "/transfers/reconcile")]
    public async Task New_endpoints_require_authentication(string method, string path)
    {
        using var response = await Send(new HttpMethod(method), path, null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_and_detail_return_the_example_transfer_and_unknown_ids_return_not_found()
    {
        var token = ApiFactory.Token("viewer");
        using var list = await Send(HttpMethod.Get, "/transfers", token);
        using var detail = await Send(HttpMethod.Get, "/transfers/1", token);
        using var missing = await Send(HttpMethod.Get, "/transfers/2", token);
        list.EnsureSuccessStatusCode();
        detail.EnsureSuccessStatusCode();
        var items = await list.Content.ReadFromJsonAsync<TransferSummary[]>(TestContext.Current.CancellationToken);
        var item = await detail.Content.ReadFromJsonAsync<TransferSummary>(TestContext.Current.CancellationToken);
        Assert.Equal(new TransferSummary(1, 100, "pending"), Assert.Single(items!));
        Assert.Equal(items![0], item);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Theory]
    [InlineData("transfers.reconcile", HttpStatusCode.OK)]
    [InlineData("openid transfers.reconcile", HttpStatusCode.OK)]
    [InlineData("", HttpStatusCode.Forbidden)]
    [InlineData("transfers", HttpStatusCode.Forbidden)]
    [InlineData("transfers.reconcile-other", HttpStatusCode.Forbidden)]
    public async Task Service_scope_works_without_user_subject_or_roles(string scope, HttpStatusCode expected)
    {
        var token = ApiFactory.ServiceToken(scope: scope);
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims;
        Assert.DoesNotContain(claims, claim => claim.Type is "sub" or "role");
        using var response = await Send(HttpMethod.Post, "/transfers/reconcile", token);
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task User_roles_do_not_grant_the_service_scope()
    {
        using var response = await Send(HttpMethod.Post, "/transfers/reconcile", ApiFactory.ServiceToken(scope: "transfers", role: "supervisor"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Service_token_still_requires_the_api_audience()
    {
        var token = ApiFactory.ServiceToken(audience: "another-api");
        using var response = await Send(HttpMethod.Post, "/transfers/reconcile", token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/transfers")]
    [InlineData("GET", "/transfers/1")]
    [InlineData("POST", "/transfers")]
    public async Task Service_issuer_is_not_accepted_by_user_endpoints(string method, string path)
    {
        var token = ApiFactory.ServiceToken(scope: "transfers transfers.reconcile", role: "supervisor");
        using var response = await Send(new HttpMethod(method), path, token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task User_token_with_service_scope_is_not_accepted_by_service_scheme()
    {
        using var response = await Send(HttpMethod.Post, "/transfers/reconcile",
            ApiFactory.Token("supervisor", "transfers.reconcile"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Service_scheme_rejects_wrong_issuer_even_with_its_own_signing_key()
    {
        using var response = await Send(HttpMethod.Post, "/transfers/reconcile",
            ApiFactory.ServiceToken(issuer: "sample-tests"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task User_scheme_rejects_service_issuer_even_with_its_own_signing_key()
    {
        using var response = await Send(HttpMethod.Get, "/transfers",
            ApiFactory.Token("supervisor", issuer: "sample-service-tests"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("expired")]
    public async Task Service_scheme_validates_signature_and_lifetime(string invalid)
    {
        var token = invalid == "expired"
            ? ApiFactory.ServiceToken(expired: true)
            : ApiFactory.ServiceToken(key: new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(new byte[32]));
        using var response = await Send(HttpMethod.Post, "/transfers/reconcile", token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, string? token)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(method, path);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
