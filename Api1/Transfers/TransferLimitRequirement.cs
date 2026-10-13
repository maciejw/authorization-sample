using Microsoft.AspNetCore.Authorization;

using OperationAuthorization;

namespace Api1.Transfers;

public sealed record TransferLimitRequirement(decimal StandardLimit, string OverridePermission) : IAuthorizationRequirement;

public sealed class TransferLimitHandler(RequestPermissions permissions) : AuthorizationHandler<TransferLimitRequirement, TransferRequest>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TransferLimitRequirement requirement, TransferRequest transfer)
    {
        if (transfer.Amount > requirement.StandardLimit)
        {
            if (permissions.GetTasks(context.User).Contains(requirement.OverridePermission))
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail(new AuthorizationFailureReason(this, "Amount exceeded StandardLimit"));
            }
        }
        else
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
