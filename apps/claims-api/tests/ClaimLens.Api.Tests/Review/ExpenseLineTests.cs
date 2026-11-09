using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaimLens.Api.AiJobs;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Tests.AiJobs;
using static ClaimLens.Api.Tests.AiJobs.AiData;
using static ClaimLens.Api.Tests.Features.IntakeData;
using static ClaimLens.Api.Tests.Review.ReviewData;

namespace ClaimLens.Api.Tests.Review;

/// <summary>US06: a line AI missed is added by hand, and a line AI invented is removed.</summary>
public sealed class ExpenseLineTests(ApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static async Task<List<JsonElement>> LinesAsync(HttpClient client, string claim) =>
        (await JsonOf(await client.GetAsync($"claims/{claim}/expense-lines", Cancel))).GetProperty("items").EnumerateArray().ToList();

    private static Task<HttpResponseMessage> AddAsync(HttpClient client, string claim, Guid document) =>
        client.PostAsJsonAsync($"claims/{claim}/expense-lines", new { documentId = document }, Cancel);

    [Fact]
    public async Task Lines_are_listed_in_order_with_their_values_for_the_reviewer_and_the_approver()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var approver = await api.SignedInAs(Staff.Approver);
        var (claim, file) = await TakenAsync(api, reviewer, page =>
            CleanInvoice(page)
                .Concat([
                    Field(FieldKey.LineDescription, "Xét nghiệm", 0.99m, page, itemNo: 2),
                    Field(FieldKey.LineAmount, "120.000", 0.99m, page, itemNo: 2),
                    Field(FieldKey.LineQuantity, "2", 0.99m, page, itemNo: 2),
                    Field(FieldKey.LineUnitPrice, "60.000", 0.99m, page, itemNo: 2),
                ])
                .ToArray());

        var lines = await LinesAsync(reviewer, claim);

        Assert.Equal([1, 2], lines.Select(line => line.GetProperty("lineNo").GetInt32()));
        var line = lines[1];
        Assert.Equal(file.DocumentId.ToString(), line.GetProperty("documentId").GetString());
        Assert.Equal("Xét nghiệm", line.GetProperty("description").GetString());
        Assert.Equal(2m, line.GetProperty("quantity").GetDecimal());
        Assert.Equal(60_000, line.GetProperty("unitPrice").GetInt64());
        Assert.Equal(120_000, line.GetProperty("amount").GetInt64());
        Assert.Equal("AI_EXTRACTED", line.GetProperty("origin").GetString());
        Assert.Equal("PENDING", line.GetProperty("mapping").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, line.GetProperty("override").ValueKind);
        Assert.Equal(2, (await LinesAsync(approver, claim)).Count);
    }

    [Fact]
    public async Task A_line_added_by_hand_is_manual_takes_the_next_number_and_brings_its_empty_fields()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer, CleanInvoice);

        var response = await AddAsync(reviewer, claim, file.DocumentId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await JsonOf(response);
        var line = body.GetProperty("line");
        Assert.Equal(("MANUAL", 2), (line.GetProperty("origin").GetString(), line.GetProperty("lineNo").GetInt32()));
        Assert.Equal(JsonValueKind.Null, line.GetProperty("amount").ValueKind);

        // Its required fields now wait in the counter, while the status stays where the lifecycle has it.
        Assert.Equal(2, body.GetProperty("progress").GetProperty("fieldsToVerify").GetInt32());
        Assert.Equal("IN_REVIEW", body.GetProperty("progress").GetProperty("status").GetString());

        var fields = (await FieldsAsync(reviewer, claim)).Where(field => field.GetProperty("itemNo").GetInt32() == 2).ToList();
        Assert.Equal(
            ["line_amount", "line_description", "line_quantity", "line_unit_price"],
            fields.Select(field => field.GetProperty("fieldKey").GetString()).Order());
        Assert.All(fields, field =>
        {
            Assert.Equal("NEEDS_ENTRY", Status(field));
            Assert.Equal(Id(line), field.GetProperty("expenseLineId").GetString());
        });

        Assert.Equal(1, await api.Scalar<long>(
            $"select count(*) from audit_event where claim_id = '{claim}' and event_type = 'EXPENSE_LINE_ADDED' and entity_id = '{Id(line)}' and actor_id = '{Staff.ReviewerId}'"));
    }

    [Fact]
    public async Task Entering_a_manual_lines_fields_needs_a_drawn_source_and_fills_the_line()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer, CleanInvoice);
        await AddAsync(reviewer, claim, file.DocumentId);
        var amount = await FieldOf(reviewer, claim, "line_amount", 2);

        var refused = await PatchAsync(reviewer, claim, amount, new { rowVersion = Version(amount), action = "SET_VALUE", valueAmount = 50_000 });
        var entered = await PatchAsync(reviewer, claim, amount, new { rowVersion = Version(amount), action = "SET_VALUE", valueAmount = 50_000, sourcePageId = file.Page, sourceBbox = Region });

        Assert.Equal("SOURCE_REQUIRED", await Code(refused));
        Assert.Equal("ENTERED", Status((await JsonOf(entered)).GetProperty("field")));
        Assert.Equal(50_000, await api.Scalar<long>($"select amount from expense_line where claim_id = '{claim}' and line_no = 2"));
    }

    [Fact]
    public async Task A_line_needs_an_invoice_of_this_claim()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer, page => [Field(FieldKey.InvoiceTotal, "300.000", 0.99m, page)], PageType.Receipt);
        var (_, elsewhere) = await TakenAsync(api, reviewer, CleanInvoice);
        var receipt = await api.Scalar<Guid>($"select id from document where claim_id = '{claim}'");

        foreach (var document in new[] { receipt, elsewhere.DocumentId, Guid.NewGuid() })
        {
            var response = await AddAsync(reviewer, claim, document);

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal("VALIDATION_FAILED", await Code(response));
        }

        var unread = await reviewer.PostAsJsonAsync($"claims/{claim}/expense-lines", new { documentId = "x" }, Cancel);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unread.StatusCode);
        Assert.Equal(0, await api.Scalar<long>($"select count(*) from expense_line where claim_id = '{claim}'"));
    }

    [Fact]
    public async Task Removing_keeps_the_row_for_earlier_revisions_and_hides_the_line_and_its_fields()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer, CleanInvoice);
        var line = Id((await LinesAsync(reviewer, claim)).Single());
        var field = await FieldOf(reviewer, claim, "line_amount", 1);

        var response = await reviewer.DeleteAsync($"claims/{claim}/expense-lines/{line}", Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("IN_REVIEW", (await JsonOf(response)).GetProperty("status").GetString());
        Assert.Empty(await LinesAsync(reviewer, claim));
        Assert.Equal(Staff.ReviewerId, (await api.Scalar<Guid>($"select removed_by from expense_line where id = '{line}'")).ToString());
        Assert.NotNull(await api.Scalar<DateTime?>($"select removed_at from expense_line where id = '{line}'"));
        Assert.DoesNotContain(await FieldsAsync(reviewer, claim), item => item.GetProperty("itemNo").GetInt32() == 1);
        Assert.Equal(1, await api.Scalar<long>(
            $"select count(*) from audit_event where claim_id = '{claim}' and event_type = 'EXPENSE_LINE_REMOVED' and entity_id = '{line}' and actor_id = '{Staff.ReviewerId}'"));

        // Removed means gone for every operation on it, and its number is not used again.
        Assert.Equal(HttpStatusCode.NotFound, (await reviewer.DeleteAsync($"claims/{claim}/expense-lines/{line}", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await AcceptAsync(reviewer, claim, field)).StatusCode);
        var document = await api.Scalar<Guid>($"select document_id from expense_line where id = '{line}'");
        Assert.Equal(2, (await JsonOf(await AddAsync(reviewer, claim, document))).GetProperty("line").GetProperty("lineNo").GetInt32());
    }

    [Fact]
    public async Task Removing_a_line_AI_invented_clears_its_fields_from_the_count_and_moves_the_claim_on()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer, page =>
            CleanInvoice(page).Concat([Field(FieldKey.LineDescription, "Dòng lạ", 0.75m, page, itemNo: 2)]).ToArray());
        Assert.Equal("NEEDS_VERIFICATION", await ClaimStatus(api, claim));
        var invented = Id((await LinesAsync(reviewer, claim)).Single(line => line.GetProperty("lineNo").GetInt32() == 2));

        var progress = await JsonOf(await reviewer.DeleteAsync($"claims/{claim}/expense-lines/{invented}", Cancel));

        Assert.Equal("IN_REVIEW", progress.GetProperty("status").GetString());
        Assert.Equal(0, progress.GetProperty("fieldsToVerify").GetInt32());
    }

    [Fact]
    public async Task A_line_of_another_claim_is_not_found()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer, CleanInvoice);
        var (other, _) = await TakenAsync(api, reviewer, CleanInvoice);
        var line = Id((await LinesAsync(reviewer, other)).Single());

        var response = await reviewer.DeleteAsync($"claims/{claim}/expense-lines/{line}", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Single(await LinesAsync(reviewer, other));
    }

    [Fact]
    public async Task With_AI_down_the_reviewer_chooses_the_type_adds_a_line_and_enters_everything()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var intake = await api.SignedInAs(Staff.Intake);
        var (claim, file) = await DraftWithFileAsync(api, intake);
        api.Ai.AnswerPage(file.Page, PageType.Invoice, 0.99m);
        await api.RunAiJobsAsync();
        await SubmitAsync(intake, claim);
        api.Ai.FailNext(file.DocumentId, FakeDocumentAi.InvalidRequest());
        await api.RunAiJobsAsync();
        Assert.Equal("NEEDS_VERIFICATION", await ClaimStatus(api, claim));
        await StartAsync(reviewer, claim);

        var added = await AddAsync(reviewer, claim, file.DocumentId);
        Assert.Equal(1, (await JsonOf(added)).GetProperty("line").GetProperty("lineNo").GetInt32());

        object Source(int version, object value) => value is long amount
            ? new { rowVersion = version, action = "SET_VALUE", valueAmount = amount, sourcePageId = file.Page, sourceBbox = Region }
            : value is DateOnly day
                ? new { rowVersion = version, action = "SET_VALUE", valueDate = day, sourcePageId = file.Page, sourceBbox = Region }
                : new { rowVersion = version, action = "SET_VALUE", valueText = value, sourcePageId = file.Page, sourceBbox = Region };

        var values = new (string Key, int Item, object Value)[]
        {
            ("insured_name", 0, "Nguyễn Văn An"),
            ("provider_name", 0, "Hồng Ngọc"),
            ("invoice_number", 0, "HN-1"),
            ("service_date", 0, new DateOnly(2026, 10, 12)),
            ("invoice_total", 0, 300_000L),
            ("line_description", 1, "Khám bệnh"),
            ("line_amount", 1, 300_000L),
        };

        JsonElement last = default;
        foreach (var (key, item, value) in values)
        {
            var field = await FieldOf(reviewer, claim, key, item);
            var response = await PatchAsync(reviewer, claim, field, Source(Version(field), value));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            last = await JsonOf(response);
        }

        Assert.Equal("IN_REVIEW", last.GetProperty("progress").GetProperty("status").GetString());
        Assert.Equal(300_000, await api.Scalar<long>($"select amount from expense_line where claim_id = '{claim}'"));
    }
}
