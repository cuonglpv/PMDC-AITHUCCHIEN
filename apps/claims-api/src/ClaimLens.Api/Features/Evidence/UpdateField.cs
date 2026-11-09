using System.Globalization;
using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Evidence;

/// <summary>
/// US06: the reviewer corrects a value, enters one the AI did not give, or marks the field not present. The server sets
/// the status. Sending back the AI's value unchanged corrects nothing, so it counts as confirming it, and the AI quality
/// figures (US21) only count real corrections.
/// </summary>
public static class UpdateField
{
    private const string SetValue = "SET_VALUE";
    private const string NotPresent = "NOT_PRESENT";

    private const int MaxTextLength = 2000;

    private sealed record Request(
        int? RowVersion,
        string? Action,
        string? ValueText,
        long? ValueAmount,
        DateOnly? ValueDate,
        Guid? SourcePageId,
        FieldBox? SourceBbox);

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

        if (request.Action is not (SetValue or NotPresent))
        {
            return Requests.Unreadable(context, new FieldError("action", "OUT_OF_RANGE", "The action is SET_VALUE or NOT_PRESENT."));
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

        var before = FieldChanges.Snapshot(field);
        var now = clock.GetUtcNow();
        if (request.Action == NotPresent)
        {
            await FieldChanges.ResolveAsync(db, field, FieldStatus.NotPresent, null, staffId, now, cancel);
            return await FieldChanges.FinishAsync(context, db, transaction, claim, field, before, staffId, now, cancel);
        }

        var (value, badValue) = Read(field.FieldKey, request);
        if (badValue is not null)
        {
            return Requests.Unreadable(context, badValue);
        }

        if (await FieldChanges.SetSourceAsync(context, db, field, drawn, aiSourceFirst: false, cancel) is { } noSource)
        {
            return noSource;
        }

        await FieldChanges.ResolveAsync(db, field, StatusOf(field, value!), value, staffId, now, cancel);
        return await FieldChanges.FinishAsync(context, db, transaction, claim, field, before, staffId, now, cancel);
    }

    /// <summary>A value below the floor was never shown, so to the reviewer the AI gave none.</summary>
    private static FieldStatus StatusOf(ClaimField field, FieldValue value)
    {
        if (field.AiValue is null || field.AiLevel is null or ConfidenceLevel.Missing)
        {
            return FieldStatus.Entered;
        }

        return FieldValues.Read(field.FieldKey, field.AiValue) is { } ai && FieldValues.SameValue(field.FieldKey, ai, value)
            ? FieldStatus.Confirmed
            : FieldStatus.Corrected;
    }

    /// <summary>The value property of the field's kind. Money and dates are also kept as text, which BR-02's constraint needs.</summary>
    private static (FieldValue? Value, FieldError? Error) Read(FieldKey key, Request request)
    {
        switch (FieldCatalogue.KindOf(key))
        {
            case FieldValueKind.Money:
                return request.ValueAmount switch
                {
                    null => (null, new FieldError("valueAmount", "REQUIRED", "Enter the amount.")),
                    < 0 => (null, new FieldError("valueAmount", "OUT_OF_RANGE", "An amount cannot be negative.")),
                    { } amount => (new FieldValue(amount.ToString(CultureInfo.InvariantCulture), amount, null, null), null),
                };

            case FieldValueKind.Date:
                return request.ValueDate is { } date
                    ? (new FieldValue(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), null, date, null), null)
                    : (null, new FieldError("valueDate", "REQUIRED", "Enter the date."));

            case FieldValueKind.Number:
                var quantity = FieldValues.Read(key, request.ValueText ?? "");
                return quantity is null
                    ? (null, new FieldError("valueText", "INVALID", "Enter a number, with at most two decimals."))
                    : (quantity, null);

            default:
                var text = request.ValueText?.Trim() ?? "";
                if (text.Length == 0)
                {
                    return (null, new FieldError("valueText", "REQUIRED", "Enter the value."));
                }

                if (text.Length > MaxTextLength)
                {
                    return (null, new FieldError("valueText", "TOO_LONG", $"At most {MaxTextLength} characters."));
                }

                // The list shows this field masked; the masked text sent back must not replace the real number.
                return FieldCatalogue.IsSensitive(key) && text.Contains('*', StringComparison.Ordinal)
                    ? (null, new FieldError("valueText", "INVALID", "Type the full number from the page."))
                    : (new FieldValue(text, null, null, null), null);
        }
    }
}
