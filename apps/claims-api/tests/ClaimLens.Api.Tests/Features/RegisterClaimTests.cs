using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using static ClaimLens.Api.Tests.Features.IntakeData;

namespace ClaimLens.Api.Tests.Features;

/// <summary>US02, one test per acceptance criterion and the rules behind them. The client checks every response against the contract.</summary>
public sealed partial class RegisterClaimTests : IDisposable
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>Inside GOLD-OP v3, which takes effect on 01/01/2026.</summary>
    private static readonly DateOnly Today = new(2026, 10, 15);

    private readonly ApiFactory api;

    public RegisterClaimTests(ApiFactory api)
    {
        this.api = api;
        api.Clock.SetToday(Today);
    }

    public void Dispose() => api.Clock.Reset();

    // ---- Given a search by member name, national ID or policy number, each result shows policy status, coverage period
    //      and remaining outpatient balance.

    [Theory]
    [InlineData("nguyen van an")]
    [InlineData("Nguyễn Văn An")]
    [InlineData("NGUYEN  VAN   AN")]
    [InlineData("van an")]
    public async Task A_name_finds_the_member_with_or_without_its_marks(string query)
    {
        var client = await api.SignedInAs(Staff.Intake);

        var items = await Search(client, query);

        var an = items.Single(item => item.GetProperty("policy").GetProperty("policyNumber").GetString() == "PL-2026-00731");
        Assert.Equal("Nguyễn Văn An", an.GetProperty("fullName").GetString());
        Assert.Equal("MB-0007731", an.GetProperty("memberCode").GetString());
        Assert.Equal("1988-04-12", an.GetProperty("dateOfBirth").GetString());
    }

    [Fact]
    public async Task Each_result_shows_the_policy_status_period_and_remaining_balance()
    {
        var client = await api.SignedInAs(Staff.Intake);

        var policy = (await Search(client, "PL-2026-00731")).Single().GetProperty("policy");

        Assert.Equal(AnPolicyId, policy.GetProperty("policyId").GetString());
        Assert.Equal("ACTIVE", policy.GetProperty("status").GetString());
        Assert.Equal("2026-01-01", policy.GetProperty("coverageStart").GetString());
        Assert.Equal("2026-12-31", policy.GetProperty("coverageEnd").GetString());
        Assert.Equal("GOLD-OP", policy.GetProperty("planCode").GetString());
        Assert.Equal(5_000_000, policy.GetProperty("annualLimit").GetInt64());
        Assert.Equal(4_200_000, policy.GetProperty("consumedAmount").GetInt64());
        Assert.Equal(800_000, policy.GetProperty("remainingBalance").GetInt64());
        Assert.Equal("2026-10-15", policy.GetProperty("asOfDate").GetString());
    }

    [Fact]
    public async Task A_national_id_or_a_policy_number_must_match_exactly()
    {
        var member = await NewMemberAsync(api);
        var client = await api.SignedInAs(Staff.Intake);

        Assert.Equal(member.MemberId.ToString(), (await Search(client, member.NationalId)).Single().GetProperty("memberId").GetString());
        Assert.Equal(member.MemberId.ToString(), (await Search(client, member.PolicyNumber)).Single().GetProperty("memberId").GetString());
        Assert.Empty(await Search(client, member.NationalId[..^1]));
        Assert.Empty(await Search(client, member.PolicyNumber[..^1]));
    }

    [Fact]
    public async Task A_member_with_two_policies_gives_two_rows()
    {
        var member = await NewMemberAsync(api);
        await api.Sql(
            $"""
            insert into policy (id, policy_number, member_id, plan_id, status, coverage_start, coverage_end, first_enrolled_on)
            values ('{Guid.CreateVersion7()}', 'PR-{member.PolicyNumber[3..]}', '{member.MemberId}', '{GoldPlan}', 'ACTIVE',
                    '2025-01-01', '2025-12-31', '2025-01-01')
            """);
        var client = await api.SignedInAs(Staff.Intake);

        var rows = await Search(client, member.FullName);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal(member.MemberId.ToString(), row.GetProperty("memberId").GetString()));
    }

    [Fact]
    public async Task Only_the_last_four_digits_of_a_national_id_leave_the_api()
    {
        var member = await NewMemberAsync(api);
        var client = await api.SignedInAs(Staff.Intake);

        var response = await client.PostAsJsonAsync("members/search", new { query = member.NationalId }, Cancel);
        var body = await response.Content.ReadAsStringAsync(Cancel);

        Assert.DoesNotContain(member.NationalId, body, StringComparison.Ordinal);
        Assert.Equal("********" + member.NationalId[^4..], (await JsonOf(response)).GetProperty("items")[0].GetProperty("nationalIdMasked").GetString());
    }

    [Fact]
    public async Task A_search_returns_at_most_20_rows()
    {
        var surname = "Vương Đình Nhiều " + Guid.NewGuid().ToString("N")[..6];
        for (var i = 0; i < 21; i++)
        {
            await NewMemberAsync(api, $"{surname} {i:D2}");
        }

        var client = await api.SignedInAs(Staff.Intake);

        Assert.Equal(20, (await Search(client, surname)).Count);
    }

    [Fact]
    public async Task No_member_found_is_an_empty_list()
    {
        var client = await api.SignedInAs(Staff.Intake);

        Assert.Empty(await Search(client, "Không Có Ai Tên Này"));
    }

    [Theory]
    [InlineData("a")]
    [InlineData(" ")]
    public async Task A_query_needs_two_characters(string query)
    {
        var client = await api.SignedInAs(Staff.Intake);

        var response = await client.PostAsJsonAsync("members/search", new { query }, Cancel);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", await Code(response));
    }

    [Fact]
    public async Task The_policy_summary_reads_the_limit_of_the_version_in_force_on_the_service_date()
    {
        var client = await api.SignedInAs(Staff.Intake);

        var today = await JsonOf(await client.GetAsync($"policies/{AnPolicyId}/summary", Cancel));
        var onServiceDate = await JsonOf(await client.GetAsync($"policies/{AnPolicyId}/summary?serviceDate=2026-03-02", Cancel));
        var beforeAnyVersion = await JsonOf(await client.GetAsync($"policies/{AnPolicyId}/summary?serviceDate=2025-12-31", Cancel));

        Assert.Equal(800_000, today.GetProperty("remainingBalance").GetInt64());
        Assert.Equal("2026-10-15", today.GetProperty("asOfDate").GetString());
        Assert.Equal(5_000_000, onServiceDate.GetProperty("annualLimit").GetInt64());
        Assert.Equal("2026-03-02", onServiceDate.GetProperty("asOfDate").GetString());
        Assert.Equal(JsonValueKind.Null, beforeAnyVersion.GetProperty("annualLimit").ValueKind);
        Assert.Equal(JsonValueKind.Null, beforeAnyVersion.GetProperty("remainingBalance").ValueKind);
        Assert.Equal(4_200_000, beforeAnyVersion.GetProperty("consumedAmount").GetInt64());
    }

    [Fact]
    public async Task The_balance_does_not_go_below_zero()
    {
        var member = await NewMemberAsync(api, consumed: 6_000_000);
        var client = await api.SignedInAs(Staff.Intake);

        var summary = await JsonOf(await client.GetAsync($"policies/{member.PolicyId}/summary", Cancel));

        Assert.Equal(0, summary.GetProperty("remainingBalance").GetInt64());
    }

    [Fact]
    public async Task An_unknown_policy_is_not_found()
    {
        var client = await api.SignedInAs(Staff.Intake);

        var response = await client.GetAsync($"policies/{Guid.NewGuid()}/summary", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Given member, policy, service date, provider, claimed total and channel, the claim gets a unique number,
    //      status Draft and an audit event.

    [Fact]
    public async Task A_new_claim_gets_a_unique_number_status_draft_and_an_audit_event()
    {
        var member = await NewMemberAsync(api);
        var client = await api.SignedInAs(Staff.Intake);

        var first = await CreateClaimAsync(client, member);
        var second = await CreateClaimAsync(client, member);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var claim = (await JsonOf(first)).GetProperty("claim");
        var number = claim.GetProperty("claimNumber").GetString()!;
        Assert.Matches(ClaimNumber(), number);
        Assert.StartsWith("CLM-2026-", number, StringComparison.Ordinal);
        Assert.NotEqual(number, (await JsonOf(second)).GetProperty("claim").GetProperty("claimNumber").GetString());
        Assert.Equal("DRAFT", claim.GetProperty("status").GetString());
        Assert.Equal(0, claim.GetProperty("rowVersion").GetInt32());
        Assert.Equal($"/api/v1/claims/{Id(claim)}", first.Headers.Location!.OriginalString);

        var id = Id(claim);
        Assert.Equal(1, await api.Scalar<long>(
            $"""
            select count(*) from audit_event
            where event_type = 'CLAIM_CREATED' and claim_id = '{id}' and actor_type = 'USER' and actor_id = '{Staff.IntakeId}'
            """));
        Assert.Equal("DRAFT", await api.Scalar<string>($"select status::text from claim where id = '{id}'"));
    }

    [Fact]
    public async Task The_saved_claim_is_the_intake_view_of_the_claim()
    {
        var member = await NewMemberAsync(api);
        var client = await api.SignedInAs(Staff.Intake);

        var claim = (await JsonOf(await CreateClaimAsync(client, member, claimedTotal: 5_000_000))).GetProperty("claim");

        Assert.Equal(member.FullName, claim.GetProperty("member").GetProperty("fullName").GetString());
        Assert.Equal("********" + member.NationalId[^4..], claim.GetProperty("member").GetProperty("nationalIdMasked").GetString());
        Assert.Equal(member.PolicyNumber, claim.GetProperty("policy").GetProperty("policyNumber").GetString());
        Assert.Equal("Phòng khám Đa khoa Hồng Ngọc", claim.GetProperty("providerName").GetString());
        Assert.Equal("2026-10-12", claim.GetProperty("serviceDate").GetString());
        Assert.Equal("COUNTER", claim.GetProperty("channel").GetString());
        Assert.Equal("HIGH", claim.GetProperty("priority").GetString());
        Assert.Equal("MA", claim.GetProperty("registeredBy").GetProperty("initials").GetString());
        Assert.Equal(3, claim.GetProperty("ruleSetVersion").GetProperty("versionNo").GetInt32());
        Assert.Equal(JsonValueKind.Null, claim.GetProperty("currentRevision").ValueKind);
        Assert.Equal(JsonValueKind.Null, claim.GetProperty("balance").ValueKind);
        Assert.Equal(JsonValueKind.Null, claim.GetProperty("counts").ValueKind);
        Assert.Equal(new[] { "EDIT_CLAIM", "UPLOAD_DOCUMENT" }, Strings(claim.GetProperty("allowedActions")));
        Assert.Equal(new[] { "NO_READY_DOCUMENT" }, claim.GetProperty("blockers").EnumerateArray().Select(blocker => blocker.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task An_officer_reads_the_claim_back_and_the_read_is_audited()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var id = await NewDraftAsync(api, client);

        var response = await client.GetAsync($"claims/{id}", Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(id, Id(await JsonOf(response)));
        Assert.Equal(1, await api.Scalar<long>(
            $"select count(*) from audit_event where event_type = 'CLAIM_VIEWED' and request_id = '{RequestId(response)}'"));
    }

    // ---- Given a service date outside the coverage period, a warning is shown but registration is allowed.

    [Fact]
    public async Task A_service_date_outside_the_policy_year_is_a_warning_and_the_claim_is_still_registered()
    {
        var member = await NewMemberAsync(api);
        var client = await api.SignedInAs(Staff.Intake);

        var response = await CreateClaimAsync(client, member, serviceDate: "2027-01-05");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(new[] { "COVERAGE_OUTSIDE_PERIOD" }, WarningCodes(await JsonOf(response)));
    }

    [Theory]
    [InlineData("2026-07-01", "LAPSED")]
    [InlineData("2026-06-01", "TERMINATED")]
    [InlineData("2026-05-31", "LAPSED")]
    public async Task A_policy_that_is_not_active_is_a_warning(string serviceDate, string status)
    {
        var member = await NewMemberAsync(api, status: status, terminatedOn: "2026-06-01");
        var client = await api.SignedInAs(Staff.Intake);

        var response = await CreateClaimAsync(client, member, serviceDate: serviceDate);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var warning = (await JsonOf(response)).GetProperty("warnings").EnumerateArray().Single();
        Assert.Equal("POLICY_NOT_ACTIVE", warning.GetProperty("code").GetString());
        Assert.Contains("01/06/2026", warning.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_active_policy_and_a_date_inside_its_year_give_no_warning()
    {
        var member = await NewMemberAsync(api);
        var client = await api.SignedInAs(Staff.Intake);

        Assert.Empty(WarningCodes(await JsonOf(await CreateClaimAsync(client, member))));
    }

    // ---- Given no member matches, no claim can be created.

    [Fact]
    public async Task A_claim_needs_a_member_that_exists()
    {
        var member = await NewMemberAsync(api);
        var client = await api.SignedInAs(Staff.Intake);

        var response = await CreateClaimAsync(client, member with { MemberId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", await Code(response));
        Assert.Contains(Errors(await JsonOf(response)), error => error is ("memberId", "NOT_FOUND"));
    }

    [Fact]
    public async Task The_policy_must_belong_to_the_member()
    {
        var member = await NewMemberAsync(api);
        var someoneElse = await NewMemberAsync(api);
        var client = await api.SignedInAs(Staff.Intake);

        var response = await CreateClaimAsync(client, member with { PolicyId = someoneElse.PolicyId });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("POLICY_MEMBER_MISMATCH", await Code(response));
    }

    [Fact]
    public async Task Each_missing_or_wrong_value_is_named()
    {
        var client = await api.SignedInAs(Staff.Intake);

        var response = await client.PostAsJsonAsync("claims", new { providerName = new string('x', 201), claimedTotal = 0 }, Cancel);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var errors = Errors(await JsonOf(response));
        Assert.Contains(("memberId", "REQUIRED"), errors);
        Assert.Contains(("policyId", "REQUIRED"), errors);
        Assert.Contains(("serviceDate", "REQUIRED"), errors);
        Assert.Contains(("channel", "REQUIRED"), errors);
        Assert.Contains(("providerName", "TOO_LONG"), errors);
        Assert.Contains(("claimedTotal", "OUT_OF_RANGE"), errors);
    }

    // ---- updateClaim: going back in the stepper.

    [Fact]
    public async Task A_draft_can_be_edited_with_the_row_version_it_was_read_at()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var id = await NewDraftAsync(api, client);

        var response = await client.PatchAsJsonAsync($"claims/{id}", new { rowVersion = 0, providerName = "Bệnh viện Bạch Mai", serviceDate = "2027-02-01" }, Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await JsonOf(response);
        Assert.Equal(1, saved.GetProperty("claim").GetProperty("rowVersion").GetInt32());
        Assert.Equal("Bệnh viện Bạch Mai", saved.GetProperty("claim").GetProperty("providerName").GetString());
        Assert.Equal(new[] { "COVERAGE_OUTSIDE_PERIOD" }, WarningCodes(saved));
        Assert.True(await api.Scalar<bool>(
            $"""
            select before_value->>'providerName' = 'Phòng khám Đa khoa Hồng Ngọc' and after_value->>'providerName' = 'Bệnh viện Bạch Mai'
            from audit_event where event_type = 'CLAIM_UPDATED' and claim_id = '{id}'
            """));
    }

    [Fact]
    public async Task An_edit_from_an_older_read_gets_VERSION_CONFLICT_with_the_current_values()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var id = await NewDraftAsync(api, client);
        (await client.PatchAsJsonAsync($"claims/{id}", new { rowVersion = 0, claimedTotal = 2_000_000 }, Cancel)).EnsureSuccessStatusCode();

        var response = await client.PatchAsJsonAsync($"claims/{id}", new { rowVersion = 0, claimedTotal = 900_000 }, Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("VERSION_CONFLICT", await Code(response));
        var details = (await JsonOf(response)).GetProperty("details");
        Assert.Equal(1, details.GetProperty("currentRowVersion").GetInt32());
        Assert.Equal(2_000_000, details.GetProperty("current").GetProperty("claimedTotal").GetInt64());
        Assert.Equal("COUNTER", details.GetProperty("current").GetProperty("channel").GetString());
        Assert.Equal(2_000_000, await api.Scalar<long>($"select claimed_total from claim where id = '{id}'"));
    }

    [Fact]
    public async Task An_edit_needs_something_to_change()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var id = await NewDraftAsync(api, client);

        var response = await client.PatchAsJsonAsync($"claims/{id}", new { rowVersion = 0 }, Cancel);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task An_edit_cannot_move_the_claim_to_another_members_policy()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var id = await NewDraftAsync(api, client);
        var someoneElse = await NewMemberAsync(api);

        var response = await client.PatchAsJsonAsync($"claims/{id}", new { rowVersion = 0, policyId = someoneElse.PolicyId }, Cancel);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("POLICY_MEMBER_MISMATCH", await Code(response));
    }

    private static async Task<List<JsonElement>> Search(HttpClient client, string query)
    {
        var response = await client.PostAsJsonAsync("members/search", new { query }, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await JsonOf(response)).GetProperty("items").EnumerateArray().ToList();
    }

    private static List<string?> WarningCodes(JsonElement saved) =>
        saved.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetProperty("code").GetString()).ToList();

    private static List<string?> Strings(JsonElement array) => array.EnumerateArray().Select(item => item.GetString()).ToList();

    private static List<(string?, string?)> Errors(JsonElement problem) =>
        problem.GetProperty("errors").EnumerateArray()
            .Select(error => (error.GetProperty("field").GetString(), error.GetProperty("code").GetString()))
            .ToList();

    [GeneratedRegex(@"^CLM-\d{4}-\d{6}$")]
    private static partial Regex ClaimNumber();
}
