using Microsoft.AspNetCore.Authorization;

namespace OperationAuthorization;

public sealed record TaskRequirement(string Task) : IAuthorizationRequirement;
