using Microsoft.AspNetCore.Authorization;

using OperationAuthorization.AspNetCore;

namespace Api1.Transfers;

public static class TransferEndpoints
{
    private const string TransferAccess = "transfers-access";

    public static AuthorizationBuilder AddTransfersApiPolicies(this AuthorizationBuilder builder) =>
        builder
            .AddPolicy(TransferAccess, policy => policy
                .AddAuthenticationSchemes(AuthenticationSchemes.Users)
                .RequireAuthenticatedUser()
                .RequireClaim("scope", "transfers"))
            .AddPolicy(Operations.ViewTransfers, policy => policy
                .AddRequirements(new OperationRequirement(Operations.ViewTransfers)))
            .AddPolicy(Operations.MakeTransfer, policy => policy
                .AddRequirements(new OperationRequirement(Operations.MakeTransfer)));

    public static void MapTransfersApi(this IEndpointRouteBuilder endpoints)
    {
        // Illustrative data only; the sample has no payment infrastructure or persistence.
        var example = new TransferSummary(1, 100, "pending");
        var transfers = endpoints.MapGroup("/transfers")
            .RequireAuthorization(TransferAccess);

        transfers.MapGet("", () => Results.Ok(new[] { example }))
            .RequireAuthorization(Operations.ViewTransfers);

        transfers.MapGet("/{id:int}", (int id) => id == example.Id ? Results.Ok(example) : Results.NotFound())
            .RequireAuthorization(Operations.ViewTransfers);

        transfers.MapPost("", async (TransferRequest transfer, HttpContext context, IAuthorizationService authorization) =>
        {
            if (transfer.Amount <= 0)
            {
                return Results.BadRequest();
            }
            if (transfer.Amount > 1_000)
            {
                var result = await authorization.AuthorizeAsync(context.User, transfer,
                    new OperationRequirement[] { new(Operations.MakeTransferHigherLimit) });
                if (!result.Succeeded)
                    return Results.Forbid(authenticationSchemes: [AuthenticationSchemes.Users]);
            }
            return Results.Ok(transfer);
        }).RequireAuthorization(Operations.MakeTransfer);

    }
}

public sealed record TransferRequest(decimal Amount);
public sealed record TransferSummary(int Id, decimal Amount, string Status);
