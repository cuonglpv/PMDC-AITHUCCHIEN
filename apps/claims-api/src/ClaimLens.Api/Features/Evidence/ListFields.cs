using System.Text.Json;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Evidence;

/// <summary>
/// The Evidence tab (US06): lowest confidence first, a field the AI never read before them, and the web app groups by
/// document. The counter always covers the whole claim, whatever is filtered. The middleware audits the read.
/// </summary>
public static class ListFields
{
    public static async Task<IResult> Handle(HttpContext context, ClaimLensDbContext db, CancellationToken cancel)
    {
        var claimId = ClaimAccess.Of(context).ClaimId;
        var query = context.Request.Query;

        Guid? documentId = null;
        if (query["documentId"].ToString() is { Length: > 0 } wantedDocument)
        {
            if (!Guid.TryParse(wantedDocument, out var parsed))
            {
                return Refused(context, new FieldError("documentId", "INVALID", "Use a document id."));
            }

            documentId = parsed;
        }

        var statuses = new List<FieldStatus>();
        foreach (var wanted in query["status"])
        {
            var status = Enum.GetValues<FieldStatus>().Where(value => Wire(value) == wanted).Cast<FieldStatus?>().FirstOrDefault();
            if (status is null)
            {
                return Refused(context, new FieldError("status", "OUT_OF_RANGE", "That is not a field status."));
            }

            statuses.Add(status.Value);
        }

        // Upload order, so fields of equal confidence stay together by document.
        var documents = await db.Documents.AsNoTracking()
            .Where(document => document.ClaimId == claimId && document.Status == DocumentStatus.Ready)
            .OrderBy(document => document.UploadedAt)
            .ThenBy(document => document.Id)
            .Select(document => document.Id)
            .ToListAsync(cancel);

        var fields = db.ClaimFields.AsNoTracking()
            .Where(field => field.ClaimId == claimId
                && documents.Contains(field.DocumentId)
                && (field.ExpenseLineId == null || db.ExpenseLines.Any(line => line.Id == field.ExpenseLineId && line.RemovedAt == null)));
        if (documentId is { } only)
        {
            fields = fields.Where(field => field.DocumentId == only);
        }

        if (statuses.Count > 0)
        {
            fields = fields.Where(field => statuses.Contains(field.Status));
        }

        var rows = await fields.ToListAsync(cancel);
        var evidence = await ReviewReadiness.ForClaimAsync(db, claimId, cancel);
        var staff = await FieldResponses.StaffAsync(db, rows, cancel);

        return Results.Ok(new FieldList(
            rows
                .OrderBy(field => field.AiConfidence ?? -1)
                .ThenBy(field => documents.IndexOf(field.DocumentId))
                .ThenBy(field => field.ItemNo)
                .ThenBy(field => field.FieldKey)
                .Select(field => FieldResponses.Of(field, evidence, staff))
                .ToList(),
            evidence.FieldsToVerify));
    }

    private static string Wire(FieldStatus status) => JsonNamingPolicy.SnakeCaseUpper.ConvertName(status.ToString());

    private static IResult Refused(HttpContext context, FieldError error) =>
        Problems.Of(context, StatusCodes.Status422UnprocessableEntity, ErrorCodes.ValidationFailed, "Check the highlighted fields", [error]);
}
