using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Features.Evidence;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Lifecycle;

/// <summary>
/// Where Slices 4a and 4b re-run validation and adjudication after a change to a claim's data (US13). It does nothing
/// yet; every change to evidence already passes through it.
/// </summary>
public static class ClaimRecheck
{
    public static Task RunAsync(ClaimLensDbContext db, Claim claim, CancellationToken cancel) => Task.CompletedTask;
}

/// <summary>What every change to a claim's evidence does next, by a person or by the AI Job Runner.</summary>
public static class ClaimProgression
{
    /// <summary>
    /// Saves the change, then moves the claim on if its evidence is now complete, and re-checks it. The caller's
    /// transaction holds the claim row. claim_status_guard refuses NEEDS_VERIFICATION to IN_REVIEW directly, so an
    /// assigned claim makes two UPDATEs, each with its own audit event. A claim already past NEEDS_VERIFICATION stays
    /// where it is, even when a change leaves new fields to verify: the lifecycle has no way back, and
    /// <see cref="ClaimCounts.FieldsToVerify"/> keeps them in front of the reviewer.
    /// </summary>
    public static async Task AfterChangeAsync(
        ClaimLensDbContext db,
        Claim claim,
        DateTimeOffset now,
        Func<ClaimStatus, ClaimStatus, AuditEvent> statusEvent,
        CancellationToken cancel)
    {
        await db.SaveChangesAsync(cancel);

        if (claim.Status == ClaimStatus.NeedsVerification && await ReviewReadiness.IsReadyAsync(db, claim.Id, cancel))
        {
            await MoveAsync(db, claim, machine => ClaimStatusMachine.FinishVerification(machine, now), statusEvent, cancel);
            if (claim.AssigneeId is not null)
            {
                await MoveAsync(db, claim, machine => ClaimStatusMachine.BeginAssignedReview(machine, now), statusEvent, cancel);
            }
        }

        await ClaimRecheck.RunAsync(db, claim, cancel);
    }

    /// <summary>A status change a person's request caused.</summary>
    public static Func<ClaimStatus, ClaimStatus, AuditEvent> ByPerson(HttpContext context, Guid staffId, Guid claimId) =>
        (before, after) => AuditLog.Event(
            context,
            ActorType.User,
            staffId,
            claimId,
            "CLAIM_STATUS_CHANGED",
            "claim",
            claimId.ToString(),
            before: new { status = before },
            after: new { status = after });

    private static async Task MoveAsync(
        ClaimLensDbContext db,
        Claim claim,
        Action<Claim> move,
        Func<ClaimStatus, ClaimStatus, AuditEvent> statusEvent,
        CancellationToken cancel)
    {
        var before = claim.Status;
        move(claim);
        db.AuditEvents.Add(statusEvent(before, claim.Status));
        await db.SaveChangesAsync(cancel);
    }
}
