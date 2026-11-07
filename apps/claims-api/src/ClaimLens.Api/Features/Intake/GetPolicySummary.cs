using System.Globalization;
using ClaimLens.Api.Features.RuleSets;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Intake;

/// <summary>
/// A policy with its remaining balance, as of the service date when one is given and today in Vietnam otherwise
/// (BR-04). The middleware audits the read.
/// </summary>
public static class GetPolicySummary
{
    public static async Task<IResult> Handle(
        HttpContext context,
        ClaimLensDbContext db,
        TimeProvider clock,
        string policyId,
        CancellationToken cancel)
    {
        if (!Guid.TryParse(policyId, out var id))
        {
            return Requests.NotFound(context);
        }

        var raw = context.Request.Query["serviceDate"].ToString();
        var asOf = VietnamDate.Today(clock);
        if (raw.Length > 0 && !DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out asOf))
        {
            return Requests.Unreadable(context, new FieldError("serviceDate", "INVALID", "Use a date as YYYY-MM-DD."));
        }

        var row = await (
                from policy in db.Policies.AsNoTracking()
                join plan in db.BenefitPlans.AsNoTracking() on policy.PlanId equals plan.Id
                where policy.Id == id
                select new { policy, plan })
            .FirstOrDefaultAsync(cancel);
        if (row is null)
        {
            return Requests.NotFound(context);
        }

        return Results.Ok(await new PolicySummaries(db).ForAsync(row.policy, row.plan, asOf, cancel));
    }
}
