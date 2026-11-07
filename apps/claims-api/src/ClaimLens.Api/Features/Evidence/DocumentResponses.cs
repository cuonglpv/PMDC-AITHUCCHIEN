using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Features.RuleSets;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Evidence;

public sealed record PageAiSuggestion(PageType PageType, decimal Confidence, ConfidenceLevel Level, decimal QualityScore, Guid RunId);

public sealed record DocumentPageResponse(
    Guid Id,
    Guid DocumentId,
    int PageNo,
    int ClaimPageNo,
    int WidthPx,
    int HeightPx,
    PageType PageType,
    ValueSource? PageTypeSource,
    StaffRef? PageTypeSetBy,
    PageQuality Quality,
    PageAiStatus AiStatus,
    PageAiSuggestion? Ai);

/// <summary>A file with its pages. Storage keys stay inside the API (docs/07-api-contract.md).</summary>
public sealed record DocumentResponse(
    Guid Id,
    string OriginalFilename,
    FileFormat FileFormat,
    long SizeBytes,
    int? PageCount,
    DocumentStatus Status,
    string? RejectionReason,
    StaffRef UploadedBy,
    DateTimeOffset UploadedAt,
    IReadOnlyList<DocumentPageResponse> Pages);

public sealed record DocumentList(IReadOnlyList<DocumentResponse> Items);

public static class DocumentResponses
{
    /// <summary>The claim's files in upload order, without removed ones, each with its pages in file order.</summary>
    public static async Task<List<DocumentResponse>> ForClaimAsync(ClaimLensDbContext db, Guid claimId, CancellationToken cancel)
    {
        var documents = await db.Documents.AsNoTracking()
            .Where(document => document.ClaimId == claimId && document.Status != DocumentStatus.Removed)
            .OrderBy(document => document.UploadedAt)
            .ThenBy(document => document.Id)
            .ToListAsync(cancel);
        var pages = await db.DocumentPages.AsNoTracking()
            .Where(page => page.ClaimId == claimId)
            .OrderBy(page => page.PageNo)
            .ToListAsync(cancel);

        return await ToResponsesAsync(db, documents, pages, cancel);
    }

    /// <summary>One page, as reclassifyPage answers it.</summary>
    public static async Task<DocumentPageResponse> PageAsync(ClaimLensDbContext db, DocumentPage page, CancellationToken cancel)
    {
        var staff = await db.StaffAccounts.AsNoTracking()
            .Where(account => account.Id == page.PageTypeSetBy)
            .Select(account => new StaffRef(account.Id, account.FullName, account.Initials))
            .ToDictionaryAsync(account => account.Id, cancel);
        return ToResponse(page, staff);
    }

    public static async Task<DocumentResponse> OneAsync(ClaimLensDbContext db, Document document, CancellationToken cancel) =>
        (await ToResponsesAsync(db, [document], [], cancel))[0];

    private static async Task<List<DocumentResponse>> ToResponsesAsync(
        ClaimLensDbContext db,
        IReadOnlyList<Document> documents,
        IReadOnlyList<DocumentPage> pages,
        CancellationToken cancel)
    {
        var staffIds = documents.Select(document => document.UploadedBy)
            .Concat(pages.Select(page => page.PageTypeSetBy).OfType<Guid>())
            .Distinct()
            .ToList();
        var staff = await db.StaffAccounts.AsNoTracking()
            .Where(account => staffIds.Contains(account.Id))
            .Select(account => new StaffRef(account.Id, account.FullName, account.Initials))
            .ToDictionaryAsync(account => account.Id, cancel);

        return documents
            .Select(document => new DocumentResponse(
                document.Id,
                document.OriginalFilename,
                document.FileFormat,
                document.SizeBytes,
                document.PageCount,
                document.Status,
                document.RejectionReason,
                staff[document.UploadedBy],
                document.UploadedAt,
                pages.Where(page => page.DocumentId == document.Id).Select(page => ToResponse(page, staff)).ToList()))
            .ToList();
    }

    private static DocumentPageResponse ToResponse(DocumentPage page, IReadOnlyDictionary<Guid, StaffRef> staff) =>
        new(
            page.Id,
            page.DocumentId,
            page.PageNo,
            page.ClaimPageNo,
            page.WidthPx,
            page.HeightPx,
            page.PageType,
            page.PageTypeSource,
            page.PageTypeSetBy is { } setBy ? staff[setBy] : null,
            page.Quality,
            page.AiStatus,
            Suggestion(page));

    /// <summary>The page check's answer, once a run has given one (US04). Queued pages have none.</summary>
    private static PageAiSuggestion? Suggestion(DocumentPage page) =>
        page is { AiPageType: { } type, AiTypeConfidence: { } confidence, AiTypeLevel: { } level, AiQualityScore: { } quality, AiRunId: { } run }
            ? new PageAiSuggestion(type, confidence, level, quality, run)
            : null;
}
