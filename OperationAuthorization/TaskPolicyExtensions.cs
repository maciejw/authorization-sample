using Microsoft.AspNetCore.Authorization;

namespace OperationAuthorization;

public static class TaskPolicyExtensions
{
    public static AuthorizationOptions AddTaskPolicies(this AuthorizationOptions builder, IReadOnlyDictionary<string, TaskDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            builder.AddPolicy(definition.Key, policy => policy.AddRequirements([.. definition.Value.Requirements]));
        }

        return builder;
    }
}
