using System.Globalization;
using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Features.Lifecycle;
using ClaimLens.Api.Features.RuleSets;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Intake;

/// <summary>US02: the claim starts as DRAFT with a unique number, and an audit event.</summary>
public static class CreateClaim
{
    private sealed record Request(
        Guid? MemberId,
        Guid? PolicyId,
        DateOnly? ServiceDate,
        string? ProviderName,
        long? ClaimedTotal,
        IntakeChannel? Channel);

    private sealed record NextNumber(long Value);

    public static async Task<IResult> Handle(
        HttpContext context,
        ClaimLensDbContext db,
        TimeProvider clock,
        QueuePriority priority,
        CancellationToken cancel)
    {
        var request = await Requests.ReadJsonAsync<Request>(context);
        if (request is null)
        {
            return Requests.Unreadable(context);
        }

        var providerName = request.ProviderName?.Trim() ?? "";
        var errors = new List<FieldError>();
        Require(errors, request.MemberId, "memberId", "Choose a member.");
        Require(errors, request.PolicyId, "policyId", "Choose a policy.");
        Require(errors, request.ServiceDate, "serviceDate", "Enter the service date.");
        Require(errors, request.ClaimedTotal, "claimedTotal", "Enter the claimed total.");
        Require(errors, request.Channel, "channel", "Choose how the claim arrived.");
        if (ClaimRules.ProviderName(providerName) is { } providerProblem)
        {
            errors.Add(providerProblem);
        }

        if (request.ClaimedTotal is { } total && ClaimRules.ClaimedTotal(total) is { } totalProblem)
        {
            errors.Add(totalProblem);
        }

        if (errors.Count > 0)
        {
            return Requests.Unreadable(context, [.. errors]);
        }

        var facts = new ClaimFacts(
            request.MemberId!.Value,
            request.PolicyId!.Value,
            request.ServiceDate!.Value,
            providerName,
            request.ClaimedTotal!.Value,
            request.Channel!.Value);
        var (policy, problem) = await ClaimRules.CheckPartiesAsync(context, db, facts.MemberId, facts.PolicyId, cancel);
        if (problem is not null)
        {
            return problem;
        }

        context.User.TryGetStaff(out var staffId, out var role);
        var now = clock.GetUtcNow();
        var claim = new Claim
        {
            Id = Guid.CreateVersion7(),
            ClaimNumber = await NumberAsync(db, clock, cancel),
            RegisteredBy = staffId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        facts.ApplyTo(claim);
        ClaimStatusMachine.Start(claim, now);

        db.Claims.Add(claim);
        db.AuditEvents.Add(AuditLog.Event(
            context,
            ActorType.User,
            staffId,
            claim.Id,
            "CLAIM_CREATED",
            "claim",
            claim.Id.ToString(),
            after: new { claim.ClaimNumber, claim.Status, facts.MemberId, facts.PolicyId, facts.ServiceDate, facts.ProviderName, facts.ClaimedTotal, facts.Channel }));
        await db.SaveChangesAsync(cancel);

        var detail = await ClaimResponses.DetailAsync(db, priority, claim.Id, staffId, role, cancel);
        return Results.Created($"/api/v1/claims/{claim.Id}", new ClaimSaved(detail, ClaimRules.Warnings(policy!, facts.ServiceDate)));
    }

    /// <summary>CLM-{year in Vietnam}-{six digits}. One sequence, never reset; gaps are allowed (docs/06-data-model.md).</summary>
    private static async Task<string> NumberAsync(ClaimLensDbContext db, TimeProvider clock, CancellationToken cancel)
    {
        var next = (await db.Database
                .SqlQuery<NextNumber>($"""select nextval('claim_number_seq') as "Value" """)
                .ToListAsync(cancel))
            .Single()
            .Value;
        return string.Create(CultureInfo.InvariantCulture, $"CLM-{VietnamDate.Today(clock).Year:D4}-{next:D6}");
    }

    private static void Require<T>(List<FieldError> errors, T? value, string field, string message)
        where T : struct
    {
        if (value is null)
        {
            errors.Add(new FieldError(field, "REQUIRED", message));
        }
    }
}
