using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Lifecycle;

/// <summary>
/// The top-bar lookup (US23). A claim outside the caller's scope answers 404, like a number that does not exist, so a
/// number reveals nothing about other claims. The route has no claim id, so the scope is checked here; the middleware
/// then audits the read as a view of this claim.
/// </summary>
public static class GetClaimByNumber
{
    private const string OperationId = "getClaimByNumber";

    public static async Task<IResult> Handle(
        HttpContext context,
        ClaimLensDbContext db,
        QueuePriority priority,
        string claimNumber,
        CancellationToken cancel)
    {
        context.User.TryGetStaff(out var staffId, out var role);
        var claim = await db.Claims.AsNoTracking()
            .Where(row => row.ClaimNumber == claimNumber)
            .Select(row => new ClaimAccess(row.Id, row.Status, row.RegisteredBy, row.AssigneeId))
            .FirstOrDefaultAsync(cancel);
        if (claim is null || !claim.InScope(OperationPolicies.Find(OperationId)!.Roles[role], staffId))
        {
            return Requests.NotFound(context);
        }

        context.Items[ClaimAccess.ItemKey] = claim;
        return Results.Ok(await ClaimResponses.DetailAsync(db, priority, claim.ClaimId, staffId, role, cancel));
    }
}
