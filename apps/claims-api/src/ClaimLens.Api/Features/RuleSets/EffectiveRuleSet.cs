using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Persistence;
using ClaimLens.RuleEngine;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.RuleSets;

/// <summary>
/// BR-04: a claim uses the published version of its plan with the greatest effective date on or before the service
/// date. Intake reads the annual limit from it before any adjudication has run.
/// </summary>
public static class EffectiveRuleSet
{
    public static Task<RuleSetVersion?> OnAsync(ClaimLensDbContext db, Guid planId, DateOnly date, CancellationToken cancel) =>
        db.RuleSetVersions.AsNoTracking()
            .Where(version => version.PlanId == planId
                && version.Status == RuleSetStatus.Published
                && version.EffectiveFrom <= date)
            .OrderByDescending(version => version.EffectiveFrom)
            .FirstOrDefaultAsync(cancel);

    /// <summary>The version's annual LIMIT rule, or null when it has none. Publishing allows at most one per scope.</summary>
    public static Task<long?> AnnualLimitAsync(ClaimLensDbContext db, Guid versionId, CancellationToken cancel) =>
        db.BenefitRules.AsNoTracking()
            .Where(rule => rule.RuleSetVersionId == versionId
                && rule.RuleType == RuleType.Limit
                && rule.LimitScope == LimitScope.Annual)
            .OrderBy(rule => rule.ApplyOrder)
            .Select(rule => rule.LimitAmount)
            .FirstOrDefaultAsync(cancel);
}
