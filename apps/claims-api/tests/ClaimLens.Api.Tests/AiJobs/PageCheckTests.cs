using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaimLens.Api.Domain.Enums;
using static ClaimLens.Api.Tests.AiJobs.AiData;
using static ClaimLens.Api.Tests.Features.IntakeData;

namespace ClaimLens.Api.Tests.AiJobs;

/// <summary>US04 through the AI Job Runner, with the fake Document AI Service, driven by the test.</summary>
public sealed class PageCheckTests(ApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    // ---- Given a page is uploaded, when the page check finishes, the page shows a type and a confidence; below 0.80 it
    //      shows "Unclassified".

    [Theory]
    [InlineData("0.80", "INVOICE", "AI", "HIGH")]
    [InlineData("0.7995", "INVOICE", "AI", "HIGH")]
    [InlineData("0.799", "UNCLASSIFIED", null, "CHECK")]
    [InlineData("0.10", "UNCLASSIFIED", null, "CHECK")]
    public async Task The_AI_type_is_used_only_at_or_above_the_page_type_threshold(string confidence, string inUse, string? source, string level)
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, file) = await DraftWithFileAsync(api, client);
        api.Ai.AnswerPage(file.Page, PageType.Invoice, decimal.Parse(confidence, CultureInfo.InvariantCulture));

        await api.RunAiJobsAsync();

        var page = await PageAsync(client, claim, file.Page);
        Assert.Equal(inUse, page.GetProperty("pageType").GetString());
        Assert.Equal(source, page.GetProperty("pageTypeSource").GetString());
        Assert.Equal("DONE", page.GetProperty("aiStatus").GetString());
        var ai = page.GetProperty("ai");
        Assert.Equal("INVOICE", ai.GetProperty("pageType").GetString());
        Assert.Equal(level, ai.GetProperty("level").GetString());
        Assert.Equal(Math.Round(decimal.Parse(confidence, CultureInfo.InvariantCulture), 3, MidpointRounding.AwayFromZero), ai.GetProperty("confidence").GetDecimal());
    }

    // ---- Given a page quality score below the configured threshold, it is marked "Low quality"; submission is possible.

    [Theory]
    [InlineData("0.50", "READABLE")]
    [InlineData("0.499", "LOW_QUALITY")]
    public async Task A_page_below_the_quality_threshold_is_low_quality_and_the_claim_can_still_be_submitted(string score, string quality)
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, file) = await DraftWithFileAsync(api, client);
        api.Ai.AnswerPage(file.Page, PageType.Receipt, 0.99m, decimal.Parse(score, CultureInfo.InvariantCulture));

        await api.RunAiJobsAsync();

        Assert.Equal(quality, (await PageAsync(client, claim, file.Page)).GetProperty("quality").GetString());
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync($"claims/{claim}/submit", null, Cancel)).StatusCode);
    }

    [Fact]
    public async Task A_finished_check_stores_the_run_its_raw_answer_and_an_audit_event()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, file) = await DraftWithFileAsync(api, client, pages: 2);
        api.Ai.AnswerPage(file.Pages[0], PageType.Invoice, 0.98m);
        api.Ai.AnswerPage(file.Pages[1], PageType.Invoice, 0.97m);

        await api.RunAiJobsAsync();

        var run = await api.Scalar<Guid>($"select id from ai_run where claim_id = '{claim}' and task = 'PAGE_CHECK'");
        Assert.Equal("SUCCEEDED", await RunStatus(api, claim, "PAGE_CHECK"));
        Assert.True(await api.Scalar<bool>($"select started_at is not null and finished_at >= started_at from ai_run where id = '{run}'"));
        var key = await api.Scalar<string>($"select raw_output_key from ai_run where id = '{run}'");
        Assert.Equal($"ai-raw/{claim}/{run}.json", key);
        using var raw = JsonDocument.Parse(api.Store[key!]!);
        Assert.Equal(2, raw.RootElement.GetProperty("pages").GetArrayLength());
        Assert.Equal("application/json", api.Store.ContentTypeOf(key!));

        var request = api.Ai.PageChecks.Last(check => check.RunId == run);
        Assert.Equal(file.Pages, request.Pages.Select(page => page.PageId));
        Assert.All(request.Pages, page => Assert.StartsWith($"pages/{claim}/", page.ImageKey, StringComparison.Ordinal));
        Assert.Equal(("page-classifier", "v2.4"), (request.Model.Name, request.Model.Version));

        Assert.Equal(1, await api.Scalar<long>(
            $"""
            select count(*) from audit_event
            where event_type = 'AI_RUN_SUCCEEDED' and claim_id = '{claim}' and ai_run_id = '{run}' and actor_type = 'SYSTEM'
              and actor_id is null and jsonb_array_length(after_value->'result'->'pages') = 2
            """));
        Assert.False(await FallbackActiveAsync(client, claim));
        var runs = (await JsonOf(await client.GetAsync($"claims/{claim}/status", Cancel))).GetProperty("aiRuns");
        Assert.Contains(runs.EnumerateArray(), item => item.GetProperty("id").GetGuid() == run && item.GetProperty("status").GetString() == "SUCCEEDED");
    }

    // ---- The officer may choose a type; the page check keeps it.

    [Fact]
    public async Task A_type_the_officer_chose_is_kept_and_the_AI_suggestion_sits_beside_it()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, file) = await DraftWithFileAsync(api, client);
        var chosen = await client.PutAsJsonAsync($"claims/{claim}/pages/{file.Page}/type", new { pageType = "RECEIPT" }, Cancel);
        Assert.Equal(HttpStatusCode.OK, chosen.StatusCode);
        api.Ai.AnswerPage(file.Page, PageType.Invoice, 0.99m);

        await api.RunAiJobsAsync();

        var page = await PageAsync(client, claim, file.Page);
        Assert.Equal("RECEIPT", page.GetProperty("pageType").GetString());
        Assert.Equal("USER", page.GetProperty("pageTypeSource").GetString());
        Assert.Equal("INVOICE", page.GetProperty("ai").GetProperty("pageType").GetString());
        Assert.Equal("DONE", page.GetProperty("aiStatus").GetString());
    }

    // ---- One bad page does not fail a whole run.

    [Fact]
    public async Task A_page_the_service_could_not_read_is_failed_and_the_others_are_classified()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, file) = await DraftWithFileAsync(api, client, pages: 2);
        api.Ai.AnswerPage(file.Pages[0], PageType.Invoice, 0.99m);
        api.Ai.FailPage(file.Pages[1]);

        await api.RunAiJobsAsync();

        var read = await PageAsync(client, claim, file.Pages[0]);
        var unread = await PageAsync(client, claim, file.Pages[1]);
        Assert.Equal(("INVOICE", "DONE"), (read.GetProperty("pageType").GetString(), read.GetProperty("aiStatus").GetString()));
        Assert.Equal(("UNCLASSIFIED", "FAILED"), (unread.GetProperty("pageType").GetString(), unread.GetProperty("aiStatus").GetString()));
        Assert.Equal(JsonValueKind.Null, unread.GetProperty("ai").ValueKind);
        Assert.Equal("NOT_CHECKED", unread.GetProperty("quality").GetString());
        Assert.Equal("SUCCEEDED", await RunStatus(api, claim, "PAGE_CHECK"));
        Assert.False(await FallbackActiveAsync(client, claim));
    }
}
