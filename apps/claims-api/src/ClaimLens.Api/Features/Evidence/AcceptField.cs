using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;

namespace ClaimLens.Api.Features.Evidence;

/// <summary>
/// US06: the reviewer accepts the AI's value unchanged; their initials replace the AI marker. A value below the floor was
/// never shown, and one that does not read as its kind could not be used, so neither can be accepted.
/// </summary>
public static class AcceptField
{
    private sealed record Request(int? RowVersion, Guid? SourcePageId, FieldBox? SourceBbox);

    public static async Task<IResult> Handle(
        HttpContext context,
        ClaimLensDbContext db,
        TimeProvider clock,
        string fieldId,
        CancellationToken cancel)
    {
        var request = await Requests.ReadJsonAsync<Request>(context);
        if (request is null)
        {
            return Requests.Unreadable(context);
        }

        if (request.RowVersion is not { } rowVersion || rowVersion < 0)
        {
            return Requests.Unreadable(context, new FieldError("rowVersion", "REQUIRED", "Send the rowVersion you read."));
        }

        if (FieldChanges.ReadDrawn(request.SourcePageId, request.SourceBbox, out var drawn) is { } badSource)
        {
            return Requests.Unreadable(context, badSource);
        }

        context.User.TryGetStaff(out var staffId, out _);
        var claimId = ClaimAccess.Of(context).ClaimId;

        await using var transaction = await db.Database.BeginTransactionAsync(cancel);
        var claim = await FieldChanges.LockEditableAsync(db, claimId, cancel);
        if (claim is null)
        {
            return FieldChanges.StateConflict(context);
        }

        var field = await FieldChanges.FindAsync(db, claimId, fieldId, cancel);
        if (field is null)
        {
            return Requests.NotFound(context);
        }

        if (field.RowVersion != rowVersion)
        {
            return await FieldChanges.VersionConflictAsync(context, db, field, cancel);
        }

        var value = field.AiValue is { } ai && field.AiLevel is not (null or ConfidenceLevel.Missing)
            ? FieldValues.Read(field.FieldKey, ai)
            : null;
        if (value is null)
        {
            return Problems.Of(context, StatusCodes.Status422UnprocessableEntity, ErrorCodes.FieldHasNoAiValue, "There is no AI value to accept");
        }

        var before = FieldChanges.Snapshot(field);
        if (await FieldChanges.SetSourceAsync(context, db, field, drawn, aiSourceFirst: true, cancel) is { } noSource)
        {
            return noSource;
        }

        var now = clock.GetUtcNow();
        await FieldChanges.ResolveAsync(db, field, FieldStatus.Confirmed, value, staffId, now, cancel);
        return await FieldChanges.FinishAsync(context, db, transaction, claim, field, before, staffId, now, cancel);
    }
}
