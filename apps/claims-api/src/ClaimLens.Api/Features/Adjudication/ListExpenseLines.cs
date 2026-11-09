using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Adjudication;

/// <summary>The claim's lines in order, by invoice as uploaded, without removed ones. The middleware audits the read.</summary>
public static class ListExpenseLines
{
    public static async Task<IResult> Handle(HttpContext context, ClaimLensDbContext db, CancellationToken cancel)
    {
        var claimId = ClaimAccess.Of(context).ClaimId;
        var lines = await (
                from line in db.ExpenseLines.AsNoTracking()
                join document in db.Documents.AsNoTracking() on line.DocumentId equals document.Id
                where line.ClaimId == claimId && line.RemovedAt == null
                orderby document.UploadedAt, document.Id, line.LineNo
                select line)
            .ToListAsync(cancel);

        return Results.Ok(new ExpenseLineList(lines.Select(ExpenseLineResponses.Of).ToList()));
    }
}
