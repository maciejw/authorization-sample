using Microsoft.AspNetCore.Authorization;

namespace OperationAuthorization.AspNetCore;

public sealed class TaskHandler(RequestPermissions permissions) : AuthorizationHandler<TaskRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TaskRequirement requirement)
    {
        if (permissions.GetTasks(context.User).Contains(requirement.Task))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
