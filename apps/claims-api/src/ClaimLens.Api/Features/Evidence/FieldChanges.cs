using System.Text.Json;
using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Features.Lifecycle;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ClaimLens.Api.Features.Evidence;

/// <summary>A region the reviewer drew on a page.</summary>
public sealed record DrawnSource(Guid PageId, FieldBox Box);

/// <summary>
/// What updateField and acceptField share. Each holds the claim row for its transaction, which orders it against the
/// other edits and the AI Job Runner's applier.
/// </summary>
public static class FieldChanges
{
    /// <summary>The claim's row, locked. Null when its status no longer allows an edit: it may have moved since the middleware looked.</summary>
    public static async Task<Claim?> LockEditableAsync(ClaimLensDbContext db, Guid claimId, CancellationToken cancel)
    {
        var claim = (await db.Claims.FromSql($"select * from claim where id = {claimId} for update").ToListAsync(cancel)).Single();
        return claim.Status is ClaimStatus.NeedsVerification or ClaimStatus.InReview ? claim : null;
    }

    public static IResult StateConflict(HttpContext context) =>
        Problems.Of(context, StatusCodes.Status409Conflict, ErrorCodes.ClaimStateConflict, "The claim's status does not allow this");

    /// <summary>A field of this claim. A field of another claim, or of a line that was removed, is not found.</summary>
    public static async Task<ClaimField?> FindAsync(ClaimLensDbContext db, Guid claimId, string fieldId, CancellationToken cancel)
    {
        if (!Guid.TryParse(fieldId, out var id))
        {
            return null;
        }

        return await db.ClaimFields
            .Where(field => field.Id == id && field.ClaimId == claimId
                && (field.ExpenseLineId == null || db.ExpenseLines.Any(line => line.Id == field.ExpenseLineId && line.RemovedAt == null)))
            .SingleOrDefaultAsync(cancel);
    }

    /// <summary>409 with the field as it is now, so the Conflict state can keep the reviewer's input and show what changed.</summary>
    public static async Task<IResult> VersionConflictAsync(HttpContext context, ClaimLensDbContext db, ClaimField field, CancellationToken cancel) =>
        Problems.Of(
            context,
            StatusCodes.Status409Conflict,
            ErrorCodes.VersionConflict,
            "Someone else changed this field first",
            details: new { currentRowVersion = field.RowVersion, current = await FieldResponses.OneAsync(db, field, cancel) });

    /// <summary>Reads a drawn source from a request: both parts or neither, and a region that lies on the page.</summary>
    public static FieldError? ReadDrawn(Guid? pageId, FieldBox? box, out DrawnSource? drawn)
    {
        drawn = null;
        if (pageId is null && box is null)
        {
            return null;
        }

        if (pageId is not { } page)
        {
            return new FieldError("sourcePageId", "REQUIRED", "Say which page the region is on.");
        }

        if (box is null)
        {
            return new FieldError("sourceBbox", "REQUIRED", "Draw the region on the page.");
        }

        if (!box.LiesOnThePage)
        {
            return new FieldError("sourceBbox", "OUT_OF_RANGE", "The region must lie on the page.");
        }

        drawn = new DrawnSource(page, box);
        return null;
    }

    /// <summary>
    /// BR-02: a value in use has a source. The drawn region wins. Without one, a value edited by hand keeps the source it
    /// has, or else the AI's; an accepted AI value takes the AI's first, because that is where it was read. Neither is 422.
    /// </summary>
    public static async Task<IResult?> SetSourceAsync(
        HttpContext context,
        ClaimLensDbContext db,
        ClaimField field,
        DrawnSource? drawn,
        bool aiSourceFirst,
        CancellationToken cancel)
    {
        if (drawn is not null)
        {
            var onDocument = await db.DocumentPages.AsNoTracking()
                .AnyAsync(page => page.Id == drawn.PageId && page.ClaimId == field.ClaimId && page.DocumentId == field.DocumentId, cancel);
            if (!onDocument)
            {
                return Requests.Unreadable(context, new FieldError("sourcePageId", "INVALID", "Draw the region on a page of this field's document."));
            }

            field.SourcePageId = drawn.PageId;
            field.SourceBbox = drawn.Box.ToJson();
            return null;
        }

        var hasAiSource = field is { AiPageId: not null, AiBbox: not null };
        var hasSource = field is { SourcePageId: not null, SourceBbox: not null };
        if (hasAiSource && (aiSourceFirst || !hasSource))
        {
            field.SourcePageId = field.AiPageId;
            field.SourceBbox = field.AiBbox;
            return null;
        }

        return hasSource
            ? null
            : Problems.Of(context, StatusCodes.Status422UnprocessableEntity, ErrorCodes.SourceRequired, "A value needs a source region");
    }

