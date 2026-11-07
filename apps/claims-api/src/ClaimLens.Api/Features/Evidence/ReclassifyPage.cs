using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Evidence;

/// <summary>
/// US04: before submission the Intake Officer may choose a page's type, which the page check then keeps. The reviewer's
/// branch, which also creates the field slots for the type (US06), arrives with Slice 3 and answers 501 until then.
/// </summary>
public static class ReclassifyPage
{
    private sealed record Request(string? PageType);

    private static readonly PageType[] Choosable =
        [PageType.Invoice, PageType.Receipt, PageType.Prescription, PageType.VisitSummary, PageType.LabResult, PageType.Other];

    public static async Task<IResult> Handle(
        HttpContext context,
        ClaimLensDbContext db,
        string pageId,
        CancellationToken cancel)
    {
        context.User.TryGetStaff(out var staffId, out var role);
        if (role != StaffRole.IntakeOfficer)
        {
            return Results.StatusCode(StatusCodes.Status501NotImplemented);
        }

        var claimId = ClaimAccess.Of(context).ClaimId;
        if (ClaimAccess.Of(context).Status != ClaimStatus.Draft)
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

        // The claim row's lock orders this against submitClaim; the page's against the page check that may be applying.
        await using var transaction = await db.Database.BeginTransactionAsync(cancel);
        var status = await db.Database
            .SqlQuery<string>($"select status::text as \"Value\" from claim where id = {claimId} for update")
            .SingleAsync(cancel);
        if (status != "DRAFT")
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

        if (page.PageType != type || page.PageTypeSource != ValueSource.User || page.PageTypeSetBy != staffId)
        {
            var before = new { page.PageType, page.PageTypeSource };
            page.PageType = type;
            page.PageTypeSource = ValueSource.User;
            page.PageTypeSetBy = staffId;
            db.AuditEvents.Add(AuditLog.Event(
                context,
                ActorType.User,
                staffId,
                claimId,
                "PAGE_RECLASSIFIED",
                "document_page",
                page.Id.ToString(),
                before: before,
                after: new { page.PageType, page.PageTypeSource }));
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
