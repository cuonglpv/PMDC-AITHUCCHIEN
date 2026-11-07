using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Features.Lifecycle;
using ClaimLens.Api.Features.RuleSets;
using ClaimLens.Api.Persistence;

namespace ClaimLens.Api.Features.Intake;

public sealed record PolicySummary(
    Guid PolicyId,
    string PolicyNumber,
    Guid MemberId,
    string PlanCode,
    string PlanName,
    PolicyStatus Status,
    DateOnly CoverageStart,
    DateOnly CoverageEnd,
    DateOnly FirstEnrolledOn,
    long ConsumedAmount,
    long? AnnualLimit,
    long? RemainingBalance,
    DateOnly AsOfDate);

/// <summary>
/// A policy with its remaining outpatient balance (US02, BR-04, BR-10). The annual limit is the LIMIT/ANNUAL rule of the
/// version effective on the date asked for; without one, both the limit and the balance are null.
/// </summary>
public sealed class PolicySummaries(ClaimLensDbContext db)
{
    private readonly Dictionary<(Guid Plan, DateOnly Date), long?> limits = [];

    public async Task<PolicySummary> ForAsync(Policy policy, BenefitPlan plan, DateOnly asOf, CancellationToken cancel)
    {
        var annual = await AnnualLimitAsync(plan.Id, asOf, cancel);
        return new PolicySummary(
            policy.Id,
            policy.PolicyNumber,
            policy.MemberId,
            plan.Code,
            plan.Name,
            policy.Status,
            policy.CoverageStart,
            policy.CoverageEnd,
            policy.FirstEnrolledOn,
            policy.ConsumedAmount,
            annual,
            ClaimResponses.Remaining(annual, policy.ConsumedAmount),
            asOf);
    }

    /// <summary>Many search rows share a plan, so each plan and date is read once.</summary>
    private async Task<long?> AnnualLimitAsync(Guid planId, DateOnly date, CancellationToken cancel)
    {
        if (limits.TryGetValue((planId, date), out var known))
        {
            return known;
        }

        var version = await EffectiveRuleSet.OnAsync(db, planId, date, cancel);
        var limit = version is null ? null : await EffectiveRuleSet.AnnualLimitAsync(db, version.Id, cancel);
        limits[(planId, date)] = limit;
        return limit;
    }
}
