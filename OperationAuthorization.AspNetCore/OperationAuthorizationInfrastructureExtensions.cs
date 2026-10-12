using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace OperationAuthorization.AspNetCore;

public static class OperationAuthorizationInfrastructureDefaults
{
    public const string AuthenticationScheme = "InfrastructureBearer";
    public const string Audience = "authorization-catalog";
    public const string Scope = "authorization.tasks.read";
    public const string Policy = "authorization-tasks-read";
}

public static class OperationAuthorizationInfrastructureExtensions
{
    // Authority binds from Authentication:Schemes:InfrastructureBearer.
    // The application supplies its own ITaskCatalog registration.
    public static IServiceCollection AddOperationAuthorizationInfrastructure(this IServiceCollection services)
    {
        services.TryAddScoped<ScopeNormalizationEvents>();
        services.AddAuthentication().AddJwtBearer(
            OperationAuthorizationInfrastructureDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters.ValidAudience = OperationAuthorizationInfrastructureDefaults.Audience;
                options.MapInboundClaims = false;
                options.EventsType = typeof(ScopeNormalizationEvents);
            });
        services.AddAuthorizationBuilder().AddPolicy(OperationAuthorizationInfrastructureDefaults.Policy, policy => policy
            .AddAuthenticationSchemes(OperationAuthorizationInfrastructureDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireClaim("scope", OperationAuthorizationInfrastructureDefaults.Scope));
        return services;
    }

    public static RouteHandlerBuilder MapOperationAuthorizationInfrastructure(
        this IEndpointRouteBuilder endpoints, string service, int version, string pattern = "/infrastructure/tasks") =>
        endpoints.MapGet(pattern, (ITaskCatalog catalog) => Results.Ok(new TaskCatalogResponse(service, version, catalog.GetTaskNames())))
            .RequireAuthorization(OperationAuthorizationInfrastructureDefaults.Policy);
}
