using ClaimLens.Api.AccessControl;
using ClaimLens.Api.AiJobs;
using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Evidence;

public sealed record AiRunRef(Guid Id, AiTask Task, AiRunStatus Status, DateTimeOffset RequestedAt);

/// <summary>
/// US06: queues a PAGE_REREAD for this one page; the AI Job Runner applies the answer. A second click while a run for the
/// page is waiting or running gets that run rather than another, whose answer would only overwrite the first.
/// </summary>
public static class RerunExtraction
{
    public static async Task<IResult> Handle(
        HttpContext context,
        ClaimLensDbContext db,
        AiJobQueue queue,
        string pageId,
        CancellationToken cancel)
    {
        context.User.TryGetStaff(out var staffId, out _);
        var claimId = ClaimAccess.Of(context).ClaimId;
        if (!Guid.TryParse(pageId, out var id))
        {
            return Requests.NotFound(context);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancel);
        if (await FieldChanges.LockEditableAsync(db, claimId, cancel) is null)
        {
            return FieldChanges.StateConflict(context);
        }

        var page = await db.DocumentPages.SingleOrDefaultAsync(row => row.Id == id && row.ClaimId == claimId, cancel);
        if (page is null || !await db.Documents.AnyAsync(document => document.Id == page.DocumentId && document.Status == DocumentStatus.Ready, cancel))
        {
            return Requests.NotFound(context);
        }

        var run = await db.AiRuns.AsNoTracking()
            .Where(row => row.ClaimId == claimId
                && row.Task == AiTask.PageReread
                && row.ScopePageId == id
                && (row.Status == AiRunStatus.Queued || row.Status == AiRunStatus.Running))
            .FirstOrDefaultAsync(cancel);
        if (run is null)
        {
            run = queue.PageReread(db, claimId, page.DocumentId, page.Id, staffId);
            page.AiStatus = PageAiStatus.Queued;
            var audit = AuditLog.Event(
                context,
                ActorType.User,
                staffId,
                claimId,
                "AI_RERUN_REQUESTED",
                "document_page",
                page.Id.ToString(),
                after: new { run.Task, runId = run.Id });
            audit.AiRunId = run.Id;
            db.AuditEvents.Add(audit);
            await db.SaveChangesAsync(cancel);
        }

        await transaction.CommitAsync(cancel);
        return Results.Accepted(
            $"/api/v1/claims/{claimId}/status",
            new AiRunRef(run.Id, run.Task, run.Status, run.RequestedAt));
    }
}
