using System.Globalization;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Features.Lifecycle;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Intake;

/// <summary>Something the officer should see that does not stop registration (contract Warning).</summary>
public sealed record Warning(string Code, string Message);

public sealed record ClaimSaved(ClaimDetail Claim, IReadOnlyList<Warning> Warnings);

/// <summary>What intake types for a claim, after a create or the merge of an edit.</summary>
public sealed record ClaimFacts(Guid MemberId, Guid PolicyId, DateOnly ServiceDate, string ProviderName, long ClaimedTotal, IntakeChannel Channel)
{
    public static ClaimFacts Of(Claim claim) =>
        new(claim.MemberId, claim.PolicyId, claim.ServiceDate, claim.ProviderName, claim.ClaimedTotal, claim.Channel);

    public void ApplyTo(Claim claim)
    {
        claim.MemberId = MemberId;
        claim.PolicyId = PolicyId;
        claim.ServiceDate = ServiceDate;
        claim.ProviderName = ProviderName;
        claim.ClaimedTotal = ClaimedTotal;
        claim.Channel = Channel;
    }
}

/// <summary>The rules createClaim and updateClaim share (US02).</summary>
public static class ClaimRules
{
    public const int ProviderNameLength = 200;

    public static FieldError? ProviderName(string providerName) =>
        providerName.Length switch
        {
            0 => new FieldError("providerName", "REQUIRED", "Enter the provider."),
            > ProviderNameLength => new FieldError("providerName", "TOO_LONG", $"A provider name has at most {ProviderNameLength} characters."),
            _ => null,
        };

    public static FieldError? ClaimedTotal(long claimedTotal) =>
        claimedTotal < 1 ? new FieldError("claimedTotal", "OUT_OF_RANGE", "The claimed total is at least 1 ₫.") : null;

    /// <summary>
    /// The member and policy exist and belong together. "No member found" means no claim can be created (US02); the
    /// claim_policy_member foreign key repeats the pairing.
    /// </summary>
    public static async Task<(Policy? Policy, IResult? Problem)> CheckPartiesAsync(
        HttpContext context,
        ClaimLensDbContext db,
        Guid memberId,
        Guid policyId,
        CancellationToken cancel)
    {
        var errors = new List<FieldError>();
        if (!await db.Members.AnyAsync(member => member.Id == memberId, cancel))
        {
            errors.Add(new FieldError("memberId", "NOT_FOUND", "No member found."));
        }

        var policy = await db.Policies.AsNoTracking().FirstOrDefaultAsync(row => row.Id == policyId, cancel);
        if (policy is null)
        {
            errors.Add(new FieldError("policyId", "NOT_FOUND", "No policy found."));
        }

        if (errors.Count > 0)
        {
            return (null, Requests.Unreadable(context, [.. errors]));
        }

        if (policy!.MemberId != memberId)
        {
            return (null, Problems.Of(
                context,
                StatusCodes.Status422UnprocessableEntity,
                ErrorCodes.PolicyMemberMismatch,
                "The policy belongs to another member",
                [new FieldError("policyId", "POLICY_MEMBER_MISMATCH", "This policy belongs to another member.")]));
        }

        return (policy, null);
    }

    /// <summary>
    /// A service date outside the policy year, or a policy that is not active, comes back as a warning: registration is
    /// allowed, and rejecting the claim later still needs a Senior Approver (US02, BR-01).
    /// </summary>
    public static List<Warning> Warnings(Policy policy, DateOnly serviceDate)
    {
        var warnings = new List<Warning>();
        if (serviceDate < policy.CoverageStart || serviceDate > policy.CoverageEnd)
        {
            warnings.Add(new Warning(
                "COVERAGE_OUTSIDE_PERIOD",
                Format($"The service date is outside the policy year, {policy.CoverageStart:dd/MM/yyyy} to {policy.CoverageEnd:dd/MM/yyyy}.")));
        }

        // A policy that is not ACTIVE always has terminated_on (ck_policy_terminated_on); ELIG-01 treats it as not in
        // force on and after that day (Q-A).
        if (policy.Status != PolicyStatus.Active)
        {
            var status = policy.Status.ToString().ToLowerInvariant();
            warnings.Add(new Warning(
                "POLICY_NOT_ACTIVE",
                policy.TerminatedOn is { } ended && serviceDate >= ended
                    ? Format($"The policy is {status} and not in force on the service date: it ended on {ended:dd/MM/yyyy}.")
                    : Format($"The policy is {status} from {policy.TerminatedOn:dd/MM/yyyy}.")));
        }

        return warnings;
    }

    private static string Format(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
