using Microsoft.AspNetCore.Authorization;
using OperationAuthorization;

using System.Text.Json;

namespace Api1.Opa;

public sealed record OpaRequirement(string DecisionPath, bool ResolveTasks = false) : IAuthorizationRequirement;

public sealed class OpaAuthorizationHandler(IHttpClientFactory clients, ILogger<OpaAuthorizationHandler> logger, RequestPermissions permissions)
    : AuthorizationHandler<OpaRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, OpaRequirement requirement)
    {
        // Caller facts come from the authenticated principal, never the request body.
        object input = requirement.ResolveTasks
            ? new { allowedTasks = permissions.GetTasks(context.User), resource = context.Resource }
            : new
            {
                roles = context.User.Identities.Where(identity => identity.IsAuthenticated)
                    .SelectMany(identity => identity.FindAll(identity.RoleClaimType))
                    .Select(claim => claim.Value).Distinct(StringComparer.Ordinal).ToArray(),
                resource = context.Resource
            };

        try
        {
            using var response = await clients.CreateClient("opa")
                .PostAsJsonAsync("v1/data/" + requirement.DecisionPath, new { input });
            response.EnsureSuccessStatusCode();
            var decision = await response.Content.ReadFromJsonAsync<OpaDecision>();
            if (decision?.Result is true)
                context.Succeed(requirement);
            else
                context.Fail(new AuthorizationFailureReason(this, "OPA did not allow the task."));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(exception, "OPA decision {DecisionPath} could not be evaluated", requirement.DecisionPath);
            context.Fail(new AuthorizationFailureReason(this, "OPA decision unavailable or invalid."));
        }
    }

    private sealed record OpaDecision(bool? Result);
}
