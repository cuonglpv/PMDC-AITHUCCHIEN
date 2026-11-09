using System.Text.Json;
using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Features.Lifecycle;
using ClaimLens.Api.Features.RuleSets;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Evidence;

/// <summary>A region of a page, normalised to the page size, as the contract's Bbox and the jsonb columns hold it.</summary>
public sealed record FieldBox(decimal X, decimal Y, decimal W, decimal H)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static FieldBox? Parse(string? json) => json is null ? null : JsonSerializer.Deserialize<FieldBox>(json, Json);

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>A region a person drew has some size and lies inside the page.</summary>
    public bool LiesOnThePage => X >= 0 && Y >= 0 && W > 0 && H > 0 && X + W <= 1 && Y + H <= 1;
}

public sealed record FieldAiSuggestion(string? Value, decimal Confidence, ConfidenceLevel Level, Guid? PageId, FieldBox? Bbox, Guid RunId);

public sealed record ClaimFieldResponse(
    Guid Id,
    Guid DocumentId,
    FieldKey FieldKey,
    int ItemNo,
    Guid? ExpenseLineId,
    FieldStatus Status,
    bool Required,
    FieldValueKind ValueKind,
    string? ValueText,
    long? ValueAmount,
    DateOnly? ValueDate,
    Guid? SourcePageId,
    FieldBox? SourceBbox,
    FieldAiSuggestion? Ai,
    bool AiMarker,
    StaffRef? ResolvedBy,
    DateTimeOffset? ResolvedAt,
    int RowVersion);

public sealed record FieldList(IReadOnlyList<ClaimFieldResponse> Items, int RemainingToVerify);

public sealed record FieldChangeResult(ClaimFieldResponse Field, ClaimProgress Progress);

public static class FieldResponses
{
    /// <summary>A full national ID never leaves the API (docs/07-api-contract.md), in an answer or in the audit trail.</summary>
    public static string? Shown(FieldKey key, string? text) =>
        text is not null && FieldCatalogue.IsSensitive(key) ? Masking.NationalId(text) : text;

    /// <summary>One field as it is now, with the rest of the claim read for its `required`.</summary>
    public static async Task<ClaimFieldResponse> OneAsync(ClaimLensDbContext db, ClaimField field, CancellationToken cancel)
    {
        var evidence = await ReviewReadiness.ForClaimAsync(db, field.ClaimId, cancel);
        var staff = await StaffAsync(db, [field], cancel);
        return Of(field, evidence, staff);
    }

    public static async Task<IReadOnlyDictionary<Guid, StaffRef>> StaffAsync(ClaimLensDbContext db, IEnumerable<ClaimField> fields, CancellationToken cancel)
    {
        var ids = fields.Select(field => field.ResolvedBy).OfType<Guid>().Distinct().ToList();
        return await db.StaffAccounts.AsNoTracking()
            .Where(account => ids.Contains(account.Id))
            .Select(account => new StaffRef(account.Id, account.FullName, account.Initials))
            .ToDictionaryAsync(account => account.Id, cancel);
    }

    public static ClaimFieldResponse Of(ClaimField field, ClaimEvidence evidence, IReadOnlyDictionary<Guid, StaffRef> staff) =>
        new(
            field.Id,
            field.DocumentId,
            field.FieldKey,
            field.ItemNo,
            field.ExpenseLineId,
            field.Status,
            evidence.RequiredFields.Contains(field.Id),
            FieldCatalogue.KindOf(field.FieldKey),
            Shown(field.FieldKey, field.ValueText),
            field.ValueAmount,
            field.ValueDate,
            field.SourcePageId,
            FieldBox.Parse(field.SourceBbox),
            Suggestion(field),
            field.ResolvedBy is null && field.AiRunId is not null,
            field.ResolvedBy is { } by ? staff[by] : null,
            field.ResolvedAt,
            field.RowVersion);

    /// <summary>What the AI read. A value below the floor stays in the database and is never shown (US05).</summary>
    private static FieldAiSuggestion? Suggestion(ClaimField field) =>
        field is { AiRunId: { } run, AiConfidence: { } confidence, AiLevel: { } level }
            ? new FieldAiSuggestion(
                level == ConfidenceLevel.Missing ? null : Shown(field.FieldKey, field.AiValue),
                confidence,
                level,
                field.AiPageId,
                FieldBox.Parse(field.AiBbox),
                run)
            : null;
}
