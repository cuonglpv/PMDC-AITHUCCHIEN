using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.AiJobs.Appliers;

/// <summary>
/// PAGE_CHECK (US04): stores the AI's type and quality beside the type in use. The AI's type is used only at or above
/// the page-type threshold, and never over a type the officer chose. A page the service could not read is FAILED and
/// is set by hand.
/// </summary>
public sealed class PageCheckApplier(ClaimLensDbContext db, Thresholds thresholds)
{
    /// <summary>The pages of the run's file, or null when it has none left (the file was removed).</summary>
    public async Task<PageCheckRequest?> RequestAsync(AiRun run, CancellationToken cancel)
    {
        var pages = await db.DocumentPages.AsNoTracking()
            .Where(page => page.DocumentId == run.ScopeDocumentId && page.ClaimId == run.ClaimId)
            .OrderBy(page => page.PageNo)
            .Select(page => new PageRef(page.Id, page.ImageKey))
            .ToListAsync(cancel);

        return pages.Count == 0 ? null : new PageCheckRequest(run.Id, new ModelRef(run.ModelName, run.ModelVersion), pages);
    }

    /// <summary>Applies the answer inside the caller's transaction. Returns what the audit event records.</summary>
    public async Task<object> ApplyAsync(AiRun run, PageCheckResponse response, CancellationToken cancel)
    {
        var pages = await LockPagesAsync(run, cancel);
        var failed = response.FailedPages.Select(failure => failure.PageId).ToHashSet();
        var results = new Dictionary<Guid, PageCheckResult>();
        foreach (var result in response.Pages)
        {
            Check(result);
            results.TryAdd(result.PageId, result);
        }

        foreach (var page in pages)
        {
            page.AiRunId = run.Id;
            if (failed.Contains(page.Id) || !results.TryGetValue(page.Id, out var result))
            {
                // Not read: the page stays as it is, and a person sets its type.
                page.AiStatus = PageAiStatus.Failed;
                continue;
            }

            var level = thresholds.PageType(result.TypeConfidence);
            page.AiPageType = result.PageType;
            page.AiTypeConfidence = Thresholds.Stored(result.TypeConfidence);
            page.AiTypeLevel = level;
            page.AiQualityScore = Thresholds.Stored(result.QualityScore);
            page.Quality = thresholds.IsLowQuality(result.QualityScore) ? PageQuality.LowQuality : PageQuality.Readable;
            page.AiStatus = PageAiStatus.Done;

            if (page.PageTypeSource != ValueSource.User)
            {
                page.PageType = level == ConfidenceLevel.High ? result.PageType : PageType.Unclassified;
                page.PageTypeSource = level == ConfidenceLevel.High ? ValueSource.Ai : null;
            }
        }

        return new { pages = pages.Select(Summary).ToList() };
    }

    /// <summary>The run failed: every page it covered is FAILED and keeps its type, Unclassified unless the officer chose one.</summary>
    public async Task<object> FailAsync(AiRun run, CancellationToken cancel)
    {
        var pages = await LockPagesAsync(run, cancel);
        foreach (var page in pages)
        {
            page.AiStatus = PageAiStatus.Failed;
        }

        return new { pages = pages.Select(Summary).ToList() };
    }

    /// <summary>Back to QUEUED for another attempt.</summary>
    public async Task RequeueAsync(AiRun run, CancellationToken cancel)
    {
        foreach (var page in await LockPagesAsync(run, cancel))
        {
            page.AiStatus = PageAiStatus.Queued;
        }
    }

    /// <summary>The run is RUNNING: its pages show as being read.</summary>
    public Task StartAsync(AiRun run, CancellationToken cancel) =>
        db.DocumentPages
            .Where(page => page.DocumentId == run.ScopeDocumentId && page.ClaimId == run.ClaimId)
            .ExecuteUpdateAsync(set => set.SetProperty(page => page.AiStatus, PageAiStatus.Reading), cancel);

    /// <summary>
    /// The officer may choose a type while the check runs; with the rows locked, whichever writes second sees the
    /// other's change, so a type the officer chose is never overwritten.
    /// </summary>
    private async Task<List<DocumentPage>> LockPagesAsync(AiRun run, CancellationToken cancel) =>
        await db.DocumentPages
            .FromSql($"""
                select * from document_page
                where document_id = {run.ScopeDocumentId} and claim_id = {run.ClaimId}
                order by page_no
                for update
                """)
            .ToListAsync(cancel);

    private static void Check(PageCheckResult result)
    {
        if (result.PageType == PageType.Unclassified
            || result.TypeConfidence is < 0 or > 1
            || result.QualityScore is < 0 or > 1)
        {
            throw new DocumentAiException(DocumentAiException.BadResponse, transient: true, "A page result breaks the contract.");
        }
    }

    private static object Summary(DocumentPage page) =>
        new
        {
            pageId = page.Id,
            page.AiStatus,
            page.AiPageType,
            confidence = page.AiTypeConfidence,
            level = page.AiTypeLevel,
            page.AiQualityScore,
            page.Quality,
            page.PageType,
            page.PageTypeSource,
        };
}
