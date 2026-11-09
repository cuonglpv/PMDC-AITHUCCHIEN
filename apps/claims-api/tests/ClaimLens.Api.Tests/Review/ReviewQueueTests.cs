using System.Net;
using System.Text.Json;
using static ClaimLens.Api.Tests.Features.IntakeData;
using static ClaimLens.Api.Tests.Review.ReviewData;

namespace ClaimLens.Api.Tests.Review;

/// <summary>US23: the reviewer's four tabs, BR-09 order and what the Available tab may show (BR-11).</summary>
public sealed class ReviewQueueTests(ApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static string Tag() => "Q" + Guid.CreateVersion7().ToString("N")[^8..].ToUpperInvariant();

    private static async Task<JsonElement> ViewAsync(HttpClient client, string view, string query = "")
    {
        var response = await client.GetAsync($"claims?view={view}{query}", Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await JsonOf(response);
    }

    private static List<string> Numbers(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("claimNumber").GetString()!).ToList();

    [Fact]
    public async Task Available_lists_unassigned_claims_with_no_member_name_and_counts_the_fields_to_verify()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await ExtractedAsync(api, Domain.Enums.PageType.Invoice, InvoiceToCheck);
        var number = await api.Scalar<string>($"select claim_number from claim where id = '{claim}'");
        var member = await api.Scalar<string>($"select m.full_name from member m join claim c on c.member_id = m.id where c.id = '{claim}'");
        var plan = await api.Scalar<string>($"select code from benefit_plan where id = '{GoldPlan}'");

        var response = await reviewer.GetAsync($"claims?view=AVAILABLE&claimNumber={number}", Cancel);

        Assert.DoesNotContain(member!, await response.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        var item = (await JsonOf(response)).GetProperty("items").EnumerateArray().Single();
        Assert.Equal(JsonValueKind.Null, item.GetProperty("memberName").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("assigneeInitials").ValueKind);
        Assert.Equal("NEEDS_VERIFICATION", item.GetProperty("status").GetString());
        Assert.Equal(plan, item.GetProperty("planCode").GetString());
        Assert.Equal("2026-10-12", item.GetProperty("serviceDate").GetString());
        Assert.Equal(1_400_000, item.GetProperty("claimedTotal").GetInt64());
        Assert.Equal(1, item.GetProperty("fieldsToVerify").GetInt32());
    }

    [Theory]
    [InlineData("&planCode=X")]
    [InlineData("&priority=HIGH")]
    [InlineData("&serviceDateFrom=2026-01-01")]
    [InlineData("&serviceDateTo=2026-12-31")]
    public async Task Available_can_only_be_filtered_by_claim_number(string filter)
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);

        var response = await reviewer.GetAsync($"claims?view=AVAILABLE{filter}", Cancel);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", await Code(response));
    }

    [Fact]
    public async Task Each_tab_holds_its_own_claims_and_counts_gives_all_four_badges()
    {
        var (reviewer, me) = await NewReviewerAsync(api);
        var (_, other) = await NewReviewerAsync(api);
        var tag = Tag();
        await InsertClaimAsync(api, $"{tag}-A1", "NEEDS_VERIFICATION");
        await InsertClaimAsync(api, $"{tag}-A2", "READY_FOR_REVIEW");
        await InsertClaimAsync(api, $"{tag}-NV", "NEEDS_VERIFICATION", assignee: me);
        await InsertClaimAsync(api, $"{tag}-IR", "IN_REVIEW", assignee: me);
        var returned = await InsertClaimAsync(api, $"{tag}-RT", "IN_REVIEW", assignee: me);
        await MarkReturnedAsync(api, returned, me);
        await InsertClaimAsync(api, $"{tag}-OT", "IN_REVIEW", assignee: other);
        await InsertClaimAsync(api, $"{tag}-ON", "NEEDS_VERIFICATION", assignee: other);
        await InsertClaimAsync(api, $"{tag}-DR", "DRAFT");
        await InsertClaimAsync(api, $"{tag}-PA", "PENDING_APPROVAL", assignee: me);

        var filter = $"&claimNumber={tag}";
        var available = await ViewAsync(reviewer, "AVAILABLE", filter);
        var assigned = await ViewAsync(reviewer, "ASSIGNED_TO_ME", filter);
        var verifying = await ViewAsync(reviewer, "NEEDS_VERIFICATION", filter);
        var returnedTab = await ViewAsync(reviewer, "RETURNED", filter);

        Assert.Equal([$"{tag}-A1", $"{tag}-A2"], Numbers(available).Order());
        Assert.Equal([$"{tag}-IR"], Numbers(assigned));
        Assert.Equal([$"{tag}-NV"], Numbers(verifying));
        Assert.Equal([$"{tag}-RT"], Numbers(returnedTab));

        var counts = assigned.GetProperty("counts");
        Assert.True(counts.GetProperty("AVAILABLE").GetInt32() >= 2);
        Assert.Equal(1, counts.GetProperty("ASSIGNED_TO_ME").GetInt32());
        Assert.Equal(1, counts.GetProperty("NEEDS_VERIFICATION").GetInt32());
        Assert.Equal(1, counts.GetProperty("RETURNED").GetInt32());

        var mine = assigned.GetProperty("items")[0];
        Assert.Equal("Nguyễn Văn An", mine.GetProperty("memberName").GetString());
        Assert.Equal("TR", mine.GetProperty("assigneeInitials").GetString());
        Assert.False(mine.GetProperty("isReturned").GetBoolean());
        Assert.True(returnedTab.GetProperty("items")[0].GetProperty("isReturned").GetBoolean());
    }

    [Fact]
    public async Task The_returned_tab_is_empty_for_a_reviewer_with_no_returned_claim()
    {
        var (reviewer, me) = await NewReviewerAsync(api);
        await InsertClaimAsync(api, $"{Tag()}-1", "IN_REVIEW", assignee: me);

        var page = await ViewAsync(reviewer, "RETURNED");

        Assert.Empty(page.GetProperty("items").EnumerateArray());
        Assert.Equal(0, page.GetProperty("counts").GetProperty("RETURNED").GetInt32());
    }

    [Fact]
    public async Task The_review_queue_sorts_high_value_first_then_the_oldest()
    {
        var (reviewer, _) = await NewReviewerAsync(api);
        var tag = Tag();
        await InsertClaimAsync(api, $"{tag}-1", "READY_FOR_REVIEW", total: 1_000_000, age: TimeSpan.FromDays(9));
        await InsertClaimAsync(api, $"{tag}-2", "NEEDS_VERIFICATION", total: 2_000_000, age: TimeSpan.FromDays(1));
        await InsertClaimAsync(api, $"{tag}-3", "READY_FOR_REVIEW", total: 5_000_000, age: TimeSpan.FromHours(1));
        await InsertClaimAsync(api, $"{tag}-4", "NEEDS_VERIFICATION", total: 9_000_000, age: TimeSpan.FromDays(2));
        await InsertClaimAsync(api, $"{tag}-5", "READY_FOR_REVIEW", total: 4_999_999, age: TimeSpan.FromDays(5));

        var page = await ViewAsync(reviewer, "AVAILABLE", $"&claimNumber={tag}");

        Assert.Equal([$"{tag}-4", $"{tag}-3", $"{tag}-1", $"{tag}-5", $"{tag}-2"], Numbers(page));
        Assert.Equal(
            ["HIGH", "HIGH", "NORMAL", "NORMAL", "NORMAL"],
            page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("priority").GetString()));
    }

    [Fact]
    public async Task A_page_of_one_is_how_open_next_claim_finds_the_most_urgent()
    {
        var (reviewer, _) = await NewReviewerAsync(api);
        var tag = Tag();
        await InsertClaimAsync(api, $"{tag}-1", "READY_FOR_REVIEW", total: 1_000_000);
        await InsertClaimAsync(api, $"{tag}-2", "READY_FOR_REVIEW", total: 7_000_000);

        var page = await ViewAsync(reviewer, "AVAILABLE", $"&claimNumber={tag}&pageSize=1");

        Assert.Equal([$"{tag}-2"], Numbers(page));
        Assert.Equal(2, page.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task A_reviewers_own_tabs_can_be_filtered_by_priority()
    {
        var (reviewer, me) = await NewReviewerAsync(api);
        var tag = Tag();
        await InsertClaimAsync(api, $"{tag}-1", "IN_REVIEW", total: 6_000_000, assignee: me);
        await InsertClaimAsync(api, $"{tag}-2", "IN_REVIEW", total: 1_000_000, assignee: me);

        var page = await ViewAsync(reviewer, "ASSIGNED_TO_ME", "&priority=HIGH");

        Assert.Equal([$"{tag}-1"], Numbers(page));
    }

    [Fact]
    public async Task A_queue_page_writes_one_audit_event()
    {
        var (reviewer, me) = await NewReviewerAsync(api);
        await InsertClaimAsync(api, $"{Tag()}-1", "IN_REVIEW", assignee: me);
        await InsertClaimAsync(api, $"{Tag()}-2", "IN_REVIEW", assignee: me);

        await ViewAsync(reviewer, "ASSIGNED_TO_ME");

        Assert.Equal(1, await api.Scalar<long>(
            $"select count(*) from audit_event where actor_id = '{me}' and event_type = 'DATA_READ' and after_value->>'operationId' = 'listClaims'"));
    }

    [Fact]
    public async Task A_view_of_another_role_is_refused_and_the_approvers_views_are_not_built_yet()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var approver = await api.SignedInAs(Staff.Approver);

        Assert.Equal(HttpStatusCode.Forbidden, (await reviewer.GetAsync("claims?view=MY_DRAFTS", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.NotImplemented, (await approver.GetAsync("claims?view=PENDING_APPROVAL", Cancel)).StatusCode);
    }
}
