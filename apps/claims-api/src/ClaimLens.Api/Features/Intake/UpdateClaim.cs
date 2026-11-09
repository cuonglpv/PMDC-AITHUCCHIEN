using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Features.Lifecycle;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Intake;

/// <summary>
/// Editing a draft, when the officer goes back in the New claim stepper. The request carries the rowVersion it read;
/// a newer one answers 409 VERSION_CONFLICT with the claim as it is now (docs/07-api-contract.md, Concurrency).
/// </summary>
public static class UpdateClaim
{
    private sealed record Request(
        int? RowVersion,
        Guid? MemberId,
        Guid? PolicyId,
        DateOnly? ServiceDate,
        string? ProviderName,
        long? ClaimedTotal,
        IntakeChannel? Channel);

    public static async Task<IResult> Handle(
        HttpContext context,
        ClaimLensDbContext db,
        TimeProvider clock,
        QueuePriority priority,
        CancellationToken cancel)
    {
        var request = await Requests.ReadJsonAsync<Request>(context);
        if (request is null)
        {
            return Requests.Unreadable(context);
        }

        if (request.RowVersion is not { } rowVersion || rowVersion < 0)
        {
            return Requests.Unreadable(context, new FieldError("rowVersion", "REQUIRED", "Send the rowVersion you read."));
        }

        if (request is { MemberId: null, PolicyId: null, ServiceDate: null, ProviderName: null, ClaimedTotal: null, Channel: null })
        {
            return Requests.Unreadable(context, new FieldError("rowVersion", "NOTHING_TO_CHANGE", "Send at least one value to change."));
        }

        var claim = await db.Claims.SingleAsync(row => row.Id == ClaimAccess.Of(context).ClaimId, cancel);
        if (claim.RowVersion != rowVersion)
        {
            return Conflict(context, claim);
        }

        var before = ClaimFacts.Of(claim);
        var after = new ClaimFacts(
            request.MemberId ?? before.MemberId,
            request.PolicyId ?? before.PolicyId,
            request.ServiceDate ?? before.ServiceDate,
            request.ProviderName?.Trim() ?? before.ProviderName,
            request.ClaimedTotal ?? before.ClaimedTotal,
            request.Channel ?? before.Channel);

        var errors = new[] { ClaimRules.ProviderName(after.ProviderName), ClaimRules.ClaimedTotal(after.ClaimedTotal) }
            .OfType<FieldError>()
            .ToArray();
        if (errors.Length > 0)
        {
            return Requests.Unreadable(context, errors);
        }

        var (policy, problem) = await ClaimRules.CheckPartiesAsync(context, db, after.MemberId, after.PolicyId, cancel);
        if (problem is not null)
        {
            return problem;
        }

        context.User.TryGetStaff(out var staffId, out var role);
        if (after != before)
        {
            after.ApplyTo(claim);
            claim.RowVersion++;
            claim.UpdatedAt = clock.GetUtcNow();
            db.AuditEvents.Add(AuditLog.Event(
                context,
                ActorType.User,
                staffId,
                claim.Id,
                "CLAIM_UPDATED",
                "claim",
                claim.Id.ToString(),
                before: before,
                after: after));

            try
            {
                await db.SaveChangesAsync(cancel);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Someone saved between the read and this write; the row_version check in the UPDATE caught it.
                var current = await db.Claims.AsNoTracking().SingleAsync(row => row.Id == claim.Id, cancel);
                return Conflict(context, current);
            }
        }

        var detail = await ClaimResponses.DetailAsync(db, priority, claim.Id, staffId, role, cancel);
        return Results.Ok(new ClaimSaved(detail, ClaimRules.Warnings(policy!, after.ServiceDate)));
    }

    private static IResult Conflict(HttpContext context, Claim current) =>
        Problems.Of(
            context,
            StatusCodes.Status409Conflict,
            ErrorCodes.VersionConflict,
            "Another user changed this claim",
            details: new { currentRowVersion = current.RowVersion, current = ClaimFacts.Of(current) });
}