    /// <summary>Puts a person's decision on the field and on the expense line it feeds. A null value is NOT_PRESENT.</summary>
    public static async Task ResolveAsync(
        ClaimLensDbContext db,
        ClaimField field,
        FieldStatus status,
        FieldValue? value,
        Guid staffId,
        DateTimeOffset now,
        CancellationToken cancel)
    {
        field.Status = status;
        field.ValueText = value?.Text;
        field.ValueAmount = value?.Amount;
        field.ValueDate = value?.Date;
        field.ResolvedBy = staffId;
        field.ResolvedAt = now;
        field.RowVersion++;
        if (value is null)
        {
            field.SourcePageId = null;
            field.SourceBbox = null;
        }

        if (field.ExpenseLineId is { } lineId)
        {
            FieldValues.FollowOnLine(await db.ExpenseLines.SingleAsync(line => line.Id == lineId, cancel), field);
        }
    }

    /// <summary>The audit event of the write, the move it may cause, the commit, and the answer every field change gives.</summary>
    public static async Task<IResult> FinishAsync(
        HttpContext context,
        ClaimLensDbContext db,
        IDbContextTransaction transaction,
        Claim claim,
        ClaimField field,
        object before,
        Guid staffId,
        DateTimeOffset now,
        CancellationToken cancel)
    {
        db.AuditEvents.Add(AuditLog.Event(
            context,
            ActorType.User,
            staffId,
            claim.Id,
            "FIELD_" + JsonNamingPolicy.SnakeCaseUpper.ConvertName(field.Status.ToString()),
            "claim_field",
            field.Id.ToString(),
            before: before,
            after: Snapshot(field)));

        await ClaimProgression.AfterChangeAsync(db, claim, now, ClaimProgression.ByPerson(context, staffId, claim.Id), cancel);
        await transaction.CommitAsync(cancel);

        return Results.Ok(new FieldChangeResult(
            await FieldResponses.OneAsync(db, field, cancel),
            await ClaimResponses.ProgressAsync(db, claim, cancel)));
    }

    /// <summary>What the audit event keeps of a field's value in use (US06: old value, new value). A national ID is masked.</summary>
    public static object Snapshot(ClaimField field) =>
        new
        {
            field.DocumentId,
            field.FieldKey,
            field.ItemNo,
            field.Status,
            value = FieldResponses.Shown(field.FieldKey, field.ValueText),
            amount = field.ValueAmount,
            date = field.ValueDate,
            field.SourcePageId,
            sourceBbox = FieldBox.Parse(field.SourceBbox),
            field.ResolvedBy,
        };
}

/// <summary>Empty field slots, waiting for a person to enter a value.</summary>
public static class FieldSlots
{
    public static ClaimField Empty(Guid claimId, Guid documentId, FieldKey key, short itemNo, Guid? lineId = null) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            ClaimId = claimId,
            DocumentId = documentId,
            FieldKey = key,
            ItemNo = itemNo,
            ExpenseLineId = lineId,
            Status = FieldStatus.NeedsEntry,
        };

    /// <summary>
    /// The header slots these types have and the document does not, added to the context. Existing slots stay as they
    /// are, whatever type they came from (US06). Items repeat, so their slots arrive with their line or the AI's reading.
    /// </summary>
    public static async Task<List<ClaimField>> AddMissingHeaderAsync(
        ClaimLensDbContext db,
        Guid claimId,
        Guid documentId,
        IEnumerable<PageType> types,
        CancellationToken cancel)
    {
        var existing = await db.ClaimFields.AsNoTracking()
            .Where(field => field.DocumentId == documentId && field.ItemNo == 0)
            .Select(field => field.FieldKey)
            .ToListAsync(cancel);

        var added = types
            .SelectMany(type => FieldCatalogue.For(type).Header)
            .Select(field => field.Key)
            .Distinct()
            .Except(existing)
            .Select(key => Empty(claimId, documentId, key, 0))
            .ToList();
        db.ClaimFields.AddRange(added);
        return added;
    }
}
