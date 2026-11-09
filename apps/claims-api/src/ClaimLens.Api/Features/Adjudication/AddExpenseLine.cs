using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Features.Evidence;
using ClaimLens.Api.Features.Lifecycle;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Adjudication;

/// <summary>
/// US06: a line the AI missed. Created with origin MANUAL and its empty field slots, which the reviewer fills through
/// updateField with a drawn source. A removed line keeps its number, so the next one never reuses it.
/// </summary>
public static class AddExpenseLine
{
    private sealed record Request(Guid? DocumentId);

    public static async Task<IResult> Handle(HttpContext context, ClaimLensDbContext db, TimeProvider clock, CancellationToken cancel)
    {
        var request = await Requests.ReadJsonAsync<Request>(context);
        if (request is null)
        {
            return Requests.Unreadable(context);
        }

        if (request.DocumentId is not { } documentId)
        {
            return Requests.Unreadable(context, new FieldError("documentId", "REQUIRED", "Say which invoice this line belongs to."));
        }

        context.User.TryGetStaff(out var staffId, out _);
        var claimId = ClaimAccess.Of(context).ClaimId;

        await using var transaction = await db.Database.BeginTransactionAsync(cancel);
        var claim = await FieldChanges.LockEditableAsync(db, claimId, cancel);
        if (claim is null)
        {
            return FieldChanges.StateConflict(context);
        }

        var pages = await db.DocumentPages.AsNoTracking()
            .Where(page => page.DocumentId == documentId && page.ClaimId == claimId
                && db.Documents.Any(document => document.Id == documentId && document.Status == DocumentStatus.Ready))
            .ToListAsync(cancel);
        if (FieldCatalogue.DocumentType(pages) is not { } type || !FieldCatalogue.GivesExpenseLines(type))
        {
            return Requests.Unreadable(context, new FieldError("documentId", "INVALID", "Choose an invoice of this claim."));
        }

        var lastLine = await db.ExpenseLines.Where(line => line.DocumentId == documentId).MaxAsync(line => (short?)line.LineNo, cancel);
        var added = new ExpenseLine
        {
            Id = Guid.CreateVersion7(),
            ClaimId = claimId,
            DocumentId = documentId,
            LineNo = (short)((lastLine ?? 0) + 1),
            Origin = LineOrigin.Manual,
            MappingStatus = MappingStatus.Pending,
        };
        db.ExpenseLines.Add(added);
        db.ClaimFields.AddRange(FieldCatalogue.For(type).Item.Select(field =>
            FieldSlots.Empty(claimId, documentId, field.Key, added.LineNo, added.Id)));
        db.AuditEvents.Add(AuditLog.Event(
            context,
            ActorType.User,
            staffId,
            claimId,
            "EXPENSE_LINE_ADDED",
            "expense_line",
            added.Id.ToString(),
            after: new { added.DocumentId, added.LineNo, added.Origin }));

        await ClaimProgression.AfterChangeAsync(db, claim, clock.GetUtcNow(), ClaimProgression.ByPerson(context, staffId, claimId), cancel);
        await transaction.CommitAsync(cancel);

        return Results.Created(
            $"/api/v1/claims/{claimId}/expense-lines/{added.Id}",
            new LineChangeResult(ExpenseLineResponses.Of(added), await ClaimResponses.ProgressAsync(db, claim, cancel)));
    }
}
