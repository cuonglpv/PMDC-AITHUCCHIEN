using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Persistence;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.AiJobs;

/// <summary>
/// Queues AI work as `ai_run` rows in QUEUED, in the caller's transaction. The AI Job Runner that claims them is not
/// built yet (Slice 2), so this slice only queues.
/// </summary>
public sealed class AiJobQueue(IOptions<AiOptions> options, TimeProvider clock)
{
    /// <summary>A document reached READY (docs/07-api-contract.md, what triggers each AI task).</summary>
    public AiRun PageCheck(ClaimLensDbContext db, Guid claimId, Guid documentId) =>
        Add(db, claimId, AiTask.PageCheck, options.Value.PageCheck, documentId);

    /// <summary>submitClaim.</summary>
    public AiRun FieldExtraction(ClaimLensDbContext db, Guid claimId) =>
        Add(db, claimId, AiTask.FieldExtraction, options.Value.FieldExtraction, null);

    private AiRun Add(ClaimLensDbContext db, Guid claimId, AiTask task, PipelineOptions pipeline, Guid? documentId)
    {
        // triggered_by stays empty: the system starts these runs as part of an upload or a submission.
        var run = new AiRun
        {
            Id = Guid.CreateVersion7(),
            ClaimId = claimId,
            Task = task,
            ScopeDocumentId = documentId,
            ModelName = pipeline.Name,
            ModelVersion = pipeline.Version,
            Status = AiRunStatus.Queued,
            RequestedAt = clock.GetUtcNow(),
        };
        db.AiRuns.Add(run);
        return run;
    }
}
