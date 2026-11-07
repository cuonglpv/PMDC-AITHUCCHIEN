using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;

namespace ClaimLens.Api.Features.RuleSets;

public static class GetPlanVersion
{
    public static async Task<IResult> Handle(
        HttpContext context,
        ClaimLensDbContext db,
        string planId,
        string versionId,
        CancellationToken cancel)
    {
        var version = await RuleSetResponses.FindVersionAsync(db, planId, versionId, cancel);
        return version is null
            ? Requests.NotFound(context)
            : Results.Ok(await RuleSetResponses.DetailAsync(db, version, cancel));
    }
}
