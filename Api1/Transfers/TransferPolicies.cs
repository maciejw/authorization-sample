using Microsoft.AspNetCore.Authorization;

namespace Api1.Transfers;

public static class TransferPolicies
{
    public static AuthorizationBuilder AddTransferPolicies(this AuthorizationBuilder builder) =>
        builder
            .AddTransfersApiPolicies()
            .AddTransferReconciliationApiPolicies();
}
