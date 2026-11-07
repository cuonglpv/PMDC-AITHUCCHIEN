using System.Globalization;
using ClaimLens.Api.AiJobs;
using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.DocumentStore;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.DocumentPreparation;

/// <summary>
/// Prepares one uploaded file: converts a DOCX or XLSX file to PDF, draws its pages, numbers them across the claim and
/// marks it READY with a PAGE_CHECK queued, or REJECTED with a reason. The worker calls it in a loop; tests call it
/// directly.
/// </summary>
public sealed class DocumentPreparer(
    ClaimLensDbContext db,
    IDocumentStore store,
    IDocumentConverter converter,
    PageRenderer renderer,
    PreparationAttempts attempts,
    AiJobQueue ai,
    IOptions<LimitsOptions> limits,
    TimeProvider clock,
    ILogger<DocumentPreparer> logger)
{
    /// <summary>What the officer reads when a file failed for reasons outside it until no attempt was left.</summary>
    public const string GaveUpReason = "The file could not be processed. Upload it again.";

    /// <summary>
    /// True when a document was prepared, rejected or put back to wait after a failure; false when none was waiting.
    /// </summary>
    public async Task<bool> PrepareNextAsync(CancellationToken cancel)
    {
        // The row lock is held through the conversion. If the process dies, the lock dies with it and the next pass
        // retries the file, so no column records who is working on it (docs/10-code-structure.md, section 4).
        // A file waiting out a failure is skipped, so it does not hold up the files behind it.
        var waiting = attempts.Waiting();
        var transaction = await db.Database.BeginTransactionAsync(cancel);
        Document? document = null;
        try
        {
            document = (await db.Documents
                    .FromSql($"""
                        select * from document
                        where status = 'CONVERTING' and id <> all({waiting})
                        order by uploaded_at, id
                        limit 1
                        for update skip locked
                        """)
                    .ToListAsync(cancel))
                .FirstOrDefault();
            if (document is null)
            {
                return false;
            }

            var outcome = await PrepareAsync(document, cancel);
            if (outcome is Rejected rejected)
            {
                Reject(document, rejected.Reason);
            }
            else if (await NumberAndStoreAsync(document, (Prepared)outcome, cancel) is { } overLimit)
            {
                Reject(document, overLimit);
            }

            await db.SaveChangesAsync(cancel);
            await transaction.CommitAsync(cancel);
            attempts.Done(document.Id);
            return true;
        }
        catch (Exception error) when (document is not null && !(error is OperationCanceledException && cancel.IsCancellationRequested))
        {
            // Not the file's fault, as far as the API can tell: the converter or storage is down, or the database
            // refused the write. The file waits and the worker goes on; past the last attempt it is rejected.
            await transaction.DisposeAsync();
            db.ChangeTracker.Clear();
            if (attempts.Failed(document.Id))
            {
                logger.LogWarning(error, "Document {DocumentId} failed on its last attempt and is rejected", document.Id);
                await GiveUpAsync(document.Id, cancel);
            }
            else
            {
                logger.LogWarning(error, "Document {DocumentId} could not be prepared; it will be tried again", document.Id);
            }

            return true;
        }
        finally
        {
            await transaction.DisposeAsync();
        }
    }

    private async Task GiveUpAsync(Guid documentId, CancellationToken cancel)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancel);
        var document = (await db.Documents
                .FromSql($"select * from document where id = {documentId} and status = 'CONVERTING' for update skip locked")
                .ToListAsync(cancel))
            .FirstOrDefault();
        if (document is null)
        {
            // Removed by the officer, or another worker has it now.
            return;
        }

        Reject(document, GaveUpReason);
        await db.SaveChangesAsync(cancel);
        await transaction.CommitAsync(cancel);
    }

    private abstract record Outcome;

    private sealed record Prepared(IReadOnlyList<RenderedPage> Pages, byte[]? Rendition) : Outcome;

    private sealed record Rejected(string Reason) : Outcome;

    private async Task<Outcome> PrepareAsync(Document document, CancellationToken cancel)
    {
        var original = await store.GetAsync(document.OriginalKey, cancel);
        if (original is null)
        {
            return new Rejected("The uploaded file is no longer in storage. Upload it again.");
        }

        try
        {
            switch (document.FileFormat)
            {
                case FileFormat.Jpg or FileFormat.Png:
                    return new Prepared([renderer.RenderImage(original)], null);

                case FileFormat.Pdf:
                    return await PdfAsync(document, original, rendition: null, cancel);

                default:
                    var pdf = await converter.ToPdfAsync(original, document.FileFormat, cancel);
                    return await PdfAsync(document, pdf, rendition: pdf, cancel);
            }
        }
        catch (ConversionFailedException failed)
        {
            logger.LogInformation("Document {DocumentId} was not converted: {Reason}", document.Id, failed.Message);
            return new Rejected(failed.Message);
        }
        catch (UnreadableFileException unreadable)
        {
            logger.LogInformation(unreadable.InnerException, "Document {DocumentId} could not be drawn", document.Id);
            return new Rejected(unreadable.Message);
        }
    }

    private async Task<Outcome> PdfAsync(Document document, byte[] pdf, byte[]? rendition, CancellationToken cancel)
    {
        // A first look before drawing anything, so a 500-page file is refused without rendering it. The count is
        // checked again under the claim's lock.
        var count = renderer.PdfPageCount(pdf);
        if (count == 0)
        {
            return new Rejected("The file has no pages.");
        }

        var used = await db.DocumentPages.CountAsync(page => page.ClaimId == document.ClaimId, cancel);
        if (OverLimit(used, count) is { } reason)
        {
            return new Rejected(reason);
        }

        return new Prepared(renderer.RenderPdf(pdf), rendition);
    }

    /// <summary>Null when the pages were numbered and stored, or the reason the claim cannot take them.</summary>
    private async Task<string?> NumberAndStoreAsync(Document document, Prepared prepared, CancellationToken cancel)
    {
        // Numbering reads the claim's pages and adds to them. The claim row's lock keeps two files of one claim, and a
        // removal renumbering the rest, from taking the same numbers.
        await db.Database.ExecuteSqlAsync($"select 1 from claim where id = {document.ClaimId} for update", cancel);
        var last = await db.DocumentPages
            .Where(page => page.ClaimId == document.ClaimId)
            .MaxAsync(page => (int?)page.ClaimPageNo, cancel) ?? 0;
        if (OverLimit(last, prepared.Pages.Count) is { } reason)
        {
            return reason;
        }

        if (prepared.Rendition is { } pdf)
        {
            document.RenditionKey = StorageKeys.Rendition(document.ClaimId, document.Id);
            await store.PutAsync(document.RenditionKey, pdf, "application/pdf", cancel);
        }

        var run = ai.PageCheck(db, document.ClaimId, document.Id);
        for (var index = 0; index < prepared.Pages.Count; index++)
        {
            var image = prepared.Pages[index];
            var pageNo = index + 1;
            var key = StorageKeys.Page(document.ClaimId, document.Id, pageNo);

            // The same keys on a retry, so a pass that dies after storing leaves nothing behind but what it overwrites.
            await store.PutAsync(key, image.Png, "image/png", cancel);
            await store.PutAsync(StorageKeys.ThumbnailOf(key), image.ThumbnailPng, "image/png", cancel);

            db.DocumentPages.Add(new DocumentPage
            {
                Id = Guid.CreateVersion7(),
                DocumentId = document.Id,
                ClaimId = document.ClaimId,
                PageNo = (short)pageNo,
                ClaimPageNo = (short)(last + pageNo),
                ImageKey = key,
                WidthPx = image.Width,
                HeightPx = image.Height,
                AiRunId = run.Id,
            });
        }

        document.Status = DocumentStatus.Ready;
        document.PageCount = (short)prepared.Pages.Count;
        db.AuditEvents.Add(SystemEvent(
            document,
            "DOCUMENT_READY",
            new { status = DocumentStatus.Ready, pageCount = document.PageCount, pageCheckRunId = run.Id }));
        return null;
    }

    private string? OverLimit(int used, int adding)
    {
        var limit = limits.Value.MaxPagesPerClaim;
        return used + adding <= limit
            ? null
            : string.Create(
                CultureInfo.InvariantCulture,
                $"This file has {adding} {(adding == 1 ? "page" : "pages")} and the claim already has {used}. A claim can have at most {limit} pages.");
    }

    private void Reject(Document document, string reason)
    {
        // rejection_reason is varchar(200).
        document.Status = DocumentStatus.Rejected;
        document.RejectionReason = reason.Length <= 200 ? reason : reason[..200];
        document.RenditionKey = null;
        document.PageCount = null;
        db.AuditEvents.Add(SystemEvent(document, "DOCUMENT_REJECTED", new { status = DocumentStatus.Rejected, reason = document.RejectionReason }));
    }

    private AuditEvent SystemEvent(Document document, string eventType, object after) =>
        AuditLog.SystemEvent(clock, document.ClaimId, eventType, "document", document.Id.ToString(), after: after);
}
