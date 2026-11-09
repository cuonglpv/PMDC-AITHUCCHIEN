using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaimLens.Api.AiJobs;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Tests.AiJobs;
using static ClaimLens.Api.Tests.AiJobs.AiData;
using static ClaimLens.Api.Tests.Features.IntakeData;

namespace ClaimLens.Api.Tests.Review;

/// <summary>Claims a reviewer can work on, built through the real intake and extraction flow or, for queues, in SQL.</summary>
public static class ReviewData
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>A region a reviewer drew, as a request sends it.</summary>
    public static readonly object Region = new { x = 0.5, y = 0.25, w = 0.25, h = 0.125 };

    /// <summary>A reviewer no other test has used, so the counts of their tabs are exact.</summary>
    public static async Task<(HttpClient Client, string Id)> NewReviewerAsync(ApiFactory api)
    {
        var id = Guid.CreateVersion7().ToString();
        var email = $"r{id[^12..]}@claimlens.test";
        await api.Sql(
            $"""
            insert into staff_account (id, email, password_hash, full_name, initials, role)
            select '{id}', '{email}', password_hash, 'Test Reviewer', 'TR', 'CLAIMS_REVIEWER'
            from staff_account where email = '{Staff.Reviewer}'
            """);
        return (await api.SignedInAs(email), id);
    }

    /// <summary>A submitted claim with one file of this type, whose extraction answered with these fields. Nobody has taken it.</summary>
    public static async Task<(string Claim, ReadyFile File)> ExtractedAsync(ApiFactory api, PageType type, Func<Guid, FieldSuggestion[]> fields)
    {
        var intake = await api.SignedInAs(Staff.Intake);
        var (claim, files) = await SubmittedAsync(api, intake, type);
        api.Ai.AnswerFields(files[0].DocumentId, fields(files[0].Page));
        await api.RunAiJobsAsync();
        return (claim, files[0]);
    }

    /// <summary>A clean invoice whose provider name only reached CHECK: the one field left to verify.</summary>
    public static FieldSuggestion[] InvoiceToCheck(Guid page) =>
        CleanInvoice(page)
            .Select(field => field.FieldKey == FieldKey.ProviderName ? field with { Confidence = 0.80m } : field)
            .ToArray();

    /// <summary>A claim of one file taken by this reviewer. By default an invoice with one field left to verify.</summary>
    public static async Task<(string Claim, ReadyFile File)> TakenAsync(
        ApiFactory api,
        HttpClient reviewer,
        Func<Guid, FieldSuggestion[]>? fields = null,
        PageType type = PageType.Invoice)
    {
        var (claim, file) = await ExtractedAsync(api, type, fields ?? InvoiceToCheck);
        await StartAsync(reviewer, claim);
        return (claim, file);
    }

    public static async Task<JsonElement> StartAsync(HttpClient reviewer, string claim)
    {
        var response = await reviewer.PostAsync($"claims/{claim}/start-review", null, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await JsonOf(response);
    }

    public static async Task<List<JsonElement>> FieldsAsync(HttpClient client, string claim, string query = "") =>
        (await JsonOf(await client.GetAsync($"claims/{claim}/fields{query}", Cancel))).GetProperty("items").EnumerateArray().ToList();

    public static JsonElement FieldOf(IEnumerable<JsonElement> fields, string key, int item = 0) =>
        fields.Single(field => field.GetProperty("fieldKey").GetString() == key && field.GetProperty("itemNo").GetInt32() == item);

    public static async Task<JsonElement> FieldOf(HttpClient client, string claim, string key, int item = 0) =>
        FieldOf(await FieldsAsync(client, claim), key, item);

    public static string Status(JsonElement field) => field.GetProperty("status").GetString()!;

    public static int Version(JsonElement field) => field.GetProperty("rowVersion").GetInt32();

    public static Task<HttpResponseMessage> PatchAsync(HttpClient client, string claim, JsonElement field, object body) =>
        client.PatchAsJsonAsync($"claims/{claim}/fields/{Id(field)}", body, Cancel);

    public static Task<HttpResponseMessage> AcceptAsync(HttpClient client, string claim, JsonElement field, object? body = null) =>
        client.PostAsJsonAsync($"claims/{claim}/fields/{Id(field)}/accept", body ?? new { rowVersion = Version(field) }, Cancel);

    public static Task<HttpResponseMessage> RerunAsync(HttpClient client, string claim, Guid page) =>
        client.PostAsync($"claims/{claim}/pages/{page}/rerun-extraction", null, Cancel);

    public static Task<long> EventsAsync(ApiFactory api, string claim, string eventType) =>
        api.Scalar<long>($"select count(*) from audit_event where claim_id = '{claim}' and event_type = '{eventType}'");

    /// <summary>A claim inserted directly and moved through the lifecycle trigger, its number chosen so a test can filter on it.</summary>
    public static async Task<Guid> InsertClaimAsync(
        ApiFactory api,
        string number,
        string status,
        long total = 1_000_000,
        string? assignee = null,
        TimeSpan? age = null,
        string registeredBy = Staff.IntakeId)
    {
        var id = Guid.CreateVersion7();
        var moves = status switch
        {
            "DRAFT" => "",
            "NEEDS_VERIFICATION" => Move(id, "PROCESSING") + Move(id, "NEEDS_VERIFICATION", assignee),
            "READY_FOR_REVIEW" => Move(id, "PROCESSING") + Move(id, "READY_FOR_REVIEW"),
            "IN_REVIEW" => Move(id, "PROCESSING") + Move(id, "READY_FOR_REVIEW") + Move(id, "IN_REVIEW", assignee),
            "PENDING_APPROVAL" => Move(id, "PROCESSING") + Move(id, "READY_FOR_REVIEW") + Move(id, "IN_REVIEW", assignee) + Move(id, "PENDING_APPROVAL"),
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
        var aged = age is { } by
            ? $"update claim set status_changed_at = now() - interval '{(long)by.TotalMinutes} minutes' where id = '{id}';"
            : "";

        await api.Sql(
            $"""
            insert into claim (id, claim_number, member_id, policy_id, provider_name, service_date, claimed_total, channel, registered_by)
            values ('{id}', '{number}', '{AnMemberId}', '{AnPolicyId}', 'Hồng Ngọc', '2026-10-12', {total}, 'COUNTER', '{registeredBy}');
            {moves}{aged}
            """);
        return id;
    }

    /// <summary>The approver sent this claim's recommendation back, so it counts as Returned.</summary>
    public static Task MarkReturnedAsync(ApiFactory api, Guid claim, string reviewerId) =>
        api.Sql(
            $"""
            with revision as (
              insert into calculation_revision (
                claim_id, revision_no, rule_set_version_id, trigger_type, engine_version, input_hash, claimed_total, eligible_total,
                after_rate_amount, consumed_before, balance_version, remaining_limit, payable_amount, projected_remaining_after, formula)
              select '{claim}', 1, v.id, 'INITIAL', 'test', repeat('a', 64), 1000000, 1000000, 1000000, 0, 0, 1000000, 1000000, 0, '[]'::jsonb
              from rule_set_version v where v.plan_id = '{GoldPlan}' and v.status = 'PUBLISHED' limit 1
              returning id
            )
            insert into recommendation (
              claim_id, calculation_revision_id, status, proposed_outcome, recommended_outcome, recommended_payable, claimed_total,
              submitted_by, submitted_at, attested_at, decision_action, decided_by, decided_at, decision_revision_id, decision_comment)
            select '{claim}', revision.id, 'RETURNED', 'APPROVED', 'APPROVED', 1000000, 1000000,
                   '{reviewerId}', now(), now(), 'RETURN', a.id, now(), revision.id, 'Check the invoice'
            from revision, staff_account a where a.email = '{Staff.Approver}'
            """);

    private static string Move(Guid id, string status, string? assignee = null) =>
        assignee is null
            ? $"update claim set status = '{status}' where id = '{id}';\n"
            : $"update claim set status = '{status}', assignee_id = '{assignee}', assigned_at = now() where id = '{id}';\n";
}
