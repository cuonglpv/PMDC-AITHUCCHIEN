using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Evidence;

/// <summary>
/// Whether a claim's evidence lets it be READY_FOR_REVIEW (docs/06-data-model.md, section 7): every required field slot
/// resolved and no page left UNCLASSIFIED. Read from the rows, so the same answer holds after a person resolves a field.
/// </summary>
public static class ReviewReadiness
{
    public static async Task<bool> IsReadyAsync(ClaimLensDbContext db, Guid claimId, CancellationToken cancel)
    {
        var ready = await db.Documents.AsNoTracking()
            .Where(document => document.ClaimId == claimId && document.Status == DocumentStatus.Ready)
            .Select(document => document.Id)
            .ToListAsync(cancel);
        var pages = await db.DocumentPages.AsNoTracking()
            .Where(page => page.ClaimId == claimId && ready.Contains(page.DocumentId))
            .ToListAsync(cancel);
        if (pages.Any(page => page.PageType == PageType.Unclassified))
        {
            return false;
        }

        var fields = await db.ClaimFields.AsNoTracking()
            .Where(field => field.ClaimId == claimId)
            .Select(field => new { field.DocumentId, field.FieldKey, field.ItemNo, field.Status })
            .ToListAsync(cancel);

        foreach (var document in pages.GroupBy(page => page.DocumentId))
        {
            if (FieldCatalogue.DocumentType(document) is not { } type)
            {
                return false;
            }

            var catalogue = FieldCatalogue.For(type);
            var slots = fields.Where(field => field.DocumentId == document.Key).ToList();
            bool Resolved(FieldKey key, short itemNo) =>
                slots.Any(slot => slot.FieldKey == key && slot.ItemNo == itemNo && FieldCatalogue.IsResolved(slot.Status));

            if (catalogue.Header.Any(field => field.Required && !Resolved(field.Key, 0)))
            {
                return false;
            }

            var items = slots.Where(slot => slot.ItemNo > 0).Select(slot => slot.ItemNo).Distinct();
            if (items.Any(item => catalogue.Item.Any(field => field.Required && !Resolved(field.Key, item))))
            {
                return false;
            }
        }

        return true;
    }
}
