namespace OperationAuthorization.AspNetCore;

public sealed record TaskCatalogResponse(string Service, int Version, IEnumerable<string> Tasks);
