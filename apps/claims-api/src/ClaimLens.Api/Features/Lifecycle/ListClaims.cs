using System.Globalization;
using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Lifecycle;

public sealed record ClaimQueueItem(
    Guid Id,
    string ClaimNumber,
    ClaimStatus Status,
    bool IsReturned,
    string? MemberName,
    string PlanCode,
    DateOnly ServiceDate,
    long ClaimedTotal,
    Priority Priority,
    DateTimeOffset StatusChangedAt,
    string? AssigneeInitials,
    int FieldsToVerify,
    int BlockingAlerts,
    int WarningAlerts,
    int OpenFlags,
    long? PayableAmount,
    ClaimOutcome? RecommendedOutcome,
    DecisionAction? DecisionAction,
    DateTimeOffset? DecidedAt);

public sealed record ClaimQueuePage(
    IReadOnlyList<ClaimQueueItem> Items,
    int Page,
    int PageSize,
    int Total,
    IReadOnlyDictionary<string, int> Counts);

/// <summary>
/// The queue tabs. This slice builds the Intake Officer's two, MY_DRAFTS and MY_SUBMISSIONS; the reviewer's and the
/// approver's arrive with US23 and US18 and answer 501 until then. A view of another role is 403 FORBIDDEN_ROLE.
/// </summary>
public static class ListClaims
{
    public const string MyDrafts = "MY_DRAFTS";
    public const string MySubmissions = "MY_SUBMISSIONS";

    private static readonly Dictionary<StaffRole, string[]> ViewsByRole = new()
    {
        [StaffRole.IntakeOfficer] = [MyDrafts, MySubmissions],
        [StaffRole.ClaimsReviewer] = ["AVAILABLE", "ASSIGNED_TO_ME", "NEEDS_VERIFICATION", "RETURNED"],
        [StaffRole.SeniorApprover] = ["PENDING_APPROVAL", "DECIDED_BY_ME"],
    };

    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    private sealed record Filters(
        string View,
        Priority? Priority,
        string? PlanCode,
        DateOnly? ServiceDateFrom,
        DateOnly? ServiceDateTo,
        string? ClaimNumber,
        int Page,
        int PageSize);

    public static async Task<IResult> Handle(HttpContext context, ClaimLensDbContext db, QueuePriority priority, CancellationToken cancel)
    {
        context.User.TryGetStaff(out var staffId, out var role);
        var (filters, errors) = Read(context.Request.Query);
        if (filters is null)
        {
            return Problems.Of(context, StatusCodes.Status422UnprocessableEntity, ErrorCodes.ValidationFailed, "Check the highlighted fields", errors);
        }

        if (!ViewsByRole.TryGetValue(role, out var views) || !views.Contains(filters.View))
        {
            return Problems.Of(context, StatusCodes.Status403Forbidden, ErrorCodes.ForbiddenRole, "This queue belongs to another role");
        }

        if (role != StaffRole.IntakeOfficer)
        {
            return Results.StatusCode(StatusCodes.Status501NotImplemented);
        }

        var mine = db.Claims.AsNoTracking().Where(claim => claim.RegisteredBy == staffId);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [MyDrafts] = await mine.CountAsync(claim => claim.Status == ClaimStatus.Draft, cancel),
            [MySubmissions] = await mine.CountAsync(claim => claim.Status != ClaimStatus.Draft, cancel),
        };

        var rows =
            from claim in mine
            join policy in db.Policies on claim.PolicyId equals policy.Id
            join plan in db.BenefitPlans on policy.PlanId equals plan.Id
            join member in db.Members on claim.MemberId equals member.Id
            select new { claim, PlanCode = plan.Code, MemberName = member.FullName };

        rows = filters.View == MyDrafts
            ? rows.Where(row => row.claim.Status == ClaimStatus.Draft)
            : rows.Where(row => row.claim.Status != ClaimStatus.Draft);

        if (filters.Priority is { } wanted)
        {
            var threshold = priority.HighValueThreshold;
            rows = wanted == Priority.High
                ? rows.Where(row => row.claim.ClaimedTotal >= threshold)
                : rows.Where(row => row.claim.ClaimedTotal < threshold);
        }

        if (filters.PlanCode is { } planCode)
        {
            rows = rows.Where(row => row.PlanCode == planCode);
        }

        if (filters.ServiceDateFrom is { } from)
        {
            rows = rows.Where(row => row.claim.ServiceDate >= from);
        }

        if (filters.ServiceDateTo is { } to)
        {
            rows = rows.Where(row => row.claim.ServiceDate <= to);
        }

        if (filters.ClaimNumber is { } number)
        {
            var pattern = "%" + Like.Escape(number) + "%";
            rows = rows.Where(row => EF.Functions.ILike(row.claim.ClaimNumber, pattern, Like.EscapeCharacter));
        }

