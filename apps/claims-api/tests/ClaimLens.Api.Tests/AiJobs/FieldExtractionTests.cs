using System.Globalization;
using System.Text.Json;
using ClaimLens.Api.AiJobs;
using ClaimLens.Api.Domain.Enums;
using static ClaimLens.Api.Tests.AiJobs.AiData;
using static ClaimLens.Api.Tests.Features.IntakeData;

namespace ClaimLens.Api.Tests.AiJobs;

/// <summary>US05 through the AI Job Runner, with the fake Document AI Service, driven by the test.</summary>
public sealed class FieldExtractionTests(ApiFactory api) : IDisposable
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public void Dispose() => api.Clock.Reset();

    // ---- Confidence ≥ 0.90 (≥ 0.95 for money and identity) is auto-accepted; from 0.70 it needs verification; below
    //      0.70 it is left empty and needs entry, with no guessed value shown.

    [Theory]
    [InlineData("invoice_number", "HN-0045821", "0.90", "AUTO_ACCEPTED", "HIGH")]
    [InlineData("invoice_number", "HN-0045821", "0.899", "NEEDS_VERIFICATION", "CHECK")]
    [InlineData("invoice_number", "HN-0045821", "0.70", "NEEDS_VERIFICATION", "CHECK")]
    [InlineData("invoice_number", "HN-0045821", "0.699", "NEEDS_ENTRY", "MISSING")]
    [InlineData("provider_tax_code", "0101234567", "0.8995", "AUTO_ACCEPTED", "HIGH")]
    [InlineData("invoice_total", "1.400.000", "0.95", "AUTO_ACCEPTED", "HIGH")]
    [InlineData("invoice_total", "1.400.000", "0.949", "NEEDS_VERIFICATION", "CHECK")]
    [InlineData("invoice_total", "1.400.000", "0.70", "NEEDS_VERIFICATION", "CHECK")]
    [InlineData("invoice_total", "1.400.000", "0.699", "NEEDS_ENTRY", "MISSING")]
    [InlineData("insured_name", "Nguyễn Văn An", "0.95", "AUTO_ACCEPTED", "HIGH")]
    [InlineData("insured_name", "Nguyễn Văn An", "0.92", "NEEDS_VERIFICATION", "CHECK")]
    [InlineData("insured_name", "Nguyễn Văn An", "0.699", "NEEDS_ENTRY", "MISSING")]
    [InlineData("service_date", null, "0.99", "NEEDS_ENTRY", "MISSING")]
    public async Task Each_field_is_held_to_the_threshold_of_its_group(string key, string? value, string confidence, string status, string level)
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, files) = await SubmittedAsync(api, client, PageType.Invoice);
        var file = files[0];
        api.Ai.AnswerFields(file.DocumentId, Field(KeyOf(key), value, decimal.Parse(confidence, CultureInfo.InvariantCulture), file.Page));

        await api.RunAiJobsAsync();

        Assert.Equal(status, await Slot(api, file.DocumentId, "status", key));
        Assert.Equal(level, await Slot(api, file.DocumentId, "ai_level", key));
        Assert.Equal(value, await Slot(api, file.DocumentId, "ai_value", key));
        if (status == "AUTO_ACCEPTED")
        {
            Assert.Equal(value, await Slot(api, file.DocumentId, "value_text", key));
            Assert.Equal(file.Page.ToString(), await Slot(api, file.DocumentId, "source_page_id", key));
            Assert.NotNull(await Slot(api, file.DocumentId, "source_bbox", key));
        }
        else
        {
            // No value in use until a person gives one (US05).
            Assert.Null(await Slot(api, file.DocumentId, "value_text", key));
            Assert.Null(await Slot(api, file.DocumentId, "source_page_id", key));
        }

        Assert.Equal("NEEDS_VERIFICATION", await ClaimStatus(api, claim));
    }

    [Fact]
    public async Task An_auto_accepted_value_keeps_its_source_and_money_and_dates_are_read_into_their_columns()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (_, files) = await SubmittedAsync(api, client, PageType.Invoice);
        var file = files[0];
        api.Ai.AnswerFields(
            file.DocumentId,
            Field(FieldKey.InvoiceTotal, "1.400.000 đ", 0.99m, file.Page),
            Field(FieldKey.ServiceDate, "12/10/2026", 0.99m, file.Page));

        await api.RunAiJobsAsync();

        Assert.Equal("1400000", await Slot(api, file.DocumentId, "value_amount", "invoice_total"));
        Assert.Equal("2026-10-12", await Slot(api, file.DocumentId, "value_date", "service_date"));
        var source = await Slot(api, file.DocumentId, "source_bbox", "invoice_total");
        using var box = JsonDocument.Parse(source!);
        Assert.Equal(0.1m, box.RootElement.GetProperty("x").GetDecimal());
        Assert.Equal(0.04m, box.RootElement.GetProperty("h").GetDecimal());
        Assert.Equal(source, await Slot(api, file.DocumentId, "ai_bbox", "invoice_total"));
    }

    [Theory]
    [InlineData("invoice_total", "một triệu bốn")]
    [InlineData("invoice_total", "1.400,50")]
    [InlineData("service_date", "10/13/2026")]
    public async Task A_confident_value_that_cannot_be_read_needs_verification(string key, string value)
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (_, files) = await SubmittedAsync(api, client, PageType.Invoice);
        var file = files[0];
        api.Ai.AnswerFields(file.DocumentId, Field(KeyOf(key), value, 0.99m, file.Page));

        await api.RunAiJobsAsync();

        Assert.Equal("NEEDS_VERIFICATION", await Slot(api, file.DocumentId, "status", key));
        Assert.Equal("HIGH", await Slot(api, file.DocumentId, "ai_level", key));
        Assert.Null(await Slot(api, file.DocumentId, "value_text", key));
    }

    [Fact]
    public async Task A_confident_value_without_a_source_on_its_document_needs_verification()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (_, files) = await SubmittedAsync(api, client, PageType.Invoice, PageType.Receipt);
        var invoice = files[0];
        var receiptPage = files[1].Page;
        api.Ai.AnswerFields(
            invoice.DocumentId,
            Field(FieldKey.InvoiceNumber, "HN-0045821", 0.99m, receiptPage),
            new FieldSuggestion(FieldKey.ProviderName, 0, "Hồng Ngọc", 0.99m, invoice.Page, null));

        await api.RunAiJobsAsync();

        Assert.Equal("NEEDS_VERIFICATION", await Slot(api, invoice.DocumentId, "status", "invoice_number"));
        Assert.Null(await Slot(api, invoice.DocumentId, "ai_page_id", "invoice_number"));
        Assert.Equal("NEEDS_VERIFICATION", await Slot(api, invoice.DocumentId, "status", "provider_name"));
    }

    // ---- Fields follow the catalogue of the document's type; a field the AI did not return still gets its slot.

    [Fact]
    public async Task Every_catalogue_field_of_the_documents_type_gets_a_slot_and_other_types_get_none()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, files) = await SubmittedAsync(api, client, PageType.Invoice, PageType.VisitSummary, PageType.Other);
        var (invoice, visit, other) = (files[0], files[1], files[2]);
        api.Ai.AnswerFields(
            invoice.DocumentId,
            Field(FieldKey.InvoiceNumber, "HN-0045821", 0.99m, invoice.Page),
            Field(FieldKey.Diagnosis, "J02.9", 0.99m, invoice.Page));

        await api.RunAiJobsAsync();

        Assert.Equal(
            ["insured_name", "invoice_number", "invoice_total", "provider_name", "provider_tax_code", "service_date"],
            await api.Column<string>($"select field_key::text from claim_field where document_id = '{invoice.DocumentId}' order by 1"));
        Assert.Equal(
            ["diagnosis", "insured_name", "national_id", "policy_number", "service_date"],
            await api.Column<string>($"select field_key::text from claim_field where document_id = '{visit.DocumentId}' order by 1"));
        Assert.Equal(0, await api.Scalar<long>($"select count(*) from claim_field where document_id = '{other.DocumentId}'"));
        Assert.Equal(5, await api.Scalar<long>(
            $"select count(*) from claim_field where document_id = '{invoice.DocumentId}' and status = 'NEEDS_ENTRY' and ai_run_id is null and ai_value is null"));

        var run = await RunId(claim);
        var request = api.Ai.Extractions.Single(item => item.RunId == run);
        Assert.Equal([invoice.DocumentId, visit.DocumentId], request.Documents.Select(document => document.DocumentId));
        Assert.Equal(PageType.Invoice, request.Documents[0].Pages.Single().PageType);
        Assert.Contains(FieldKey.LineAmount, request.FieldCatalogue);
        Assert.Contains(FieldKey.Diagnosis, request.FieldCatalogue);
        Assert.DoesNotContain(FieldKey.Medication, request.FieldCatalogue);
        Assert.Equal(("field-extractor", "v2.4"), (request.Model.Name, request.Model.Version));
        Assert.Equal("NEEDS_VERIFICATION", await ClaimStatus(api, claim));
    }

    // ---- Expense lines come only from invoice items, built only from auto-accepted values.

    [Fact]
    public async Task Expense_lines_come_only_from_invoice_items_and_carry_only_auto_accepted_values()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, files) = await SubmittedAsync(api, client, PageType.Invoice, PageType.Receipt, PageType.Prescription);
        var (invoice, receipt, prescription) = (files[0], files[1], files[2]);
        api.Ai.AnswerFields(
            invoice.DocumentId,
            Field(FieldKey.LineDescription, "Khám bệnh", 0.99m, invoice.Page, itemNo: 1),
            Field(FieldKey.LineQuantity, "1", 0.99m, invoice.Page, itemNo: 1),
            Field(FieldKey.LineUnitPrice, "300.000", 0.99m, invoice.Page, itemNo: 1),
            Field(FieldKey.LineAmount, "300.000", 0.99m, invoice.Page, itemNo: 1),
            Field(FieldKey.LineDescription, "Kem dưỡng phục hồi da Obagi", 0.78m, invoice.Page, itemNo: 4),
            Field(FieldKey.LineAmount, "300.000", 0.96m, invoice.Page, itemNo: 4));
        api.Ai.AnswerFields(
            receipt.DocumentId,
            Field(FieldKey.LineDescription, "Khám bệnh", 0.99m, receipt.Page, itemNo: 1),
            Field(FieldKey.LineAmount, "300.000", 0.99m, receipt.Page, itemNo: 1));
        api.Ai.AnswerFields(
            prescription.DocumentId,
            Field(FieldKey.Medication, "Amoxicillin 500mg", 0.95m, prescription.Page, itemNo: 1),
            Field(FieldKey.Medication, "Paracetamol 500mg", 0.6m, prescription.Page, itemNo: 2));

        await api.RunAiJobsAsync();

        Assert.Equal(2, await api.Scalar<long>($"select count(*) from expense_line where claim_id = '{claim}'"));
        Assert.Equal(0, await api.Scalar<long>($"select count(*) from expense_line where claim_id = '{claim}' and document_id <> '{invoice.DocumentId}'"));
        Assert.Equal(
            "1|Khám bệnh|1.00|300000|300000|AI_EXTRACTED|PENDING",
            await api.Scalar<string>(
                $"select concat_ws('|', line_no, description, quantity, unit_price, amount, origin, mapping_status) from expense_line where document_id = '{invoice.DocumentId}' and line_no = 1"));
        Assert.Equal(
            "4|||300000",
            await api.Scalar<string>(
                $"select concat(line_no, '|', description, '|', unit_price, '|', amount) from expense_line where document_id = '{invoice.DocumentId}' and line_no = 4"));
        Assert.Equal(8, await api.Scalar<long>(
            $"select count(*) from claim_field f join expense_line l on l.id = f.expense_line_id and l.line_no = f.item_no where f.document_id = '{invoice.DocumentId}'"));
        Assert.Equal(0, await api.Scalar<long>($"select count(*) from claim_field where document_id = '{receipt.DocumentId}' and item_no > 0"));
        Assert.Equal(
            ["1:AUTO_ACCEPTED", "2:NEEDS_ENTRY"],
            await api.Column<string>(
                $"select item_no || ':' || status from claim_field where document_id = '{prescription.DocumentId}' and field_key = 'medication' order by item_no"));
        Assert.Equal(0, await api.Scalar<long>($"select count(*) from claim_field where document_id = '{prescription.DocumentId}' and expense_line_id is not null"));
    }

    // ---- When extraction finishes the claim moves on: READY_FOR_REVIEW, or NEEDS_VERIFICATION.

    [Fact]
    public async Task Every_required_field_auto_accepted_and_every_page_typed_makes_the_claim_ready_for_review()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, files) = await SubmittedAsync(api, client, PageType.Invoice, PageType.Other);
        api.Ai.AnswerFields(files[0].DocumentId, CleanInvoice(files[0].Page));

        await api.RunAiJobsAsync();

        Assert.Equal("READY_FOR_REVIEW", await ClaimStatus(api, claim));
        var run = await RunId(claim);
        Assert.Equal("SUCCEEDED", await RunStatus(api, claim, "FIELD_EXTRACTION"));
        var key = await api.Scalar<string>($"select raw_output_key from ai_run where id = '{run}'");
        Assert.Equal($"ai-raw/{claim}/{run}.json", key);
        Assert.NotNull(api.Store[key!]);

        Assert.Equal(1, await api.Scalar<long>(
            $"""
            select count(*) from audit_event
            where event_type = 'CLAIM_STATUS_CHANGED' and claim_id = '{claim}' and actor_type = 'SYSTEM' and ai_run_id = '{run}'
              and before_value->>'status' = 'PROCESSING' and after_value->>'status' = 'READY_FOR_REVIEW'
            """));
        Assert.Equal(7, await api.Scalar<long>(
            $"""
            select count(*) from audit_event
            where event_type = 'FIELD_AUTO_ACCEPTED' and claim_id = '{claim}' and actor_type = 'SYSTEM' and ai_run_id = '{run}'
              and after_value->>'acceptedBy' = 'threshold' and entity_type = 'claim_field'
            """));
        Assert.Equal(1, await api.Scalar<long>(
            $"""
            select count(*) from audit_event
            where event_type = 'AI_RUN_SUCCEEDED' and ai_run_id = '{run}'
              and (after_value->'result'->'fields'->>'autoAccepted')::int = 7 and after_value->'model'->>'name' = 'field-extractor'
            """));

        var status = await JsonOf(await client.GetAsync($"claims/{claim}/status", Cancel));
        Assert.Equal("READY_FOR_REVIEW", status.GetProperty("status").GetString());
        Assert.False(await FallbackActiveAsync(client, claim));
    }

    [Fact]
    public async Task A_required_field_that_needs_verification_leaves_the_claim_needing_verification()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, files) = await SubmittedAsync(api, client, PageType.Invoice);
        var page = files[0].Page;
        api.Ai.AnswerFields(
            files[0].DocumentId,
            [.. CleanInvoice(page).Where(field => field.FieldKey != FieldKey.LineAmount), Field(FieldKey.LineAmount, "300.000", 0.94m, page, itemNo: 1)]);

        await api.RunAiJobsAsync();

        Assert.Equal("NEEDS_VERIFICATION", await ClaimStatus(api, claim));
    }

    [Fact]
    public async Task A_page_left_unclassified_leaves_the_claim_needing_verification()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);
        var invoice = await AddFileAsync(api, client, claim);
        var unknown = await AddFileAsync(api, client, claim);
        api.Ai.AnswerPage(invoice.Page, PageType.Invoice, 0.99m);
        api.Ai.AnswerPage(unknown.Page, PageType.Receipt, 0.6m);
        await api.RunAiJobsAsync();
        await SubmitAsync(client, claim);
        api.Ai.AnswerFields(invoice.DocumentId, CleanInvoice(invoice.Page));

        await api.RunAiJobsAsync();

        Assert.Equal("NEEDS_VERIFICATION", await ClaimStatus(api, claim));
        Assert.Equal(0, await api.Scalar<long>($"select count(*) from claim_field where document_id = '{unknown.DocumentId}'"));
        var run = await RunId(claim);
        Assert.DoesNotContain(
            api.Ai.Extractions.Single(request => request.RunId == run).Documents,
            document => document.DocumentId == unknown.DocumentId);
    }

    [Fact]
    public async Task With_no_document_to_read_the_service_is_not_called_and_the_claim_still_moves_on()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, _) = await SubmittedAsync(api, client, PageType.Other);

        await api.RunAiJobsAsync();

        var run = await RunId(claim);
        Assert.DoesNotContain(api.Ai.Extractions, request => request.RunId == run);
        Assert.Equal("SUCCEEDED", await RunStatus(api, claim, "FIELD_EXTRACTION"));
        Assert.Equal("READY_FOR_REVIEW", await ClaimStatus(api, claim));
    }

    // ---- Extraction reads by page type, so it waits for the claim's page checks.

    [Fact]
    public async Task Extraction_waits_until_the_claims_page_checks_have_finished()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, file) = await DraftWithFileAsync(api, client);
        api.Ai.AnswerPage(file.Page, PageType.Invoice, 0.99m);
        api.Ai.FailNext(file.Page, FakeDocumentAi.ModelError());
        api.Ai.AnswerFields(file.DocumentId, CleanInvoice(file.Page));
        await api.RunAiJobsAsync();
        Assert.Equal("QUEUED", await RunStatus(api, claim, "PAGE_CHECK"));
        await SubmitAsync(client, claim);

        await api.RunAiJobsAsync();

        Assert.Equal("QUEUED", await RunStatus(api, claim, "FIELD_EXTRACTION"));
        Assert.Equal("PROCESSING", await ClaimStatus(api, claim));

        api.Clock.Advance(TimeSpan.FromMinutes(1));
        await api.RunAiJobsAsync();

        Assert.Equal("SUCCEEDED", await RunStatus(api, claim, "PAGE_CHECK"));
        Assert.Equal("SUCCEEDED", await RunStatus(api, claim, "FIELD_EXTRACTION"));
        var run = await RunId(claim);
        Assert.Equal(PageType.Invoice, api.Ai.Extractions.Single(request => request.RunId == run).Documents.Single().Pages.Single().PageType);
        Assert.Equal("READY_FOR_REVIEW", await ClaimStatus(api, claim));
    }

    private static FieldKey KeyOf(string key) => Enum.Parse<FieldKey>(key.Replace("_", "", StringComparison.Ordinal), ignoreCase: true);

    private Task<Guid> RunId(string claim) =>
        api.Scalar<Guid>($"select id from ai_run where claim_id = '{claim}' and task = 'FIELD_EXTRACTION'");
}
