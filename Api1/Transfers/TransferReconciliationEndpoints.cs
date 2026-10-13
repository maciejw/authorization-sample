using Microsoft.AspNetCore.Authorization;

namespace Api1.Transfers;

public static class TransferReconciliationEndpoints
{
    private const string ReconcileAccess = "transfers-reconcile-access";

    public static AuthorizationOptions AddTransferReconciliationApiPolicies(this AuthorizationOptions builder)
    {
        builder.AddPolicy(ReconcileAccess, policy => policy
            .AddAuthenticationSchemes(AuthenticationSchemes.Services)
            .RequireAuthenticatedUser()
            .RequireClaim("scope", "transfers.reconcile"));

        return builder;
    }

    public static void MapTransferReconciliationApi(this IEndpointRouteBuilder endpoints)
    {
        // A client-credentials token needs the service scope, with no user
        // subject, roles or task grants. This response is illustrative.
        endpoints.MapPost("/transfers/reconcile", () => Results.Ok(new { TransferCount = 1 }))
            .RequireAuthorization(ReconcileAccess);
    }
}
