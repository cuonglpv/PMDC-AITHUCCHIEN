using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Features.Evidence;
using ClaimLens.Api.Features.Lifecycle;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.AiJobs.Appliers;

/// <summary>
/// FIELD_EXTRACTION (US05). Creates a field slot for every catalogue field of each document's type, filled from the AI
/// by BR-03: HIGH is an auto-accepted suggestion, CHECK needs verification with no value in use, and below the floor
/// or not found needs entry, with the AI value kept but never shown. Each invoice item becomes an expense line built
/// only from auto-accepted values. Then the claim moves on. A failed run leaves empty slots for manual entry.
/// </summary>
public sealed class FieldExtractionApplier(ClaimLensDbContext db, FieldReadings readings, TimeProvider clock)
{
    private sealed record TypedDocument(Document Document, PageType Type, List<DocumentPage> Pages);

    /// <summary>The documents whose type has fields to read, or null when none has.</summary>
    public async Task<FieldExtractionRequest?> RequestAsync(AiRun run, CancellationToken cancel)
    {
        var documents = await TypedDocumentsAsync(run.ClaimId, cancel);
        var withFields = documents.Where(document => FieldCatalogue.For(document.Type).All.Any()).ToList();
        if (withFields.Count == 0)
        {
            return null;
        }

        var catalogue = withFields
            .SelectMany(document => FieldCatalogue.For(document.Type).All.Select(field => field.Key))
            .Distinct()
            .Order()
            .ToList();

        return new FieldExtractionRequest(
            run.Id,
            new ModelRef(run.ModelName, run.ModelVersion),
            withFields
                .Select(document => new ExtractionDocument(
                    document.Document.Id,
                    document.Pages
                        .Select(page => new TypedPageRef(page.Id, page.ImageKey, page.PageType == PageType.Unclassified ? null : page.PageType))
                        .ToList()))
                .ToList(),
            catalogue);
    }

    /// <summary>Applies the answer inside the caller's transaction, with the claim row locked. Returns what the run's audit event records.</summary>
    public async Task<object> ApplyAsync(AiRun run, FieldExtractionResponse response, CancellationToken cancel)
    {
        var claim = await LockClaimAsync(run.ClaimId, cancel);
        var documents = await TypedDocumentsAsync(run.ClaimId, cancel);
        var filled = await DocumentsWithSlotsAsync(run.ClaimId, cancel);
        var answers = response.Documents
            .GroupBy(document => document.DocumentId)
            .ToDictionary(group => group.Key, group => group.SelectMany(document => document.Fields).ToList());

        var slots = new List<ClaimField>();
        var lines = new List<ExpenseLine>();
        foreach (var document in documents.Where(document => !filled.Contains(document.Document.Id)))
        {
            var catalogue = FieldCatalogue.For(document.Type);
            if (!catalogue.All.Any())
            {
                continue;
            }

            var pageIds = document.Pages.Select(page => page.Id).ToHashSet();
            var suggestions = (answers.GetValueOrDefault(document.Document.Id) ?? [])
                .Select(FieldReadings.Checked)
                .Where(suggestion => catalogue.All.Any(field => field.Key == suggestion.FieldKey))
                .ToList();

            foreach (var field in catalogue.Header)
            {
                slots.Add(Slot(run, document.Document, field.Key, 0, FieldReadings.Best(suggestions, field.Key, 0), pageIds));
            }

            var items = suggestions
                .Where(suggestion => FieldCatalogue.IsItemField(suggestion.FieldKey) && suggestion.ItemNo is >= 1 and <= short.MaxValue)
                .Select(suggestion => (short)suggestion.ItemNo)
                .Distinct()
                .Order();
            foreach (var item in items)
            {
                var itemSlots = catalogue.Item
                    .Select(field => Slot(run, document.Document, field.Key, item, FieldReadings.Best(suggestions, field.Key, item), pageIds))
                    .ToList();
                slots.AddRange(itemSlots);

                if (FieldCatalogue.GivesExpenseLines(document.Type))
                {
                    var line = Line(document.Document, item, itemSlots);
                    lines.Add(line);
                    foreach (var slot in itemSlots)
                    {
                        slot.ExpenseLineId = line.Id;
                    }
                }
            }
        }

        db.ExpenseLines.AddRange(lines);
        db.ClaimFields.AddRange(slots);
        foreach (var accepted in slots.Where(slot => slot.Status == FieldStatus.AutoAccepted))
        {
            db.AuditEvents.Add(readings.AutoAccepted(run, accepted));
        }

        await db.SaveChangesAsync(cancel);
        var status = Finish(run, claim, await ReviewReadiness.IsReadyAsync(db, claim.Id, cancel));

        return new
        {
            documents = documents.Count(document => FieldCatalogue.For(document.Type).All.Any()),
            fields = new
            {
                autoAccepted = slots.Count(slot => slot.Status == FieldStatus.AutoAccepted),
                needsVerification = slots.Count(slot => slot.Status == FieldStatus.NeedsVerification),
                needsEntry = slots.Count(slot => slot.Status == FieldStatus.NeedsEntry),
            },
            expenseLines = lines.Count,
            failedPages = response.FailedPages,
            claimStatus = status,
        };
    }

