using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;

namespace ClaimLens.Api.Features.Lifecycle;

/// <summary>The case header and Overview, with this caller's allowed actions and blockers. The middleware audits the read.</summary>
public static class GetClaim
{
    public static async Task<IResult> Handle(HttpContext context, ClaimLensDbContext db, QueuePriority priority, CancellationToken cancel)
    {
        context.User.TryGetStaff(out _, out var role);
        return Results.Ok(await ClaimResponses.DetailAsync(db, priority, ClaimAccess.Of(context).ClaimId, role, cancel));
    }
}
