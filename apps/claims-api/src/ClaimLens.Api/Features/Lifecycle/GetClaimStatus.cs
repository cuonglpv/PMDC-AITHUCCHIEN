using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;

namespace ClaimLens.Api.Features.Lifecycle;

/// <summary>The one endpoint the web app polls. It carries no names of any kind, so it is not audited (BR-12).</summary>
public static class GetClaimStatus
{
    public static async Task<IResult> Handle(HttpContext context, ClaimLensDbContext db, CancellationToken cancel)
    {
        var snapshot = await ClaimResponses.StatusAsync(db, ClaimAccess.Of(context).ClaimId, cancel);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(snapshot);
    }
}