    /// <summary>
    /// The manual fallback (US05): the run failed, so each document of a known type gets its header slots, empty and
    /// NEEDS_ENTRY, and the claim goes to NEEDS_VERIFICATION, where the reviewer enters the values.
    /// </summary>
    public async Task<object> FailAsync(AiRun run, CancellationToken cancel)
    {
        var claim = await LockClaimAsync(run.ClaimId, cancel);
        var documents = await TypedDocumentsAsync(run.ClaimId, cancel);
        var filled = await DocumentsWithSlotsAsync(run.ClaimId, cancel);

        var slots = documents
            .Where(document => !filled.Contains(document.Document.Id))
            .SelectMany(document => FieldCatalogue.For(document.Type).Header.Select(field =>
                FieldSlots.Empty(run.ClaimId, document.Document.Id, field.Key, 0)))
            .ToList();
        db.ClaimFields.AddRange(slots);
        await db.SaveChangesAsync(cancel);

        var status = Finish(run, claim, readyForReview: false);
        return new { fallback = true, emptyFields = slots.Count, claimStatus = status };
    }

    private ClaimStatus Finish(AiRun run, Claim claim, bool readyForReview)
    {
        // Only a submitted claim waits for extraction; a claim moved on some other way keeps its status.
        if (claim.Status != ClaimStatus.Processing)
        {
            return claim.Status;
        }

        var before = claim.Status;
        ClaimStatusMachine.FinishExtraction(claim, readyForReview, clock.GetUtcNow());
        var audit = AuditLog.SystemEvent(
            clock,
            claim.Id,
            "CLAIM_STATUS_CHANGED",
            "claim",
            claim.Id.ToString(),
            before: new { status = before },
            after: new { status = claim.Status });
        audit.AiRunId = run.Id;
        db.AuditEvents.Add(audit);
        return claim.Status;
    }

    private ClaimField Slot(AiRun run, Document document, FieldKey key, short itemNo, FieldSuggestion? suggestion, HashSet<Guid> pageIds)
    {
        var slot = FieldSlots.Empty(document.ClaimId, document.Id, key, itemNo);

        // The AI did not return this field: the slot is there to be entered, with no suggestion beside it.
        if (suggestion is not null)
        {
            readings.Record(slot, run, suggestion, pageIds);
            FieldReadings.TakeValue(slot);
        }

        return slot;
    }

    /// <summary>An invoice item as an expense line: only auto-accepted values go in; the rest wait for the reviewer.</summary>
    private static ExpenseLine Line(Document document, short item, List<ClaimField> slots)
    {
        var line = new ExpenseLine
        {
            Id = Guid.CreateVersion7(),
            ClaimId = document.ClaimId,
            DocumentId = document.Id,
            LineNo = item,
            Origin = LineOrigin.AiExtracted,
            MappingStatus = MappingStatus.Pending,
        };
        foreach (var slot in slots)
        {
            FieldValues.FollowOnLine(line, slot);
        }

        return line;
    }

    private async Task<Claim> LockClaimAsync(Guid claimId, CancellationToken cancel) =>
        (await db.Claims.FromSql($"select * from claim where id = {claimId} for update").ToListAsync(cancel)).Single();

    private async Task<HashSet<Guid>> DocumentsWithSlotsAsync(Guid claimId, CancellationToken cancel) =>
        (await db.ClaimFields.AsNoTracking()
            .Where(field => field.ClaimId == claimId)
            .Select(field => field.DocumentId)
            .Distinct()
            .ToListAsync(cancel))
        .ToHashSet();

    /// <summary>The claim's ready documents of a known type, in upload order, each with its pages in file order.</summary>
    private async Task<List<TypedDocument>> TypedDocumentsAsync(Guid claimId, CancellationToken cancel)
    {
        var documents = await db.Documents.AsNoTracking()
            .Where(document => document.ClaimId == claimId && document.Status == DocumentStatus.Ready)
            .OrderBy(document => document.UploadedAt)
            .ThenBy(document => document.Id)
            .ToListAsync(cancel);
        var pages = await db.DocumentPages.AsNoTracking()
            .Where(page => page.ClaimId == claimId)
            .OrderBy(page => page.PageNo)
            .ToListAsync(cancel);

        return documents
            .Select(document => (document, pages: pages.Where(page => page.DocumentId == document.Id).ToList()))
            .Select(pair => FieldCatalogue.DocumentType(pair.pages) is { } type ? new TypedDocument(pair.document, type, pair.pages) : null)
            .OfType<TypedDocument>()
            .ToList();
    }
}
