using System.Text.Json;
using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Features.Evidence;

namespace ClaimLens.Api.AiJobs.Appliers;

/// <summary>
/// How an AI reading lands on a field slot (BR-03), for extraction and re-read alike: HIGH is an auto-accepted value, CHECK
/// needs verification with no value in use, and below the floor or not found needs entry, with the AI value kept but
/// never shown. The value is held apart from the reading, because a slot a person resolved takes only the reading.
/// </summary>
public sealed class FieldReadings(Thresholds thresholds, TimeProvider clock)
{
    /// <summary>The `ai_*` columns. No suggestion clears them: the AI gave nothing for this slot.</summary>
    public void Record(ClaimField slot, AiRun run, FieldSuggestion? suggestion, IReadOnlySet<Guid> pageIds)
    {
        if (suggestion is null)
        {
            slot.AiRunId = null;
            slot.AiValue = null;
            slot.AiConfidence = null;
            slot.AiLevel = null;
            slot.AiPageId = null;
            slot.AiBbox = null;
            return;
        }

        var value = string.IsNullOrWhiteSpace(suggestion.Value) ? null : suggestion.Value.Trim();
        slot.AiRunId = run.Id;
        slot.AiValue = value;
        slot.AiConfidence = Thresholds.Stored(suggestion.Confidence);
        slot.AiLevel = thresholds.Field(slot.FieldKey, suggestion.Confidence, value is not null);
        slot.AiPageId = suggestion.PageId is { } page && pageIds.Contains(page) ? page : null;
        slot.AiBbox = suggestion.Bbox is { } box ? JsonSerializer.Serialize(box, DocumentAiJson.Options) : null;
    }

    /// <summary>
    /// The status and value in use, from the `ai_*` columns. An auto-accepted value must be readable as its kind and
    /// carry its source (BR-02); otherwise a person checks it.
    /// </summary>
    public static void TakeValue(ClaimField slot)
    {
        slot.Status = slot.AiLevel is ConfidenceLevel.High or ConfidenceLevel.Check ? FieldStatus.NeedsVerification : FieldStatus.NeedsEntry;
        slot.ValueText = null;
        slot.ValueAmount = null;
        slot.ValueDate = null;
        slot.SourcePageId = null;
        slot.SourceBbox = null;

        if (slot is { AiLevel: ConfidenceLevel.High, AiPageId: not null, AiBbox: not null }
            && FieldValues.Read(slot.FieldKey, slot.AiValue!) is { } parsed)
        {
            slot.Status = FieldStatus.AutoAccepted;
            slot.ValueText = parsed.Text;
            slot.ValueAmount = parsed.Amount;
            slot.ValueDate = parsed.Date;
            slot.SourcePageId = slot.AiPageId;
            slot.SourceBbox = slot.AiBbox;
        }
    }

    /// <summary>A field may be read on several pages; the most confident reading with a value wins.</summary>
    public static FieldSuggestion? Best(IEnumerable<FieldSuggestion> suggestions, FieldKey key, int itemNo) =>
        suggestions
            .Where(suggestion => suggestion.FieldKey == key && suggestion.ItemNo == itemNo)
            .OrderByDescending(suggestion => !string.IsNullOrWhiteSpace(suggestion.Value))
            .ThenByDescending(suggestion => suggestion.Confidence)
            .FirstOrDefault();

    public static FieldSuggestion Checked(FieldSuggestion suggestion)
    {
        var box = suggestion.Bbox;
        if (suggestion.Confidence is < 0 or > 1
            || suggestion.ItemNo < 0
            || (box is not null && (box.X is < 0 or > 1 || box.Y is < 0 or > 1 || box.W is < 0 or > 1 || box.H is < 0 or > 1)))
        {
            throw new DocumentAiException(DocumentAiException.BadResponse, transient: true, "A field suggestion breaks the contract.");
        }

        return suggestion;
    }

    public AuditEvent AutoAccepted(AiRun run, ClaimField field)
    {
        var audit = AuditLog.SystemEvent(
            clock,
            field.ClaimId,
            "FIELD_AUTO_ACCEPTED",
            "claim_field",
            field.Id.ToString(),
            after: new
            {
                field.DocumentId,
                field.FieldKey,
                field.ItemNo,
                field.Status,
                value = FieldResponses.Shown(field.FieldKey, field.ValueText),
                confidence = field.AiConfidence,
                level = field.AiLevel,
                acceptedBy = "threshold",
            });
        audit.AiRunId = run.Id;
        return audit;
    }
}
