using Microsoft.AspNetCore.Authorization;

using System.Collections.Immutable;

namespace OperationAuthorization;

public sealed record TaskDefinition(ImmutableArray<string> GrantedTasks, ImmutableArray<IAuthorizationRequirement> Requirements);

public sealed record TaskExtension(string Name, params ImmutableArray<IAuthorizationRequirement> Requirements);

public sealed class TaskCatalogBuilder
{
    private readonly ImmutableDictionary<string, TaskDefinition>.Builder definitions =
        ImmutableDictionary.CreateBuilder<string, TaskDefinition>(StringComparer.Ordinal);

    public TaskCatalogBuilder Create(string name, params TaskExtension[] extensions)
    {
        ImmutableArray<IAuthorizationRequirement> requirements = [new TaskRequirement(name)];
        definitions.Add(name, new TaskDefinition([name], requirements));
        foreach (var extension in extensions)
        {
            definitions.Add(extension.Name, new TaskDefinition([extension.Name, name], requirements.AddRange(extension.Requirements)));
        }
        return this;
    }

    public ImmutableDictionary<string, TaskDefinition> Build() => definitions.ToImmutable();
}
