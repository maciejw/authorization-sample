using Moq;

using OperationAuthorization;

using System.Security.Claims;

namespace Api1.Tests;

public sealed class RequestPermissionsTests
{
    [Fact]
    public void Expands_flat_task_grants_once_and_ignores_unknown_tasks()
    {
        var roles = new Mock<IRoleDefinitionStore>(MockBehavior.Strict);

        roles.Setup(store => store.GetRoleDefinition("supervisor")).Returns(["api1:make-transfer-higher-limit", "unknown-task"]);
        roles.Setup(store => store.GetRoleDefinition("unknown")).Returns([]);

        var permissions = new RequestPermissions(roles.Object, new TaskCatalog());

        IEnumerable<Claim> claims = [new Claim(ClaimTypes.Role, "supervisor"), new Claim(ClaimTypes.Role, "supervisor"), new Claim(ClaimTypes.Role, "unknown")];

        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        var tasks = permissions.GetTasks(user);
        Assert.Equal(2, tasks.Count);
        Assert.DoesNotContain("unknown-task", tasks);
        Assert.Contains(TaskCatalog.Tasks.MakeTransfer, tasks);
        Assert.Contains(TaskCatalog.Tasks.MakeTransferHigherLimit, tasks);
        Assert.Same(tasks, permissions.GetTasks(user));

        roles.Verify(store => store.GetRoleDefinition("supervisor"), Times.Once);
        roles.Verify(store => store.GetRoleDefinition("unknown"), Times.Once);
        roles.VerifyNoOtherCalls();
    }

    [Fact]
    public void Unauthenticated_role_claims_grant_nothing()
    {
        var permissions = new RequestPermissions(new RoleDefinitions(), new TaskCatalog());
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "supervisor")]));

        Assert.Empty(permissions.GetTasks(user));
    }
}
