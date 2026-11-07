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
