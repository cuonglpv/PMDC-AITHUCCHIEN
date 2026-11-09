using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Features.Lifecycle;
using ClaimLens.Api.Persistence;
using Microsoft.Extensions.DependencyInjection;
using static ClaimLens.Api.Tests.AiJobs.AiData;
using static ClaimLens.Api.Tests.Features.IntakeData;
using static ClaimLens.Api.Tests.Review.ReviewData;

namespace ClaimLens.Api.Tests.Review;

/// <summary>getClaim for the people working on a claim, and the top-bar lookup by number (US23, BR-11).</summary>
public sealed class ClaimViewTests(ApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static IEnumerable<string?> Actions(JsonElement detail) =>
        detail.GetProperty("allowedActions").EnumerateArray().Select(action => action.GetString());

    private static IEnumerable<string?> Blockers(JsonElement detail) =>
        detail.GetProperty("blockers").EnumerateArray().Select(blocker => blocker.GetProperty("code").GetString());

    [Fact]
    public async Task The_assignee_may_edit_fields_and_is_told_how_many_are_left()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer);

        var detail = await JsonOf(await reviewer.GetAsync($"claims/{claim}", Cancel));

        Assert.Equal(["EDIT_FIELDS"], Actions(detail));
        var blocker = detail.GetProperty("blockers").EnumerateArray().Single();
        Assert.Equal("FIELDS_TO_VERIFY", blocker.GetProperty("code").GetString());
        Assert.Contains("1 field", blocker.GetProperty("message").GetString(), StringComparison.Ordinal);
        var counts = detail.GetProperty("counts");
        Assert.Equal(1, counts.GetProperty("fieldsToVerify").GetInt32());
        Assert.Equal(0, counts.GetProperty("blockingAlerts").GetInt32());
        Assert.Equal(0, counts.GetProperty("keptFlags").GetInt32());
    }

    [Fact]
    public async Task A_page_still_unclassified_is_named_too()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var intake = await api.SignedInAs(Staff.Intake);
        var (claim, file) = await DraftWithFileAsync(api, intake);
        api.Ai.AnswerPage(file.Page, PageType.Receipt, 0.64m);
        await api.RunAiJobsAsync();
        await SubmitAsync(intake, claim);
        await api.RunAiJobsAsync();

        var detail = await StartAsync(reviewer, claim);

        Assert.Equal(["PAGE_UNCLASSIFIED"], Blockers(detail));
    }

    [Fact]
    public async Task A_claim_with_nothing_left_has_no_blockers()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer, CleanInvoice);

        var detail = await JsonOf(await reviewer.GetAsync($"claims/{claim}", Cancel));

        Assert.Equal(["EDIT_FIELDS"], Actions(detail));
        Assert.Empty(Blockers(detail));
        Assert.Equal(0, detail.GetProperty("counts").GetProperty("fieldsToVerify").GetInt32());
    }

    [Fact]
    public async Task The_approver_reads_only()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var approver = await api.SignedInAs(Staff.Approver);
        var (claim, _) = await TakenAsync(api, reviewer);

        var detail = await JsonOf(await approver.GetAsync($"claims/{claim}", Cancel));

        Assert.Empty(Actions(detail));
        Assert.Empty(Blockers(detail));
        Assert.Equal(1, detail.GetProperty("counts").GetProperty("fieldsToVerify").GetInt32());
    }

    /// <summary>A reviewer cannot reach getClaim on a claim nobody has taken, so this reads the response builder directly.</summary>
    [Fact]
    public async Task An_unassigned_claim_offers_start_review_to_a_reviewer()
    {
        var (claim, _) = await ExtractedAsync(api, PageType.Invoice, InvoiceToCheck);
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClaimLensDbContext>();
        var priority = scope.ServiceProvider.GetRequiredService<QueuePriority>();

        var detail = await ClaimResponses.DetailAsync(db, priority, Guid.Parse(claim), Guid.Parse(Staff.ReviewerId), StaffRole.ClaimsReviewer, Cancel);

        Assert.Equal([AllowedAction.StartReview], detail.AllowedActions);
        Assert.Empty(detail.Blockers);
    }

    [Fact]
    public async Task A_reviewer_cannot_read_a_claim_before_taking_it()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await ExtractedAsync(api, PageType.Invoice, InvoiceToCheck);

        var response = await reviewer.GetAsync($"claims/{claim}", Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("CLAIM_OUT_OF_SCOPE", await Code(response));
    }

    [Fact]
    public async Task The_number_opens_a_claim_the_caller_may_see_and_the_read_is_audited_as_a_claim_view()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer);
        var number = await api.Scalar<string>($"select claim_number from claim where id = '{claim}'");
        var views = await EventsAsync(api, claim, "CLAIM_VIEWED");

        var response = await reviewer.GetAsync($"claim-numbers/{number}", Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(claim, Id(await JsonOf(response)));
        Assert.Equal(views + 1, await EventsAsync(api, claim, "CLAIM_VIEWED"));
    }

    [Fact]
    public async Task A_number_outside_the_callers_scope_answers_404_like_one_that_does_not_exist()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var other = await api.SignedInAs(Staff.OtherReviewer);
        var (taken, _) = await TakenAsync(api, other);
        var (available, _) = await ExtractedAsync(api, PageType.Invoice, InvoiceToCheck);
        var takenNumber = await api.Scalar<string>($"select claim_number from claim where id = '{taken}'");
        var availableNumber = await api.Scalar<string>($"select claim_number from claim where id = '{available}'");
        var views = await EventsAsync(api, taken, "CLAIM_VIEWED");

        foreach (var number in new[] { takenNumber, availableNumber, "CLM-2026-999998", "not-a-number" })
        {
            var response = await reviewer.GetAsync($"claim-numbers/{number}", Cancel);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("NOT_FOUND", await Code(response));
        }

        Assert.Equal(views, await EventsAsync(api, taken, "CLAIM_VIEWED"));
    }

    [Fact]
    public async Task The_officer_opens_only_a_claim_they_registered_and_the_approver_any()
    {
        var intake = await api.SignedInAs(Staff.Intake);
        var approver = await api.SignedInAs(Staff.Approver);
        await api.Sql(
            $"""
            insert into staff_account (id, email, password_hash, full_name, initials, role)
            values ('{Staff.OtherIntakeId}', 'intake2@claimlens.test', 'x', 'Other Intake', 'OI', 'INTAKE_OFFICER')
            on conflict do nothing
            """);
        var theirs = $"CLM-2026-9{RandomNumberGenerator.GetInt32(100_000):D5}";
        await InsertClaimAsync(api, theirs, "DRAFT", registeredBy: Staff.OtherIntakeId);
        var mine = await api.Scalar<string>($"select claim_number from claim where id = '{await NewDraftAsync(api, intake)}'");

        Assert.Equal(HttpStatusCode.OK, (await intake.GetAsync($"claim-numbers/{mine}", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intake.GetAsync($"claim-numbers/{theirs}", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await approver.GetAsync($"claim-numbers/{theirs}", Cancel)).StatusCode);
    }
}
