using System.Net;
using ClaimLens.Api.Domain.Enums;
using static ClaimLens.Api.Tests.AiJobs.AiData;
using static ClaimLens.Api.Tests.Features.IntakeData;
using static ClaimLens.Api.Tests.Review.ReviewData;

namespace ClaimLens.Api.Tests.Review;

/// <summary>US23: a reviewer takes a claim, and each claim has one owner.</summary>
public sealed class StartReviewTests(ApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_ready_claim_moves_to_in_review_and_the_reviewer_sees_it_in_full()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await ExtractedAsync(api, PageType.Invoice, CleanInvoice);
        Assert.Equal("READY_FOR_REVIEW", await ClaimStatus(api, claim));

        var detail = await StartAsync(reviewer, claim);

        Assert.Equal("IN_REVIEW", detail.GetProperty("status").GetString());
        Assert.Equal(Staff.ReviewerId, detail.GetProperty("assignee").GetProperty("id").GetString());
        Assert.StartsWith("Lê Thị Thử", detail.GetProperty("member").GetProperty("fullName").GetString(), StringComparison.Ordinal);
        Assert.Contains("EDIT_FIELDS", detail.GetProperty("allowedActions").EnumerateArray().Select(action => action.GetString()));
        Assert.Equal(1, await api.Scalar<long>(
            $"""
            select count(*) from audit_event
            where claim_id = '{claim}' and event_type = 'REVIEW_STARTED' and actor_id = '{Staff.ReviewerId}' and actor_type = 'USER'
              and before_value->>'status' = 'READY_FOR_REVIEW' and after_value->>'status' = 'IN_REVIEW'
              and after_value->>'assigneeId' = '{Staff.ReviewerId}'
            """));
    }

    [Fact]
    public async Task A_claim_needing_verification_is_assigned_and_stays_where_it_is()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await ExtractedAsync(api, PageType.Invoice, InvoiceToCheck);

        var detail = await StartAsync(reviewer, claim);

        Assert.Equal("NEEDS_VERIFICATION", detail.GetProperty("status").GetString());
        Assert.Equal(Staff.ReviewerId, detail.GetProperty("assignee").GetProperty("id").GetString());
        Assert.Equal(1, detail.GetProperty("counts").GetProperty("fieldsToVerify").GetInt32());
    }

    [Fact]
    public async Task Another_reviewers_claim_cannot_be_taken()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var other = await api.SignedInAs(Staff.OtherReviewer);
        var (claim, _) = await ExtractedAsync(api, PageType.Invoice, CleanInvoice);
        await StartAsync(reviewer, claim);

        var response = await other.PostAsync($"claims/{claim}/start-review", null, Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("CLAIM_ALREADY_ASSIGNED", await Code(response));
        Assert.Equal(1, await EventsAsync(api, claim, "REVIEW_STARTED"));
    }

    [Fact]
    public async Task Repeating_the_call_as_the_same_reviewer_succeeds_and_changes_nothing()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await ExtractedAsync(api, PageType.Invoice, CleanInvoice);
        var first = await StartAsync(reviewer, claim);

        var second = await StartAsync(reviewer, claim);

        Assert.Equal(first.GetProperty("rowVersion").GetInt32(), second.GetProperty("rowVersion").GetInt32());
        Assert.Equal(1, await EventsAsync(api, claim, "REVIEW_STARTED"));
    }

    [Fact]
    public async Task Two_reviewers_racing_for_one_claim_get_one_success_and_one_409()
    {
        var one = await api.SignedInAs(Staff.Reviewer);
        var two = await api.SignedInAs(Staff.OtherReviewer);

        for (var round = 0; round < 5; round++)
        {
            var (claim, _) = await ExtractedAsync(api, PageType.Invoice, CleanInvoice);

            var answers = await Task.WhenAll(
                one.PostAsync($"claims/{claim}/start-review", null, Cancel),
                two.PostAsync($"claims/{claim}/start-review", null, Cancel));

            Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], answers.Select(answer => answer.StatusCode).Order());
            Assert.Equal("CLAIM_ALREADY_ASSIGNED", await Code(answers.Single(answer => answer.StatusCode == HttpStatusCode.Conflict)));
            Assert.Equal(1, await EventsAsync(api, claim, "REVIEW_STARTED"));
            Assert.Equal("IN_REVIEW", await ClaimStatus(api, claim));
        }
    }

    [Fact]
    public async Task A_claim_past_review_cannot_be_taken()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (_, other) = await NewReviewerAsync(api);
        var claim = await InsertClaimAsync(api, "S" + Guid.CreateVersion7().ToString("N")[^10..], "PENDING_APPROVAL", assignee: other);

        var response = await reviewer.PostAsync($"claims/{claim}/start-review", null, Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("CLAIM_STATE_CONFLICT", await Code(response));
    }
}
