using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ClaimLens.Api.Tests.Features;

/// <summary>US19. The contract handler in the client checks every response against claims-api.yaml as well.</summary>
public class RuleSetTests(ApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private const string GoldPlan = "20000000-0000-4000-8000-000000000001";
    private const string GoldV3 = "21000000-0000-4000-8000-000000000003";

    private static readonly string[] Categories =
        ["CONSULTATION", "PROCEDURE", "LAB_TEST", "DIAGNOSTIC_IMAGING", "PRESCRIPTION_DRUG", "COSMETIC"];

    [Theory]
    [InlineData(Staff.Admin)]
    [InlineData(Staff.Reviewer)]
    [InlineData(Staff.Approver)]
    public async Task Reviewers_and_approvers_read_what_the_administrator_edits(string email)
    {
        var client = await api.SignedInAs(email);

        var plans = await Json(await client.GetAsync("plans", Cancel));
        var versions = await Json(await client.GetAsync($"plans/{GoldPlan}/versions", Cancel));
        var version = await Json(await client.GetAsync($"plans/{GoldPlan}/versions/{GoldV3}", Cancel));

        Assert.Contains(plans.GetProperty("items").EnumerateArray(), plan => plan.GetProperty("code").GetString() == "GOLD-OP");
        Assert.Contains(versions.GetProperty("items").EnumerateArray(), row => row.GetProperty("versionNo").GetInt32() == 3);
        Assert.Equal("PUBLISHED", version.GetProperty("status").GetString());
        Assert.Equal(11, version.GetProperty("rules").GetArrayLength());
    }

    [Fact]
    public async Task A_rule_reads_the_same_as_in_the_database()
    {
        var client = await api.SignedInAs(Staff.Admin);

        var rules = Rules(await Json(await client.GetAsync($"plans/{GoldPlan}/versions/{GoldV3}", Cancel)));

        var limit = rules.Single(rule => rule.GetProperty("ruleCode").GetString() == "LIM-VISIT");
        Assert.Equal("LIMIT", limit.GetProperty("ruleType").GetString());
        Assert.Equal("PER_VISIT", limit.GetProperty("limitScope").GetString());
        Assert.Equal(2_000_000, limit.GetProperty("limitAmount").GetInt64());
        Assert.Equal(JsonValueKind.Null, limit.GetProperty("rateBp").ValueKind);
        Assert.Equal("WAITING_PERIOD", rules.Single(rule => rule.GetProperty("ruleCode").GetString() == "WAIT-30").GetProperty("ruleType").GetString());
    }

    [Fact]
    public async Task The_category_list_is_the_active_taxonomy()
    {
        var client = await api.SignedInAs(Staff.Admin);

        var codes = (await Json(await client.GetAsync("benefit-categories", Cancel))).GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("code").GetString())
            .ToList();

        Assert.All(Categories, code => Assert.Contains(code, codes));
    }

    [Fact]
    public async Task A_new_draft_copies_the_version_it_is_based_on_with_new_ids()
    {
        var plan = await NewPlan();
        var first = await PublishFullVersion(plan, "2026-01-01");
        var client = await api.SignedInAs(Staff.Admin);

        var response = await client.PostAsJsonAsync($"plans/{plan}/versions", new { basedOnVersionId = first, changeNote = "Next year" }, Cancel);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var draft = await Json(response);
        Assert.Equal(2, draft.GetProperty("versionNo").GetInt32());
        Assert.Equal("DRAFT", draft.GetProperty("status").GetString());
        Assert.Equal(first, draft.GetProperty("basedOnVersionId").GetString());
        Assert.Equal("Next year", draft.GetProperty("changeNote").GetString());
        Assert.Equal(JsonValueKind.Null, draft.GetProperty("effectiveFrom").ValueKind);

        var original = Rules(await Json(await client.GetAsync($"plans/{plan}/versions/{first}", Cancel)));
        var copied = Rules(draft);
        Assert.Equal(Codes(original).Order(), Codes(copied).Order());
        Assert.Empty(copied.Select(Id).Intersect(original.Select(Id)));
    }

    [Fact]
    public async Task An_empty_draft_starts_with_only_the_system_eligibility_rule()
    {
        var plan = await NewPlan();
        var client = await api.SignedInAs(Staff.Admin);

        var draft = await Json(await client.PostAsJsonAsync($"plans/{plan}/versions", new { }, Cancel));

        var rule = Assert.Single(Rules(draft));
        Assert.Equal("ELIGIBILITY", rule.GetProperty("ruleType").GetString());
    }

    [Fact]
    public async Task A_plan_has_at_most_one_draft()
    {
        var plan = await NewPlan();
        var client = await api.SignedInAs(Staff.Admin);
        await NewDraft(client, plan);

        var second = await client.PostAsJsonAsync($"plans/{plan}/versions", new { }, Cancel);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("DRAFT_ALREADY_EXISTS", await Code(second));
    }

    [Fact]
    public async Task A_draft_cannot_be_based_on_a_version_of_another_plan()
    {
        var plan = await NewPlan();
        var client = await api.SignedInAs(Staff.Admin);

        var response = await client.PostAsJsonAsync($"plans/{plan}/versions", new { basedOnVersionId = GoldV3 }, Cancel);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", await Code(response));
    }

    [Fact]
    public async Task Saving_replaces_the_rules_and_keeps_eligibility()
    {
        var plan = await NewPlan();
        var client = await api.SignedInAs(Staff.Admin);
        var draft = await NewDraft(client, plan);
        await Save(client, plan, draft, FullRules());

        var response = await client.PutAsJsonAsync(
            $"plans/{plan}/versions/{draft}/rules",
            new { changeNote = "Second save", rules = new[] { Coverage("COV-01", "CONSULTATION"), Exclusion("EX-01", "COSMETIC") } },
            Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await Json(response);
        Assert.Equal("Second save", saved.GetProperty("changeNote").GetString());
        Assert.Equal(["ELIG-01", "EX-01", "COV-01"], Codes(Rules(saved)));
    }

    [Fact]
    public async Task A_code_can_stay_the_same_across_a_save()
    {
        var plan = await NewPlan();
        var client = await api.SignedInAs(Staff.Admin);
        var draft = await NewDraft(client, plan);
        await Save(client, plan, draft, [Coverage("COV-01", "CONSULTATION")]);

        var response = await Save(client, plan, draft, [Coverage("COV-01", "LAB_TEST")]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Values_a_rule_type_does_not_use_are_not_kept()
    {
        var plan = await NewPlan();
        var client = await api.SignedInAs(Staff.Admin);
        var draft = await NewDraft(client, plan);

        var saved = await Json(await Save(client, plan, draft,
            [new { ruleCode = "COINS-80", ruleType = "COINSURANCE", clauseText = "c", applyOrder = 50, rateBp = 8000, limitAmount = 99L, categoryCode = "COSMETIC" }]));

        var rule = Rules(saved).Single(item => item.GetProperty("ruleCode").GetString() == "COINS-80");
        Assert.Equal(8000, rule.GetProperty("rateBp").GetInt32());
        Assert.Equal(JsonValueKind.Null, rule.GetProperty("limitAmount").ValueKind);
        Assert.Equal(JsonValueKind.Null, rule.GetProperty("categoryCode").ValueKind);
    }

    [Theory]
    [InlineData("""{"ruleCode":"","ruleType":"COVERAGE","categoryCode":"LAB_TEST","clauseText":"c","applyOrder":1}""", "VALIDATION_FAILED", "rules[0].ruleCode")]
    [InlineData("""{"ruleCode":"A","ruleType":"COVERAGE","categoryCode":"LAB_TEST","clauseText":" ","applyOrder":1}""", "VALIDATION_FAILED", "rules[0].clauseText")]
    [InlineData("""{"ruleCode":"A","ruleType":"COVERAGE","categoryCode":"LAB_TEST","clauseText":"c"}""", "VALIDATION_FAILED", "rules[0].applyOrder")]
    [InlineData("""{"ruleCode":"A","ruleType":"ELIGIBILITY","clauseText":"c","applyOrder":1}""", "VALIDATION_FAILED", "rules[0].ruleType")]
    [InlineData("""{"ruleCode":"ELIG-01","ruleType":"COVERAGE","categoryCode":"LAB_TEST","clauseText":"c","applyOrder":1}""", "VALIDATION_FAILED", "rules[0].ruleCode")]
    [InlineData("""{"ruleCode":"A","ruleType":"COVERAGE","clauseText":"c","applyOrder":1}""", "RULE_INVALID", "rules[0].categoryCode")]
    [InlineData("""{"ruleCode":"A","ruleType":"COINSURANCE","clauseText":"c","applyOrder":1}""", "RULE_INVALID", "rules[0].rateBp")]
    [InlineData("""{"ruleCode":"A","ruleType":"COINSURANCE","clauseText":"c","applyOrder":1,"rateBp":10001}""", "RULE_INVALID", "rules[0].rateBp")]
    [InlineData("""{"ruleCode":"A","ruleType":"LIMIT","clauseText":"c","applyOrder":1,"limitScope":"ANNUAL"}""", "RULE_INVALID", "rules[0].limitAmount")]
    [InlineData("""{"ruleCode":"A","ruleType":"LIMIT","clauseText":"c","applyOrder":1,"limitAmount":1}""", "RULE_INVALID", "rules[0].limitScope")]
    [InlineData("""{"ruleCode":"A","ruleType":"WAITING_PERIOD","clauseText":"c","applyOrder":1}""", "RULE_INVALID", "rules[0].waitingDays")]
    [InlineData("""{"ruleCode":"A","ruleType":"COVERAGE","categoryCode":"DENTAL","clauseText":"c","applyOrder":1}""", "UNKNOWN_CATEGORY", "rules[0].categoryCode")]
    public async Task A_bad_rule_is_refused_and_named(string rule, string code, string field)
    {
        var plan = await NewPlan();
        var client = await api.SignedInAs(Staff.Admin);
        var draft = await NewDraft(client, plan);

        var response = await client.PutAsync(
            $"plans/{plan}/versions/{draft}/rules",
            new StringContent($$"""{"rules":[{{rule}}]}""", System.Text.Encoding.UTF8, "application/json"),
            Cancel);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await Json(response);
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Contains(body.GetProperty("errors").EnumerateArray(), error => error.GetProperty("field").GetString() == field);
    }

    [Fact]
    public async Task Every_problem_in_a_list_is_reported_at_once()
    {
        var plan = await NewPlan();
        var client = await api.SignedInAs(Staff.Admin);
        var draft = await NewDraft(client, plan);

        var response = await Save(client, plan, draft,
            [new { ruleCode = "", ruleType = "COVERAGE", categoryCode = "LAB_TEST", clauseText = "", applyOrder = 1 }]);

        Assert.Equal(2, (await Json(response)).GetProperty("errors").GetArrayLength());
    }

    [Theory]
    [InlineData("the same code twice", "rules[1].ruleCode")]
    [InlineData("two rules for a category", "rules[1].categoryCode")]
    [InlineData("two rates", "rules[1].ruleType")]
    [InlineData("two limits of a scope", "rules[1].limitScope")]
    public async Task A_repeat_the_engine_would_quietly_ignore_is_refused(string kind, string field)
    {
        object[] rules = kind switch
        {
            "the same code twice" => [Coverage("A", "LAB_TEST"), Coverage("A", "COSMETIC")],
            "two rules for a category" => [Coverage("A", "LAB_TEST"), Exclusion("B", "LAB_TEST")],
            "two rates" => [Rate("A"), Rate("B")],
            _ => [Limit("A", "ANNUAL"), Limit("B", "ANNUAL")],
        };
        var plan = await NewPlan();
        var client = await api.SignedInAs(Staff.Admin);
        var draft = await NewDraft(client, plan);

        var response = await Save(client, plan, draft, rules);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await Json(response);
        Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
        Assert.Contains(body.GetProperty("errors").EnumerateArray(), error => error.GetProperty("field").GetString() == field);
    }

    [Fact]
    public async Task Publishing_requires_a_date()
    {
        var plan = await NewPlan();
        var client = await api.SignedInAs(Staff.Admin);
        var draft = await NewDraft(client, plan);
        await Save(client, plan, draft, FullRules());

        var response = await client.PostAsJsonAsync($"plans/{plan}/versions/{draft}/publish", new { }, Cancel);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", await Code(response));
    }

    [Theory]
    [InlineData("2026-01-01")]
    [InlineData("2025-06-01")]
    public async Task Publishing_requires_a_date_later_than_the_current_version(string date)
    {
        var plan = await NewPlan();
        await PublishFullVersion(plan, "2026-01-01");
        var client = await api.SignedInAs(Staff.Admin);
        var draft = await NewDraft(client, plan);
        await Save(client, plan, draft, FullRules());

        var response = await client.PostAsJsonAsync($"plans/{plan}/versions/{draft}/publish", new { effectiveFrom = date }, Cancel);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("EFFECTIVE_DATE_NOT_LATER", await Code(response));
    }

    [Fact]
    public async Task Publishing_names_each_category_with_no_rule()
    {
        var plan = await NewPlan();
        var client = await api.SignedInAs(Staff.Admin);
        var draft = await NewDraft(client, plan);
        await Save(client, plan, draft, [Coverage("COV-01", "CONSULTATION")]);

        var response = await client.PostAsJsonAsync($"plans/{plan}/versions/{draft}/publish", new { effectiveFrom = "2027-01-01" }, Cancel);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await Json(response);
        Assert.Equal("CATEGORY_NOT_CONFIGURED", body.GetProperty("code").GetString());
        var messages = body.GetProperty("errors").EnumerateArray().Select(error => error.GetProperty("message").GetString()!).ToList();
        Assert.Contains(messages, message => message.Contains("Laboratory tests", StringComparison.Ordinal));
        Assert.DoesNotContain(messages, message => message.Contains("Consultation", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_published_version_records_who_and_when_and_can_no_longer_change()
    {
        var plan = await NewPlan();
        var version = await PublishFullVersion(plan, "2026-03-01");
        var client = await api.SignedInAs(Staff.Admin);

        var published = await Json(await client.GetAsync($"plans/{plan}/versions/{version}", Cancel));
        Assert.Equal("PUBLISHED", published.GetProperty("status").GetString());
        Assert.Equal("2026-03-01", published.GetProperty("effectiveFrom").GetString());
        Assert.Equal("DN", published.GetProperty("publishedBy").GetProperty("initials").GetString());

        Assert.Equal("RULE_SET_NOT_DRAFT", await Code(await Save(client, plan, version, FullRules())));
        Assert.Equal("RULE_SET_NOT_DRAFT", await Code(await client.PostAsJsonAsync($"plans/{plan}/versions/{version}/publish", new { effectiveFrom = "2027-01-01" }, Cancel)));
        Assert.Equal("RULE_SET_NOT_DRAFT", await Code(await client.PostAsync($"plans/{plan}/versions/{version}/discard", null, Cancel)));
    }

    [Fact]
    public async Task Superseding_keeps_the_old_version_as_it_was()
    {
        var plan = await NewPlan();
        var first = await PublishFullVersion(plan, "2026-01-01");
        var client = await api.SignedInAs(Staff.Admin);
        var before = await (await client.GetAsync($"plans/{plan}/versions/{first}", Cancel)).Content.ReadAsStringAsync(Cancel);

        var second = Id(await Json(await client.PostAsJsonAsync($"plans/{plan}/versions", new { basedOnVersionId = first }, Cancel)));
        await Save(client, plan, second, FullRules(rateBp: 7000));
        await client.PostAsJsonAsync($"plans/{plan}/versions/{second}/publish", new { effectiveFrom = "2026-06-01" }, Cancel);

        var after = await (await client.GetAsync($"plans/{plan}/versions/{first}", Cancel)).Content.ReadAsStringAsync(Cancel);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Discarding_frees_the_plan_for_a_new_draft_with_the_next_number()
    {
        var plan = await NewPlan();
        var client = await api.SignedInAs(Staff.Admin);
        var draft = await NewDraft(client, plan);

        var discarded = await Json(await client.PostAsync($"plans/{plan}/versions/{draft}/discard", null, Cancel));
        var next = await Json(await client.PostAsJsonAsync($"plans/{plan}/versions", new { }, Cancel));

        Assert.Equal("DISCARDED", discarded.GetProperty("status").GetString());
        Assert.Equal(2, next.GetProperty("versionNo").GetInt32());
    }

    [Fact]
    public async Task The_plan_list_shows_the_version_in_force_today_and_the_open_draft()
    {
        var plan = await NewPlan();
        var current = await PublishFullVersion(plan, "2026-01-01");
        var client = await api.SignedInAs(Staff.Admin);
        var later = Id(await Json(await client.PostAsJsonAsync($"plans/{plan}/versions", new { basedOnVersionId = current }, Cancel)));
        await client.PostAsJsonAsync($"plans/{plan}/versions/{later}/publish", new { effectiveFrom = "2099-01-01" }, Cancel);
        var draft = await NewDraft(client, plan);

        var row = (await Json(await client.GetAsync("plans", Cancel))).GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == plan);

        Assert.Equal(current, row.GetProperty("currentVersion").GetProperty("id").GetString());
        Assert.Equal(draft, row.GetProperty("draftVersionId").GetString());
    }

    [Fact]
    public async Task Versions_are_listed_newest_first()
    {
        var plan = await NewPlan();
        var first = await PublishFullVersion(plan, "2026-01-01");
        var client = await api.SignedInAs(Staff.Admin);
        await client.PostAsJsonAsync($"plans/{plan}/versions", new { basedOnVersionId = first }, Cancel);

        var numbers = (await Json(await client.GetAsync($"plans/{plan}/versions", Cancel))).GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("versionNo").GetInt32());

        Assert.Equal([2, 1], numbers);
    }

    [Fact]
    public async Task A_version_is_found_only_through_its_own_plan()
    {
        var other = await NewPlan();
        var client = await api.SignedInAs(Staff.Admin);

        var response = await client.GetAsync($"plans/{other}/versions/{GoldV3}", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Each_change_writes_an_audit_event_with_before_and_after()
    {
        var plan = await NewPlan();
        var client = await api.SignedInAs(Staff.Admin);
        var draft = await NewDraft(client, plan);
        await Save(client, plan, draft, FullRules());
        await client.PostAsJsonAsync($"plans/{plan}/versions/{draft}/publish", new { effectiveFrom = "2026-01-01" }, Cancel);
        var other = await NewDraft(client, plan);
        await client.PostAsync($"plans/{plan}/versions/{other}/discard", null, Cancel);

        foreach (var (type, id) in new[]
        {
            ("RULE_SET_DRAFT_CREATED", draft),
            ("RULE_SET_UPDATED", draft),
            ("RULE_SET_PUBLISHED", draft),
            ("RULE_SET_DISCARDED", other),
        })
        {
            Assert.Equal(1, await api.Scalar<long>($"select count(*) from audit_event where event_type = '{type}' and entity_id = '{id}' and actor_id is not null"));
        }

        Assert.True(await api.Scalar<bool>($"select before_value is not null and after_value is not null from audit_event where event_type = 'RULE_SET_UPDATED' and entity_id = '{draft}'"));
    }

    [Fact]
    public async Task A_refused_save_changes_nothing()
    {
        var plan = await NewPlan();
        var client = await api.SignedInAs(Staff.Admin);
        var draft = await NewDraft(client, plan);
        await Save(client, plan, draft, [Coverage("COV-01", "CONSULTATION")]);

        await Save(client, plan, draft, [Coverage("COV-09", "DENTAL")]);

        Assert.Equal(["ELIG-01", "COV-01"], Codes(Rules(await Json(await client.GetAsync($"plans/{plan}/versions/{draft}", Cancel)))));
    }

    [Fact]
    public async Task A_published_version_keeps_its_rules_and_the_eligibility_rule()
    {
        var plan = await NewPlan();
        var version = await PublishFullVersion(plan, "2026-01-01");

        var count = await api.Scalar<long>($"select count(*) from benefit_rule where rule_set_version_id = '{version}'");

        // Five coverages, one exclusion, rate, limit and the system eligibility rule.
        Assert.Equal(9, count);
    }

    private async Task<string> NewPlan()
    {
        var id = Guid.CreateVersion7();
        await api.Sql($"insert into benefit_plan (id, code, name) values ('{id}', 'T-{id.ToString("N")[20..]}', 'Test plan')");
        return id.ToString();
    }

    private static async Task<string> NewDraft(HttpClient client, string plan)
    {
        var response = await client.PostAsJsonAsync($"plans/{plan}/versions", new { }, Cancel);
        response.EnsureSuccessStatusCode();
        return Id(await Json(response));
    }

    private async Task<string> PublishFullVersion(string plan, string effectiveFrom)
    {
        var client = await api.SignedInAs(Staff.Admin);
        var draft = await NewDraft(client, plan);
        (await Save(client, plan, draft, FullRules())).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"plans/{plan}/versions/{draft}/publish", new { effectiveFrom }, Cancel)).EnsureSuccessStatusCode();
        return draft;
    }

    private static Task<HttpResponseMessage> Save(HttpClient client, string plan, string version, object[] rules) =>
        client.PutAsJsonAsync($"plans/{plan}/versions/{version}/rules", new { rules }, Cancel);

    private static object[] FullRules(int rateBp = 8000) =>
    [
        .. Categories.Where(code => code != "COSMETIC").Select((code, index) => Coverage($"COV-{index}", code)),
        Exclusion("EX-04", "COSMETIC"),
        Rate("COINS", rateBp),
        Limit("LIM-A", "ANNUAL"),
    ];

    private static object Coverage(string code, string category) =>
        new { ruleCode = code, ruleType = "COVERAGE", categoryCode = category, clauseText = "Covered.", applyOrder = 40 };

    private static object Exclusion(string code, string category) =>
        new { ruleCode = code, ruleType = "EXCLUSION", categoryCode = category, clauseText = "Excluded.", applyOrder = 30 };

    private static object Rate(string code, int rateBp = 8000) =>
        new { ruleCode = code, ruleType = "COINSURANCE", clauseText = "Rate.", applyOrder = 50, rateBp };

    private static object Limit(string code, string scope) =>
        new { ruleCode = code, ruleType = "LIMIT", clauseText = "Limit.", applyOrder = 60, limitAmount = 5_000_000L, limitScope = scope };

    private static List<JsonElement> Rules(JsonElement version) => version.GetProperty("rules").EnumerateArray().ToList();

    private static List<string> Codes(IEnumerable<JsonElement> rules) => rules.Select(rule => rule.GetProperty("ruleCode").GetString()!).ToList();

    private static string Id(JsonElement item) => item.GetProperty("id").GetString()!;

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel)).RootElement;

    private static async Task<string?> Code(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemBody>(Cancel))!.Code;
}
