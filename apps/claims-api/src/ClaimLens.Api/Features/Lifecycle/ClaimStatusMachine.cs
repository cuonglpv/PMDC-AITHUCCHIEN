using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;

namespace ClaimLens.Api.Features.Lifecycle;

/// <summary>
/// The only code that changes <c>claim.status</c> (C3 rule 5; an architecture test holds every other type to it). The
/// moves are the lifecycle in docs/03-use-cases.md, which <c>claim_status_guard</c> in 0003_triggers.sql repeats.
/// </summary>
public static class ClaimStatusMachine
{
    private static readonly IReadOnlyDictionary<ClaimStatus, ClaimStatus[]> Moves = new Dictionary<ClaimStatus, ClaimStatus[]>
    {
        [ClaimStatus.Draft] = [ClaimStatus.Processing],
        [ClaimStatus.Processing] = [ClaimStatus.NeedsVerification, ClaimStatus.ReadyForReview],
        [ClaimStatus.NeedsVerification] = [ClaimStatus.ReadyForReview],
        [ClaimStatus.ReadyForReview] = [ClaimStatus.InReview],
        [ClaimStatus.InReview] = [ClaimStatus.PendingApproval],
        [ClaimStatus.PendingApproval] = [ClaimStatus.InReview, ClaimStatus.Approved, ClaimStatus.PartiallyApproved, ClaimStatus.Rejected],
    };

    public static bool CanMove(ClaimStatus from, ClaimStatus to) =>
        Moves.TryGetValue(from, out var next) && next.Contains(to);

    /// <summary>A new claim always starts as a draft; the trigger refuses any other status on insert.</summary>
    public static void Start(Claim claim, DateTimeOffset now)
    {
        claim.Status = ClaimStatus.Draft;
        claim.StatusChangedAt = now;
    }

    /// <summary>US03: the officer hands the claim over and extraction starts.</summary>
    public static void Submit(Claim claim, DateTimeOffset now)
    {
        Move(claim, ClaimStatus.Processing, now);
        claim.SubmittedAt = now;
    }

    private static void Move(Claim claim, ClaimStatus to, DateTimeOffset now)
    {
        if (!CanMove(claim.Status, to))
        {
            throw new InvalidOperationException($"A claim cannot move from {claim.Status} to {to}.");
        }

        claim.Status = to;
        claim.StatusChangedAt = now;
        claim.UpdatedAt = now;
        claim.RowVersion++;
    }
}
