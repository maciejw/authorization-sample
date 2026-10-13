using System.Collections.Immutable;
using System.Security.Claims;

namespace OperationAuthorization;

// Register per request: expansion is lazy and shared by all task checks.
public sealed class RequestPermissions(IRoleDefinitionStore roles, ITaskCatalog tasks)
{
    private ClaimsPrincipal? principal;
    private ImmutableHashSet<string>? grantedTasks;

    public IReadOnlySet<string> GetTasks(ClaimsPrincipal user)
    {
        if (ReferenceEquals(principal, user) && grantedTasks is not null)
            return grantedTasks;

        grantedTasks = user.Identities.Where(identity => identity.IsAuthenticated)
            .SelectMany(identity => identity.FindAll(identity.RoleClaimType))
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .SelectMany(roles.GetRoleDefinition)
            .Distinct(StringComparer.Ordinal)
            .SelectMany(tasks.GetGrantedTasks)
            .ToImmutableHashSet(StringComparer.Ordinal);

        principal = user;

        return grantedTasks;
    }
}
