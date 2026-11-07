using ClaimLens.Api.Domain.Enums;

namespace ClaimLens.Api.Domain.Entities;

public sealed class Member
{
    public Guid Id { get; set; }
    public required string MemberCode { get; set; }
    public required string FullName { get; set; }

    /// <summary>Identity data (BR-11). Never leaves the API in full; responses carry a masked form.</summary>
    public required string NationalId { get; set; }

    public DateOnly DateOfBirth { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Policy
{
    public Guid Id { get; set; }
    public required string PolicyNumber { get; set; }
    public Guid MemberId { get; set; }
    public Guid PlanId { get; set; }
    public PolicyStatus Status { get; set; }
    public DateOnly CoverageStart { get; set; }
    public DateOnly CoverageEnd { get; set; }
    public DateOnly FirstEnrolledOn { get; set; }

    /// <summary>First day a LAPSED or TERMINATED policy is not in force; null while ACTIVE (Q-A).</summary>
    public DateOnly? TerminatedOn { get; set; }

    public long OpeningConsumed { get; set; }
    public long ConsumedAmount { get; set; }
    public int BalanceVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// A claim. Only <c>Features/Lifecycle/ClaimStatusMachine</c> sets <see cref="Status"/> (C3 rule 5); the database
/// trigger repeats the lifecycle. <see cref="RowVersion"/> is the optimistic lock behind VERSION_CONFLICT.
/// </summary>
public sealed class Claim
{
    public Guid Id { get; set; }
    public string ClaimNumber { get; set; } = "";
    public Guid MemberId { get; set; }
    public Guid PolicyId { get; set; }
    public string ProviderName { get; set; } = "";
    public DateOnly ServiceDate { get; set; }
    public long ClaimedTotal { get; set; }
    public IntakeChannel Channel { get; set; }
    public ClaimStatus Status { get; set; }
    public DateTimeOffset StatusChangedAt { get; set; }
    public Guid RegisteredBy { get; set; }
    public Guid? AssigneeId { get; set; }
    public DateTimeOffset? AssignedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public int RowVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Document
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }

    /// <summary>Shown to the people who may see the claim; never part of a storage key, because it can hold a name.</summary>
    public required string OriginalFilename { get; set; }

    public FileFormat FileFormat { get; set; }
    public long SizeBytes { get; set; }
    public required string Sha256 { get; set; }
    public required string OriginalKey { get; set; }
    public string? RenditionKey { get; set; }
    public short? PageCount { get; set; }
    public DocumentStatus Status { get; set; }
    public string? RejectionReason { get; set; }
    public Guid UploadedBy { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public Guid? RemovedBy { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
}

public sealed class DocumentPage
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid ClaimId { get; set; }
    public short PageNo { get; set; }
    public short ClaimPageNo { get; set; }
    public required string ImageKey { get; set; }
    public int WidthPx { get; set; }
    public int HeightPx { get; set; }
    public PageType PageType { get; set; } = PageType.Unclassified;
    public ValueSource? PageTypeSource { get; set; }
    public Guid? PageTypeSetBy { get; set; }
    public PageQuality Quality { get; set; } = PageQuality.NotChecked;
    public PageAiStatus AiStatus { get; set; } = PageAiStatus.Queued;
    public Guid? AiRunId { get; set; }
    public PageType? AiPageType { get; set; }
    public decimal? AiTypeConfidence { get; set; }
    public ConfidenceLevel? AiTypeLevel { get; set; }
    public decimal? AiQualityScore { get; set; }
}

public sealed class AiRun
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }
    public AiTask Task { get; set; }
    public Guid? ScopeDocumentId { get; set; }
    public Guid? ScopePageId { get; set; }
    public required string ModelName { get; set; }
    public required string ModelVersion { get; set; }
    public AiRunStatus Status { get; set; }
    public string? ErrorCode { get; set; }
    public Guid? TriggeredBy { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? RawOutputKey { get; set; }
}

/// <summary>
/// One field slot per document, field and item (`ux_claim_field_slot`). The value in use sits beside the AI's
/// suggestion: `ai_*` keeps what the AI read, even below 0.70, and the `value_*` columns hold only what is in use
/// (US05). <c>ck_claim_field_source_required</c> makes an accepted value carry its source (BR-02).
/// </summary>
public sealed class ClaimField
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }
    public Guid DocumentId { get; set; }
    public FieldKey FieldKey { get; set; }

    /// <summary>0 is the document header; n is expense line or prescription item n.</summary>
    public short ItemNo { get; set; }

    public Guid? ExpenseLineId { get; set; }
    public FieldStatus Status { get; set; }
    public string? ValueText { get; set; }
    public long? ValueAmount { get; set; }
    public DateOnly? ValueDate { get; set; }
    public Guid? SourcePageId { get; set; }

    /// <summary>`{x, y, w, h}` as JSON, normalised to the page.</summary>
    public string? SourceBbox { get; set; }

    public Guid? AiRunId { get; set; }

    /// <summary>The raw AI value. Kept even below 0.70, and never shown then (US05).</summary>
    public string? AiValue { get; set; }

    public decimal? AiConfidence { get; set; }
    public ConfidenceLevel? AiLevel { get; set; }
    public Guid? AiPageId { get; set; }
    public string? AiBbox { get; set; }
    public Guid? ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public int RowVersion { get; set; }
}

/// <summary>
/// An expense line read from an invoice. Its values are the accepted line fields, written in the same transaction as
/// the fields that feed them (docs/06-data-model.md, section 10). Columns are added as the features that use them arrive.
/// </summary>
public sealed class ExpenseLine
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }
    public Guid DocumentId { get; set; }
    public short LineNo { get; set; }
    public string? Description { get; set; }
    public decimal? Quantity { get; set; }
    public long? UnitPrice { get; set; }
    public long? Amount { get; set; }
    public LineOrigin Origin { get; set; }
    public MappingStatus MappingStatus { get; set; }
}
