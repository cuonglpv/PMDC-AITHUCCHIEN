using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Features.Evidence;
using ClaimLens.Api.Features.RuleSets;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Lifecycle;

public sealed record ClaimMember(Guid Id, string MemberCode, string FullName, DateOnly DateOfBirth, string NationalIdMasked);

public sealed record ClaimPolicy(
    Guid Id,
    string PolicyNumber,
    PolicyStatus Status,
    DateOnly CoverageStart,
    DateOnly CoverageEnd,
    string PlanCode,
    string PlanName);

public sealed record CurrentRevision(int RevisionNo, long PayableAmount, bool IsStale, DateTimeOffset CreatedAt);

public sealed record ClaimBalance(long? AnnualLimit, long ConsumedAmount, long? RemainingBalance, long? ProjectedAfter);

public sealed record ClaimCounts(int FieldsToVerify, int BlockingAlerts, int WarningAlerts, int MappingsToConfirm, int OpenFlags, int KeptFlags);

public enum ClinicalCheckState
{
    NotRun,
    Running,
    Done,
    Failed,
}

public sealed record ClaimAiState(bool FallbackActive, ClinicalCheckState ClinicalCheck);

/// <summary>What this caller can do now (contract AllowedAction). The web app renders these and never decides them.</summary>
public enum AllowedAction
{
    EditClaim,
    UploadDocument,
    SubmitClaim,
    StartReview,
    EditFields,
    DismissAlert,
    RunAdjudication,
    OverrideLine,
    ReviewFlags,
    SubmitRecommendation,
    Approve,
    PartiallyApprove,
    Reject,
    Return,
}

/// <summary>Why an action is not open yet, so the screen can name the missing item.</summary>
public sealed record Blocker(string Code, string Message);

/// <summary>
/// What a change did to the claim. Alerts, mappings and revisions stay at zero and null until Slices 4a and 4b fill them
/// through <see cref="ClaimRecheck"/>.
/// </summary>
public sealed record ClaimProgress(
    ClaimStatus Status,
    bool IsReturned,
    int RowVersion,
    int FieldsToVerify,
    int BlockingAlerts,
    int WarningAlerts,
    int MappingsToConfirm,
    int? CurrentRevisionNo,
    long? PayableAmount,
    bool IsRevisionStale,
    bool RevisionCreated);

public sealed record ClaimDetail(
    Guid Id,
    string ClaimNumber,
    ClaimStatus Status,
    bool IsReturned,
    string? ReturnComment,
    ClaimMember Member,
    ClaimPolicy Policy,
    string ProviderName,
    DateOnly ServiceDate,
    long ClaimedTotal,
    IntakeChannel Channel,
    Priority Priority,
    StaffRef RegisteredBy,
    StaffRef? Assignee,
    DateTimeOffset? AssignedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset StatusChangedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int RowVersion,
    RuleSetRef? RuleSetVersion,
    CurrentRevision? CurrentRevision,
    ClaimBalance? Balance,
    ClaimCounts? Counts,
    ClaimAiState Ai,
    IReadOnlyList<AllowedAction> AllowedActions,
    IReadOnlyList<Blocker> Blockers);

public sealed record PageState(Guid Id, int ClaimPageNo, PageType PageType, PageQuality Quality, PageAiStatus AiStatus);

public sealed record DocumentState(Guid Id, DocumentStatus Status, string? RejectionReason, IReadOnlyList<PageState> Pages);

public sealed record AiRunState(
    Guid Id,
    AiTask Task,
    AiRunStatus Status,
    string? ErrorCode,
    string ModelName,
    string ModelVersion,
    DateTimeOffset RequestedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt);

/// <summary>The polling answer. No medical data, identity data or file names, so it is not audited (BR-12).</summary>
public sealed record ClaimStatusSnapshot(
    Guid ClaimId,
    ClaimStatus Status,
    bool IsReturned,
    int RowVersion,
    bool FallbackActive,
    ClinicalCheckState ClinicalCheck,
    int? CurrentRevisionNo,
    bool IsRevisionStale,
    IReadOnlyList<DocumentState> Documents,
    IReadOnlyList<AiRunState> AiRuns);

