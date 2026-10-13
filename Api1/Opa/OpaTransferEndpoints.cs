using Api1.Transfers;

using Microsoft.AspNetCore.Authorization;

namespace Api1.Opa;

public static class OpaTransferEndpoints
{
    private static readonly AuthorizationPolicy opaTransferPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddRequirements(new OpaRequirement("api1/make_transfer_higher_limit_v1/allow", ResolveTasks: true))
        .Build();

    private static readonly AuthorizationPolicy centralizedOpaTransferPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddRequirements(new OpaRequirement("api1/make_transfer_higher_limit_v2/allow"))
        .Build();

    public static void MapOpaTransfersApi(this RouteGroupBuilder transfers)
    {
        // OPA policy: this endpoint supplies facts; OPA owns the decision.
        transfers.MapPost("/opa", async (TransferRequest transfer, HttpContext context,
            IAuthorizationService authorization) =>
        {
            if (transfer.Amount <= 0)
                return Results.BadRequest();

            var result = await authorization.AuthorizeAsync(context.User, transfer, opaTransferPolicy);
            if (!result.Succeeded)
            {
                return Results.Forbid(authenticationSchemes: [AuthenticationSchemes.Users]);
            }

            return Results.Ok(transfer);
        }).RequireAuthorization(TaskCatalog.Tasks.MakeTransfer);

        // Centralized OPA: only validated roles are sent; OPA resolves task grants.
        transfers.MapPost("/opa-centralized", async (TransferRequest transfer, HttpContext context,
            IAuthorizationService authorization) =>
        {
            if (transfer.Amount <= 0)
                return Results.BadRequest();

            var result = await authorization.AuthorizeAsync(context.User, transfer, centralizedOpaTransferPolicy);
            if (!result.Succeeded)
            {
                return Results.Forbid(authenticationSchemes: [AuthenticationSchemes.Users]);
            }
            return Results.Ok(transfer);
        }); // The group checks JWT and scope; no local task policy is applied here.

    }
}