        var total = await rows.CountAsync(cancel);

        // Newest first: a draft by when it was registered, a submission by when it was handed over.
        var ordered = filters.View == MyDrafts
            ? rows.OrderByDescending(row => row.claim.CreatedAt).ThenByDescending(row => row.claim.Id)
            : rows.OrderByDescending(row => row.claim.SubmittedAt ?? row.claim.CreatedAt).ThenByDescending(row => row.claim.Id);
        var page = await ordered
            .Skip((filters.Page - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .ToListAsync(cancel);

        var assigneeIds = page.Select(row => row.claim.AssigneeId).OfType<Guid>().Distinct().ToList();
        var initials = await db.StaffAccounts.AsNoTracking()
            .Where(account => assigneeIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, account => account.Initials, cancel);
        var returned = await ClaimResponses.ReturnedAsync(
            db,
            page.Where(row => row.claim.Status == ClaimStatus.InReview).Select(row => row.claim.Id).ToList(),
            cancel);

        // Alert, field and flag counts, payables and decisions belong to the reviewer's and approver's views.
        var items = page
            .Select(row => new ClaimQueueItem(
                row.claim.Id,
                row.claim.ClaimNumber,
                row.claim.Status,
                returned.Contains(row.claim.Id),
                row.MemberName,
                row.PlanCode,
                row.claim.ServiceDate,
                row.claim.ClaimedTotal,
                priority.Of(row.claim.ClaimedTotal),
                row.claim.StatusChangedAt,
                row.claim.AssigneeId is { } assignee ? initials[assignee] : null,
                FieldsToVerify: 0,
                BlockingAlerts: 0,
                WarningAlerts: 0,
                OpenFlags: 0,
                PayableAmount: null,
                RecommendedOutcome: null,
                DecisionAction: null,
                DecidedAt: null))
            .ToList();

        return Results.Ok(new ClaimQueuePage(items, filters.Page, filters.PageSize, total, counts));
    }

    private static (Filters? Filters, List<FieldError> Errors) Read(IQueryCollection query)
    {
        var errors = new List<FieldError>();

        var view = query["view"].ToString();
        if (view.Length == 0)
        {
            errors.Add(new FieldError("view", "REQUIRED", "Choose a queue."));
        }
        else if (!ViewsByRole.Values.Any(views => views.Contains(view)))
        {
            errors.Add(new FieldError("view", "OUT_OF_RANGE", "That is not a queue."));
        }

        Priority? priority = query["priority"].ToString() switch
        {
            "" => null,
            "HIGH" => Priority.High,
            "NORMAL" => Priority.Normal,
            _ => Error(errors, "priority", "OUT_OF_RANGE", "Priority is HIGH or NORMAL."),
        };

        var planCode = Text(query, "planCode", 20, errors);
        var claimNumber = Text(query, "claimNumber", 20, errors);
        var from = Date(query, "serviceDateFrom", errors);
        var to = Date(query, "serviceDateTo", errors);
        if (from > to)
        {
            errors.Add(new FieldError("serviceDateTo", "OUT_OF_RANGE", "The last day is before the first."));
        }

        var page = Number(query, "page", 1, 1, int.MaxValue, errors);
        var pageSize = Number(query, "pageSize", DefaultPageSize, 1, MaxPageSize, errors);

        return errors.Count > 0
            ? (null, errors)
            : (new Filters(view, priority, planCode, from, to, claimNumber, page, pageSize), errors);
    }

    private static Priority? Error(List<FieldError> errors, string field, string code, string message)
    {
        errors.Add(new FieldError(field, code, message));
        return null;
    }

    private static string? Text(IQueryCollection query, string name, int maxLength, List<FieldError> errors)
    {
        var value = query[name].ToString().Trim();
        if (value.Length > maxLength)
        {
            errors.Add(new FieldError(name, "TOO_LONG", $"At most {maxLength} characters."));
        }

        return value.Length == 0 ? null : value;
    }

    private static DateOnly? Date(IQueryCollection query, string name, List<FieldError> errors)
    {
        var value = query[name].ToString();
        if (value.Length == 0)
        {
            return null;
        }

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        errors.Add(new FieldError(name, "INVALID", "Use a date as YYYY-MM-DD."));
        return null;
    }

    private static int Number(IQueryCollection query, string name, int fallback, int min, int max, List<FieldError> errors)
    {
        var value = query[name].ToString();
        if (value.Length == 0)
        {
            return fallback;
        }

        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= min && number <= max)
        {
            return number;
        }

        errors.Add(new FieldError(name, "OUT_OF_RANGE", $"Use a whole number from {min} to {max}."));
        return fallback;
    }
}
