using Api1.Tests.Transfers;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Moq;
using Moq.Protected;

using OperationAuthorization;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api1.Tests.Opa;

public sealed class OpaTransferTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData(200, "{\"result\":true}", HttpStatusCode.OK)]
    [InlineData(200, "{\"result\":false}", HttpStatusCode.Forbidden)]
    [InlineData(200, "{}", HttpStatusCode.Forbidden)]
    [InlineData(200, "not-json", HttpStatusCode.Forbidden)]
    [InlineData(200, "{\"result\":\"allow\"}", HttpStatusCode.Forbidden)]
    [InlineData(500, "{}", HttpStatusCode.Forbidden)]
    public async Task Only_an_explicit_true_decision_grants_access(int status, string body, HttpStatusCode expected)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        string? sentBody = null;
        Uri? decisionUri = null;
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken cancellation) =>
            {
                decisionUri = request.RequestUri;
                sentBody = await request.Content!.ReadAsStringAsync(cancellation);
                return new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body) };
            });
        using var app = WithOpa(handler.Object);
        using var response = await Send(app, ApiFactory.Token("cashier"), 1500);
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("/v1/data/api1/make_transfer_higher_limit_v1/allow", decisionUri!.AbsolutePath);
        using var document = JsonDocument.Parse(sentBody!);
        var input = document.RootElement.GetProperty("input");
        Assert.False(input.TryGetProperty("service", out _));
        Assert.False(input.TryGetProperty("task", out _));
        Assert.False(input.TryGetProperty("subject", out _));
        Assert.Equal(1500, input.GetProperty("resource").GetProperty("amount").GetDecimal());
        Assert.Contains("api1:make-transfer", input.GetProperty("allowedTasks").EnumerateArray().Select(value => value.GetString()));
        Assert.DoesNotContain("api1:make-transfer-higher-limit", input.GetProperty("allowedTasks").EnumerateArray().Select(value => value.GetString()));
        handler.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unavailable_or_timed_out_opa_does_not_grant_access(bool timeout)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(timeout ? new TaskCanceledException("OPA timeout") : new HttpRequestException("OPA unavailable"));
        using var app = WithOpa(handler.Object);
        using var response = await Send(app, ApiFactory.Token("supervisor"), 1500);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(null, "transfers", 100, HttpStatusCode.Unauthorized)]
    [InlineData("viewer", "transfers", 100, HttpStatusCode.Forbidden)]
    [InlineData("cashier", "", 100, HttpStatusCode.Forbidden)]
    [InlineData("cashier", "transfers", 0, HttpStatusCode.BadRequest)]
    public async Task Authentication_scope_task_and_input_checks_precede_opa(
        string? role, string scope, int amount, HttpStatusCode expected)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        using var app = WithOpa(handler.Object);
        using var response = await Send(app, role is null ? null : ApiFactory.Token(role, scope), amount);
        Assert.Equal(expected, response.StatusCode);
        handler.Protected().Verify("SendAsync", Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task Centralized_input_contains_validated_roles_without_resolved_tasks()
    {
        var roles = new Mock<IRoleDefinitionStore>(MockBehavior.Strict);
        var tasks = new Mock<ITaskCatalog>(MockBehavior.Strict);
        tasks.Setup(catalog => catalog.GetDefinitions()).Returns(new TaskCatalog().GetDefinitions());
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        string? sentBody = null;
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken cancellation) =>
            {
                Assert.Equal("/v1/data/api1/make_transfer_higher_limit_v2/allow", request.RequestUri!.AbsolutePath);
                sentBody = await request.Content!.ReadAsStringAsync(cancellation);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"result\":true}") };
            });
        using var app = WithOpa(handler.Object).WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRoleDefinitionStore>();
            services.RemoveAll<ITaskCatalog>();
            services.AddSingleton(roles.Object);
            services.AddSingleton(tasks.Object);
        }));
        using var response = await Send(app, ApiFactory.Token("viewer"), 1500, "/transfers/opa-centralized");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(sentBody!);
        var input = document.RootElement.GetProperty("input");
        Assert.False(input.TryGetProperty("service", out _));
        Assert.False(input.TryGetProperty("subject", out _));
        Assert.False(input.TryGetProperty("task", out _));
        Assert.Equal("viewer", Assert.Single(input.GetProperty("roles").EnumerateArray()).GetString());
        Assert.False(input.TryGetProperty("allowedTasks", out _));
        roles.VerifyNoOtherCalls();
        tasks.Verify(catalog => catalog.GetDefinitions(), Times.Once);
        tasks.VerifyNoOtherCalls();
    }

    private WebApplicationFactory<Program> WithOpa(HttpMessageHandler handler) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddHttpClient("opa").ConfigurePrimaryHttpMessageHandler(() => handler)));

    private static async Task<HttpResponseMessage> Send(WebApplicationFactory<Program> app, string? token, int amount, string path = "/transfers/opa")
    {
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { amount })
        };
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
