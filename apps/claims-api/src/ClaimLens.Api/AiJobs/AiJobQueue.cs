using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Persistence;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.AiJobs;

/// <summary>
/// Queues AI work as `ai_run` rows in QUEUED, in the caller's transaction. The AI Job Runner claims them.
/// </summary>
public sealed class AiJobQueue(IOptions<AiOptions> options, TimeProvider clock)
{
    /// <summary>A document reached READY (docs/07-api-contract.md, what triggers each AI task).</summary>
    public AiRun PageCheck(ClaimLensDbContext db, Guid claimId, Guid documentId) =>
        Add(db, claimId, AiTask.PageCheck, options.Value.PageCheck, documentId);

    /// <summary>submitClaim.</summary>
    public AiRun FieldExtraction(ClaimLensDbContext db, Guid claimId) =>
        Add(db, claimId, AiTask.FieldExtraction, options.Value.FieldExtraction, null);

    /// <summary>rerunExtraction: a reviewer asked for this one page to be read again.</summary>
    public AiRun PageReread(ClaimLensDbContext db, Guid claimId, Guid documentId, Guid pageId, Guid requestedBy) =>
        Add(db, claimId, AiTask.PageReread, options.Value.PageReread, documentId, pageId, requestedBy);

    private AiRun Add(
        ClaimLensDbContext db,
        Guid claimId,
        AiTask task,
        PipelineOptions pipeline,
        Guid? documentId,
        Guid? pageId = null,
        Guid? triggeredBy = null)
    {
        // triggered_by stays empty when the system starts a run as part of an upload or a submission.
        var run = new AiRun
        {
            Id = Guid.CreateVersion7(),
            ClaimId = claimId,
            Task = task,
            ScopeDocumentId = documentId,
            ScopePageId = pageId,
            TriggeredBy = triggeredBy,
            ModelName = pipeline.Name,
            ModelVersion = pipeline.Version,
            Status = AiRunStatus.Queued,
            RequestedAt = clock.GetUtcNow(),
        };
        db.AiRuns.Add(run);
        return run;
    }
}
