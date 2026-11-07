namespace ClaimLens.Api.Features.Lifecycle;

public static class LifecycleEndpoints
{
    public static void MapLifecycle(this IEndpointRouteBuilder api)
    {
        api.MapGet("/claims", ListClaims.Handle).WithName("listClaims");
        api.MapGet("/claims/{claimId}", GetClaim.Handle).WithName("getClaim");
        api.MapGet("/claims/{claimId}/status", GetClaimStatus.Handle).WithName("getClaimStatus");
    }
}
