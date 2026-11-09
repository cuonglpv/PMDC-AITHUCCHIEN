using System.Net;
using System.Text.Json;
using ClaimLens.Api.AiJobs;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Tests.AiJobs;
using static ClaimLens.Api.Tests.AiJobs.AiData;
using static ClaimLens.Api.Tests.Features.IntakeData;
using static ClaimLens.Api.Tests.Review.ReviewData;

namespace ClaimLens.Api.Tests.Review;

/// <summary>US06: re-reading one page with AI, which never touches what a person has resolved.</summary>
public sealed class PageRereadTests(ApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static async Task<(string Id, string Task)> QueuedAsync(HttpClient reviewer, string claim, Guid page)
    {
        var response = await RerunAsync(reviewer, claim, page);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var run = await JsonOf(response);
        Assert.Equal("QUEUED", run.GetProperty("status").GetString());
        return (Id(run), run.GetProperty("task").GetString()!);
    }

    [Fact]
    public async Task A_rerun_queues_one_run_for_that_page_and_nothing_else()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer);

        var (run, task) = await QueuedAsync(reviewer, claim, file.Page);

        Assert.Equal("PAGE_REREAD", task);
        Assert.Equal(file.Page, await api.Scalar<Guid>($"select scope_page_id from ai_run where id = '{run}'"));
        Assert.Equal(Staff.ReviewerId, (await api.Scalar<Guid>($"select triggered_by from ai_run where id = '{run}'")).ToString());
        Assert.Equal("QUEUED", (await PageAsync(reviewer, claim, file.Page)).GetProperty("aiStatus").GetString());
        Assert.Equal(1, await api.Scalar<long>(
            $"select count(*) from audit_event where claim_id = '{claim}' and event_type = 'AI_RERUN_REQUESTED' and ai_run_id = '{run}' and actor_id = '{Staff.ReviewerId}'"));

        // A second click while it waits gets the same run, not another.
        var (again, _) = await QueuedAsync(reviewer, claim, file.Page);
        Assert.Equal(run, again);
        Assert.Equal(1, await EventsAsync(api, claim, "AI_RERUN_REQUESTED"));

        await api.RunAiJobsAsync();

        var request = api.Ai.Rereads.Last(item => item.Page.PageId == file.Page);
        Assert.Equal(Guid.Parse(run), request.RunId);
        Assert.Equal(file.DocumentId, request.DocumentId);
        Assert.Equal(PageType.Invoice, request.Page.PageType);
        Assert.Contains(FieldKey.InvoiceTotal, request.FieldCatalogue);
        Assert.Equal("field-extractor", request.Model.Name);
        Assert.Equal("SUCCEEDED", await RunStatus(api, claim, "PAGE_REREAD"));
        Assert.Equal("DONE", (await PageAsync(reviewer, claim, file.Page)).GetProperty("aiStatus").GetString());
    }

    [Fact]
    public async Task A_page_of_another_claim_is_not_found()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer);
        var (_, elsewhere) = await TakenAsync(api, reviewer);

        var response = await RerunAsync(reviewer, claim, elsewhere.Page);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await EventsAsync(api, claim, "AI_RERUN_REQUESTED"));
    }

    [Fact]
    public async Task A_field_a_person_resolved_keeps_its_value_while_the_AI_columns_update()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer, page =>
            InvoiceToCheck(page).Concat([Field(FieldKey.ProviderTaxCode, "0100", 0.72m, page)]).ToArray());
        var fields = await FieldsAsync(reviewer, claim);
        var provider = FieldOf(fields, "provider_name");
        var total = FieldOf(fields, "invoice_total");
        var number = FieldOf(fields, "invoice_number");
        var tax = FieldOf(fields, "provider_tax_code");
        await PatchAsync(reviewer, claim, provider, new { rowVersion = Version(provider), action = "SET_VALUE", valueText = "Do người sửa" });
        await PatchAsync(reviewer, claim, total, new { rowVersion = Version(total), action = "NOT_PRESENT" });
        await AcceptAsync(reviewer, claim, number);
        await PatchAsync(reviewer, claim, tax, new { rowVersion = Version(tax), action = "SET_VALUE", valueText = "0200" });
        api.Ai.AnswerReread(
            file.Page,
            Field(FieldKey.ProviderName, "AI đọc lại", 0.99m, file.Page),
            Field(FieldKey.InvoiceTotal, "999.000", 0.99m, file.Page),
            Field(FieldKey.InvoiceNumber, "AI-2", 0.99m, file.Page),
            Field(FieldKey.ProviderTaxCode, "0300", 0.99m, file.Page));

        await QueuedAsync(reviewer, claim, file.Page);
        await api.RunAiJobsAsync();

        var after = await FieldsAsync(reviewer, claim);
        var kept = FieldOf(after, "provider_name");
        Assert.Equal(("CORRECTED", "Do người sửa"), (Status(kept), kept.GetProperty("valueText").GetString()));
        Assert.Equal("AI đọc lại", kept.GetProperty("ai").GetProperty("value").GetString());
        Assert.Equal(0.99m, kept.GetProperty("ai").GetProperty("confidence").GetDecimal());
        Assert.False(kept.GetProperty("aiMarker").GetBoolean());
        Assert.Equal("NOT_PRESENT", Status(FieldOf(after, "invoice_total")));
        Assert.Equal(JsonValueKind.Null, FieldOf(after, "invoice_total").GetProperty("valueAmount").ValueKind);
        Assert.Equal(("CONFIRMED", "HN-0045821"), (Status(FieldOf(after, "invoice_number")), FieldOf(after, "invoice_number").GetProperty("valueText").GetString()));
        Assert.Equal(("CORRECTED", "0200"), (Status(FieldOf(after, "provider_tax_code")), FieldOf(after, "provider_tax_code").GetProperty("valueText").GetString()));
    }

    [Fact]
    public async Task Fields_nobody_resolved_take_the_new_reading_by_the_thresholds_auto_accepted_ones_too()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer, CleanInvoice);
        FieldKey[] rereadDifferently = [FieldKey.InvoiceTotal, FieldKey.LineAmount, FieldKey.ProviderName];
        api.Ai.AnswerReread(
            file.Page,
            [
                .. CleanInvoice(file.Page).Where(field => !rereadDifferently.Contains(field.FieldKey)),

                // AUTO_ACCEPTED to a new auto-accepted value.
                Field(FieldKey.InvoiceTotal, "450.000", 0.98m, file.Page),

                // AUTO_ACCEPTED down to CHECK: the value leaves use, and the line with it.
                Field(FieldKey.LineAmount, "300.000", 0.80m, file.Page, itemNo: 1),

                // AUTO_ACCEPTED down below the floor: needs entry, the value never shown.
                Field(FieldKey.ProviderName, "???", 0.40m, file.Page),

                // NEEDS_ENTRY up to auto-accepted.
                Field(FieldKey.ProviderTaxCode, "0100", 0.95m, file.Page),
            ]);

        await QueuedAsync(reviewer, claim, file.Page);
        await api.RunAiJobsAsync();

        var after = await FieldsAsync(reviewer, claim);
        var total = FieldOf(after, "invoice_total");
        Assert.Equal(("AUTO_ACCEPTED", 450_000L), (Status(total), total.GetProperty("valueAmount").GetInt64()));
        Assert.True(total.GetProperty("aiMarker").GetBoolean());

        var amount = FieldOf(after, "line_amount", 1);
        Assert.Equal("NEEDS_VERIFICATION", Status(amount));
        Assert.Equal(JsonValueKind.Null, amount.GetProperty("valueAmount").ValueKind);
        Assert.Equal(JsonValueKind.Null, amount.GetProperty("sourcePageId").ValueKind);
        Assert.Null(await api.Scalar<long?>($"select amount from expense_line where claim_id = '{claim}'"));

        var provider = FieldOf(after, "provider_name");
        Assert.Equal("NEEDS_ENTRY", Status(provider));
        Assert.Equal(JsonValueKind.Null, provider.GetProperty("ai").GetProperty("value").ValueKind);

        Assert.Equal("AUTO_ACCEPTED", Status(FieldOf(after, "provider_tax_code")));

        // The status never goes back, but the work left is counted and named.
        Assert.Equal("IN_REVIEW", await ClaimStatus(api, claim));
        var detail = await JsonOf(await reviewer.GetAsync($"claims/{claim}", Cancel));
        Assert.Equal(2, detail.GetProperty("counts").GetProperty("fieldsToVerify").GetInt32());
        Assert.Equal("FIELDS_TO_VERIFY", detail.GetProperty("blockers")[0].GetProperty("code").GetString());

        // What the table overwrote stays in the trail, one event per field that changed.
        Assert.Equal(1, await api.Scalar<long>(
            $"""
            select count(*) from audit_event
            where entity_id = '{Id(total)}' and event_type = 'FIELD_AI_REREAD' and actor_type = 'SYSTEM' and ai_run_id is not null
              and before_value->>'aiValue' = '300.000' and after_value->>'aiValue' = '450.000'
              and before_value->>'status' = 'AUTO_ACCEPTED' and after_value->>'acceptedBy' = 'threshold'
            """));
    }

    [Fact]
    public async Task A_stale_edit_after_a_reread_is_refused()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer, CleanInvoice);
        var total = await FieldOf(reviewer, claim, "invoice_total");
        api.Ai.AnswerReread(file.Page, Field(FieldKey.InvoiceTotal, "450.000", 0.98m, file.Page));
        await QueuedAsync(reviewer, claim, file.Page);
        await api.RunAiJobsAsync();

        var response = await AcceptAsync(reviewer, claim, total);

        Assert.Equal("VERSION_CONFLICT", await Code(response));
    }

    [Fact]
    public async Task A_field_the_new_reading_no_longer_finds_loses_its_AI_value_and_goes_to_entry()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer, CleanInvoice);
        var invoiceNumber = await FieldOf(reviewer, claim, "invoice_number");
        await AcceptAsync(reviewer, claim, invoiceNumber);
        api.Ai.AnswerReread(file.Page, Field(FieldKey.ProviderName, null, 0m, file.Page));

        await QueuedAsync(reviewer, claim, file.Page);
        await api.RunAiJobsAsync();

        var after = await FieldsAsync(reviewer, claim);
        Assert.Equal("NEEDS_ENTRY", Status(FieldOf(after, "provider_name")));
        Assert.Equal("NEEDS_ENTRY", Status(FieldOf(after, "invoice_total")));
        Assert.Equal(JsonValueKind.Null, FieldOf(after, "invoice_total").GetProperty("ai").ValueKind);
        Assert.Equal(JsonValueKind.Null, FieldOf(after, "invoice_total").GetProperty("valueAmount").ValueKind);

        // What a person resolved stays, even with no reading beside it.
        Assert.Equal(("CONFIRMED", "HN-0045821"), (Status(FieldOf(after, "invoice_number")), FieldOf(after, "invoice_number").GetProperty("valueText").GetString()));
    }

    [Fact]
    public async Task Only_that_page_changes_and_a_field_read_on_another_page_stays()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var intake = await api.SignedInAs(Staff.Intake);
        var (claim, file) = await DraftWithFileAsync(api, intake, pages: 2);
        api.Ai.AnswerPage(file.Pages[0], PageType.Invoice, 0.99m);
        api.Ai.AnswerPage(file.Pages[1], PageType.Invoice, 0.99m);
        await api.RunAiJobsAsync();
        await SubmitAsync(intake, claim);
        api.Ai.AnswerFields(
            file.DocumentId,
            CleanInvoice(file.Pages[0])
                .Select(field => field.FieldKey == FieldKey.InvoiceTotal ? Field(FieldKey.InvoiceTotal, "300.000", 0.99m, file.Pages[1]) : field)
                .ToArray());
        await api.RunAiJobsAsync();
        await StartAsync(reviewer, claim);

        // The re-read of page 1 does not find the total, which was read on page 2.
        api.Ai.AnswerReread(
            file.Pages[0],
            Field(FieldKey.InvoiceTotal, null, 0m, file.Pages[0]),
            Field(FieldKey.InsuredName, "Nguyễn Văn Ân", 0.99m, file.Pages[0]));
        await QueuedAsync(reviewer, claim, file.Pages[0]);
        await api.RunAiJobsAsync();

        var after = await FieldsAsync(reviewer, claim);
        var total = FieldOf(after, "invoice_total");
        Assert.Equal(("AUTO_ACCEPTED", 300_000L), (Status(total), total.GetProperty("valueAmount").GetInt64()));
        Assert.Equal(file.Pages[1].ToString(), total.GetProperty("sourcePageId").GetString());
        Assert.Equal("Nguyễn Văn Ân", FieldOf(after, "insured_name").GetProperty("valueText").GetString());
    }

    [Fact]
    public async Task A_new_item_on_an_invoice_page_adds_an_expense_line_with_its_fields()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer, CleanInvoice);
        api.Ai.AnswerReread(
            file.Page,
            Field(FieldKey.LineDescription, "Xét nghiệm", 0.95m, file.Page, itemNo: 2),
            Field(FieldKey.LineAmount, "120.000", 0.99m, file.Page, itemNo: 2));

        await QueuedAsync(reviewer, claim, file.Page);
        await api.RunAiJobsAsync();

        var lines = (await JsonOf(await reviewer.GetAsync($"claims/{claim}/expense-lines", Cancel))).GetProperty("items").EnumerateArray().ToList();
        var added = lines.Single(line => line.GetProperty("lineNo").GetInt32() == 2);
        Assert.Equal("AI_EXTRACTED", added.GetProperty("origin").GetString());
        Assert.Equal("Xét nghiệm", added.GetProperty("description").GetString());
        Assert.Equal(120_000, added.GetProperty("amount").GetInt64());
        var fields = await FieldsAsync(reviewer, claim);
        Assert.Equal(Id(added), FieldOf(fields, "line_amount", 2).GetProperty("expenseLineId").GetString());
        Assert.Equal("NEEDS_ENTRY", Status(FieldOf(fields, "line_quantity", 2)));
    }

    [Fact]
    public async Task A_line_the_reviewer_removed_does_not_come_back()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer, CleanInvoice);
        var line = await api.Scalar<Guid>($"select id from expense_line where claim_id = '{claim}'");
        Assert.Equal(HttpStatusCode.OK, (await reviewer.DeleteAsync($"claims/{claim}/expense-lines/{line}", Cancel)).StatusCode);
        api.Ai.AnswerReread(file.Page, Field(FieldKey.LineAmount, "300.000", 0.99m, file.Page, itemNo: 1));

        await QueuedAsync(reviewer, claim, file.Page);
        await api.RunAiJobsAsync();

        Assert.Empty((await JsonOf(await reviewer.GetAsync($"claims/{claim}/expense-lines", Cancel))).GetProperty("items").EnumerateArray());
        Assert.Equal(1, await api.Scalar<long>($"select count(*) from expense_line where claim_id = '{claim}'"));
    }

    [Fact]
    public async Task A_reread_that_resolves_the_last_field_moves_the_claim_on()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer);
        api.Ai.AnswerReread(file.Page, CleanInvoice(file.Page));

        await QueuedAsync(reviewer, claim, file.Page);
        await api.RunAiJobsAsync();

        Assert.Equal("IN_REVIEW", await ClaimStatus(api, claim));
        Assert.Equal(1, await api.Scalar<long>(
            $"""
            select count(*) from audit_event
            where claim_id = '{claim}' and event_type = 'CLAIM_STATUS_CHANGED' and actor_type = 'SYSTEM' and ai_run_id is not null
              and before_value->>'status' = 'NEEDS_VERIFICATION' and after_value->>'status' = 'READY_FOR_REVIEW'
            """));
    }

    [Fact]
    public async Task A_page_chosen_by_hand_is_read_as_that_type()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var intake = await api.SignedInAs(Staff.Intake);
        var (claim, file) = await DraftWithFileAsync(api, intake);
        api.Ai.AnswerPage(file.Page, PageType.Receipt, 0.64m);
        await api.RunAiJobsAsync();
        await SubmitAsync(intake, claim);
        await api.RunAiJobsAsync();
        await StartAsync(reviewer, claim);
        await reviewer.PutAsync(
            $"claims/{claim}/pages/{file.Page}/type",
            new StringContent("""{"pageType":"RECEIPT"}""", System.Text.Encoding.UTF8, "application/json"),
            Cancel);
        api.Ai.AnswerReread(file.Page, Field(FieldKey.InvoiceTotal, "300.000", 0.99m, file.Page));

        await QueuedAsync(reviewer, claim, file.Page);
        await api.RunAiJobsAsync();

        var request = api.Ai.Rereads.Last(item => item.Page.PageId == file.Page);
        Assert.Equal(PageType.Receipt, request.Page.PageType);
        Assert.DoesNotContain(FieldKey.LineAmount, request.FieldCatalogue);
        Assert.Equal("AUTO_ACCEPTED", Status(await FieldOf(reviewer, claim, "invoice_total")));
        Assert.Equal("IN_REVIEW", await ClaimStatus(api, claim));
    }

    [Fact]
    public async Task A_failed_run_falls_back_for_that_page_only()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer);
        var before = await FieldsAsync(reviewer, claim);
        api.Ai.FailNext(file.Page, FakeDocumentAi.InvalidRequest());

        await QueuedAsync(reviewer, claim, file.Page);
        await api.RunAiJobsAsync();

        Assert.Equal("FAILED", await RunStatus(api, claim, "PAGE_REREAD"));
        Assert.Equal("FAILED", (await PageAsync(reviewer, claim, file.Page)).GetProperty("aiStatus").GetString());
        Assert.True(await FallbackActiveAsync(reviewer, claim));
        Assert.Equal("NEEDS_VERIFICATION", await ClaimStatus(api, claim));
        var after = await FieldsAsync(reviewer, claim);
        Assert.Equal(before.Count, after.Count);
        Assert.Equal(Version(FieldOf(before, "invoice_total")), Version(FieldOf(after, "invoice_total")));
        Assert.Equal("AUTO_ACCEPTED", Status(FieldOf(after, "invoice_total")));

        // A later run that succeeds clears the banner.
        await QueuedAsync(reviewer, claim, file.Page);
        await api.RunAiJobsAsync();
        Assert.False(await FallbackActiveAsync(reviewer, claim));
    }

    [Fact]
    public async Task A_failed_run_on_a_page_with_no_fields_yet_leaves_the_empty_ones_to_enter()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var intake = await api.SignedInAs(Staff.Intake);
        var (claim, file) = await DraftWithFileAsync(api, intake);
        api.Ai.AnswerPage(file.Page, PageType.Other, 0.99m);
        await api.RunAiJobsAsync();
        await SubmitAsync(intake, claim);
        await api.RunAiJobsAsync();
        await StartAsync(reviewer, claim);
        await api.Sql($"update document_page set page_type = 'RECEIPT' where id = '{file.Page}'");
        api.Ai.FailNext(file.Page, FakeDocumentAi.InvalidRequest());

        await QueuedAsync(reviewer, claim, file.Page);
        await api.RunAiJobsAsync();

        var fields = await FieldsAsync(reviewer, claim);
        Assert.Equal("NEEDS_ENTRY", Status(FieldOf(fields, "invoice_total")));
    }

    [Fact]
    public async Task A_run_left_running_past_its_timeout_fails_like_the_others()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer);
        var (run, _) = await QueuedAsync(reviewer, claim, file.Page);
        await api.Sql($"update ai_run set status = 'RUNNING', started_at = now() - interval '10 minutes' where id = '{run}'");

        await api.RunAiJobsAsync();

        Assert.Equal("FAILED", await api.Scalar<string>($"select status::text from ai_run where id = '{run}'"));
        Assert.Equal("TIMEOUT", await api.Scalar<string>($"select error_code from ai_run where id = '{run}'"));
        Assert.Equal("FAILED", (await PageAsync(reviewer, claim, file.Page)).GetProperty("aiStatus").GetString());
    }

    [Fact]
    public async Task A_national_id_read_again_stays_out_of_the_audit_trail()
    {
        const string FullId = "012345678901";
        const string NewId = "098765432109";
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(
            api,
            reviewer,
            page =>
            [
                Field(FieldKey.InsuredName, "Nguyễn Văn An", 0.99m, page),
                Field(FieldKey.ServiceDate, "12/10/2026", 0.99m, page),
                Field(FieldKey.Diagnosis, "J02.9", 0.99m, page),
                Field(FieldKey.NationalId, FullId, 0.99m, page),
            ],
            PageType.VisitSummary);
        api.Ai.AnswerReread(file.Page, Field(FieldKey.NationalId, NewId, 0.99m, file.Page));

        await QueuedAsync(reviewer, claim, file.Page);
        await api.RunAiJobsAsync();

        Assert.Equal(NewId, await Slot(api, file.DocumentId, "value_text", "national_id"));
        Assert.Equal(0, await api.Scalar<long>(
            $"""
            select count(*) from audit_event
            where claim_id = '{claim}' and (before_value::text like '%{FullId}%' or after_value::text like '%{FullId}%'
                                         or before_value::text like '%{NewId}%' or after_value::text like '%{NewId}%')
            """));
    }
}
