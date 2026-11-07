using ClaimLens.Api.AccessControl;
using ClaimLens.Api.AiJobs;
using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Features.Lifecycle;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Intake;

/// <summary>
/// US03: the claim moves to PROCESSING and FIELD_EXTRACTION is queued. A file still uploading or converting stops it,
/// and so does having no READY file. A page check still running does not.
/// </summary>
public static class SubmitClaim
{
    public static async Task<IResult> Handle(
        HttpContext context,
        ClaimLensDbContext db,
        TimeProvider clock,
        AiJobQueue ai,
        CancellationToken cancel)
    {
        var claimId = ClaimAccess.Of(context).ClaimId;

        // The claim row's lock makes an upload that is about to add a file wait, and then see the claim is submitted.
        await using var transaction = await db.Database.BeginTransactionAsync(cancel);
        var claim = (await db.Claims.FromSql($"select * from claim where id = {claimId} for update").ToListAsync(cancel)).Single();
        if (claim.Status != ClaimStatus.Draft)
        {
            return Problems.Of(context, StatusCodes.Status409Conflict, ErrorCodes.ClaimStateConflict, "The claim's status does not allow this");
        }

        var statuses = await db.Documents
            .Where(document => document.ClaimId == claimId)
            .Select(document => document.Status)
            .ToListAsync(cancel);
        if (statuses.Any(status => status is DocumentStatus.Uploading or DocumentStatus.Converting))
        {
            return Problems.Of(context, StatusCodes.Status409Conflict, ErrorCodes.UploadInProgress, "A file is still being processed");
        }

        if (!statuses.Contains(DocumentStatus.Ready))
        {
            return Problems.Of(context, StatusCodes.Status409Conflict, ErrorCodes.NoReadyDocument, "Add at least one file that is ready");
        }

        context.User.TryGetStaff(out var staffId, out _);
        var before = claim.Status;
        ClaimStatusMachine.Submit(claim, clock.GetUtcNow());
        var run = ai.FieldExtraction(db, claim.Id);

        var audit = AuditLog.Event(
            context,
            ActorType.User,
            staffId,
            claim.Id,
            "CLAIM_STATUS_CHANGED",
            "claim",
            claim.Id.ToString(),
            before: new { status = before },
            after: new { status = claim.Status, claim.SubmittedAt, fieldExtractionRunId = run.Id });
        audit.AiRunId = run.Id;
        db.AuditEvents.Add(audit);

        await db.SaveChangesAsync(cancel);
        await transaction.CommitAsync(cancel);

        return Results.Accepted($"/api/v1/claims/{claimId}/status", await ClaimResponses.StatusAsync(db, claimId, cancel));
    }
}
