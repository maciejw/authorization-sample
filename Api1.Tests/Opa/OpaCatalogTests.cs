using Api1.Transfers;
using System.Text.Json;

namespace Api1.Tests.Opa;

public sealed class OpaCatalogTests
{
    [Fact]
    public void Opa_snapshot_matches_application_task_grants_limit_and_roles()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Opa", "authorization.json")));
        var service = document.RootElement.GetProperty("authorization").GetProperty("services").GetProperty("api1");
        var tasks = service.GetProperty("tasks");
        var catalog = new TaskCatalog();
        Assert.Equal(catalog.GetTaskNames().Order(StringComparer.Ordinal),
            tasks.EnumerateObject().Select(task => task.Name).Order(StringComparer.Ordinal));
        foreach (var (name, definition) in catalog.GetDefinitions())
        {
            Assert.Equal(definition.GrantedTasks,
                tasks.GetProperty(name).EnumerateArray().Select(task => task.GetString()!));

        }

        var limit = Assert.Single(catalog.GetAuthorizationRequirements(TaskCatalog.Tasks.MakeTransferHigherLimit)
            .OfType<TransferLimitRequirement>());
        Assert.Equal(limit.StandardLimit, service.GetProperty("standardLimit").GetDecimal());

        var roles = new RoleDefinitions();
        foreach (var role in service.GetProperty("roles").EnumerateObject())
            Assert.Equal(roles.GetRoleDefinition(role.Name), role.Value.EnumerateArray().Select(task => task.GetString()!));
    }

}
