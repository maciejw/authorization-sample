using OperationAuthorization;

using System.Collections.Immutable;

namespace Api1;

// Security-officer-owned role composition, shown in memory for this sample.
public sealed class RoleDefinitions : IRoleDefinitionStore
{
    private static readonly Dictionary<string, ImmutableArray<string>> Definitions = new(StringComparer.Ordinal)
    {
        ["viewer"] = [TaskCatalog.Tasks.ViewTransfers],
        ["cashier"] = [TaskCatalog.Tasks.ViewTransfers, TaskCatalog.Tasks.MakeTransfer],
        ["supervisor"] = [TaskCatalog.Tasks.ViewTransfers, TaskCatalog.Tasks.MakeTransferHigherLimit]
    };

    public IEnumerable<string> GetRoleDefinition(string role) =>
        Definitions.TryGetValue(role, out var tasks) ? tasks : [];
}
