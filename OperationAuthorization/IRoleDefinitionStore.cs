namespace OperationAuthorization;

public interface IRoleDefinitionStore
{
    IEnumerable<string> GetRoleDefinition(string role);
}
