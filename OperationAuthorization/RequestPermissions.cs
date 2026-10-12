using System.Collections.Immutable;
using System.Security.Claims;

namespace OperationAuthorization;

public interface IRoleDefinitionStore
{
    IEnumerable<string> GetTasks(string role);
}

public interface ITaskCatalog
{
    IEnumerable<string> GetTaskNames();
    IEnumerable<string> GetOperations(string task);
}

// Register per request: expansion is lazy and shared by all operation checks.
public sealed class RequestPermissions(IRoleDefinitionStore roles, ITaskCatalog tasks)
{
    private ClaimsPrincipal? principal;
    private ImmutableHashSet<string>? operations;

    public IReadOnlySet<string> GetOperations(ClaimsPrincipal user)
    {
        if (ReferenceEquals(principal, user) && operations is not null)
            return operations;

        operations = user.Identities.Where(identity => identity.IsAuthenticated)
            .SelectMany(identity => identity.FindAll(identity.RoleClaimType))
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .SelectMany(roles.GetTasks)
            .SelectMany(tasks.GetOperations)
            .ToImmutableHashSet(StringComparer.Ordinal);

        principal = user;

        return operations;
    }
}
