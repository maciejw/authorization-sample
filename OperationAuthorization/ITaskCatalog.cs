using Microsoft.AspNetCore.Authorization;
using System.Collections.Immutable;

namespace OperationAuthorization;

public interface ITaskCatalog
{
    ImmutableDictionary<string, TaskDefinition> GetDefinitions();
    IEnumerable<string> GetTaskNames();
    IEnumerable<string> GetGrantedTasks(string task);
    IEnumerable<IAuthorizationRequirement> GetAuthorizationRequirements(string task);
}
