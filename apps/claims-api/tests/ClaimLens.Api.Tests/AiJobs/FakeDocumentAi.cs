using System.Collections.Concurrent;
using System.Text.Json;
using ClaimLens.Api.AiJobs;
using ClaimLens.Api.Domain.Enums;

namespace ClaimLens.Api.Tests.AiJobs;

/// <summary>
/// Stands in for the Document AI Service. Answers are set per page (page check) and per document (extraction), so runs
/// other tests left queued get the default answer and never a test's scripted one. Failures are scripted the same way.
/// </summary>
public sealed class FakeDocumentAi : IDocumentAiClient
{
    private readonly ConcurrentDictionary<Guid, PageCheckResult> pages = new();
    private readonly ConcurrentDictionary<Guid, string> failedPages = new();
    private readonly ConcurrentDictionary<Guid, List<FieldSuggestion>> fields = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<DocumentAiException>> failures = new();
    private readonly ConcurrentDictionary<Guid, List<FieldSuggestion>> rereads = new();

    public ConcurrentQueue<PageCheckRequest> PageChecks { get; } = new();

    public ConcurrentQueue<FieldExtractionRequest> Extractions { get; } = new();

    public void AnswerPage(Guid pageId, PageType type, decimal confidence, decimal quality = 0.95m) =>
        pages[pageId] = new PageCheckResult(pageId, type, confidence, quality);

    /// <summary>The service reports this page in failedPages.</summary>
    public void FailPage(Guid pageId, string code = "PAGE_READ_FAILED") => failedPages[pageId] = code;

    public void AnswerFields(Guid documentId, params FieldSuggestion[] suggestions) => fields[documentId] = [.. suggestions];

    public ConcurrentQueue<PageRereadRequest> Rereads { get; } = new();

    /// <summary>What a re-read of this page returns. A page with no answer set returns no fields.</summary>
    public void AnswerReread(Guid pageId, params FieldSuggestion[] suggestions) => rereads[pageId] = [.. suggestions];

    /// <summary>The next calls that include this page or document fail with these errors, one per call.</summary>
    public void FailNext(Guid pageOrDocumentId, params DocumentAiException[] errors)
    {
        var queue = failures.GetOrAdd(pageOrDocumentId, _ => new ConcurrentQueue<DocumentAiException>());
        foreach (var error in errors)
        {
            queue.Enqueue(error);
        }
    }

    public static DocumentAiException ModelError() => new("MODEL_ERROR", transient: true, "502 MODEL_ERROR (test)");

    public static DocumentAiException InvalidRequest() => new("INVALID_REQUEST", transient: false, "422 INVALID_REQUEST (test)");

    public Task<DocumentAiAnswer<PageCheckResponse>> RunPageCheckAsync(PageCheckRequest request, CancellationToken cancel)
    {
        PageChecks.Enqueue(request);
        ThrowIfScripted(request.Pages.Select(page => page.PageId));

        var response = new PageCheckResponse(
            request.RunId,
            request.Model,
            request.Pages
                .Where(page => !failedPages.ContainsKey(page.PageId))
                .Select(page => pages.TryGetValue(page.PageId, out var answer)
                    ? answer
                    : new PageCheckResult(page.PageId, PageType.Other, 0.5m, 0.9m))
                .ToList(),
            request.Pages
                .Where(page => failedPages.ContainsKey(page.PageId))
                .Select(page => new PageFailure(page.PageId, failedPages[page.PageId]))
                .ToList());
        return Task.FromResult(Answer(response));
    }

    public Task<DocumentAiAnswer<FieldExtractionResponse>> RunFieldExtractionAsync(FieldExtractionRequest request, CancellationToken cancel)
    {
        Extractions.Enqueue(request);
        ThrowIfScripted(request.Documents.Select(document => document.DocumentId));

        var response = new FieldExtractionResponse(
            request.RunId,
            request.Model,
            request.Documents
                .Select(document => new ExtractionDocumentResult(document.DocumentId, fields.GetValueOrDefault(document.DocumentId) ?? []))
                .ToList(),
            []);
        return Task.FromResult(Answer(response));
    }

    public Task<DocumentAiAnswer<PageRereadResponse>> RunPageRereadAsync(PageRereadRequest request, CancellationToken cancel)
    {
        Rereads.Enqueue(request);
        ThrowIfScripted([request.Page.PageId]);

        var response = new PageRereadResponse(
            request.RunId,
            request.Model,
            request.DocumentId,
            rereads.GetValueOrDefault(request.Page.PageId) ?? []);
        return Task.FromResult(Answer(response));
    }

    private void ThrowIfScripted(IEnumerable<Guid> ids)
    {
        foreach (var id in ids)
        {
            if (failures.TryGetValue(id, out var queue) && queue.TryDequeue(out var error))
            {
                throw error;
            }
        }
    }

    private static DocumentAiAnswer<T> Answer<T>(T body) => new(body, JsonSerializer.SerializeToUtf8Bytes(body, DocumentAiJson.Options));
}
