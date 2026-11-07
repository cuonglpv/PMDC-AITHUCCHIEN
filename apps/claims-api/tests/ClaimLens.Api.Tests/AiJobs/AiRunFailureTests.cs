using System.Globalization;
using ClaimLens.Api.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using static ClaimLens.Api.Tests.AiJobs.AiData;
using static ClaimLens.Api.Tests.Features.IntakeData;

namespace ClaimLens.Api.Tests.AiJobs;

/// <summary>
/// The AI service fails: a failure that may pass is tried again while other runs go ahead, and every run that ends
/// FAILED sends the claim to manual work with the fallback banner (US04, US05).
/// </summary>
public sealed class AiRunFailureTests(ApiFactory api) : IDisposable
{
    public void Dispose() => api.Clock.Reset();

    [Fact]
    public async Task A_transient_failure_waits_while_other_runs_go_ahead_and_then_succeeds()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (failing, first) = await DraftWithFileAsync(api, client);
        var (other, second) = await DraftWithFileAsync(api, client);
        api.Ai.AnswerPage(first.Page, PageType.Invoice, 0.99m);
        api.Ai.AnswerPage(second.Page, PageType.Receipt, 0.99m);
        api.Ai.FailNext(first.Page, FakeDocumentAi.ModelError());

        await api.RunAiJobsAsync();

        Assert.Equal("QUEUED", await RunStatus(api, failing, "PAGE_CHECK"));
        Assert.Null(await api.Scalar<string>($"select error_code from ai_run where claim_id = '{failing}'"));
        Assert.Equal("QUEUED", (await PageAsync(client, failing, first.Page)).GetProperty("aiStatus").GetString());
        Assert.Equal("SUCCEEDED", await RunStatus(api, other, "PAGE_CHECK"));
        Assert.False(await FallbackActiveAsync(client, failing));

        api.Clock.Advance(TimeSpan.FromMinutes(1));
        await api.RunAiJobsAsync();

