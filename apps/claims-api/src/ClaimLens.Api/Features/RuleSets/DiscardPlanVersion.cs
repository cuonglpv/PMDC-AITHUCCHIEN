using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;

namespace ClaimLens.Api.Features.RuleSets;

/// <summary>Marks the draft DISCARDED, which frees the plan to have a new draft. Its rules stay as they were.</summary>
public static class DiscardPlanVersion
{
    public static async Task<IResult> Handle(
        HttpContext context,
        ClaimLensDbContext db,
        string planId,
        string versionId,
        CancellationToken cancel)
    {
        var version = await RuleSetResponses.FindVersionAsync(db, planId, versionId, cancel);
        if (version is null)
        {
            return Requests.NotFound(context);
        }

        if (version.Status != RuleSetStatus.Draft)
        {
            return Problems.Of(context, StatusCodes.Status409Conflict, ErrorCodes.RuleSetNotDraft, "Only a draft can be discarded");
        }

        context.User.TryGetStaff(out var staffId, out _);
        version.Status = RuleSetStatus.Discarded;
        db.AuditEvents.Add(AuditLog.Event(
            context,
            ActorType.User,
            staffId,
            null,
            "RULE_SET_DISCARDED",
            "rule_set_version",
            version.Id.ToString(),
            after: new { version.VersionNo }));
        await db.SaveChangesAsync(cancel);

        return Results.Ok((await RuleSetResponses.SummariesAsync(db, [version], cancel))[0]);
    }
}