/// <summary>Builds the claim views that several operations answer with.</summary>
public static class ClaimResponses
{
    public static async Task<ClaimDetail> DetailAsync(
        ClaimLensDbContext db,
        QueuePriority priority,
        Guid claimId,
        Guid viewerId,
        StaffRole viewer,
        CancellationToken cancel)
    {
        var claim = await db.Claims.AsNoTracking().SingleAsync(row => row.Id == claimId, cancel);
        var member = await db.Members.AsNoTracking().SingleAsync(row => row.Id == claim.MemberId, cancel);
        var policy = await db.Policies.AsNoTracking().SingleAsync(row => row.Id == claim.PolicyId, cancel);
        var plan = await db.BenefitPlans.AsNoTracking().SingleAsync(row => row.Id == policy.PlanId, cancel);

        var staffIds = new[] { claim.RegisteredBy, claim.AssigneeId ?? claim.RegisteredBy }.Distinct().ToList();
        var staff = await db.StaffAccounts.AsNoTracking()
            .Where(account => staffIds.Contains(account.Id))
            .Select(account => new StaffRef(account.Id, account.FullName, account.Initials))
            .ToDictionaryAsync(account => account.Id, cancel);

        var version = await EffectiveRuleSet.OnAsync(db, policy.PlanId, claim.ServiceDate, cancel);
        var returned = await ReturnAsync(db, claim, cancel);
        var ai = await AiStateAsync(db, claim.Id, cancel);
        var evidence = viewer == StaffRole.IntakeOfficer ? null : await ReviewReadiness.ForClaimAsync(db, claim.Id, cancel);
        var (actions, blockers) = viewer == StaffRole.IntakeOfficer
            ? await IntakeActionsAsync(db, claim, cancel)
            : ReviewActions(claim, viewerId, viewer, evidence!);

        // An Intake Officer gets the same shape with the reviewer's numbers left out (contract, getClaim).
        ClaimBalance? balance = null;
        if (viewer != StaffRole.IntakeOfficer)
        {
            var annual = version is null ? null : await EffectiveRuleSet.AnnualLimitAsync(db, version.Id, cancel);
            balance = new ClaimBalance(annual, policy.ConsumedAmount, Remaining(annual, policy.ConsumedAmount), null);
        }

        return new ClaimDetail(
            claim.Id,
            claim.ClaimNumber,
            claim.Status,
            returned.IsReturned,
            returned.Comment,
            new ClaimMember(member.Id, member.MemberCode, member.FullName, member.DateOfBirth, Masking.NationalId(member.NationalId)),
            new ClaimPolicy(policy.Id, policy.PolicyNumber, policy.Status, policy.CoverageStart, policy.CoverageEnd, plan.Code, plan.Name),
            claim.ProviderName,
            claim.ServiceDate,
            claim.ClaimedTotal,
            claim.Channel,
            priority.Of(claim.ClaimedTotal),
            staff[claim.RegisteredBy],
            claim.AssigneeId is { } assignee ? staff[assignee] : null,
            claim.AssignedAt,
            claim.SubmittedAt,
            claim.StatusChangedAt,
            claim.CreatedAt,
            claim.UpdatedAt,
            claim.RowVersion,
            version is null ? null : new RuleSetRef(version.Id, version.VersionNo, version.EffectiveFrom!.Value),
            // Calculation revisions arrive with adjudication (US11, US12); until then no claim has one.
            CurrentRevision: null,
            balance,
            // Fields are counted now; alerts, mappings and flags arrive with US07 to US14.
            evidence is null ? null : new ClaimCounts(evidence.FieldsToVerify, 0, 0, 0, 0, 0),
            ai,
            actions,
            blockers);
    }

    /// <summary>The answer to a change on a claim, after the change and any move it caused are saved.</summary>
    public static async Task<ClaimProgress> ProgressAsync(ClaimLensDbContext db, Claim claim, CancellationToken cancel)
    {
        var evidence = await ReviewReadiness.ForClaimAsync(db, claim.Id, cancel);
        var returned = await ReturnAsync(db, claim, cancel);
        return new ClaimProgress(
            claim.Status,
            returned.IsReturned,
            claim.RowVersion,
            evidence.FieldsToVerify,
            BlockingAlerts: 0,
            WarningAlerts: 0,
            MappingsToConfirm: 0,
            CurrentRevisionNo: null,
            PayableAmount: null,
            IsRevisionStale: false,
            RevisionCreated: false);
    }

