using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Persistence;
using ClaimLens.RuleEngine;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.RuleSets;

public sealed record Items<T>(IReadOnlyList<T> items);

public sealed record RuleSetRef(Guid Id, int VersionNo, DateOnly EffectiveFrom);

public sealed record PlanResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    RuleSetRef? CurrentVersion,
    Guid? DraftVersionId);

public sealed record StaffRef(Guid Id, string FullName, string Initials);

public sealed record VersionSummary(
    Guid Id,
    Guid PlanId,
    int VersionNo,
    RuleSetStatus Status,
    DateOnly? EffectiveFrom,
    Guid? BasedOnVersionId,
    string? ChangeNote,
    StaffRef CreatedBy,
    DateTimeOffset CreatedAt,
    StaffRef? PublishedBy,
    DateTimeOffset? PublishedAt);

public sealed record VersionDetail(
    Guid Id,
    Guid PlanId,
    int VersionNo,
    RuleSetStatus Status,
    DateOnly? EffectiveFrom,
    Guid? BasedOnVersionId,
    string? ChangeNote,
    StaffRef CreatedBy,
    DateTimeOffset CreatedAt,
    StaffRef? PublishedBy,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<RuleResponse> Rules);

public sealed record RuleResponse(
    Guid Id,
    string RuleCode,
    RuleType RuleType,
    string? CategoryCode,
    string ClauseText,
    string? ClauseRef,
    int ApplyOrder,
    int? RateBp,
    long? LimitAmount,
    LimitScope? LimitScope,
    int? WaitingDays);

public static class RuleSetResponses
{
    public static async Task<List<VersionSummary>> SummariesAsync(
        ClaimLensDbContext db,
        IReadOnlyList<RuleSetVersion> versions,
        CancellationToken cancel)
    {
        var ids = versions
            .SelectMany(version => new[] { version.CreatedBy, version.PublishedBy ?? version.CreatedBy })
            .Distinct()
            .ToList();
        var staff = await db.StaffAccounts.AsNoTracking()
            .Where(account => ids.Contains(account.Id))
            .Select(account => new StaffRef(account.Id, account.FullName, account.Initials))
            .ToDictionaryAsync(account => account.Id, cancel);

        return versions
            .Select(version => new VersionSummary(
                version.Id,
                version.PlanId,
                version.VersionNo,
                version.Status,
                version.EffectiveFrom,
                version.BasedOnVersionId,
                version.ChangeNote,
                staff[version.CreatedBy],
                version.CreatedAt,
                version.PublishedBy is { } by ? staff[by] : null,
                version.PublishedAt))
            .ToList();
    }

    public static async Task<VersionDetail> DetailAsync(ClaimLensDbContext db, RuleSetVersion version, CancellationToken cancel)
    {
        var summary = (await SummariesAsync(db, [version], cancel))[0];
        var rules = await db.BenefitRules.AsNoTracking()
            .Where(rule => rule.RuleSetVersionId == version.Id)
            .OrderBy(rule => rule.ApplyOrder)
            .ThenBy(rule => rule.RuleCode)
            .ToListAsync(cancel);

        return new VersionDetail(
            summary.Id,
            summary.PlanId,
            summary.VersionNo,
            summary.Status,
            summary.EffectiveFrom,
            summary.BasedOnVersionId,
            summary.ChangeNote,
            summary.CreatedBy,
            summary.CreatedAt,
            summary.PublishedBy,
            summary.PublishedAt,
            rules.Select(ToResponse).ToList());
    }

    public static RuleResponse ToResponse(BenefitRule rule) =>
        new(
            rule.Id,
            rule.RuleCode,
            rule.RuleType,
            rule.CategoryCode,
            rule.ClauseText,
            rule.ClauseRef,
            rule.ApplyOrder,
            rule.RateBp,
            rule.LimitAmount,
            rule.LimitScope,
            rule.WaitingDays);

    /// <summary>A version is reached only through its own plan, so a version of another plan is simply not found.</summary>
    public static async Task<RuleSetVersion?> FindVersionAsync(
        ClaimLensDbContext db,
        string planId,
        string versionId,
        CancellationToken cancel)
    {
        if (!Guid.TryParse(planId, out var plan) || !Guid.TryParse(versionId, out var version))
        {
            return null;
        }

        return await db.RuleSetVersions.FirstOrDefaultAsync(row => row.Id == version && row.PlanId == plan, cancel);
    }
}
