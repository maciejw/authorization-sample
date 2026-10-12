using OperationAuthorization;

using System.Collections.Immutable;

namespace Api1.Transfers;

public static class Operations
{
    public const string ViewTransfers = "view-transfers";
    public const string MakeTransfer = "make-transfer";
    public const string MakeTransferHigherLimit = "make-transfer-higher-limit";
}

// Application-owned task definitions. A higher-limit task grants both operations.
public sealed class TransferTasks : ITaskCatalog
{
    // One definition map supplies both IAM export names and permission resolution.
    private static readonly Dictionary<string, ImmutableArray<string>> Definitions = new(StringComparer.Ordinal)
    {
        ["view-transfers"] = [Operations.ViewTransfers],
        ["make-transfer"] = [Operations.MakeTransfer],
        ["make-transfer-higher-limit"] = [Operations.MakeTransfer, Operations.MakeTransferHigherLimit]
    };

    public IEnumerable<string> GetTaskNames() => Definitions.Keys;

    public IEnumerable<string> GetOperations(string task) =>
        Definitions.TryGetValue(task, out var operations) ? operations : [];
}

// Security-officer-owned role composition, shown in memory for this sample.
public sealed class TransferRoles : IRoleDefinitionStore
{
    private static readonly Dictionary<string, ImmutableArray<string>> Definitions = new(StringComparer.Ordinal)
    {
        ["viewer"] = ["view-transfers"],
        ["cashier"] = ["view-transfers", "make-transfer"],
        ["supervisor"] = ["view-transfers", "make-transfer-higher-limit"]
    };

    public IEnumerable<string> GetTasks(string role) =>
        Definitions.TryGetValue(role, out var tasks) ? tasks : [];
}
