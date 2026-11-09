using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Features.Lifecycle;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Evidence;

/// <summary>
/// A page's type chosen by a person. Before submission the Intake Officer may choose it, and the page check then keeps
/// it (US04). In review the assignee chooses it, which also creates the empty header slots of that type and may be what
/// the claim was waiting for (US06).
/// </summary>
public static class ReclassifyPage
{
    private sealed record Request(string? PageType);

    private static readonly PageType[] Choosable =
        [PageType.Invoice, PageType.Receipt, PageType.Prescription, PageType.VisitSummary, PageType.LabResult, PageType.Other];

    public static async Task<IResult> Handle(
        HttpContext context,
        ClaimLensDbContext db,
        TimeProvider clock,
        string pageId,
        CancellationToken cancel)
    {
        context.User.TryGetStaff(out var staffId, out var role);
        var inReview = role != StaffRole.IntakeOfficer;
        var claimId = ClaimAccess.Of(context).ClaimId;
        if (!inReview && ClaimAccess.Of(context).Status != ClaimStatus.Draft)
        {
            return StateConflict(context);
        }

        var request = await Requests.ReadJsonAsync<Request>(context);
        var type = Choosable.FirstOrDefault(choice => Wire(choice) == request?.PageType, PageType.Unclassified);
        if (type == PageType.Unclassified)
        {
            return Requests.Unreadable(
                context,
                new FieldError("pageType", "INVALID", "Choose Invoice, Receipt, Prescription, Visit summary, Lab result or Other."));
        }

        if (!Guid.TryParse(pageId, out var id))
        {
            return Requests.NotFound(context);
        }

        // The claim row's lock orders this against submitClaim and the other edits; the page's against the page check.
        await using var transaction = await db.Database.BeginTransactionAsync(cancel);
        var claim = (await db.Claims.FromSql($"select * from claim where id = {claimId} for update").ToListAsync(cancel)).Single();
        var allowed = inReview
            ? claim.Status is ClaimStatus.NeedsVerification or ClaimStatus.InReview
            : claim.Status == ClaimStatus.Draft;
        if (!allowed)
        {
            return StateConflict(context);
        }

        var page = (await db.DocumentPages
                .FromSql($"select * from document_page where id = {id} and claim_id = {claimId} for update")
                .ToListAsync(cancel))
            .SingleOrDefault();
        if (page is null)
        {
            return Requests.NotFound(context);
        }

        var before = new { page.PageType, page.PageTypeSource };
        var changed = page.PageType != type || page.PageTypeSource != ValueSource.User || page.PageTypeSetBy != staffId;
        if (changed)
        {
            page.PageType = type;
            page.PageTypeSource = ValueSource.User;
            page.PageTypeSetBy = staffId;
            await db.SaveChangesAsync(cancel);
        }

        var createdFields = 0;
        if (inReview)
        {
            // Its required fields are the document's, by its first classified page, so that type's slots must exist too.
            var pages = await db.DocumentPages.AsNoTracking()
                .Where(row => row.DocumentId == page.DocumentId)
                .ToListAsync(cancel);
            var types = new[] { type, FieldCatalogue.DocumentType(pages) ?? type };
            createdFields = (await FieldSlots.AddMissingHeaderAsync(db, claimId, page.DocumentId, types, cancel)).Count;
        }

        if (changed)
        {
            db.AuditEvents.Add(AuditLog.Event(
                context,
                ActorType.User,
                staffId,
                claimId,
                "PAGE_RECLASSIFIED",
                "document_page",
                page.Id.ToString(),
                before: before,
                after: new { page.PageType, page.PageTypeSource, createdFields }));
        }

        if (inReview)
        {
            await ClaimProgression.AfterChangeAsync(db, claim, clock.GetUtcNow(), ClaimProgression.ByPerson(context, staffId, claimId), cancel);
        }
        else
        {
            await db.SaveChangesAsync(cancel);
        }

        await transaction.CommitAsync(cancel);
        return Results.Ok(await DocumentResponses.PageAsync(db, page, cancel));
    }

    /// <summary>The name the contract uses, such as VISIT_SUMMARY.</summary>
    private static string Wire(PageType type) => System.Text.Json.JsonNamingPolicy.SnakeCaseUpper.ConvertName(type.ToString());

    private static IResult StateConflict(HttpContext context) =>
        Problems.Of(context, StatusCodes.Status409Conflict, ErrorCodes.ClaimStateConflict, "The claim's status does not allow this");
}
