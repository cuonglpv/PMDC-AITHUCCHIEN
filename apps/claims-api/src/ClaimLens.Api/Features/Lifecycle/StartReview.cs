using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Lifecycle;

/// <summary>
/// US23: a reviewer takes a claim. The claim row's lock decides a race: the second reviewer waits, then sees the first
/// one's name and gets 409. Repeating the call as the same reviewer succeeds and changes nothing.
/// </summary>
public static class StartReview
{
    public static async Task<IResult> Handle(
        HttpContext context,
        ClaimLensDbContext db,
        QueuePriority priority,
        TimeProvider clock,
        CancellationToken cancel)
    {
        context.User.TryGetStaff(out var staffId, out var role);
        var claimId = ClaimAccess.Of(context).ClaimId;

        await using var transaction = await db.Database.BeginTransactionAsync(cancel);
        var claim = (await db.Claims.FromSql($"select * from claim where id = {claimId} for update").ToListAsync(cancel)).Single();
        if (claim.Status is not (ClaimStatus.NeedsVerification or ClaimStatus.ReadyForReview or ClaimStatus.InReview))
        {
            return Problems.Of(context, StatusCodes.Status409Conflict, ErrorCodes.ClaimStateConflict, "The claim's status does not allow this");
        }

        if (claim.AssigneeId is { } assignee && assignee != staffId)
        {
            return Problems.Of(context, StatusCodes.Status409Conflict, ErrorCodes.ClaimAlreadyAssigned, "Another reviewer took this claim");
        }

        if (claim.AssigneeId is null)
        {
            var now = clock.GetUtcNow();
            var before = new { claim.Status, claim.AssigneeId };
            ClaimStatusMachine.StartReview(claim, staffId, now);
            db.AuditEvents.Add(AuditLog.Event(
                context,
                ActorType.User,
                staffId,
                claim.Id,
                "REVIEW_STARTED",
                "claim",
                claim.Id.ToString(),
                before: before,
                after: new { claim.Status, claim.AssigneeId }));

            // A claim left in NEEDS_VERIFICATION with nothing to verify, as after a run with no fields, must not wait forever.
            await ClaimProgression.AfterChangeAsync(db, claim, now, ClaimProgression.ByPerson(context, staffId, claim.Id), cancel);
        }

        await transaction.CommitAsync(cancel);
        return Results.Ok(await ClaimResponses.DetailAsync(db, priority, claim.Id, staffId, role, cancel));
    }
}
