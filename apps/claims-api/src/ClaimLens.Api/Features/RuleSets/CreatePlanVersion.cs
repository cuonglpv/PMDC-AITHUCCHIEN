using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using ClaimLens.RuleEngine;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.RuleSets;

public static class CreatePlanVersion
{
    private sealed record Request(Guid? BasedOnVersionId, string? ChangeNote);

    public static async Task<IResult> Handle(
        HttpContext context,
        ClaimLensDbContext db,
        TimeProvider clock,
        string planId,
        CancellationToken cancel)
    {
        if (!Guid.TryParse(planId, out var plan) || !await db.BenefitPlans.AnyAsync(row => row.Id == plan, cancel))
        {
            return Requests.NotFound(context);
        }

        if (await db.RuleSetVersions.AnyAsync(row => row.PlanId == plan && row.Status == RuleSetStatus.Draft, cancel))
        {
            return Problems.Of(context, StatusCodes.Status409Conflict, ErrorCodes.DraftAlreadyExists, "This plan already has a draft");
        }

        var request = await Requests.ReadJsonAsync<Request>(context);
        if (request is null)
        {
            return Requests.Unreadable(context);
        }

        if (request.ChangeNote?.Length > 2000)
        {
            return Requests.Unreadable(context, new FieldError("changeNote", "TOO_LONG", "A change note can have at most 2,000 characters."));
        }

        if (request.BasedOnVersionId is { } baseId
            && !await db.RuleSetVersions.AnyAsync(row => row.Id == baseId && row.PlanId == plan, cancel))
        {
            return Requests.Unreadable(context, new FieldError("basedOnVersionId", "OUT_OF_RANGE", "That version belongs to another plan."));
        }

        context.User.TryGetStaff(out var staffId, out _);
        var nextNo = await db.RuleSetVersions.Where(row => row.PlanId == plan).MaxAsync(row => (int?)row.VersionNo, cancel) ?? 0;
        var draft = new RuleSetVersion
        {
            Id = Guid.CreateVersion7(),
            PlanId = plan,
            VersionNo = nextNo + 1,
            Status = RuleSetStatus.Draft,
            BasedOnVersionId = request.BasedOnVersionId,
            ChangeNote = request.ChangeNote,
            CreatedBy = staffId,
            CreatedAt = clock.GetUtcNow(),
        };

        db.RuleSetVersions.Add(draft);
        db.BenefitRules.AddRange(await StartingRulesAsync(db, draft, cancel));
        db.AuditEvents.Add(AuditLog.Event(
            context,
            ActorType.User,
            staffId,
            null,
            "RULE_SET_DRAFT_CREATED",
            "rule_set_version",
            draft.Id.ToString(),
            after: new { draft.VersionNo, draft.BasedOnVersionId }));
        await db.SaveChangesAsync(cancel);

        var body = await RuleSetResponses.DetailAsync(db, draft, cancel);
        return Results.Created($"/api/v1/plans/{plan}/versions/{draft.Id}", body);
    }

    private static async Task<List<BenefitRule>> StartingRulesAsync(ClaimLensDbContext db, RuleSetVersion draft, CancellationToken cancel)
    {
        if (draft.BasedOnVersionId is not { } baseId)
        {
            return [RuleSetRules.Eligibility(draft.Id)];
        }

        var copied = await db.BenefitRules.AsNoTracking()
            .Where(rule => rule.RuleSetVersionId == baseId)
            .ToListAsync(cancel);

        var rules = copied
            .Select(rule => new BenefitRule
            {
                Id = Guid.CreateVersion7(),
                RuleSetVersionId = draft.Id,
                RuleCode = rule.RuleCode,
                RuleType = rule.RuleType,
                CategoryCode = rule.CategoryCode,
                ClauseText = rule.ClauseText,
                ClauseRef = rule.ClauseRef,
                ApplyOrder = rule.ApplyOrder,
                RateBp = rule.RateBp,
                LimitAmount = rule.LimitAmount,
                LimitScope = rule.LimitScope,
                WaitingDays = rule.WaitingDays,
            })
            .ToList();

        if (rules.All(rule => rule.RuleType != RuleType.Eligibility))
        {
            rules.Add(RuleSetRules.Eligibility(draft.Id));
        }

        return rules;
    }
}
