using ClaimLens.Api.AiJobs.Appliers;
using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.DocumentStore;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.AiJobs;

/// <summary>
/// Runs one queued ai_run: claims it with FOR UPDATE SKIP LOCKED and commits it as RUNNING, calls the Document AI
/// Service outside any transaction, keeps the raw answer in object storage, then applies it in a new transaction. The
/// worker calls it in a loop; tests call it directly.
/// <para>
/// A failure that may pass (the service down, slow or answering off the contract) puts the run back in the queue to
/// wait, doubling each time, while other runs go ahead; after Workers:AiJobs:MaxAttempts it is FAILED. A request the
/// service refused as invalid fails at once. A run left RUNNING past its task's timeout, by a process that died, is
/// FAILED as TIMEOUT. Every FAILED run triggers the manual fallback.
/// </para>
/// </summary>
public sealed class AiRunProcessor(
    ClaimLensDbContext db,
    IDocumentAiClient ai,
    IDocumentStore store,
    PageCheckApplier pageCheck,
    FieldExtractionApplier extraction,
    PageRereadApplier reread,
    AiRunAttempts attempts,
    IOptions<AiOptions> options,
    TimeProvider clock,
    ILogger<AiRunProcessor> logger)
{
    public const string TimeoutCode = "TIMEOUT";
    public const string NothingToReadCode = "NOTHING_TO_READ";
    public const string NotAppliedCode = "RESULT_NOT_APPLIED";

    /// <summary>
    /// The client gives up at the task's timeout, so a run still RUNNING this long after it was left by a process that
    /// stopped. The margin keeps a run whose answer arrived at the last moment from being failed while it is applied.
    /// </summary>
    public static readonly TimeSpan StuckMargin = TimeSpan.FromSeconds(30);

    /// <summary>True when a run was handled: finished, failed or put back to wait. False when nothing was waiting.</summary>
    public async Task<bool> RunNextAsync(CancellationToken cancel)
    {
        if (await FailStuckRunAsync(cancel))
        {
            return true;
        }

        var run = await ClaimNextAsync(cancel);
        if (run is null)
        {
            return false;
        }

        try
        {
            await ExecuteAsync(run, cancel);
            attempts.Done(run.Id);
        }
        catch (DocumentAiException error) when (!error.Transient)
        {
            logger.LogWarning(error, "AI run {RunId} failed: {Code}", run.Id, error.Code);
            attempts.Done(run.Id);
            await FailAsync(run.Id, error.Code, cancel);
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            // Shutting down: the run goes back to the queue rather than waiting for the stuck-run check.
            await RequeueAsync(run.Id, CancellationToken.None);
            throw;
        }
        catch (Exception error)
        {
            var code = error is DocumentAiException { Code: var known } ? known : NotAppliedCode;
            if (attempts.Failed(run.Id))
            {
                logger.LogWarning(error, "AI run {RunId} failed on its last attempt: {Code}", run.Id, code);
                await FailAsync(run.Id, code, cancel);
            }
            else
            {
                logger.LogWarning(error, "AI run {RunId} failed ({Code}); it will be tried again", run.Id, code);
                await RequeueAsync(run.Id, cancel);
            }
        }

        return true;
    }

    /// <summary>
    /// The oldest QUEUED run of a task this slice runs that is not waiting out a failure. A claim's FIELD_EXTRACTION
    /// waits until none of its PAGE_CHECK runs is QUEUED or RUNNING, because extraction reads by page type.
    /// </summary>
    private async Task<AiRun?> ClaimNextAsync(CancellationToken cancel)
    {
        var waiting = attempts.Waiting();
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancel);
        var run = (await db.AiRuns
                .FromSql($"""
                    select r.* from ai_run r
                    where r.status = 'QUEUED'
                      and r.task in ('PAGE_CHECK', 'FIELD_EXTRACTION', 'PAGE_REREAD')
                      and r.id <> all({waiting})
                      and (r.task <> 'FIELD_EXTRACTION' or not exists (
                            select 1 from ai_run p
                            where p.claim_id = r.claim_id and p.task = 'PAGE_CHECK' and p.status in ('QUEUED', 'RUNNING')))
                    order by r.requested_at, r.id
                    limit 1
                    for update of r skip locked
                    """)
                .ToListAsync(cancel))
            .FirstOrDefault();
        if (run is null)
        {
            return null;
        }

        run.Status = AiRunStatus.Running;
        run.StartedAt = clock.GetUtcNow();
        if (run.Task == AiTask.PageCheck)
        {
            await pageCheck.StartAsync(run, cancel);
        }
        else if (run.Task == AiTask.PageReread)
        {
            await reread.StartAsync(run, cancel);
        }

        await db.SaveChangesAsync(cancel);
        await transaction.CommitAsync(cancel);
        db.ChangeTracker.Clear();
        return run;
    }

    private async Task ExecuteAsync(AiRun run, CancellationToken cancel)
    {
        switch (run.Task)
        {
            case AiTask.PageCheck:
                if (await pageCheck.RequestAsync(run, cancel) is not { } check)
                {
                    // The file was removed after it was queued: there is nothing to read and no page to set.
                    await FailAsync(run.Id, NothingToReadCode, cancel);
                    return;
                }

                var checkAnswer = await ai.RunPageCheckAsync(check, cancel);
                var checkKey = await StoreRawAsync(run, checkAnswer.Raw, cancel);
                await CompleteAsync(run.Id, checkKey, checkAnswer.Body.Model, locked => pageCheck.ApplyAsync(locked, checkAnswer.Body, cancel), cancel);
                return;

            case AiTask.FieldExtraction:
                if (await extraction.RequestAsync(run, cancel) is not { } read)
                {
                    // No document has a type with fields, so there is nothing to ask. The claim still moves on.
                    var empty = new FieldExtractionResponse(run.Id, new ModelRef(run.ModelName, run.ModelVersion), [], []);
                    await CompleteAsync(run.Id, null, null, locked => extraction.ApplyAsync(locked, empty, cancel), cancel);
                    return;
                }

                var readAnswer = await ai.RunFieldExtractionAsync(read, cancel);
                var readKey = await StoreRawAsync(run, readAnswer.Raw, cancel);
                await CompleteAsync(run.Id, readKey, readAnswer.Body.Model, locked => extraction.ApplyAsync(locked, readAnswer.Body, cancel), cancel);
                return;

            case AiTask.PageReread:
                if (await reread.RequestAsync(run, cancel) is not { } again)
                {
                    await FailAsync(run.Id, NothingToReadCode, cancel);
                    return;
                }

                if (again.FieldCatalogue.Count == 0)
                {
                    // The page's type has no fields, so there is nothing to ask, and what it gave before no longer applies.
                    var nothing = new PageRereadResponse(run.Id, new ModelRef(run.ModelName, run.ModelVersion), again.DocumentId, []);
                    await CompleteAsync(run.Id, null, null, locked => reread.ApplyAsync(locked, nothing, cancel), cancel);
                    return;
                }

                var againAnswer = await ai.RunPageRereadAsync(again, cancel);
                var againKey = await StoreRawAsync(run, againAnswer.Raw, cancel);
                await CompleteAsync(run.Id, againKey, againAnswer.Body.Model, locked => reread.ApplyAsync(locked, againAnswer.Body, cancel), cancel);
                return;

            default:
                throw new InvalidOperationException($"The AI Job Runner does not run {run.Task} yet.");
        }
    }

    /// <summary>The answer exactly as it arrived, for the AI quality review and for disputes (ai_run.raw_output_key).</summary>
    private async Task<string> StoreRawAsync(AiRun run, byte[] raw, CancellationToken cancel)
    {
        var key = StorageKeys.AiRaw(run.ClaimId, run.Id);
        await store.PutAsync(key, raw, "application/json", cancel);
        return key;
    }

    private async Task CompleteAsync(Guid runId, string? rawKey, ModelRef? model, Func<AiRun, Task<object>> apply, CancellationToken cancel)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancel);
        var run = await LockRunAsync(runId, cancel);
        if (run.Status != AiRunStatus.Running)
        {
            // The stuck-run check failed it meanwhile, and the fallback has run. This late answer is not used.
            logger.LogWarning("AI run {RunId} answered after it was {Status}; the answer is not used", runId, run.Status);
            return;
        }

        var summary = await apply(run);
        run.Status = AiRunStatus.Succeeded;
        run.FinishedAt = clock.GetUtcNow();
        run.RawOutputKey = rawKey;
        if (model is { Name.Length: > 0 and <= 80, Version.Length: > 0 and <= 40 })
        {
            // The model actually used is the one the run records (document-ai.yaml, TaskResponse).
            run.ModelName = model.Name;
            run.ModelVersion = model.Version;
        }

        db.AuditEvents.Add(RunEvent(run, "AI_RUN_SUCCEEDED", summary));
        await db.SaveChangesAsync(cancel);
        await transaction.CommitAsync(cancel);
        db.ChangeTracker.Clear();
    }

    private async Task FailAsync(Guid runId, string code, CancellationToken cancel)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancel);
        var run = await LockRunAsync(runId, cancel);
        if (run.Status is AiRunStatus.Succeeded or AiRunStatus.Failed)
        {
            return;
        }

        await FailLockedAsync(run, code, cancel);
        await transaction.CommitAsync(cancel);
        db.ChangeTracker.Clear();
    }

    /// <summary>Marks the run FAILED and runs the manual fallback for its task, in the caller's transaction.</summary>
    private async Task FailLockedAsync(AiRun run, string code, CancellationToken cancel)
    {
        run.Status = AiRunStatus.Failed;
        run.ErrorCode = code.Length <= 60 ? code : code[..60];
        run.FinishedAt = clock.GetUtcNow();
        var fallback = run.Task switch
        {
            AiTask.PageCheck => await pageCheck.FailAsync(run, cancel),
            AiTask.FieldExtraction => await extraction.FailAsync(run, cancel),
            AiTask.PageReread => await reread.FailAsync(run, cancel),
            _ => new { },
        };

        db.AuditEvents.Add(RunEvent(run, "AI_RUN_FAILED", fallback));
        await db.SaveChangesAsync(cancel);
        attempts.Done(run.Id);
    }

    private async Task RequeueAsync(Guid runId, CancellationToken cancel)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancel);
        var run = await LockRunAsync(runId, cancel);
        if (run.Status != AiRunStatus.Running)
        {
            return;
        }

        run.Status = AiRunStatus.Queued;
        run.StartedAt = null;
        if (run.Task == AiTask.PageCheck)
        {
            await pageCheck.RequeueAsync(run, cancel);
        }
        else if (run.Task == AiTask.PageReread)
        {
            await reread.RequeueAsync(run, cancel);
        }

        await db.SaveChangesAsync(cancel);
        await transaction.CommitAsync(cancel);
        db.ChangeTracker.Clear();
    }

    /// <summary>One run left RUNNING past its task's timeout (and the margin) is FAILED as TIMEOUT. True when one was.</summary>
    private async Task<bool> FailStuckRunAsync(CancellationToken cancel)
    {
        var now = clock.GetUtcNow();
        var pageCheckCutoff = now - TimeSpan.FromSeconds(options.Value.PageCheck.TimeoutSeconds) - StuckMargin;
        var extractionCutoff = now - TimeSpan.FromSeconds(options.Value.FieldExtraction.TimeoutSeconds) - StuckMargin;
        var rereadCutoff = now - TimeSpan.FromSeconds(options.Value.PageReread.TimeoutSeconds) - StuckMargin;

        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancel);
        var run = (await db.AiRuns
                .FromSql($"""
                    select * from ai_run
                    where status = 'RUNNING'
                      and ((task = 'PAGE_CHECK' and started_at < {pageCheckCutoff})
                        or (task = 'FIELD_EXTRACTION' and started_at < {extractionCutoff})
                        or (task = 'PAGE_REREAD' and started_at < {rereadCutoff}))
                    order by started_at, id
                    limit 1
                    for update skip locked
                    """)
                .ToListAsync(cancel))
            .FirstOrDefault();
        if (run is null)
        {
            return false;
        }

        logger.LogWarning("AI run {RunId} was left RUNNING since {StartedAt}; it is failed as {Code}", run.Id, run.StartedAt, TimeoutCode);
        await FailLockedAsync(run, TimeoutCode, cancel);
        await transaction.CommitAsync(cancel);
        db.ChangeTracker.Clear();
        return true;
    }

    private async Task<AiRun> LockRunAsync(Guid runId, CancellationToken cancel) =>
        (await db.AiRuns.FromSql($"select * from ai_run where id = {runId} for update").ToListAsync(cancel)).Single();

    private AuditEvent RunEvent(AiRun run, string eventType, object details)
    {
        var audit = AuditLog.SystemEvent(
            clock,
            run.ClaimId,
            eventType,
            "ai_run",
            run.Id.ToString(),
            after: new
            {
                run.Task,
                run.Status,
                run.ErrorCode,
                model = new { name = run.ModelName, version = run.ModelVersion },
                run.StartedAt,
                run.FinishedAt,
                result = details,
            });
        audit.AiRunId = run.Id;
        return audit;
    }
}
