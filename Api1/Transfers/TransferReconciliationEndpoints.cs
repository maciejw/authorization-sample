using Microsoft.AspNetCore.Authorization;

namespace Api1.Transfers;

public static class TransferReconciliationEndpoints
{
    private const string ReconcileAccess = "transfers-reconcile-access";

    public static AuthorizationBuilder AddTransferReconciliationApiPolicies(this AuthorizationBuilder builder) =>
        builder.AddPolicy(ReconcileAccess, policy => policy
            .AddAuthenticationSchemes(AuthenticationSchemes.Services)
            .RequireAuthenticatedUser()
            .RequireClaim("scope", "transfers.reconcile"));

    public static void MapTransferReconciliationApi(this IEndpointRouteBuilder endpoints)
    {
        // A client-credentials token needs the service scope, with no user
        // subject, roles or operation expansion. This response is illustrative.
        endpoints.MapPost("/transfers/reconcile", () => Results.Ok(new { TransferCount = 1 }))
            .RequireAuthorization(ReconcileAccess);
    }
}
