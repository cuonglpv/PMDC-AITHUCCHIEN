using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using ClaimLens.RuleEngine;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.RuleSets;

/// <summary>Publishing makes a version immutable. The database repeats these checks (db/migrations/0003_triggers.sql).</summary>
public static class PublishPlanVersion
{
    private sealed record Request(DateOnly? EffectiveFrom);

    public static async Task<IResult> Handle(
        HttpContext context,
        ClaimLensDbContext db,
        TimeProvider clock,
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
            return Problems.Of(context, StatusCodes.Status409Conflict, ErrorCodes.RuleSetNotDraft, "Only a draft can be published");
        }

        var request = await Requests.ReadJsonAsync<Request>(context);
        if (request?.EffectiveFrom is not { } effectiveFrom)
        {
            return Requests.Unreadable(context, new FieldError("effectiveFrom", "REQUIRED", "Enter the effective-from date."));
        }

        var latest = await db.RuleSetVersions
            .Where(row => row.PlanId == version.PlanId && row.Status == RuleSetStatus.Published)
            .MaxAsync(row => row.EffectiveFrom, cancel);

        if (effectiveFrom <= latest)
        {
            return Problems.Of(
                context,
                StatusCodes.Status422UnprocessableEntity,
                ErrorCodes.EffectiveDateNotLater,
                "The effective date must be later than the current version's",
                [new FieldError("effectiveFrom", "NOT_LATER", $"It must be later than {latest:dd/MM/yyyy}.")]);
        }

        var missing = await MissingCategoriesAsync(db, version.Id, cancel);
        if (missing.Count > 0)
        {
            return Problems.Of(
                context,
                StatusCodes.Status422UnprocessableEntity,
                ErrorCodes.CategoryNotConfigured,
                "Every active category must be covered or excluded",
                missing.Select(name => new FieldError("rules", "MISSING_CATEGORY", $"{name} is neither covered nor excluded.")).ToList());
        }

        context.User.TryGetStaff(out var staffId, out _);
        version.Status = RuleSetStatus.Published;
        version.EffectiveFrom = effectiveFrom;
        version.PublishedBy = staffId;
        version.PublishedAt = clock.GetUtcNow();

        db.AuditEvents.Add(AuditLog.Event(
            context,
            ActorType.User,
            staffId,
            null,
            "RULE_SET_PUBLISHED",
            "rule_set_version",
            version.Id.ToString(),
            after: new { version.VersionNo, effectiveFrom }));
        await db.SaveChangesAsync(cancel);

        return Results.Ok(await RuleSetResponses.DetailAsync(db, version, cancel));
    }

    private static async Task<List<string>> MissingCategoriesAsync(ClaimLensDbContext db, Guid versionId, CancellationToken cancel)
    {
        var configured = db.BenefitRules
            .Where(rule => rule.RuleSetVersionId == versionId
                && (rule.RuleType == RuleType.Coverage || rule.RuleType == RuleType.Exclusion))
            .Select(rule => rule.CategoryCode);

        return await db.BenefitCategories
            .Where(category => category.IsActive && !configured.Contains(category.Code))
            .OrderBy(category => category.Name)
            .Select(category => category.Name)
            .ToListAsync(cancel);
    }
}
