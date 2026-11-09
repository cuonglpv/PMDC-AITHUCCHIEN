using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Evidence;

/// <summary>What is left to do on a claim's evidence. The required slots are those of the document's current type.</summary>
public sealed record ClaimEvidence(bool IsReady, int FieldsToVerify, int PagesToClassify, IReadOnlySet<Guid> RequiredFields);

/// <summary>
/// Whether a claim's evidence lets it be READY_FOR_REVIEW (docs/06-data-model.md, section 7): every required field slot
/// resolved and no page left UNCLASSIFIED. Read from the rows, so the same answer holds after a person resolves a field.
/// The slots of a removed expense line no longer count.
/// </summary>
public static class ReviewReadiness
{
    private sealed record Slot(Guid Id, Guid ClaimId, Guid DocumentId, FieldKey FieldKey, short ItemNo, FieldStatus Status);

    public static async Task<bool> IsReadyAsync(ClaimLensDbContext db, Guid claimId, CancellationToken cancel) =>
        (await ForClaimAsync(db, claimId, cancel)).IsReady;

    public static async Task<ClaimEvidence> ForClaimAsync(ClaimLensDbContext db, Guid claimId, CancellationToken cancel) =>
        (await ForClaimsAsync(db, [claimId], cancel))[claimId];

    /// <summary>The same for a page of a queue, in a few queries rather than a few per claim.</summary>
    public static async Task<IReadOnlyDictionary<Guid, ClaimEvidence>> ForClaimsAsync(
        ClaimLensDbContext db,
        IReadOnlyCollection<Guid> claimIds,
        CancellationToken cancel)
    {
        var ids = claimIds.Distinct().ToList();
        var ready = await db.Documents.AsNoTracking()
            .Where(document => ids.Contains(document.ClaimId) && document.Status == DocumentStatus.Ready)
            .Select(document => document.Id)
            .ToListAsync(cancel);
        var pages = await db.DocumentPages.AsNoTracking()
            .Where(page => ids.Contains(page.ClaimId) && ready.Contains(page.DocumentId))
            .ToListAsync(cancel);
        var slots = await db.ClaimFields.AsNoTracking()
            .Where(field => ids.Contains(field.ClaimId)
                && (field.ExpenseLineId == null || db.ExpenseLines.Any(line => line.Id == field.ExpenseLineId && line.RemovedAt == null)))
            .Select(field => new Slot(field.Id, field.ClaimId, field.DocumentId, field.FieldKey, field.ItemNo, field.Status))
            .ToListAsync(cancel);

        return ids.ToDictionary(
            id => id,
            id => Evaluate(
                pages.Where(page => page.ClaimId == id).ToList(),
                slots.Where(slot => slot.ClaimId == id).ToList()));
    }

    private static ClaimEvidence Evaluate(List<Domain.Entities.DocumentPage> pages, List<Slot> slots)
    {
        var pagesToClassify = pages.Count(page => page.PageType == PageType.Unclassified);
        var ready = pagesToClassify == 0;
        var fieldsToVerify = 0;
        var required = new HashSet<Guid>();

        foreach (var document in pages.GroupBy(page => page.DocumentId))
        {
            if (FieldCatalogue.DocumentType(document) is not { } type)
            {
                ready = false;
                continue;
            }

            var ofDocument = slots.Where(slot => slot.DocumentId == document.Key).ToList();
            foreach (var slot in ofDocument.Where(slot => FieldCatalogue.IsRequired(type, slot.FieldKey, slot.ItemNo)))
            {
                required.Add(slot.Id);
                if (!FieldCatalogue.IsResolved(slot.Status))
                {
                    fieldsToVerify++;
                }
            }

            bool Resolved(FieldKey key, short itemNo) =>
                ofDocument.Any(slot => slot.FieldKey == key && slot.ItemNo == itemNo && FieldCatalogue.IsResolved(slot.Status));

            var catalogue = FieldCatalogue.For(type);
            var items = ofDocument.Where(slot => slot.ItemNo > 0).Select(slot => slot.ItemNo).Distinct();
            if (catalogue.Header.Any(field => field.Required && !Resolved(field.Key, 0))
                || items.Any(item => catalogue.Item.Any(field => field.Required && !Resolved(field.Key, item))))
            {
                ready = false;
            }
        }

        return new ClaimEvidence(ready, fieldsToVerify, pagesToClassify, required);
    }
}
