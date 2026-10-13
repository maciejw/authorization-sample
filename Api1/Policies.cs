using Api1.Transfers;

using Microsoft.AspNetCore.Authorization;

using OperationAuthorization;

namespace Api1;

public static class Policies
{
    public static void AddTransferPolicies(AuthorizationOptions builder, ITaskCatalog tasks)
    {
        builder.AddTransfersApiPolicies()
            .AddTransferReconciliationApiPolicies()
            .AddTaskPolicies(tasks.GetDefinitions());
    }
}
