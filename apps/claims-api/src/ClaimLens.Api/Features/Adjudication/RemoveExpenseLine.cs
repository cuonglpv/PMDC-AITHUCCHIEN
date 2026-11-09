using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Features.Evidence;
using ClaimLens.Api.Features.Lifecycle;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Adjudication;

/// <summary>
/// US06: a line the AI invented. The row stays so earlier revisions remain readable; its slots no longer count towards
/// READY_FOR_REVIEW, so removing it may be what the claim was waiting for.
/// </summary>
public static class RemoveExpenseLine
{
    public static async Task<IResult> Handle(
        HttpContext context,
        ClaimLensDbContext db,
        TimeProvider clock,
        string lineId,
        CancellationToken cancel)
    {
        context.User.TryGetStaff(out var staffId, out _);
        var claimId = ClaimAccess.Of(context).ClaimId;
        if (!Guid.TryParse(lineId, out var id))
        {
            return Requests.NotFound(context);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancel);
        var claim = await FieldChanges.LockEditableAsync(db, claimId, cancel);
        if (claim is null)
        {
            return FieldChanges.StateConflict(context);
        }

        var line = await db.ExpenseLines.SingleOrDefaultAsync(row => row.Id == id && row.ClaimId == claimId && row.RemovedAt == null, cancel);
        if (line is null)
        {
            return Requests.NotFound(context);
        }

        var now = clock.GetUtcNow();
        line.RemovedBy = staffId;
        line.RemovedAt = now;
        db.AuditEvents.Add(AuditLog.Event(
            context,
            ActorType.User,
            staffId,
            claimId,
            "EXPENSE_LINE_REMOVED",
            "expense_line",
            line.Id.ToString(),
            before: new { line.DocumentId, line.LineNo, line.Origin, line.Description, line.Quantity, line.UnitPrice, line.Amount },
            after: new { line.RemovedBy, line.RemovedAt }));

        await ClaimProgression.AfterChangeAsync(db, claim, now, ClaimProgression.ByPerson(context, staffId, claimId), cancel);
        await transaction.CommitAsync(cancel);
        return Results.Ok(await ClaimResponses.ProgressAsync(db, claim, cancel));
    }
}