    public static async Task<ClaimStatusSnapshot> StatusAsync(ClaimLensDbContext db, Guid claimId, CancellationToken cancel)
    {
        var claim = await db.Claims.AsNoTracking().SingleAsync(row => row.Id == claimId, cancel);
        var documents = await db.Documents.AsNoTracking()
            .Where(document => document.ClaimId == claimId && document.Status != DocumentStatus.Removed)
            .OrderBy(document => document.UploadedAt)
            .ThenBy(document => document.Id)
            .Select(document => new { document.Id, document.Status, document.RejectionReason })
            .ToListAsync(cancel);
        var pages = await db.DocumentPages.AsNoTracking()
            .Where(page => page.ClaimId == claimId)
            .OrderBy(page => page.ClaimPageNo)
            .ToListAsync(cancel);
        var runs = await LatestRunsAsync(db, claimId, cancel);
        var returned = await ReturnAsync(db, claim, cancel);

        return new ClaimStatusSnapshot(
            claim.Id,
            claim.Status,
            returned.IsReturned,
            claim.RowVersion,
            runs.Any(run => run.Status == AiRunStatus.Failed),
            ClinicalCheck(runs),
            CurrentRevisionNo: null,
            IsRevisionStale: false,
            documents
                .Select(document => new DocumentState(
                    document.Id,
                    document.Status,
                    document.RejectionReason,
                    pages
                        .Where(page => page.DocumentId == document.Id)
                        .Select(page => new PageState(page.Id, page.ClaimPageNo, page.PageType, page.Quality, page.AiStatus))
                        .ToList()))
                .ToList(),
            runs
                .Select(run => new AiRunState(
                    run.Id,
                    run.Task,
                    run.Status,
                    run.ErrorCode,
                    run.ModelName,
                    run.ModelVersion,
                    run.RequestedAt,
                    run.StartedAt,
                    run.FinishedAt))
                .ToList());
    }

    public static long? Remaining(long? annualLimit, long consumed) =>
        annualLimit is { } limit ? Math.Max(0, limit - consumed) : null;

    /// <summary>
    /// The latest run of each task, or of each file or page for the tasks scoped to one. Runs scoped to a removed
    /// file are left out: their pages are gone.
    /// </summary>
    private static async Task<List<AiRun>> LatestRunsAsync(ClaimLensDbContext db, Guid claimId, CancellationToken cancel)
    {
        var removed = db.Documents
            .Where(document => document.ClaimId == claimId && document.Status == DocumentStatus.Removed)
            .Select(document => (Guid?)document.Id);
        var runs = await db.AiRuns.AsNoTracking()
            .Where(run => run.ClaimId == claimId && (run.ScopeDocumentId == null || !removed.Contains(run.ScopeDocumentId)))
            .ToListAsync(cancel);

        return runs
            .GroupBy(run => (run.Task, run.ScopeDocumentId, run.ScopePageId))
            .Select(group => group.OrderByDescending(run => run.RequestedAt).ThenByDescending(run => run.Id).First())
            .OrderBy(run => run.RequestedAt)
            .ThenBy(run => run.Id)
            .ToList();
    }

    private static async Task<ClaimAiState> AiStateAsync(ClaimLensDbContext db, Guid claimId, CancellationToken cancel)
    {
        var runs = await LatestRunsAsync(db, claimId, cancel);
        return new ClaimAiState(runs.Any(run => run.Status == AiRunStatus.Failed), ClinicalCheck(runs));
    }

    private static ClinicalCheckState ClinicalCheck(IEnumerable<AiRun> runs) =>
        runs.LastOrDefault(run => run.Task == AiTask.ClinicalCheck)?.Status switch
        {
            null => ClinicalCheckState.NotRun,
            AiRunStatus.Queued or AiRunStatus.Running => ClinicalCheckState.Running,
            AiRunStatus.Succeeded => ClinicalCheckState.Done,
            _ => ClinicalCheckState.Failed,
        };

    private sealed record ReturnRow(string Status, string? DecisionComment);

