using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.DocumentStore;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClaimLens.Api.Features.Intake;

/// <summary>
/// US03: a file is removed before submission. The row stays as REMOVED with its original; its pages and their images
/// go, and the claim's other pages close the gap so they stay p1 to pN.
/// </summary>
public static class DeleteDocument
{
    private const string LockNotAvailable = "55P03";

    public static async Task<IResult> Handle(
        HttpContext context,
        ClaimLensDbContext db,
        IDocumentStore store,
        TimeProvider clock,
        ILoggerFactory logs,
        string documentId,
        CancellationToken cancel)
    {
        if (!Guid.TryParse(documentId, out var id))
        {
            return Requests.NotFound(context);
        }

        var claimId = ClaimAccess.Of(context).ClaimId;
        await using var transaction = await db.Database.BeginTransactionAsync(cancel);

        // The preparation worker holds this lock while it converts the file. Waiting for it could take a minute, so the
        // officer is told the file is still being processed instead.
        Domain.Entities.Document? document;
        try
        {
            document = (await db.Documents
                    .FromSql($"select * from document where id = {id} and claim_id = {claimId} for update nowait")
                    .ToListAsync(cancel))
                .SingleOrDefault();
        }
        catch (Exception error) when (IsLockNotAvailable(error))
        {
            return Problems.Of(context, StatusCodes.Status409Conflict, ErrorCodes.UploadInProgress, "This file is still being processed");
        }

        if (document is null || document.Status == DocumentStatus.Removed)
        {
            return Requests.NotFound(context);
        }

        // Renumbering reads and rewrites the claim's pages; the claim row's lock keeps the worker from numbering at the
        // same time, and a submission from slipping in.
        var claim = (await db.Claims.FromSql($"select * from claim where id = {claimId} for update").AsNoTracking().ToListAsync(cancel)).Single();
        if (claim.Status != ClaimStatus.Draft)
        {
            return Problems.Of(context, StatusCodes.Status409Conflict, ErrorCodes.ClaimStateConflict, "The claim's status does not allow this");
        }

        var removed = await db.DocumentPages.AsNoTracking()
            .Where(page => page.DocumentId == document.Id)
            .Select(page => page.ImageKey)
            .ToListAsync(cancel);
        await db.DocumentPages.Where(page => page.DocumentId == document.Id).ExecuteDeleteAsync(cancel);
        await RenumberAsync(db, claimId, cancel);

        context.User.TryGetStaff(out var staffId, out _);
        var before = document.Status;
        document.Status = DocumentStatus.Removed;
        document.RemovedBy = staffId;
        document.RemovedAt = clock.GetUtcNow();
        db.AuditEvents.Add(AuditLog.Event(
            context,
            ActorType.User,
            staffId,
            claimId,
            "DOCUMENT_REMOVED",
            "document",
            document.Id.ToString(),
            before: new { status = before, pageCount = removed.Count },
            after: new { status = document.Status }));
        await db.SaveChangesAsync(cancel);
        await transaction.CommitAsync(cancel);

        // After the commit, so a failed delete leaves unused images rather than pages with no image.
        try
        {
            await store.DeleteAsync(removed.SelectMany(key => new[] { key, StorageKeys.ThumbnailOf(key) }).ToList(), cancel);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logs.CreateLogger(typeof(DeleteDocument)).LogWarning(error, "Could not delete the page images of removed document {DocumentId}", document.Id);
        }

        return Results.NoContent();
    }

    /// <summary>Npgsql's execution strategy reports 55P03 as a possibly transient failure, wrapped once.</summary>
    private static bool IsLockNotAvailable(Exception error) =>
        error is PostgresException { SqlState: LockNotAvailable }
        || error.InnerException is PostgresException { SqlState: LockNotAvailable };

    /// <summary>
    /// Closes the gaps one page at a time, lowest first. Each page moves down into a number that is already free, so
    /// neither ux_document_page_claim_no nor ck_page_limit_30 is broken on the way, and no number goes below 1.
    /// </summary>
    private static async Task RenumberAsync(ClaimLensDbContext db, Guid claimId, CancellationToken cancel)
    {
        var pages = await db.DocumentPages.AsNoTracking()
            .Where(page => page.ClaimId == claimId)
            .OrderBy(page => page.ClaimPageNo)
            .Select(page => new { page.Id, page.ClaimPageNo })
            .ToListAsync(cancel);

        for (var index = 0; index < pages.Count; index++)
        {
            var number = (short)(index + 1);
            if (pages[index].ClaimPageNo != number)
            {
                await db.Database.ExecuteSqlAsync($"update document_page set claim_page_no = {number} where id = {pages[index].Id}", cancel);
            }
        }
    }
}
