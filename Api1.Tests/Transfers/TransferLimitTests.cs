using Api1.Transfers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Moq;

using OperationAuthorization;

using System.Security.Claims;

namespace Api1.Tests.Transfers;

public sealed class TransferLimitTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData(null)]
    [InlineData("not-a-transfer")]
    public async Task Policy_rejects_missing_or_wrong_resource_even_for_a_supervisor(object? resource)
    {
        using var scope = factory.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("role", "supervisor")], "test", "sub", "role"));

        var result = await authorization.AuthorizeAsync(user, resource, TaskCatalog.Tasks.MakeTransferHigherLimit);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Policy_requires_authentication_even_for_a_standard_amount()
    {
        using var scope = factory.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var result = await authorization.AuthorizeAsync(new ClaimsPrincipal(), new TransferRequest(100),
            TaskCatalog.Tasks.MakeTransferHigherLimit);
        Assert.False(result.Succeeded);
    }
    [Theory]
    [InlineData(50, false, true)]
    [InlineData(51, false, false)]
    [InlineData(51, true, true)]
    public async Task Limit_uses_its_configured_threshold_and_override_task(
        int amount, bool grantedOverride, bool expected)
    {
        var roles = new Mock<IRoleDefinitionStore>();
        roles.Setup(store => store.GetRoleDefinition("custom-role"))
            .Returns(grantedOverride ? ["custom-override"] : []);
        var tasks = new Mock<ITaskCatalog>();
        tasks.Setup(catalog => catalog.GetGrantedTasks("custom-override")).Returns(["custom-override"]);
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "custom-role")], "test"));
        var requirement = new TransferLimitRequirement(50, "custom-override");
        var context = new AuthorizationHandlerContext([requirement], user, new TransferRequest(amount));

        await new TransferLimitHandler(new RequestPermissions(roles.Object, tasks.Object)).HandleAsync(context);

        Assert.Equal(expected, context.HasSucceeded);
        Assert.Equal(!expected, context.HasFailed);
        if (!expected)
            Assert.Equal("Amount exceeded StandardLimit", Assert.Single(context.FailureReasons).Message);
    }

    [Fact]
    public async Task Higher_limit_grant_also_satisfies_the_base_task_requirement()
    {
        var roles = new Mock<IRoleDefinitionStore>();
        roles.Setup(store => store.GetRoleDefinition("higher-only")).Returns([TaskCatalog.Tasks.MakeTransferHigherLimit]);
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRoleDefinitionStore>();
            services.AddSingleton(roles.Object);
        }));
        using var scope = app.Services.CreateScope();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", "higher-only")], "test", "sub", "role"));
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(user, new TransferRequest(2000), TaskCatalog.Tasks.MakeTransferHigherLimit);

        Assert.True(result.Succeeded);
    }

}
