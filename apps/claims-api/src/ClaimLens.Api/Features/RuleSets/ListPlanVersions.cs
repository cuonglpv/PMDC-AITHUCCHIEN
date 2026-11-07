using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.RuleSets;

public static class ListPlanVersions
{
    public static async Task<IResult> Handle(HttpContext context, ClaimLensDbContext db, string planId, CancellationToken cancel)
    {
        if (!Guid.TryParse(planId, out var plan) || !await db.BenefitPlans.AnyAsync(row => row.Id == plan, cancel))
        {
            return Requests.NotFound(context);
        }

        var versions = await db.RuleSetVersions.AsNoTracking()
            .Where(version => version.PlanId == plan)
            .OrderByDescending(version => version.VersionNo)
            .ToListAsync(cancel);

        return Results.Ok(new Items<VersionSummary>(await RuleSetResponses.SummariesAsync(db, versions, cancel)));
    }
}
