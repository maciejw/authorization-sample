using Api1.Transfers;

using Microsoft.AspNetCore.Authorization;

using OperationAuthorization;

using System.Collections.Immutable;

namespace Api1;

// Application-owned task policies, constructed once.
public sealed class TaskCatalog : ITaskCatalog
{
    public static class Tasks
    {
        public const string ViewTransfers = "api1:view-transfers";
        public const string MakeTransfer = "api1:make-transfer";
        public const string MakeTransferHigherLimit = "api1:make-transfer-higher-limit";
    }

    // One definition map supplies IAM export names and policy requirements.
    private readonly ImmutableDictionary<string, TaskDefinition> definitions = new TaskCatalogBuilder()
        .Create(Tasks.ViewTransfers)
        .Create(Tasks.MakeTransfer, [
            new(Tasks.MakeTransferHigherLimit, new TransferLimitRequirement(1_000, Tasks.MakeTransferHigherLimit))
        ])
        .Build();

    public ImmutableDictionary<string, TaskDefinition> GetDefinitions() => definitions;

    public IEnumerable<string> GetTaskNames() => definitions.Keys;

    public IEnumerable<string> GetGrantedTasks(string task) =>
        definitions.TryGetValue(task, out var definition) ? definition.GrantedTasks : [];

    public IEnumerable<IAuthorizationRequirement> GetAuthorizationRequirements(string task) =>
        definitions.TryGetValue(task, out var definition) ? definition.Requirements : [];

}
