using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaimLens.Api.AiJobs;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Tests.AiJobs;
using ClaimLens.Api.Tests.Features;
using static ClaimLens.Api.Tests.Features.IntakeData;
using static ClaimLens.Api.Tests.Review.ReviewData;

namespace ClaimLens.Api.Tests.Golden;

/// <summary>
/// The golden scenarios through intake, page check, field extraction and review (Definition of Done). The documents are the files
/// in fixtures/golden, and the Document AI answers are the same recordings the fake gateway replays in the full stack
/// (fixtures/golden/*/ai-responses/pages.json), so these tests and the e2e run see the same AI.
/// </summary>
public sealed class GoldenScenarioTests(ApiFactory api) : IDisposable
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static readonly string[] CleanPacket = ["invoice.pdf", "receipt.jpg", "prescription.docx", "itemised-bill.xlsx", "visit-summary.pdf"];

    /// <summary>The golden member, Nguyễn Văn An, on PL-2026-00731 (db/seed/dev/040_members_and_policies.sql).</summary>
    private static readonly TestMember An = new(Guid.Parse(AnMemberId), Guid.Parse(AnPolicyId), "Nguyễn Văn An", "000000000001", "PL-2026-00731");

    public void Dispose() => api.Converter.Reset();

    [Fact]
    public async Task Clean_packet_is_read_and_waits_only_for_the_diagnosis_a_person_must_enter()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await GoldenDraftAsync(client);
        var packet = await UploadAsync(client, claim, "clean", CleanPacket);
        var recorded = Recording.Load("clean");

        // Page check: every page gets the type the recording gives it.
        ScriptPageCheck(packet, recorded);
        await api.RunAiJobsAsync();
        foreach (var page in recorded)
        {
            var pageId = packet[page.Document].Pages[page.PageNo];
            Assert.Equal(
                FieldName(page.PageCheck.PageType),
                await api.Scalar<string>($"select page_type::text from document_page where id = '{pageId}'"));
        }

        // Field extraction after submission.
        ScriptExtraction(packet, recorded);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync($"claims/{claim}/submit", null, Cancel)).StatusCode);
        await api.RunAiJobsAsync();

        Assert.Equal("NEEDS_VERIFICATION", await api.Scalar<string>($"select status::text from claim where id = '{claim}'"));

        // Everything else was auto-accepted; the diagnosis was read below 0.70 on both documents that carry it.
        var open = await OpenFieldsAsync(claim);
        Assert.Equal(new[] { "prescription.docx diagnosis#0 NEEDS_ENTRY", "visit-summary.pdf diagnosis#0 NEEDS_ENTRY" }, open);

        // The five invoice lines, with the golden amounts, and the invoice total.
        Assert.Equal(
            "1:300000,2:250000,3:150000,4:300000,5:400000",
            await api.Scalar<string>(
                $"select string_agg(line_no || ':' || amount, ',' order by line_no) from expense_line where claim_id = '{claim}'"));
        Assert.Equal(
            packet["invoice.pdf"].DocumentId.ToString(),
            await api.Scalar<string>($"select string_agg(distinct document_id::text, ',') from expense_line where claim_id = '{claim}'"));
        Assert.Equal(
            1_400_000,
            await api.Scalar<long>(
                $"select value_amount from claim_field where document_id = '{packet["invoice.pdf"].DocumentId}' and field_key = 'invoice_total'"));
        Assert.Equal(
            "Kem dưỡng phục hồi da Obagi",
            await api.Scalar<string>($"select description from expense_line where claim_id = '{claim}' and line_no = 4"));

        // US23: a reviewer finds it in Available, with no member name, and takes it. Nobody else can after that.
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var number = await api.Scalar<string>($"select claim_number from claim where id = '{claim}'");
        var available = (await JsonOf(await reviewer.GetAsync($"claims?view=AVAILABLE&claimNumber={number}", Cancel)))
            .GetProperty("items").EnumerateArray().Single();
        Assert.Equal(claim, Id(available));
        Assert.Equal(JsonValueKind.Null, available.GetProperty("memberName").ValueKind);
        await StartAsync(reviewer, claim);

        var (other, _) = await NewReviewerAsync(api);
        var refused = await other.PostAsync($"claims/{claim}/start-review", null, Cancel);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("CLAIM_ALREADY_ASSIGNED", (await JsonOf(refused)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"claim-numbers/{number}", Cancel)).StatusCode);

        // US06: lowest confidence first, the two diagnoses at the top with their values withheld, and the ID number masked.
        var fields = await FieldsAsync(reviewer, claim);
        var confidences = fields.Select(field => field.GetProperty("ai").GetProperty("confidence").GetDecimal()).ToList();
        Assert.Equal(confidences.Order(), confidences);
        Assert.Equal(["diagnosis", "diagnosis"], fields.Take(2).Select(field => field.GetProperty("fieldKey").GetString()));
        Assert.All(fields.Take(2), field => Assert.Equal(JsonValueKind.Null, field.GetProperty("ai").GetProperty("value").ValueKind));
        Assert.Equal("********0001", FieldOf(fields, "national_id").GetProperty("valueText").GetString());

        // The visit summary's diagnosis is the one required field left: entering it with a drawn region moves the claim on.
        var visitSummary = packet["visit-summary.pdf"];
        var diagnosis = fields.Single(field =>
            field.GetProperty("fieldKey").GetString() == "diagnosis"
            && field.GetProperty("documentId").GetString() == visitSummary.DocumentId.ToString());
        Assert.True(diagnosis.GetProperty("required").GetBoolean());
        var entered = await PatchAsync(reviewer, claim, diagnosis, new
        {
            rowVersion = Version(diagnosis),
            action = "SET_VALUE",
            valueText = "J02.9",
            sourcePageId = visitSummary.Pages[1],
            sourceBbox = Region,
        });

        Assert.Equal(HttpStatusCode.OK, entered.StatusCode);
        var answer = await JsonOf(entered);
        Assert.Equal("ENTERED", answer.GetProperty("field").GetProperty("status").GetString());
        Assert.Equal("IN_REVIEW", answer.GetProperty("progress").GetProperty("status").GetString());
        Assert.Equal(0, answer.GetProperty("progress").GetProperty("fieldsToVerify").GetInt32());
        Assert.Equal(
            "PROCESSING,NEEDS_VERIFICATION,READY_FOR_REVIEW,IN_REVIEW",
            await api.Scalar<string>(
                $"select string_agg(after_value->>'status', ',' order by id) from audit_event where claim_id = '{claim}' and event_type = 'CLAIM_STATUS_CHANGED'"));

        // The prescription's diagnosis is optional, so it never held the claim back.
        Assert.Equal(new[] { "prescription.docx diagnosis#0 NEEDS_ENTRY" }, await OpenFieldsAsync(claim));
    }

    [Fact]
    public async Task Blurred_receipt_is_unclassified_and_marked_low_quality()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await GoldenDraftAsync(client);
        var packet = await UploadAsync(client, claim, "blurry", "receipt-blurry.jpg");
        var recorded = Recording.Load("blurry");

        ScriptPageCheck(packet, recorded);
        await api.RunAiJobsAsync();

        var page = packet["receipt-blurry.jpg"].Pages[1];
        Assert.Equal(
            "UNCLASSIFIED RECEIPT CHECK LOW_QUALITY DONE",
            await api.Scalar<string>(
                $"select page_type || ' ' || ai_page_type || ' ' || ai_type_level || ' ' || quality || ' ' || ai_status from document_page where id = '{page}'"));
    }

    [Fact]
    public async Task With_the_AI_down_the_clean_packet_goes_to_manual_work()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await GoldenDraftAsync(client);
        var packet = await UploadAsync(client, claim, "clean", CleanPacket);
        foreach (var page in packet.Values.SelectMany(document => document.Pages.Values))
        {
            api.Ai.FailNext(page, FakeDocumentAi.InvalidRequest());
        }

        await api.RunAiJobsAsync();
        Assert.Equal(
            0,
            await api.Scalar<long>(
                $"select count(*) from document_page where claim_id = '{claim}' and (ai_status <> 'FAILED' or page_type <> 'UNCLASSIFIED')"));

        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync($"claims/{claim}/submit", null, Cancel)).StatusCode);
        await api.RunAiJobsAsync();

        // Nothing has a type to read by, so the extraction asks nothing, and the claim waits for a person.
        Assert.Equal("NEEDS_VERIFICATION", await api.Scalar<string>($"select status::text from claim where id = '{claim}'"));
        Assert.DoesNotContain(api.Ai.Extractions, request => request.Documents.Any(document => packet.Values.Any(file => file.DocumentId == document.DocumentId)));
        var status = await JsonOf(await client.GetAsync($"claims/{claim}/status", Cancel));
        Assert.True(status.GetProperty("fallbackActive").GetBoolean());

        // US06: the reviewer takes it, chooses each page's type, which creates the empty fields, and works by hand.
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        await StartAsync(reviewer, claim);
        Assert.Empty(await FieldsAsync(reviewer, claim));
        foreach (var page in Recording.Load("clean"))
        {
            var chosen = await reviewer.PutAsJsonAsync(
                $"claims/{claim}/pages/{packet[page.Document].Pages[page.PageNo]}/type",
                new { pageType = FieldName(page.PageCheck.PageType) },
                Cancel);
            Assert.Equal(HttpStatusCode.OK, chosen.StatusCode);
        }

        // AI read nothing, so nothing has a source: a value needs a region drawn (BR-02).
        var invoice = packet["invoice.pdf"];
        var total = (await FieldsAsync(reviewer, claim)).Single(field =>
            field.GetProperty("fieldKey").GetString() == "invoice_total"
            && field.GetProperty("documentId").GetString() == invoice.DocumentId.ToString());
        var noSource = await PatchAsync(reviewer, claim, total, new { rowVersion = Version(total), action = "SET_VALUE", valueAmount = 1_400_000 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noSource.StatusCode);
        Assert.Equal("SOURCE_REQUIRED", (await JsonOf(noSource)).GetProperty("code").GetString());

        // The invoice's line, added by hand, brings its empty fields.
        var added = await reviewer.PostAsJsonAsync($"claims/{claim}/expense-lines", new { documentId = invoice.DocumentId }, Cancel);
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);

        var pageOf = packet.Values.ToDictionary(file => file.DocumentId.ToString(), file => file.Pages[1]);
        foreach (var field in (await FieldsAsync(reviewer, claim)).Where(field => field.GetProperty("required").GetBoolean()))
        {
            var entered = await PatchAsync(reviewer, claim, field, Entry(field, pageOf[field.GetProperty("documentId").GetString()!]));
            Assert.Equal(HttpStatusCode.OK, entered.StatusCode);
        }

        Assert.Equal("IN_REVIEW", await api.Scalar<string>($"select status::text from claim where id = '{claim}'"));
        Assert.Equal(
            "1 MANUAL 300000 Khám nội tổng quát",
            await api.Scalar<string>($"select line_no || ' ' || origin || ' ' || amount || ' ' || description from expense_line where claim_id = '{claim}'"));
    }

    // ---- The packet and the recorded answers.

    private sealed record UploadedFile(Guid DocumentId, Dictionary<int, Guid> Pages);

    private async Task<string> GoldenDraftAsync(HttpClient client)
    {
        var response = await CreateClaimAsync(client, An);
        response.EnsureSuccessStatusCode();
        return Id((await JsonOf(response)).GetProperty("claim"));
    }

    /// <summary>Uploads the files and prepares them. DOCX and XLSX go through the test converter, one page each, as the real ones convert.</summary>
    private async Task<Dictionary<string, UploadedFile>> UploadAsync(HttpClient client, string claim, string scenario, params string[] files)
    {
        var ids = new Dictionary<string, Guid>();
        foreach (var file in files)
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "golden", scenario, "documents", file), Cancel);
            ids[file] = Guid.Parse(await UploadedAsync(client, claim, bytes, file));
        }

        await api.PrepareDocumentsAsync();

        var packet = new Dictionary<string, UploadedFile>();
        foreach (var (file, documentId) in ids)
        {
            Assert.Equal("READY", await api.Scalar<string>($"select status::text from document where id = '{documentId}'"));
            var pages = (await api.Scalar<string>(
                    $"select string_agg(page_no || '=' || id, ',' order by page_no) from document_page where document_id = '{documentId}'"))!
                .Split(',')
                .Select(pair => pair.Split('='))
                .ToDictionary(pair => int.Parse(pair[0], System.Globalization.CultureInfo.InvariantCulture), pair => Guid.Parse(pair[1]));
            packet[file] = new UploadedFile(documentId, pages);
        }

        return packet;
    }

    private void ScriptPageCheck(Dictionary<string, UploadedFile> packet, IReadOnlyList<RecordedPage> recorded)
    {
        foreach (var page in recorded.Where(page => packet.ContainsKey(page.Document)))
        {
            api.Ai.AnswerPage(packet[page.Document].Pages[page.PageNo], page.PageCheck.PageType, page.PageCheck.TypeConfidence, page.PageCheck.QualityScore);
        }
    }

    private void ScriptExtraction(Dictionary<string, UploadedFile> packet, IReadOnlyList<RecordedPage> recorded)
    {
        foreach (var document in recorded.Where(page => packet.ContainsKey(page.Document)).GroupBy(page => page.Document))
        {
            var file = packet[document.Key];
            api.Ai.AnswerFields(
                file.DocumentId,
                [.. document.SelectMany(page => page.Fields.Select(field => new FieldSuggestion(
                    field.FieldKey,
                    field.ItemNo,
                    field.Value,
                    field.Confidence,
                    field.Value is null ? null : file.Pages[page.PageNo],
                    field.Value is null ? null : field.Bbox)))]);
        }
    }

    private async Task<List<string>> OpenFieldsAsync(string claim) =>
        [.. (await api.Scalar<string>(
                $"""
                select string_agg(d.original_filename || ' ' || f.field_key || '#' || f.item_no || ' ' || f.status, '|'
                                  order by d.original_filename, f.field_key)
                from claim_field f join document d on d.id = f.document_id
                where f.claim_id = '{claim}' and f.status in ('NEEDS_ENTRY', 'NEEDS_VERIFICATION')
                """) ?? "")
            .Split('|', StringSplitOptions.RemoveEmptyEntries)];

    /// <summary>What a reviewer types for a field by its kind, with the region they drew.</summary>
    private static Dictionary<string, object> Entry(JsonElement field, Guid page)
    {
        var body = new Dictionary<string, object>
        {
            ["rowVersion"] = Version(field),
            ["action"] = "SET_VALUE",
            ["sourcePageId"] = page,
            ["sourceBbox"] = Region,
        };
        var key = field.GetProperty("fieldKey").GetString();
        switch (field.GetProperty("valueKind").GetString())
        {
            case "MONEY":
                body["valueAmount"] = key == "line_amount" ? 300_000 : 1_400_000;
                break;
            case "DATE":
                body["valueDate"] = "2026-10-12";
                break;
            default:
                body["valueText"] = key == "line_description" ? "Khám nội tổng quát" : "Đã nhập tay";
                break;
        }

        return body;
    }

    private static string FieldName(PageType type) => JsonNamingPolicy.SnakeCaseUpper.ConvertName(type.ToString());

    // ---- fixtures/golden/<scenario>/ai-responses/pages.json, as apps/document-ai reads it.

    private sealed record RecordedCheck(PageType PageType, decimal TypeConfidence, decimal QualityScore);

    private sealed record RecordedField(FieldKey FieldKey, int ItemNo, string? Value, decimal Confidence, Bbox? Bbox);

    private sealed record RecordedPage(string Document, int PageNo, RecordedCheck PageCheck, List<RecordedField> Fields);

    private static class Recording
    {
        public static IReadOnlyList<RecordedPage> Load(string scenario) =>
            JsonSerializer.Deserialize<List<RecordedPage>>(
                File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "golden", scenario, "ai-responses", "pages.json")),
                DocumentAiJson.Options)!;
    }
}
