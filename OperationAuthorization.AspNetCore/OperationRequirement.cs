using Microsoft.AspNetCore.Authorization;

namespace OperationAuthorization.AspNetCore;

public sealed record OperationRequirement(string Operation) : IAuthorizationRequirement;

public sealed class OperationHandler(RequestPermissions permissions) : AuthorizationHandler<OperationRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, OperationRequirement requirement)
    {
        if (permissions.GetOperations(context.User).Contains(requirement.Operation))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
