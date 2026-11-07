using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.RuleSets;

public static class ListPlans
{
    public static async Task<IResult> Handle(ClaimLensDbContext db, TimeProvider clock, CancellationToken cancel)
    {
        var today = VietnamDate.Today(clock);
        var plans = await db.BenefitPlans.AsNoTracking().OrderBy(plan => plan.Code).ToListAsync(cancel);
        var versions = await db.RuleSetVersions.AsNoTracking()
            .Where(version => version.Status == RuleSetStatus.Published || version.Status == RuleSetStatus.Draft)
            .ToListAsync(cancel);

        var items = plans
            .Select(plan =>
            {
                var own = versions.Where(version => version.PlanId == plan.Id).ToList();
                var current = own
                    .Where(version => version.Status == RuleSetStatus.Published && version.EffectiveFrom <= today)
                    .OrderByDescending(version => version.EffectiveFrom)
                    .FirstOrDefault();

                return new PlanResponse(
                    plan.Id,
                    plan.Code,
                    plan.Name,
                    plan.Description,
                    current is null ? null : new RuleSetRef(current.Id, current.VersionNo, current.EffectiveFrom!.Value),
                    own.FirstOrDefault(version => version.Status == RuleSetStatus.Draft)?.Id);
            })
            .ToList();

        return Results.Ok(new Items<PlanResponse>(items));
    }
}

/// <summary>Business dates are Vietnamese dates. Vietnam has no daylight saving, so a fixed offset is exact.</summary>
public static class VietnamDate
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(Offset).DateTime);
}
