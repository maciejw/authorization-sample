using Api1.Transfers;

using OperationAuthorization;

namespace Api1.Tests;

public sealed class TaskCatalogTests
{
    [Fact]
    public void Export_lists_every_defined_task_with_its_operations()
    {
        ITaskCatalog catalog = new TransferTasks();
        var exported = catalog.GetTaskNames().ToDictionary(name => name, name => catalog.GetOperations(name).ToArray());

        Assert.Equal(["make-transfer", "make-transfer-higher-limit", "view-transfers"],
            exported.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(new[] { Operations.MakeTransfer }, exported["make-transfer"]);
        Assert.Equal(new[] { Operations.MakeTransfer, Operations.MakeTransferHigherLimit },
            exported["make-transfer-higher-limit"]);
        Assert.Equal(new[] { Operations.ViewTransfers }, exported["view-transfers"]);
        Assert.Empty(catalog.GetOperations("unknown-task"));
    }
}
