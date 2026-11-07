using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using ClaimLens.RuleEngine;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.RuleSets;

/// <summary>Replaces every rule of the draft at once; the editor tabs are views of one list.</summary>
public static class UpdateRuleSet
{
    private sealed record Request(string? ChangeNote, List<RuleInput>? Rules);

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
            return Problems.Of(context, StatusCodes.Status409Conflict, ErrorCodes.RuleSetNotDraft, "Only a draft can change");
        }

        var request = await Requests.ReadJsonAsync<Request>(context);
        if (request?.Rules is not { } inputs)
        {
            return Requests.Unreadable(context, new FieldError("rules", "REQUIRED", "Send the list of rules."));
        }

        if (request.ChangeNote?.Length > 2000)
        {
            return Requests.Unreadable(context, new FieldError("changeNote", "TOO_LONG", "A change note can have at most 2,000 characters."));
        }

        var active = (await db.BenefitCategories.AsNoTracking()
                .Where(category => category.IsActive)
                .Select(category => category.Code)
                .ToListAsync(cancel))
            .ToHashSet(StringComparer.Ordinal);

        var kept = await db.BenefitRules.AsNoTracking()
            .Where(rule => rule.RuleSetVersionId == version.Id)
            .ToListAsync(cancel);
        var eligibility = kept.FirstOrDefault(rule => rule.RuleType == RuleType.Eligibility);
        var system = eligibility is null
            ? new OrderedRule(RuleSetRules.EligibilityCode, RuleType.Eligibility, RuleSetRules.EligibilityOrder)
            : new OrderedRule(eligibility.RuleCode, RuleType.Eligibility, eligibility.ApplyOrder);

        var problems = RuleSetRules.Check(inputs, active, system);
        if (problems is not null)
        {
            return Problems.Of(context, StatusCodes.Status422UnprocessableEntity, problems.Code, problems.Title, problems.Errors);
        }

        var before = kept.Where(rule => rule.RuleType != RuleType.Eligibility).Select(RuleSetResponses.ToResponse).ToList();
        var rules = inputs.Select(input => RuleSetRules.ToEntity(input, version.Id)).ToList();
        context.User.TryGetStaff(out var staffId, out _);

        // The old rows must be gone before the new ones arrive, because a code may stay the same across a save.
        await using var transaction = await db.Database.BeginTransactionAsync(cancel);
        await db.BenefitRules
            .Where(rule => rule.RuleSetVersionId == version.Id && rule.RuleType != RuleType.Eligibility)
            .ExecuteDeleteAsync(cancel);

        db.BenefitRules.AddRange(rules);
        if (eligibility is null)
        {
            db.BenefitRules.Add(RuleSetRules.Eligibility(version.Id));
        }

        if (request.ChangeNote is not null)
        {
            version.ChangeNote = request.ChangeNote;
        }

        db.AuditEvents.Add(AuditLog.Event(
            context,
            ActorType.User,
            staffId,
            null,
            "RULE_SET_UPDATED",
            "rule_set_version",
            version.Id.ToString(),
            before: new { rules = before },
            after: new { rules = rules.Select(RuleSetResponses.ToResponse) }));
        await db.SaveChangesAsync(cancel);
        await transaction.CommitAsync(cancel);

        return Results.Ok(await RuleSetResponses.DetailAsync(db, version, cancel));
    }
}
