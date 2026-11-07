using System.Text.RegularExpressions;
using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using ClaimLens.Api.Features.RuleSets;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Intake;

public sealed record MemberSearchResult(
    Guid MemberId,
    string MemberCode,
    string FullName,
    DateOnly DateOfBirth,
    string NationalIdMasked,
    PolicySummary Policy);

public sealed record MemberSearchResponse(IReadOnlyList<MemberSearchResult> Items);

/// <summary>
/// US02 step 1. A name matches without its Vietnamese marks; a national ID or policy number must match exactly. One row
/// per member and policy, at most 20, the exact matches first. A POST, so a citizen ID never reaches a URL. The
/// middleware audits the read.
/// </summary>
public static partial class SearchMembers
{
    public const int MaxResults = 20;
    private const int MinLength = 2;
    private const int MaxLength = 120;

    private sealed record Request(string? Query);

    public static async Task<IResult> Handle(HttpContext context, ClaimLensDbContext db, TimeProvider clock, CancellationToken cancel)
    {
        var request = await Requests.ReadJsonAsync<Request>(context);
        var query = Spaces().Replace(request?.Query ?? "", " ").Trim();
        var problem = query.Length switch
        {
            0 => new FieldError("query", "REQUIRED", "Enter a name, national ID or policy number."),
            < MinLength => new FieldError("query", "TOO_SHORT", $"Enter at least {MinLength} characters."),
            > MaxLength => new FieldError("query", "TOO_LONG", $"Enter at most {MaxLength} characters."),
            _ => null,
        };
        if (problem is not null)
        {
            return Requests.Unreadable(context, problem);
        }

        var policyNumber = query.ToUpperInvariant();
        var pattern = "%" + Like.Escape(query) + "%";
        var rows = await (
                from member in db.Members.AsNoTracking()
                join policy in db.Policies.AsNoTracking() on member.Id equals policy.MemberId
                join plan in db.BenefitPlans.AsNoTracking() on policy.PlanId equals plan.Id
                let exact = member.NationalId == query || policy.PolicyNumber == policyNumber
                where exact || EF.Functions.ILike(Search.Unaccent(member.FullName), Search.Unaccent(pattern), Like.EscapeCharacter)
                orderby exact descending, member.FullName, member.Id, policy.CoverageStart descending, policy.Id
                select new { member, policy, plan })
            .Take(MaxResults)
            .ToListAsync(cancel);

        var summaries = new PolicySummaries(db);
        var today = VietnamDate.Today(clock);
        var items = new List<MemberSearchResult>(rows.Count);
        foreach (var row in rows)
        {
            items.Add(new MemberSearchResult(
                row.member.Id,
                row.member.MemberCode,
                row.member.FullName,
                row.member.DateOfBirth,
                Masking.NationalId(row.member.NationalId),
                await summaries.ForAsync(row.policy, row.plan, today, cancel)));
        }

        return Results.Ok(new MemberSearchResponse(items));
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Spaces();
}