    /// <summary>Returned is IN_REVIEW whose latest recommendation the approver sent back (docs/06-data-model.md).</summary>
    private static async Task<(bool IsReturned, string? Comment)> ReturnAsync(ClaimLensDbContext db, Claim claim, CancellationToken cancel)
    {
        if (claim.Status != ClaimStatus.InReview)
        {
            return (false, null);
        }

        var latest = await db.Database
            .SqlQuery<ReturnRow>($"""
                select status::text as "Status", decision_comment as "DecisionComment"
                from recommendation
                where claim_id = {claim.Id}
                order by created_at desc, id desc
                limit 1
                """)
            .ToListAsync(cancel);

        return latest is [{ Status: "RETURNED" } row] ? (true, row.DecisionComment) : (false, null);
    }

    private sealed record LatestRecommendation(Guid ClaimId, string Status);

    /// <summary>Which of these IN_REVIEW claims were returned, in one query for a queue page.</summary>
    public static async Task<HashSet<Guid>> ReturnedAsync(ClaimLensDbContext db, IReadOnlyCollection<Guid> inReview, CancellationToken cancel)
    {
        if (inReview.Count == 0)
        {
            return [];
        }

        var ids = inReview.ToArray();
        var latest = await db.Database
            .SqlQuery<LatestRecommendation>($"""
                select distinct on (claim_id) claim_id as "ClaimId", status::text as "Status"
                from recommendation
                where claim_id = any({ids})
                order by claim_id, created_at desc, id desc
                """)
            .ToListAsync(cancel);

        return latest.Where(row => row.Status == "RETURNED").Select(row => row.ClaimId).ToHashSet();
    }

    /// <summary>
    /// START_REVIEW for a claim nobody has taken, EDIT_FIELDS for the assignee while it can still change, with what is
    /// left named. The approver reads only until Slice 5; the later actions arrive with US07 onwards.
    /// </summary>
    private static (List<AllowedAction> Actions, List<Blocker> Blockers) ReviewActions(
        Claim claim,
        Guid viewerId,
        StaffRole viewer,
        ClaimEvidence evidence)
    {
        if (viewer != StaffRole.ClaimsReviewer)
        {
            return ([], []);
        }

        if (claim.AssigneeId is null)
        {
            return claim.Status is ClaimStatus.NeedsVerification or ClaimStatus.ReadyForReview
                ? ([AllowedAction.StartReview], [])
                : ([], []);
        }

        if (claim.AssigneeId != viewerId || claim.Status is not (ClaimStatus.NeedsVerification or ClaimStatus.InReview))
        {
            return ([], []);
        }

        var blockers = new List<Blocker>();
        if (evidence.FieldsToVerify > 0)
        {
            blockers.Add(new Blocker(ErrorCodes.FieldsToVerify, $"{evidence.FieldsToVerify} {Plural(evidence.FieldsToVerify, "field")} left to verify."));
        }

        if (evidence.PagesToClassify > 0)
        {
            blockers.Add(new Blocker(ErrorCodes.PageUnclassified, $"{evidence.PagesToClassify} {Plural(evidence.PagesToClassify, "page")} still needs a type."));
        }

        return ([AllowedAction.EditFields], blockers);
    }

    private static string Plural(int count, string word) => count == 1 ? word : word + "s";

    private static async Task<(List<AllowedAction> Actions, List<Blocker> Blockers)> IntakeActionsAsync(
        ClaimLensDbContext db,
        Claim claim,
        CancellationToken cancel)
    {
        if (claim.Status != ClaimStatus.Draft)
        {
            return ([], []);
        }

        var statuses = await db.Documents.AsNoTracking()
            .Where(document => document.ClaimId == claim.Id)
            .Select(document => document.Status)
            .ToListAsync(cancel);

        var blockers = new List<Blocker>();
        if (statuses.Any(status => status is DocumentStatus.Uploading or DocumentStatus.Converting))
        {
            blockers.Add(new Blocker("UPLOAD_IN_PROGRESS", "Wait until every file has finished processing."));
        }

        if (!statuses.Contains(DocumentStatus.Ready))
        {
            blockers.Add(new Blocker("NO_READY_DOCUMENT", "Add at least one file that is ready."));
        }

        List<AllowedAction> actions = [AllowedAction.EditClaim, AllowedAction.UploadDocument];
        if (blockers.Count == 0)
        {
            actions.Add(AllowedAction.SubmitClaim);
        }

        return (actions, blockers);
    }
}
