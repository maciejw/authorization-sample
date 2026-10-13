using Api1.Opa;

using Microsoft.AspNetCore.Authorization;

namespace Api1.Transfers;

public static class TransferEndpoints
{
    private const string TransferAccessPolicy = "transfers-access";

    public static AuthorizationOptions AddTransfersApiPolicies(this AuthorizationOptions builder)
    {
        builder.AddPolicy(TransferAccessPolicy, policy => policy
                .AddAuthenticationSchemes(AuthenticationSchemes.Users)
                .RequireAuthenticatedUser()
                .RequireClaim("scope", "transfers"));

        return builder;
    }

    public static void MapTransfersApi(this IEndpointRouteBuilder endpoints)
    {
        // Illustrative data only; the sample has no payment infrastructure or persistence.
        var example = new TransferSummary(1, 100, "pending");
        var transfers = endpoints.MapGroup("/transfers")
            .RequireAuthorization(TransferAccessPolicy);

        transfers.MapGet("", () => Results.Ok(new[] { example }))
            .RequireAuthorization(TaskCatalog.Tasks.ViewTransfers);

        transfers.MapGet("/{id:int}", (int id) => id == example.Id ? Results.Ok(example) : Results.NotFound())
            .RequireAuthorization(TaskCatalog.Tasks.ViewTransfers);

        // Resource-based policy: the handler owns the amount-dependent rule.
        transfers.MapPost("", async (TransferRequest transfer, HttpContext context, IAuthorizationService authorization) =>
        {
            if (transfer.Amount <= 0)
            {
                return Results.BadRequest();
            }
            var result = await authorization.AuthorizeAsync(context.User, transfer, TaskCatalog.Tasks.MakeTransferHigherLimit);
            if (!result.Succeeded)
            {
                return Results.Forbid(authenticationSchemes: [AuthenticationSchemes.Users]);
            }

            // process transfer

            return Results.Ok(transfer);
        }).RequireAuthorization(TaskCatalog.Tasks.MakeTransfer);

        transfers.MapOpaTransfersApi();
    }
}

public sealed record TransferRequest(decimal Amount);
public sealed record TransferSummary(int Id, decimal Amount, string Status);