        Assert.Equal("SUCCEEDED", await RunStatus(api, failing, "PAGE_CHECK"));
        Assert.Equal("INVOICE", (await PageAsync(client, failing, first.Page)).GetProperty("pageType").GetString());
        Assert.Equal(2, api.Ai.PageChecks.Count(request => request.Pages.Any(page => page.PageId == first.Page)));
    }

    [Fact]
    public async Task A_page_check_that_keeps_failing_is_failed_after_the_last_attempt_and_its_pages_go_manual()
    {
        await using var host = Retrying(maxAttempts: 3);
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, file) = await DraftWithFileAsync(api, client, pages: 2);
        api.Ai.FailNext(file.Page, FakeDocumentAi.ModelError(), FakeDocumentAi.ModelError(), FakeDocumentAi.ModelError());

        await api.RunAiJobsAsync(host.Services);

        Assert.Equal("FAILED", await RunStatus(api, claim, "PAGE_CHECK"));
        Assert.Equal("MODEL_ERROR", await api.Scalar<string>($"select error_code from ai_run where claim_id = '{claim}'"));
        Assert.Equal(3, api.Ai.PageChecks.Count(request => request.Pages.Any(page => page.PageId == file.Page)));
        foreach (var page in file.Pages)
        {
            var item = await PageAsync(client, claim, page);
            Assert.Equal(("UNCLASSIFIED", "FAILED"), (item.GetProperty("pageType").GetString(), item.GetProperty("aiStatus").GetString()));
        }

        Assert.True(await FallbackActiveAsync(client, claim));
        Assert.Equal(1, await api.Scalar<long>(
            $"select count(*) from audit_event where event_type = 'AI_RUN_FAILED' and claim_id = '{claim}' and actor_type = 'SYSTEM' and ai_run_id is not null"));

        // Submission continues (US04).
        await SubmitAsync(client, claim);
    }

    [Fact]
    public async Task A_request_the_service_refuses_fails_at_once_and_extraction_falls_back_to_empty_fields()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, files) = await SubmittedAsync(api, client, PageType.Invoice, PageType.Prescription, PageType.Other);
        var (invoice, prescription, other) = (files[0], files[1], files[2]);
        api.Ai.FailNext(invoice.DocumentId, FakeDocumentAi.InvalidRequest());

        await api.RunAiJobsAsync();

        var run = await api.Scalar<Guid>($"select id from ai_run where claim_id = '{claim}' and task = 'FIELD_EXTRACTION'");
        Assert.Equal("FAILED", await RunStatus(api, claim, "FIELD_EXTRACTION"));
        Assert.Equal("INVALID_REQUEST", await api.Scalar<string>($"select error_code from ai_run where id = '{run}'"));
        Assert.Single(api.Ai.Extractions, request => request.RunId == run);
        Assert.Null(await api.Scalar<string>($"select raw_output_key from ai_run where id = '{run}'"));

        Assert.Equal("NEEDS_VERIFICATION", await ClaimStatus(api, claim));
        Assert.Equal(6, await api.Scalar<long>(
            $"select count(*) from claim_field where document_id = '{invoice.DocumentId}' and item_no = 0 and status = 'NEEDS_ENTRY' and ai_run_id is null"));
        Assert.Equal(
            ["diagnosis", "insured_name", "service_date"],
            await api.Column<string>($"select field_key::text from claim_field where document_id = '{prescription.DocumentId}' order by 1"));
        Assert.Equal(0, await api.Scalar<long>($"select count(*) from claim_field where document_id = '{other.DocumentId}'"));
        Assert.Equal(0, await api.Scalar<long>($"select count(*) from expense_line where claim_id = '{claim}'"));
        Assert.True(await FallbackActiveAsync(client, claim));
        Assert.Equal(1, await api.Scalar<long>(
            $"""
            select count(*) from audit_event
            where event_type = 'AI_RUN_FAILED' and ai_run_id = '{run}' and after_value->>'errorCode' = 'INVALID_REQUEST'
              and (after_value->'result'->>'fallback')::boolean
            """));
        Assert.Equal(1, await api.Scalar<long>(
            $"select count(*) from audit_event where event_type = 'CLAIM_STATUS_CHANGED' and ai_run_id = '{run}' and after_value->>'status' = 'NEEDS_VERIFICATION'"));
    }

    [Fact]
    public async Task Extraction_that_keeps_failing_falls_back_after_the_last_attempt()
    {
        await using var host = Retrying(maxAttempts: 2);
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, files) = await SubmittedAsync(api, client, PageType.Invoice);
        api.Ai.FailNext(files[0].DocumentId, FakeDocumentAi.ModelError(), FakeDocumentAi.ModelError());

        await api.RunAiJobsAsync(host.Services);

        Assert.Equal("FAILED", await RunStatus(api, claim, "FIELD_EXTRACTION"));
        Assert.Equal("NEEDS_VERIFICATION", await ClaimStatus(api, claim));
        Assert.True(await FallbackActiveAsync(client, claim));
    }

    [Fact]
    public async Task A_run_left_running_past_its_timeout_is_failed_and_the_fallback_runs()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, files) = await SubmittedAsync(api, client, PageType.Invoice);
        var started = (api.Clock.GetUtcNow() - TimeSpan.FromMinutes(10)).ToString("O", CultureInfo.InvariantCulture);
        await api.Sql($"update ai_run set status = 'RUNNING', started_at = '{started}' where claim_id = '{claim}' and task = 'FIELD_EXTRACTION'");

        await api.RunAiJobsAsync();

        Assert.Equal("FAILED", await RunStatus(api, claim, "FIELD_EXTRACTION"));
        Assert.Equal("TIMEOUT", await api.Scalar<string>($"select error_code from ai_run where claim_id = '{claim}' and task = 'FIELD_EXTRACTION'"));
        Assert.Equal("NEEDS_VERIFICATION", await ClaimStatus(api, claim));
        Assert.Equal(6, await api.Scalar<long>($"select count(*) from claim_field where document_id = '{files[0].DocumentId}' and status = 'NEEDS_ENTRY'"));
        Assert.True(await FallbackActiveAsync(client, claim));
    }

    [Fact]
    public async Task A_run_still_within_its_timeout_is_left_running()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, _) = await SubmittedAsync(api, client, PageType.Invoice);
        var started = (api.Clock.GetUtcNow() - TimeSpan.FromSeconds(60)).ToString("O", CultureInfo.InvariantCulture);
        await api.Sql($"update ai_run set status = 'RUNNING', started_at = '{started}' where claim_id = '{claim}' and task = 'FIELD_EXTRACTION'");

        await api.RunAiJobsAsync();

        Assert.Equal("RUNNING", await RunStatus(api, claim, "FIELD_EXTRACTION"));
        Assert.Equal("PROCESSING", await ClaimStatus(api, claim));

        // Leave nothing running for the tests that follow.
        api.Clock.Advance(TimeSpan.FromMinutes(5));
        await api.RunAiJobsAsync();
        Assert.Equal("FAILED", await RunStatus(api, claim, "FIELD_EXTRACTION"));
    }

    /// <summary>The same app with fewer attempts and no wait between them, as PreparationFailureTests does.</summary>
    private WebApplicationFactory<Program> Retrying(int maxAttempts) =>
        api.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Workers:AiJobs:MaxAttempts", maxAttempts.ToString(CultureInfo.InvariantCulture));
            builder.UseSetting("Workers:AiJobs:RetryDelaySeconds", "0");
        });
}
