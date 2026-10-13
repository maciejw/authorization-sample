using Api1.Transfers;

using Microsoft.AspNetCore.Authorization;

using OperationAuthorization;

namespace Api1.Tests.Transfers;

public sealed class TaskCatalogTests
{
    [Fact]
    public void Catalog_exposes_prebuilt_requirements_for_each_task()
    {
        ITaskCatalog catalog = new TaskCatalog();
        Assert.Equal([TaskCatalog.Tasks.MakeTransfer, TaskCatalog.Tasks.MakeTransferHigherLimit, TaskCatalog.Tasks.ViewTransfers],
            catalog.GetTaskNames().Order(StringComparer.Ordinal));
        Assert.Equal(new TaskRequirement(TaskCatalog.Tasks.MakeTransfer),
            Assert.Single(catalog.GetAuthorizationRequirements(TaskCatalog.Tasks.MakeTransfer)));
        Assert.Equal(new TaskRequirement(TaskCatalog.Tasks.ViewTransfers),
            Assert.Single(catalog.GetAuthorizationRequirements(TaskCatalog.Tasks.ViewTransfers)));
        Assert.Equal<IAuthorizationRequirement>(
            [new TaskRequirement(TaskCatalog.Tasks.MakeTransfer), new TransferLimitRequirement(1000, TaskCatalog.Tasks.MakeTransferHigherLimit)],
            catalog.GetAuthorizationRequirements(TaskCatalog.Tasks.MakeTransferHigherLimit));
        Assert.Equal([TaskCatalog.Tasks.MakeTransferHigherLimit, TaskCatalog.Tasks.MakeTransfer],
            catalog.GetGrantedTasks(TaskCatalog.Tasks.MakeTransferHigherLimit));
        Assert.Empty(catalog.GetGrantedTasks("unknown-task"));
        Assert.Empty(catalog.GetAuthorizationRequirements("unknown-task"));
    }
}
