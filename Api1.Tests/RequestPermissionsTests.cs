using Api1.Transfers;

using Moq;

using OperationAuthorization;

using System.Security.Claims;

namespace Api1.Tests;

public sealed class RequestPermissionsTests
{
    [Fact]
    public void Expands_roles_and_tasks_once_and_ignores_unknown_definitions()
    {
        var roles = new Mock<IRoleDefinitionStore>(MockBehavior.Strict);

        roles.Setup(store => store.GetTasks("supervisor")).Returns(["make-transfer-higher-limit", "unknown-task"]);
        roles.Setup(store => store.GetTasks("unknown")).Returns([]);

        var permissions = new RequestPermissions(roles.Object, new TransferTasks());

        IEnumerable<Claim> claims = [new Claim(ClaimTypes.Role, "supervisor"), new Claim(ClaimTypes.Role, "supervisor"), new Claim(ClaimTypes.Role, "unknown")];

        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        var operations = permissions.GetOperations(user);
        Assert.Equal(2, operations.Count);
        Assert.Contains(Operations.MakeTransfer, operations);
        Assert.Contains(Operations.MakeTransferHigherLimit, operations);
        Assert.Same(operations, permissions.GetOperations(user));

        roles.Verify(store => store.GetTasks("supervisor"), Times.Once);
        roles.Verify(store => store.GetTasks("unknown"), Times.Once);
        roles.VerifyNoOtherCalls();
    }

    [Fact]
    public void Unauthenticated_role_claims_grant_nothing()
    {
        var permissions = new RequestPermissions(new TransferRoles(), new TransferTasks());
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "supervisor")]));

        Assert.Empty(permissions.GetOperations(user));
    }
}
