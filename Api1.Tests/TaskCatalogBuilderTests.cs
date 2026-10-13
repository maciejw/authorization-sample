using Microsoft.AspNetCore.Authorization;
using OperationAuthorization;

namespace Api1.Tests;

public sealed class TaskCatalogBuilderTests
{
    [Fact]
    public void Extensions_share_base_requirements_and_grants_but_not_sibling_extensions()
    {
        var firstRequirement = new TaskRequirement("first-check");
        var secondRequirement = new TaskRequirement("second-check");
        var definitions = new TaskCatalogBuilder()
            .Create("base",
                new TaskExtension("first", firstRequirement),
                new TaskExtension("second", secondRequirement))
            .Build();

        var baseRequirement = Assert.IsType<TaskRequirement>(Assert.Single(definitions["base"].Requirements));
        Assert.Equal("base", baseRequirement.Task);
        Assert.Equal(["base"], definitions["base"].GrantedTasks);
        Assert.Equal(["first", "base"], definitions["first"].GrantedTasks);
        Assert.Equal(["second", "base"], definitions["second"].GrantedTasks);
        Assert.Equal<IAuthorizationRequirement>([baseRequirement, firstRequirement], definitions["first"].Requirements);
        Assert.Equal<IAuthorizationRequirement>([baseRequirement, secondRequirement], definitions["second"].Requirements);
        Assert.Same(baseRequirement, definitions["first"].Requirements[0]);
    }

    [Fact]
    public void Built_catalog_is_a_snapshot_even_when_building_continues()
    {
        var builder = new TaskCatalogBuilder().Create("base");
        var snapshot = builder.Build();

        builder.Create("later");

        Assert.Single(snapshot);
        Assert.False(snapshot.ContainsKey("later"));
        Assert.Equal(2, builder.Build().Count);
    }

    [Fact]
    public void Duplicate_names_cannot_replace_an_existing_task_policy()
    {
        var builder = new TaskCatalogBuilder().Create("base");

        Assert.Throws<ArgumentException>(() => builder.Create("base"));
        Assert.Throws<ArgumentException>(() => builder.Create("other", new TaskExtension("base")));
        Assert.Equal(new TaskRequirement("base"), Assert.Single(builder.Build()["base"].Requirements));
    }
}
