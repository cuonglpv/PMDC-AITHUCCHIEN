using ClaimLens.Api.Domain.Enums;
using ClaimLens.RuleEngine;

namespace ClaimLens.Api.Domain.Entities;

public sealed class BenefitPlan
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class RuleSetVersion
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public int VersionNo { get; set; }
    public RuleSetStatus Status { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public Guid? BasedOnVersionId { get; set; }
    public string? ChangeNote { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? PublishedBy { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

public sealed class BenefitCategory
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public sealed class BenefitRule
{
    public Guid Id { get; set; }
    public Guid RuleSetVersionId { get; set; }
    public required string RuleCode { get; set; }
    public RuleType RuleType { get; set; }
    public string? CategoryCode { get; set; }
    public required string ClauseText { get; set; }
    public string? ClauseRef { get; set; }
    public short ApplyOrder { get; set; }
    public int? RateBp { get; set; }
    public long? LimitAmount { get; set; }
    public LimitScope? LimitScope { get; set; }
    public int? WaitingDays { get; set; }
}
