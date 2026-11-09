using System.Net;
using System.Text.Json;
using ClaimLens.Api.AiJobs;
using ClaimLens.Api.Domain.Enums;
using static ClaimLens.Api.Tests.AiJobs.AiData;
using static ClaimLens.Api.Tests.Features.IntakeData;
using static ClaimLens.Api.Tests.Review.ReviewData;

namespace ClaimLens.Api.Tests.Review;

/// <summary>US06: the Evidence tab's list, and accepting, correcting, entering and marking fields not present.</summary>
public sealed class FieldVerificationTests(ApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private const string FullId = "012345678901";
    private const string MaskedId = "********8901";

    private static FieldSuggestion[] VisitSummary(Guid page) =>
    [
        Field(FieldKey.InsuredName, "Nguyễn Văn An", 0.99m, page),
        Field(FieldKey.ServiceDate, "12/10/2026", 0.95m, page),
        Field(FieldKey.Diagnosis, "J02.9", 0.58m, page),
        Field(FieldKey.NationalId, FullId, 0.99m, page),
        Field(FieldKey.PolicyNumber, "PT-1", 0.72m, page),
    ];

    /// <summary>AI read the provider name but gave no region, so accepting it needs one drawn.</summary>
    private static FieldSuggestion[] WithoutSource(Guid page) =>
        CleanInvoice(page)
            .Select(field => field.FieldKey == FieldKey.ProviderName ? field with { Confidence = 0.80m, PageId = null, Bbox = null } : field)
            .ToArray();

    private static object Drawn(int version, Guid page, object? value = null) =>
        new { rowVersion = version, action = "SET_VALUE", valueText = value, sourcePageId = page, sourceBbox = Region };

    private static JsonElement Resolved(JsonElement body) => body.GetProperty("field");

    [Fact]
    public async Task Fields_come_lowest_confidence_first_with_the_counter_of_what_remains()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer, VisitSummary, PageType.VisitSummary);

        var response = await reviewer.GetAsync($"claims/{claim}/fields", Cancel);

        var list = await JsonOf(response);
        Assert.Equal(1, list.GetProperty("remainingToVerify").GetInt32());
        var keys = list.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("fieldKey").GetString()).ToList();
        Assert.Equal(["diagnosis", "policy_number", "service_date"], keys.Take(3));

        var diagnosis = list.GetProperty("items")[0];
        Assert.Equal(file.DocumentId.ToString(), diagnosis.GetProperty("documentId").GetString());
        Assert.Equal("NEEDS_ENTRY", Status(diagnosis));
        Assert.True(diagnosis.GetProperty("required").GetBoolean());
        Assert.Equal("TEXT", diagnosis.GetProperty("valueKind").GetString());
        Assert.True(diagnosis.GetProperty("aiMarker").GetBoolean());
        Assert.Equal(JsonValueKind.Null, diagnosis.GetProperty("valueText").ValueKind);

        // Below 0.70 the value stays in the database and is never shown.
        Assert.Equal("MISSING", diagnosis.GetProperty("ai").GetProperty("level").GetString());
        Assert.Equal(JsonValueKind.Null, diagnosis.GetProperty("ai").GetProperty("value").ValueKind);
        Assert.Equal(0.58m, diagnosis.GetProperty("ai").GetProperty("confidence").GetDecimal());
        Assert.Equal("J02.9", await Slot(api, file.DocumentId, "ai_value", "diagnosis"));

        // A CHECK value is shown as a suggestion, not used.
        var policy = list.GetProperty("items")[1];
        Assert.Equal("NEEDS_VERIFICATION", Status(policy));
        Assert.False(policy.GetProperty("required").GetBoolean());
        Assert.Equal("PT-1", policy.GetProperty("ai").GetProperty("value").GetString());
        Assert.Equal(JsonValueKind.Null, policy.GetProperty("valueText").ValueKind);

        var date = list.GetProperty("items")[2];
        Assert.Equal("AUTO_ACCEPTED", Status(date));
        Assert.Equal("DATE", date.GetProperty("valueKind").GetString());
        Assert.Equal("2026-10-12", date.GetProperty("valueDate").GetString());
        Assert.Equal(file.Page.ToString(), date.GetProperty("sourcePageId").GetString());
        Assert.Equal(0.1m, date.GetProperty("sourceBbox").GetProperty("x").GetDecimal());
    }

    [Fact]
    public async Task Each_kind_of_field_names_its_kind_and_a_slot_with_no_AI_reading_has_no_AI_marker()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer);
        var line = await api.Scalar<Guid>($"select id from expense_line where claim_id = '{claim}'");

        var fields = await FieldsAsync(reviewer, claim);

        Assert.Equal("MONEY", FieldOf(fields, "invoice_total").GetProperty("valueKind").GetString());
        Assert.Equal(300_000, FieldOf(fields, "invoice_total").GetProperty("valueAmount").GetInt64());
        Assert.Equal("NUMBER", FieldOf(fields, "line_quantity", 1).GetProperty("valueKind").GetString());
        Assert.Equal(line.ToString(), FieldOf(fields, "line_amount", 1).GetProperty("expenseLineId").GetString());
        var taxCode = FieldOf(fields, "provider_tax_code");
        Assert.False(taxCode.GetProperty("required").GetBoolean());
        Assert.False(taxCode.GetProperty("aiMarker").GetBoolean());
        Assert.Equal(JsonValueKind.Null, taxCode.GetProperty("ai").ValueKind);
    }

    [Fact]
    public async Task The_list_can_be_narrowed_by_document_and_status_and_the_counter_still_covers_the_claim()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer);

        var waiting = await reviewer.GetAsync($"claims/{claim}/fields?status=NEEDS_ENTRY&status=AUTO_ACCEPTED&documentId={file.DocumentId}", Cancel);
        var none = await FieldsAsync(reviewer, claim, $"?documentId={Guid.NewGuid()}");
        var bad = await reviewer.GetAsync($"claims/{claim}/fields?status=WRONG", Cancel);

        var list = await JsonOf(waiting);
        Assert.Equal(1, list.GetProperty("remainingToVerify").GetInt32());
        Assert.NotEmpty(list.GetProperty("items").EnumerateArray());
        Assert.All(list.GetProperty("items").EnumerateArray(), item => Assert.Contains(Status(item), new[] { "NEEDS_ENTRY", "AUTO_ACCEPTED" }));
        Assert.Empty(none);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);
    }

    [Fact]
    public async Task Accepting_replaces_the_AI_marker_with_the_reviewer_stores_old_and_new_and_moves_the_claim_in_two_steps()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer);
        var provider = await FieldOf(reviewer, claim, "provider_name");

        var response = await AcceptAsync(reviewer, claim, provider);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonOf(response);
        var field = Resolved(body);
        Assert.Equal("CONFIRMED", Status(field));
        Assert.Equal("Phòng khám Đa khoa Hồng Ngọc", field.GetProperty("valueText").GetString());
        Assert.False(field.GetProperty("aiMarker").GetBoolean());
        Assert.Equal(Staff.ReviewerId, field.GetProperty("resolvedBy").GetProperty("id").GetString());
        Assert.NotEqual(JsonValueKind.Null, field.GetProperty("resolvedAt").ValueKind);
        Assert.Equal(Version(provider) + 1, Version(field));

        var progress = body.GetProperty("progress");
        Assert.Equal("IN_REVIEW", progress.GetProperty("status").GetString());
        Assert.Equal(0, progress.GetProperty("fieldsToVerify").GetInt32());
        Assert.Equal(0, progress.GetProperty("blockingAlerts").GetInt32());
        Assert.Equal(0, progress.GetProperty("warningAlerts").GetInt32());
        Assert.Equal(0, progress.GetProperty("mappingsToConfirm").GetInt32());
        Assert.Equal(JsonValueKind.Null, progress.GetProperty("currentRevisionNo").ValueKind);
        Assert.Equal(JsonValueKind.Null, progress.GetProperty("payableAmount").ValueKind);
        Assert.False(progress.GetProperty("isRevisionStale").GetBoolean());
        Assert.False(progress.GetProperty("revisionCreated").GetBoolean());
        Assert.Equal("IN_REVIEW", await ClaimStatus(api, claim));

        // One event for the write, and one for each step of the move, in order.
        Assert.Equal(1, await EventsAsync(api, claim, "FIELD_CONFIRMED"));
        Assert.Equal(1, await api.Scalar<long>(
            $"""
            select count(*) from audit_event
            where event_type = 'FIELD_CONFIRMED' and entity_id = '{Id(provider)}' and actor_type = 'USER' and actor_id = '{Staff.ReviewerId}'
              and before_value->>'status' = 'NEEDS_VERIFICATION' and before_value->>'value' is null
              and after_value->>'status' = 'CONFIRMED' and after_value->>'value' = 'Phòng khám Đa khoa Hồng Ngọc'
            """));
        var moves = await api.Column<string>(
            $"""
            select concat(before_value->>'status', '>', after_value->>'status') from audit_event
            where claim_id = '{claim}' and event_type = 'CLAIM_STATUS_CHANGED' and before_value->>'status' not in ('DRAFT', 'PROCESSING') order by id
            """);
        Assert.Equal(["NEEDS_VERIFICATION>READY_FOR_REVIEW", "READY_FOR_REVIEW>IN_REVIEW"], moves);
    }

    [Fact]
    public async Task A_value_with_nothing_from_AI_cannot_be_accepted()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer, VisitSummary, PageType.VisitSummary);
        var invoice = await TakenAsync(api, reviewer);

        var hidden = await AcceptAsync(reviewer, claim, await FieldOf(reviewer, claim, "diagnosis"));
        var absent = await AcceptAsync(reviewer, invoice.Claim, await FieldOf(reviewer, invoice.Claim, "provider_tax_code"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, hidden.StatusCode);
        Assert.Equal("FIELD_HAS_NO_AI_VALUE", await Code(hidden));
        Assert.Equal("FIELD_HAS_NO_AI_VALUE", await Code(absent));
    }

    [Fact]
    public async Task An_AI_value_with_no_source_needs_a_drawn_one_to_be_accepted()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer, WithoutSource);
        var provider = await FieldOf(reviewer, claim, "provider_name");

        var refused = await AcceptAsync(reviewer, claim, provider);
        var accepted = await AcceptAsync(reviewer, claim, provider, new { rowVersion = Version(provider), sourcePageId = file.Page, sourceBbox = Region });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal("SOURCE_REQUIRED", await Code(refused));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var field = Resolved(await JsonOf(accepted));
        Assert.Equal(file.Page.ToString(), field.GetProperty("sourcePageId").GetString());
        Assert.Equal(0.5m, field.GetProperty("sourceBbox").GetProperty("x").GetDecimal());
    }

    [Fact]
    public async Task Changing_an_AI_value_is_a_correction_that_keeps_the_AI_source()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer);
        var provider = await FieldOf(reviewer, claim, "provider_name");

        var response = await PatchAsync(reviewer, claim, provider, new { rowVersion = Version(provider), action = "SET_VALUE", valueText = "  Bệnh viện Hồng Ngọc " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var field = Resolved(await JsonOf(response));
        Assert.Equal("CORRECTED", Status(field));
        Assert.Equal("Bệnh viện Hồng Ngọc", field.GetProperty("valueText").GetString());
        Assert.Equal(file.Page.ToString(), field.GetProperty("sourcePageId").GetString());
        Assert.Equal("Phòng khám Đa khoa Hồng Ngọc", field.GetProperty("ai").GetProperty("value").GetString());
        Assert.False(field.GetProperty("aiMarker").GetBoolean());
        Assert.Equal(1, await api.Scalar<long>(
            $"""
            select count(*) from audit_event
            where event_type = 'FIELD_CORRECTED' and entity_id = '{Id(provider)}' and actor_id = '{Staff.ReviewerId}'
              and before_value->>'status' = 'NEEDS_VERIFICATION' and after_value->>'value' = 'Bệnh viện Hồng Ngọc'
            """));
    }

    [Fact]
    public async Task Sending_the_AI_value_back_unchanged_confirms_it()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer);
        var provider = await FieldOf(reviewer, claim, "provider_name");

        var response = await PatchAsync(reviewer, claim, provider, new { rowVersion = Version(provider), action = "SET_VALUE", valueText = "Phòng khám Đa khoa Hồng Ngọc" });

        Assert.Equal("CONFIRMED", Status(Resolved(await JsonOf(response))));
    }

    [Fact]
    public async Task A_value_where_AI_gave_none_is_entered_and_needs_a_drawn_source()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer, page => InvoiceToCheck(page).Where(field => field.FieldKey != FieldKey.ProviderName).ToArray());
        var provider = await FieldOf(reviewer, claim, "provider_name");
        Assert.Equal("NEEDS_ENTRY", Status(provider));

        var refused = await PatchAsync(reviewer, claim, provider, new { rowVersion = Version(provider), action = "SET_VALUE", valueText = "Hồng Ngọc" });
        var entered = await PatchAsync(reviewer, claim, provider, Drawn(Version(provider), file.Page, "Hồng Ngọc"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal("SOURCE_REQUIRED", await Code(refused));
        Assert.Equal(HttpStatusCode.OK, entered.StatusCode);
        var body = await JsonOf(entered);
        Assert.Equal("ENTERED", Status(Resolved(body)));
        Assert.Equal(file.Page.ToString(), Resolved(body).GetProperty("sourcePageId").GetString());
        Assert.Equal("IN_REVIEW", body.GetProperty("progress").GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_value_below_the_floor_counts_as_none_from_AI_and_is_entered()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer, VisitSummary, PageType.VisitSummary);
        var diagnosis = await FieldOf(reviewer, claim, "diagnosis");

        var response = await PatchAsync(reviewer, claim, diagnosis, Drawn(Version(diagnosis), file.Page, "J02.9"));

        var field = Resolved(await JsonOf(response));
        Assert.Equal("ENTERED", Status(field));

        // A MISSING suggestion stays hidden after the reviewer's entry.
        Assert.Equal(JsonValueKind.Null, field.GetProperty("ai").GetProperty("value").ValueKind);
    }

    [Fact]
    public async Task Not_present_is_its_own_action_with_no_value_and_no_source()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer, CleanInvoice);
        var total = await FieldOf(reviewer, claim, "invoice_total");

        var response = await PatchAsync(reviewer, claim, total, new { rowVersion = Version(total), action = "NOT_PRESENT" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var field = Resolved(await JsonOf(response));
        Assert.Equal("NOT_PRESENT", Status(field));
        Assert.Equal(JsonValueKind.Null, field.GetProperty("valueText").ValueKind);
        Assert.Equal(JsonValueKind.Null, field.GetProperty("valueAmount").ValueKind);
        Assert.Equal(JsonValueKind.Null, field.GetProperty("sourcePageId").ValueKind);
        Assert.False(field.GetProperty("aiMarker").GetBoolean());
        Assert.Equal(1, await EventsAsync(api, claim, "FIELD_NOT_PRESENT"));
    }

    [Fact]
    public async Task A_value_goes_in_by_the_kind_of_its_field()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer);
        var fields = await FieldsAsync(reviewer, claim);
        var total = FieldOf(fields, "invoice_total");
        var date = FieldOf(fields, "service_date");

        var money = Resolved(await JsonOf(await PatchAsync(reviewer, claim, total, new { rowVersion = Version(total), action = "SET_VALUE", valueAmount = 450_000 })));
        var day = Resolved(await JsonOf(await PatchAsync(reviewer, claim, date, new { rowVersion = Version(date), action = "SET_VALUE", valueDate = "2026-10-13" })));

        Assert.Equal(("CORRECTED", 450_000L), (Status(money), money.GetProperty("valueAmount").GetInt64()));
        Assert.Equal("450000", money.GetProperty("valueText").GetString());
        Assert.Equal(("CORRECTED", "2026-10-13"), (Status(day), day.GetProperty("valueDate").GetString()));
        Assert.Equal("2026-10-13", day.GetProperty("valueText").GetString());
    }

    public static TheoryData<string, string> WrongValues => new()
    {
        { "invoice_total", """{"action":"SET_VALUE","valueText":"300.000"}""" },
        { "invoice_total", """{"action":"SET_VALUE","valueAmount":-1}""" },
        { "service_date", """{"action":"SET_VALUE","valueText":"12/10/2026"}""" },
        { "provider_name", """{"action":"SET_VALUE"}""" },
        { "provider_name", """{"action":"SET_VALUE","valueText":"   "}""" },
        { "provider_name", """{"action":"WRONG","valueText":"x"}""" },
        { "provider_name", """{"valueText":"x"}""" },
        { "provider_name", """{"action":"SET_VALUE","valueText":"x","sourceBbox":{"x":0.1,"y":0.1,"w":0.1,"h":0.1}}""" },
    };

    [Theory]
    [MemberData(nameof(WrongValues))]
    public async Task A_value_that_does_not_fit_is_refused(string key, string request)
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer);
        var field = await FieldOf(reviewer, claim, key);
        var body = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request)!;
        body["rowVersion"] = JsonSerializer.SerializeToElement(Version(field));

        var response = await PatchAsync(reviewer, claim, field, body);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", await Code(response));
        Assert.Equal(Status(field), Status(await FieldOf(reviewer, claim, key)));
    }

    [Fact]
    public async Task A_quantity_must_read_as_a_number()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer);
        var quantity = await FieldOf(reviewer, claim, "line_quantity", 1);

        var wrong = await PatchAsync(reviewer, claim, quantity, Drawn(Version(quantity), file.Page, "many"));
        var right = await PatchAsync(reviewer, claim, quantity, Drawn(Version(quantity), file.Page, "2,5"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        Assert.Equal(2.5m, await api.Scalar<decimal>($"select quantity from expense_line where claim_id = '{claim}'"));
    }

    public static TheoryData<string> WrongRegions => new()
    {
        """{"x":-0.1,"y":0.1,"w":0.1,"h":0.1}""",
        """{"x":0.1,"y":0.1,"w":0,"h":0.1}""",
        """{"x":0.9,"y":0.1,"w":0.2,"h":0.1}""",
        """{"x":0.1,"y":0.95,"w":0.1,"h":0.1}""",
    };

    [Theory]
    [MemberData(nameof(WrongRegions))]
    public async Task A_region_must_lie_on_the_page(string region)
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer);
        var provider = await FieldOf(reviewer, claim, "provider_name");

        var response = await PatchAsync(reviewer, claim, provider, new
        {
            rowVersion = Version(provider),
            action = "SET_VALUE",
            valueText = "x",
            sourcePageId = file.Page,
            sourceBbox = JsonSerializer.Deserialize<JsonElement>(region),
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", await Code(response));
    }

    [Fact]
    public async Task A_source_must_be_a_page_of_the_fields_own_document()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer);
        var (_, elsewhere) = await TakenAsync(api, reviewer);
        var provider = await FieldOf(reviewer, claim, "provider_name");

        var edit = await PatchAsync(reviewer, claim, provider, Drawn(Version(provider), elsewhere.Page, "x"));
        var accept = await AcceptAsync(reviewer, claim, provider, new { rowVersion = Version(provider), sourcePageId = elsewhere.Page, sourceBbox = Region });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, edit.StatusCode);
        Assert.Equal("VALIDATION_FAILED", await Code(edit));
        Assert.Equal("VALIDATION_FAILED", await Code(accept));
    }

    [Fact]
    public async Task A_stale_row_version_answers_409_with_the_current_values_and_changes_nothing()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer);
        var provider = await FieldOf(reviewer, claim, "provider_name");
        await PatchAsync(reviewer, claim, provider, new { rowVersion = Version(provider), action = "SET_VALUE", valueText = "Giá trị mới" });

        var edit = await PatchAsync(reviewer, claim, provider, new { rowVersion = Version(provider), action = "SET_VALUE", valueText = "Giá trị cũ" });
        var accept = await AcceptAsync(reviewer, claim, provider);

        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal("VERSION_CONFLICT", await Code(edit));
        var details = (await JsonOf(edit)).GetProperty("details");
        Assert.Equal(Version(provider) + 1, details.GetProperty("currentRowVersion").GetInt32());
        Assert.Equal("Giá trị mới", details.GetProperty("current").GetProperty("valueText").GetString());
        Assert.Equal("CORRECTED", details.GetProperty("current").GetProperty("status").GetString());
        Assert.Equal("VERSION_CONFLICT", await Code(accept));
        Assert.Equal(1, await EventsAsync(api, claim, "FIELD_CORRECTED"));
    }

    [Fact]
    public async Task A_field_of_another_claim_is_not_found_through_this_claim()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer);
        var (other, _) = await TakenAsync(api, reviewer);
        var field = await FieldOf(reviewer, other, "provider_name");

        var edit = await PatchAsync(reviewer, claim, field, new { rowVersion = Version(field), action = "NOT_PRESENT" });
        var accept = await AcceptAsync(reviewer, claim, field);

        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, accept.StatusCode);
        Assert.Equal("NEEDS_VERIFICATION", Status(await FieldOf(reviewer, other, "provider_name")));
    }

    [Fact]
    public async Task Line_fields_write_the_expense_line_in_the_same_change()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer, page =>
            InvoiceToCheck(page)
                .Select(field => field.FieldKey == FieldKey.LineDescription ? field with { Confidence = 0.80m } : field)
                .ToArray());
        Assert.Null(await api.Scalar<string>($"select description from expense_line where claim_id = '{claim}'"));
        var fields = await FieldsAsync(reviewer, claim);
        var amount = FieldOf(fields, "line_amount", 1);

        await AcceptAsync(reviewer, claim, FieldOf(fields, "line_description", 1));
        await PatchAsync(reviewer, claim, amount, new { rowVersion = Version(amount), action = "SET_VALUE", valueAmount = 280_000 });

        Assert.Equal("Khám bệnh", await api.Scalar<string>($"select description from expense_line where claim_id = '{claim}'"));
        Assert.Equal(280_000, await api.Scalar<long>($"select amount from expense_line where claim_id = '{claim}'"));

        await PatchAsync(reviewer, claim, amount, new { rowVersion = Version(amount) + 1, action = "NOT_PRESENT" });

        Assert.Null(await api.Scalar<long?>($"select amount from expense_line where claim_id = '{claim}'"));
    }

    [Fact]
    public async Task A_national_id_is_masked_in_every_answer_and_in_the_audit_trail()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var approver = await api.SignedInAs(Staff.Approver);
        var (claim, file) = await TakenAsync(api, reviewer, VisitSummary, PageType.VisitSummary);

        var listed = await reviewer.GetAsync($"claims/{claim}/fields", Cancel);
        var asApprover = await approver.GetAsync($"claims/{claim}/fields", Cancel);
        Assert.DoesNotContain(FullId, await listed.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        Assert.DoesNotContain(FullId, await asApprover.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        var field = FieldOf((await JsonOf(listed)).GetProperty("items").EnumerateArray(), "national_id");
        Assert.Equal(MaskedId, field.GetProperty("valueText").GetString());
        Assert.Equal(MaskedId, field.GetProperty("ai").GetProperty("value").GetString());
        Assert.Equal("TEXT", field.GetProperty("valueKind").GetString());

        // The masked text sent back must not overwrite the real number.
        var maskedBack = await PatchAsync(reviewer, claim, field, new { rowVersion = Version(field), action = "SET_VALUE", valueText = MaskedId });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, maskedBack.StatusCode);
        Assert.Equal(FullId, await Slot(api, file.DocumentId, "value_text", "national_id"));

        const string Typed = "098765432109";
        var edit = await PatchAsync(reviewer, claim, field, new { rowVersion = Version(field), action = "SET_VALUE", valueText = Typed });
        var stale = await PatchAsync(reviewer, claim, field, new { rowVersion = Version(field), action = "SET_VALUE", valueText = Typed });

        foreach (var answer in new[] { edit, stale })
        {
            var text = await answer.Content.ReadAsStringAsync(Cancel);
            Assert.DoesNotContain(Typed, text, StringComparison.Ordinal);
            Assert.DoesNotContain(FullId, text, StringComparison.Ordinal);
        }

        Assert.Equal("********2109", Resolved(await JsonOf(edit)).GetProperty("valueText").GetString());
        Assert.Equal(Typed, await Slot(api, file.DocumentId, "value_text", "national_id"));
        Assert.Equal(0, await api.Scalar<long>(
            $"""
            select count(*) from audit_event
            where claim_id = '{claim}' and (before_value::text like '%{FullId}%' or after_value::text like '%{FullId}%'
                                         or before_value::text like '%{Typed}%' or after_value::text like '%{Typed}%')
            """));
    }

    [Fact]
    public async Task The_last_required_field_moves_the_claim_on_and_an_optional_one_does_not_hold_it_back()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer, page =>
            InvoiceToCheck(page).Where(field => field.FieldKey != FieldKey.LineDescription).ToArray());
        var fields = await FieldsAsync(reviewer, claim);

        var first = (await JsonOf(await AcceptAsync(reviewer, claim, FieldOf(fields, "provider_name")))).GetProperty("progress");

        Assert.Equal(("NEEDS_VERIFICATION", 1), (first.GetProperty("status").GetString(), first.GetProperty("fieldsToVerify").GetInt32()));
        var description = FieldOf(fields, "line_description", 1);
        var last = (await JsonOf(await PatchAsync(reviewer, claim, description, Drawn(Version(description), file.Page, "Khám bệnh")))).GetProperty("progress");
        Assert.Equal(("IN_REVIEW", 0), (last.GetProperty("status").GetString(), last.GetProperty("fieldsToVerify").GetInt32()));
    }

    [Fact]
    public async Task An_edit_in_review_keeps_the_claim_in_review_and_writes_no_status_event()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, _) = await TakenAsync(api, reviewer, CleanInvoice);
        var moves = await EventsAsync(api, claim, "CLAIM_STATUS_CHANGED");
        var provider = await FieldOf(reviewer, claim, "provider_name");

        var body = await JsonOf(await PatchAsync(reviewer, claim, provider, new { rowVersion = Version(provider), action = "SET_VALUE", valueText = "x" }));

        Assert.Equal("IN_REVIEW", body.GetProperty("progress").GetProperty("status").GetString());
        Assert.Equal(moves, await EventsAsync(api, claim, "CLAIM_STATUS_CHANGED"));
    }
}
