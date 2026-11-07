using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClaimLens.Api.Tests.Documents;
using Npgsql;
using SkiaSharp;
using static ClaimLens.Api.Tests.Features.IntakeData;

namespace ClaimLens.Api.Tests.Features;

/// <summary>
/// US03 with the in-memory store and a fake converter; Document Preparation runs when a test calls it. The real
/// SeaweedFS and Gotenberg are in <see cref="Integration.DocumentPipelineTests"/>. Other tests may leave files waiting, so
/// a test never counts how many files one preparation pass handled. The clock is the real one, so "newest first" holds
/// against claims other tests inserted with the database's own now().
/// </summary>
public sealed partial class UploadAndSubmitTests : IDisposable
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly ApiFactory api;

    public UploadAndSubmitTests(ApiFactory api)
    {
        this.api = api;
    }

    public void Dispose()
    {
        api.Converter.Reset();
        api.Store.FailWrites = false;
    }

    // ---- Given JPG, PNG, PDF, DOCX or XLSX files of at most 20 MB and 30 pages per claim, each shows its pages and
    //      thumbnails; any other type or size is rejected with the reason.

    [Fact]
    public async Task A_photo_is_stored_then_drawn_as_one_page_with_a_thumbnail()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);

        var response = await UploadAsync(client, claim, TestFiles.Jpg(600, 300), "biên lai.jpg", "image/jpeg");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var uploaded = await JsonOf(response);
        var document = Id(uploaded);
        Assert.Equal("CONVERTING", uploaded.GetProperty("status").GetString());
        Assert.Equal("biên lai.jpg", uploaded.GetProperty("originalFilename").GetString());
        Assert.Equal("JPG", uploaded.GetProperty("fileFormat").GetString());
        Assert.Equal(JsonValueKind.Null, uploaded.GetProperty("pageCount").ValueKind);
        Assert.Equal(0, uploaded.GetProperty("pages").GetArrayLength());
        Assert.NotNull(api.Store[$"originals/{claim}/{document}.jpg"]);

        await api.PrepareDocumentsAsync();

        var ready = (await DocumentsAsync(client, claim)).Single();
        Assert.Equal("READY", ready.GetProperty("status").GetString());
        Assert.Equal(1, ready.GetProperty("pageCount").GetInt32());
        var page = ready.GetProperty("pages").EnumerateArray().Single();
        Assert.Equal(1, page.GetProperty("claimPageNo").GetInt32());
        Assert.Equal(600, page.GetProperty("widthPx").GetInt32());
        Assert.Equal(300, page.GetProperty("heightPx").GetInt32());
        Assert.Equal("UNCLASSIFIED", page.GetProperty("pageType").GetString());
        Assert.Equal("QUEUED", page.GetProperty("aiStatus").GetString());
        Assert.Equal(JsonValueKind.Null, page.GetProperty("ai").ValueKind);

        var full = await client.GetAsync($"claims/{claim}/pages/{Id(page)}/image", Cancel);
        var thumbnail = await client.GetAsync($"claims/{claim}/pages/{Id(page)}/image?variant=thumbnail", Cancel);
        Assert.Equal("image/png", full.Content.Headers.ContentType!.MediaType);
        Assert.True(full.Headers.CacheControl!.Private);
        Assert.True(full.Headers.CacheControl.NoStore, "A page image must not stay in the browser's cache after sign-out.");
        Assert.Equal((600, 300), Size(await full.Content.ReadAsByteArrayAsync(Cancel)));
        Assert.Equal((240, 120), Size(await thumbnail.Content.ReadAsByteArrayAsync(Cancel)));
    }

    [Fact]
    public async Task A_ready_file_queues_one_page_check_and_both_steps_are_audited()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);
        var document = await UploadedAsync(client, claim, TestFiles.Pdf(2), "hoa-don.pdf");

        await api.PrepareDocumentsAsync();

        Assert.Equal(1, await api.Scalar<long>(
            $"""
            select count(*) from ai_run
            where claim_id = '{claim}' and task = 'PAGE_CHECK' and status = 'QUEUED' and scope_document_id = '{document}'
              and model_name = 'page-classifier' and model_version = 'v2.4'
            """));
        Assert.Equal(2, await api.Scalar<long>(
            $"select count(*) from document_page p join ai_run r on r.id = p.ai_run_id where p.document_id = '{document}' and r.task = 'PAGE_CHECK'"));
        Assert.Equal(1, await api.Scalar<long>(
            $"select count(*) from audit_event where event_type = 'DOCUMENT_UPLOADED' and entity_id = '{document}' and actor_type = 'USER'"));
        Assert.Equal(1, await api.Scalar<long>(
            $"select count(*) from audit_event where event_type = 'DOCUMENT_READY' and entity_id = '{document}' and actor_type = 'SYSTEM' and actor_id is null"));
    }

    [Fact]
    public async Task A_photo_is_turned_upright_by_its_EXIF_orientation()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);

        // Stored 200 × 100 with red on the left; orientation 6 means "turn 90° clockwise to read".
        await UploadedAsync(client, claim, TestFiles.Jpg(200, 100, orientation: 6), "receipt.jpg");
        await api.PrepareDocumentsAsync();

        var page = (await DocumentsAsync(client, claim)).Single().GetProperty("pages")[0];
        Assert.Equal(100, page.GetProperty("widthPx").GetInt32());
        Assert.Equal(200, page.GetProperty("heightPx").GetInt32());

        using var image = SKBitmap.Decode(await (await client.GetAsync($"claims/{claim}/pages/{Id(page)}/image", Cancel)).Content.ReadAsByteArrayAsync(Cancel));
        Assert.True(IsRed(image.GetPixel(50, 50)), "The red half belongs on top once the photo is upright.");
        Assert.True(IsBlue(image.GetPixel(50, 150)), "The blue half belongs at the bottom once the photo is upright.");
    }

    [Fact]
    public async Task Pages_are_numbered_across_the_claim_in_upload_order()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);
        var pdf = await UploadedAsync(client, claim, TestFiles.Pdf(2), "invoice.pdf");
        var png = await UploadedAsync(client, claim, TestFiles.Png(), "receipt.png");

        await api.PrepareDocumentsAsync();

        Assert.Equal([(1, 1), (2, 2), (1, 3)], PageNumbers(await DocumentsAsync(client, claim), pdf, png));
    }

    [Theory]
    [InlineData("DOCX")]
    [InlineData("XLSX")]
    public async Task An_office_file_keeps_its_original_and_gets_a_PDF_rendition_whose_pages_are_its_pages(string format)
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);
        api.Converter.Pages = 2;
        var file = format == "DOCX"
            ? TestFiles.Docx("Đơn thuốc", null, "Trang hai")
            : TestFiles.Xlsx("Bảng kê", ["Dịch vụ", "Số tiền"], ["Khám bệnh", 350_000L]);

        var document = await UploadedAsync(client, claim, file, $"tep.{format.ToLowerInvariant()}");
        await api.PrepareDocumentsAsync();

        var ready = (await DocumentsAsync(client, claim)).Single();
        Assert.Equal(format, ready.GetProperty("fileFormat").GetString());
        Assert.Equal("READY", ready.GetProperty("status").GetString());
        Assert.Equal(2, ready.GetProperty("pages").GetArrayLength());
        Assert.Equal([format], api.Converter.Calls.Select(call => call.ToString().ToUpperInvariant()));
        Assert.NotNull(api.Store[$"originals/{claim}/{document}.{format.ToLowerInvariant()}"]);
        Assert.NotNull(api.Store[$"renditions/{claim}/{document}.pdf"]);
        Assert.Equal($"renditions/{claim}/{document}.pdf", await api.Scalar<string>($"select rendition_key from document where id = '{document}'"));
    }

    // ---- Given the conversion fails, the file is rejected with the reason.

    [Fact]
    public async Task A_file_the_converter_refuses_is_rejected_with_the_reason()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);
        api.Converter.FailWith = "The file could not be converted to PDF.";

        var document = await UploadedAsync(client, claim, TestFiles.Docx("Hỏng"), "hong.docx");
        await api.PrepareDocumentsAsync();

        var rejected = (await DocumentsAsync(client, claim)).Single();
        Assert.Equal("REJECTED", rejected.GetProperty("status").GetString());
        Assert.Equal("The file could not be converted to PDF.", rejected.GetProperty("rejectionReason").GetString());
        Assert.Equal(0, rejected.GetProperty("pages").GetArrayLength());
        Assert.Equal(0, await api.Scalar<long>($"select count(*) from ai_run where scope_document_id = '{document}'"));
        Assert.Equal(1, await api.Scalar<long>(
            $"select count(*) from audit_event where event_type = 'DOCUMENT_REJECTED' and entity_id = '{document}' and actor_type = 'SYSTEM'"));
    }

    [Fact]
    public async Task A_file_that_would_take_the_claim_past_30_pages_is_rejected_with_the_reason()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);
        await UploadedAsync(client, claim, TestFiles.Pdf(29), "a.pdf");
        await UploadedAsync(client, claim, TestFiles.Pdf(2), "b.pdf");
        await UploadedAsync(client, claim, TestFiles.Png(), "c.png");

        await api.PrepareDocumentsAsync();

        var documents = await DocumentsAsync(client, claim);
        Assert.Equal(["READY", "REJECTED", "READY"], documents.Select(document => document.GetProperty("status").GetString()));
        Assert.Contains("at most 30 pages", documents[1].GetProperty("rejectionReason").GetString(), StringComparison.Ordinal);
        Assert.Equal(30, documents.Sum(document => document.GetProperty("pages").GetArrayLength()));
    }

    [Theory]
    [InlineData(20 * 1024 * 1024 + 1)]
    [InlineData(21 * 1024 * 1024)]
    public async Task A_file_over_20_MB_is_refused_with_413_and_nothing_is_kept(int size)
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);
        var file = new byte[size];
        TestFiles.Jpg().AsSpan(0, 3).CopyTo(file);

        var response = await UploadAsync(client, claim, file, "big.jpg");

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("FILE_TOO_LARGE", await Code(response));
        await AssertNothingKept(claim);
    }

    [Theory]
    [InlineData("fake-docx")]
    [InlineData("zip")]
    [InlineData("office-of-neither")]
    [InlineData("gif")]
    [InlineData("empty")]
    public async Task Any_other_type_is_refused_by_its_bytes_with_415_and_nothing_is_kept(string kind)
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);
        var (file, name) = kind switch
        {
            "fake-docx" => (TestFiles.FakeDocx(), "don-thuoc.docx"),
            "zip" => (TestFiles.PlainZip(), "hoso.docx"),
            "office-of-neither" => (TestFiles.OfficePackageOfNeither(), "slides.xlsx"),
            "gif" => ("GIF89a"u8.ToArray(), "scan.jpg"),
            _ => (Array.Empty<byte>(), "empty.pdf"),
        };

        var response = await UploadAsync(client, claim, file, name);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("UNSUPPORTED_FILE_TYPE", await Code(response));
        await AssertNothingKept(claim);
    }

    [Fact]
    public async Task A_body_that_is_not_a_file_upload_is_refused_with_415()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);

        var response = await client.PostAsJsonAsync($"claims/{claim}/documents", new { file = "not a file" }, Cancel);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task The_type_comes_from_the_bytes_and_the_storage_keys_never_hold_the_file_name()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);

        var response = await UploadAsync(client, claim, TestFiles.Png(), "Nguyen Van An - hoa don.pdf", "application/pdf");
        await api.PrepareDocumentsAsync();

        Assert.Equal("PNG", (await JsonOf(response)).GetProperty("fileFormat").GetString());
        var keys = api.Store.Keys.Where(key => key.Contains(claim, StringComparison.Ordinal)).ToList();
        Assert.Equal(3, keys.Count);
        Assert.All(keys, key => Assert.Matches(StorageKey(), key));
        Assert.All(keys, key => Assert.DoesNotContain("Nguyen", key, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_failed_store_leaves_no_row_that_would_block_submission()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);
        api.Store.FailWrites = true;

        // INTERNAL_ERROR is not part of any one operation in the contract, so this call skips the contract check.
        var withoutContract = api.CreateClient(new() { BaseAddress = new Uri("https://localhost/api/v1/") });
        withoutContract.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
        var response = await UploadAsync(withoutContract, claim, TestFiles.Png(), "receipt.png");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(0, await api.Scalar<long>($"select count(*) from document where claim_id = '{claim}'"));
    }

    [Fact]
    public async Task A_page_is_found_only_through_its_own_claim()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var mine = await NewDraftAsync(api, client);
        var other = await NewDraftAsync(api, client);
        await UploadedAsync(client, other, TestFiles.Png(), "other.png");
        await api.PrepareDocumentsAsync();
        var page = Id((await DocumentsAsync(client, other)).Single().GetProperty("pages")[0]);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"claims/{mine}/pages/{page}/image", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"claims/{other}/pages/{page}/image", Cancel)).StatusCode);
    }

    // ---- Given an upload is running, Submit is disabled; given no upload is running, files can be removed.

    [Fact]
    public async Task Removing_a_file_deletes_its_pages_and_the_others_close_the_gap()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);
        var first = await UploadedAsync(client, claim, TestFiles.Pdf(2), "first.pdf");
        var second = await UploadedAsync(client, claim, TestFiles.Pdf(3), "second.pdf");
        var third = await UploadedAsync(client, claim, TestFiles.Png(), "third.png");
        await api.PrepareDocumentsAsync();
        var firstImages = api.Store.Keys.Where(key => key.Contains($"/{first}/", StringComparison.Ordinal)).ToList();
        Assert.Equal(4, firstImages.Count);

        var response = await client.DeleteAsync($"claims/{claim}/documents/{first}", Cancel);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var documents = await DocumentsAsync(client, claim);
        Assert.Equal([second, third], documents.Select(Id));
        Assert.Equal([(1, 1), (2, 2), (3, 3), (1, 4)], PageNumbers(documents, second, third));
        Assert.All(firstImages, key => Assert.Null(api.Store[key]));
        Assert.NotNull(api.Store[$"originals/{claim}/{first}.pdf"]);
        Assert.True(await api.Scalar<bool>(
            $"select status = 'REMOVED' and removed_by = '{Staff.IntakeId}' and removed_at is not null from document where id = '{first}'"));
        Assert.Equal(0, await api.Scalar<long>($"select count(*) from document_page where document_id = '{first}'"));

        // The next file takes the next number, and a removed file is gone for good.
        await UploadedAsync(client, claim, TestFiles.Png(), "fourth.png");
        await api.PrepareDocumentsAsync();
        Assert.Equal(5, await api.Scalar<short>($"select max(claim_page_no) from document_page where claim_id = '{claim}'"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"claims/{claim}/documents/{first}", Cancel)).StatusCode);
    }

    [Fact]
    public async Task A_file_waiting_for_conversion_can_be_removed_and_is_then_skipped()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);
        var document = await UploadedAsync(client, claim, TestFiles.Png(), "receipt.png");

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"claims/{claim}/documents/{document}", Cancel)).StatusCode);
        await api.PrepareDocumentsAsync();

        Assert.Empty(await DocumentsAsync(client, claim));
        Assert.Equal("REMOVED", await api.Scalar<string>($"select status::text from document where id = '{document}'"));
        Assert.Equal(0, await api.Scalar<long>($"select count(*) from document_page where document_id = '{document}'"));
    }

    [Fact]
    public async Task A_file_the_worker_is_converting_cannot_be_removed_until_it_is_done()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);
        var document = await UploadedAsync(client, claim, TestFiles.Png(), "receipt.png");

        // Hold the row the way the worker does while it converts.
        await using var worker = new NpgsqlConnection(api.OwnerConnection);
        await worker.OpenAsync(Cancel);
        await using var transaction = await worker.BeginTransactionAsync(Cancel);
        await using (var hold = new NpgsqlCommand($"select 1 from document where id = '{document}' for update", worker, transaction))
        {
            await hold.ExecuteNonQueryAsync(Cancel);
        }

        var response = await client.DeleteAsync($"claims/{claim}/documents/{document}", Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("UPLOAD_IN_PROGRESS", await Code(response));
        await transaction.RollbackAsync(Cancel);
    }

    [Fact]
    public async Task Submit_is_blocked_while_a_file_is_still_converting()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);
        await UploadedAsync(client, claim, TestFiles.Png(), "ready.png");
        await api.PrepareDocumentsAsync();
        await UploadedAsync(client, claim, TestFiles.Png(), "converting.png");

        var detail = await JsonOf(await client.GetAsync($"claims/{claim}", Cancel));
        Assert.Equal(["UPLOAD_IN_PROGRESS"], detail.GetProperty("blockers").EnumerateArray().Select(blocker => blocker.GetProperty("code").GetString()));
        Assert.DoesNotContain("SUBMIT_CLAIM", detail.GetProperty("allowedActions").EnumerateArray().Select(action => action.GetString()));

        var response = await client.PostAsync($"claims/{claim}/submit", null, Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("UPLOAD_IN_PROGRESS", await Code(response));
        Assert.Equal("DRAFT", await api.Scalar<string>($"select status::text from claim where id = '{claim}'"));
    }

    [Fact]
    public async Task Submit_needs_a_file_that_is_ready()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);

        var empty = await client.PostAsync($"claims/{claim}/submit", null, Cancel);
        api.Converter.FailWith = "The file could not be converted to PDF.";
        await UploadedAsync(client, claim, TestFiles.Docx("x"), "broken.docx");
        await api.PrepareDocumentsAsync();
        var onlyRejected = await client.PostAsync($"claims/{claim}/submit", null, Cancel);

        Assert.Equal("NO_READY_DOCUMENT", await Code(empty));
        Assert.Equal(HttpStatusCode.Conflict, onlyRejected.StatusCode);
        Assert.Equal("NO_READY_DOCUMENT", await Code(onlyRejected));
    }

    // ---- Given at least one document, Submit moves the claim to Processing and extraction starts.

    [Fact]
    public async Task Submitting_moves_the_claim_to_processing_and_queues_field_extraction()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);
        await UploadedAsync(client, claim, TestFiles.Pdf(1), "invoice.pdf");
        await api.PrepareDocumentsAsync();
        var detail = await JsonOf(await client.GetAsync($"claims/{claim}", Cancel));
        Assert.Contains("SUBMIT_CLAIM", detail.GetProperty("allowedActions").EnumerateArray().Select(action => action.GetString()));

        var response = await client.PostAsync($"claims/{claim}/submit", null, Cancel);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var snapshot = await JsonOf(response);
        Assert.Equal("PROCESSING", snapshot.GetProperty("status").GetString());
        Assert.Equal(1, snapshot.GetProperty("rowVersion").GetInt32());
        Assert.Contains(snapshot.GetProperty("aiRuns").EnumerateArray(), run =>
            run.GetProperty("task").GetString() == "FIELD_EXTRACTION" && run.GetProperty("status").GetString() == "QUEUED"
            && run.GetProperty("modelName").GetString() == "field-extractor");
        Assert.True(await api.Scalar<bool>($"select submitted_at is not null from claim where id = '{claim}'"));
        Assert.Equal(1, await api.Scalar<long>(
            $"""
            select count(*) from audit_event e join ai_run r on r.id = e.ai_run_id
            where e.event_type = 'CLAIM_STATUS_CHANGED' and e.claim_id = '{claim}' and r.task = 'FIELD_EXTRACTION'
              and e.before_value->>'status' = 'DRAFT' and e.after_value->>'status' = 'PROCESSING'
            """));

        var after = await JsonOf(await client.GetAsync($"claims/{claim}", Cancel));
        Assert.Empty(after.GetProperty("allowedActions").EnumerateArray());
        Assert.Equal("CLAIM_STATE_CONFLICT", await Code(await client.PostAsync($"claims/{claim}/submit", null, Cancel)));
        Assert.Equal("CLAIM_STATE_CONFLICT", await Code(await UploadAsync(client, claim, TestFiles.Png(), "late.png")));
    }

    [Fact]
    public async Task Polling_carries_no_file_names_and_is_never_cached()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);
        await UploadedAsync(client, claim, TestFiles.Png(), "Tran Thi B - receipt.png");
        await UploadedAsync(client, claim, TestFiles.Png(), "Tran Thi B - second.png");
        await api.PrepareDocumentsAsync();

        var response = await client.GetAsync($"claims/{claim}/status", Cancel);

        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.DoesNotContain("Tran Thi B", await response.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        var snapshot = await JsonOf(response);
        Assert.Equal(["READY", "READY"], snapshot.GetProperty("documents").EnumerateArray().Select(document => document.GetProperty("status").GetString()));
        Assert.Equal(2, snapshot.GetProperty("aiRuns").EnumerateArray().Count(run => run.GetProperty("task").GetString() == "PAGE_CHECK"));
        Assert.Equal(0, await api.Scalar<long>($"select count(*) from audit_event where request_id = '{RequestId(response)}'"));
    }

    // ---- Queues: the Intake Officer's two tabs.

    [Fact]
    public async Task My_drafts_and_my_submissions_hold_my_claims_newest_first_with_their_badges()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var older = await NewDraftAsync(api, client);
        var newer = await NewDraftAsync(api, client);
        var submittedFirst = await NewDraftAsync(api, client);
        var submitted = await NewDraftAsync(api, client);
        await UploadedAsync(client, submittedFirst, TestFiles.Png(), "r.png");
        await UploadedAsync(client, submitted, TestFiles.Png(), "r.png");
        await api.PrepareDocumentsAsync();
        (await client.PostAsync($"claims/{submittedFirst}/submit", null, Cancel)).EnsureSuccessStatusCode();
        (await client.PostAsync($"claims/{submitted}/submit", null, Cancel)).EnsureSuccessStatusCode();
        var someoneElses = await SomeoneElsesDraftAsync();

        var drafts = await JsonOf(await client.GetAsync("claims?view=MY_DRAFTS&pageSize=100", Cancel));
        var submissions = await JsonOf(await client.GetAsync("claims?view=MY_SUBMISSIONS&pageSize=100", Cancel));

        var draftIds = drafts.GetProperty("items").EnumerateArray().Select(Id).ToList();
        Assert.True(draftIds.IndexOf(newer) < draftIds.IndexOf(older) && draftIds.IndexOf(newer) >= 0);
        Assert.DoesNotContain(submitted, draftIds);
        Assert.DoesNotContain(someoneElses, draftIds);
        // Only this test's own claims are compared: rows other tests inserted carry the database's clock, not the API's.
        var submittedIds = submissions.GetProperty("items").EnumerateArray().Select(Id).ToList();
        Assert.True(submittedIds.IndexOf(submitted) >= 0 && submittedIds.IndexOf(submitted) < submittedIds.IndexOf(submittedFirst));

        var counts = drafts.GetProperty("counts");
        Assert.Equal(await Mine("status = 'DRAFT'"), counts.GetProperty("MY_DRAFTS").GetInt32());
        Assert.Equal(await Mine("status <> 'DRAFT'"), counts.GetProperty("MY_SUBMISSIONS").GetInt32());
        Assert.Equal(await Mine("status = 'DRAFT'"), drafts.GetProperty("total").GetInt32());

        var row = drafts.GetProperty("items")[draftIds.IndexOf(newer)];
        Assert.StartsWith("Lê Thị Thử", row.GetProperty("memberName").GetString(), StringComparison.Ordinal);
        Assert.Equal("GOLD-OP", row.GetProperty("planCode").GetString());
        Assert.Equal("NORMAL", row.GetProperty("priority").GetString());
    }

    [Fact]
    public async Task A_queue_can_be_filtered_by_part_of_a_claim_number_and_paged()
    {
        var client = await api.SignedInAs(Staff.Intake);
        await NewDraftAsync(api, client);
        var claim = await NewDraftAsync(api, client);
        var number = (await JsonOf(await client.GetAsync($"claims/{claim}", Cancel))).GetProperty("claimNumber").GetString()!;

        var found = await JsonOf(await client.GetAsync($"claims?view=MY_DRAFTS&claimNumber={number[^6..]}", Cancel));
        var onePerPage = await JsonOf(await client.GetAsync("claims?view=MY_DRAFTS&pageSize=1&page=2", Cancel));

        Assert.Equal([claim], found.GetProperty("items").EnumerateArray().Select(Id));
        Assert.Equal(1, onePerPage.GetProperty("items").GetArrayLength());
        Assert.Equal(2, onePerPage.GetProperty("page").GetInt32());
    }

    [Theory]
    [InlineData(Staff.Intake, "AVAILABLE", HttpStatusCode.Forbidden)]
    [InlineData(Staff.Reviewer, "MY_DRAFTS", HttpStatusCode.Forbidden)]
    [InlineData(Staff.Intake, "NOT_A_VIEW", HttpStatusCode.UnprocessableEntity)]
    [InlineData(Staff.Intake, "", HttpStatusCode.UnprocessableEntity)]
    public async Task A_view_must_be_one_of_the_callers_tabs(string email, string view, HttpStatusCode expected)
    {
        var client = await api.SignedInAs(email);

        var response = await client.GetAsync($"claims?view={view}", Cancel);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Each_intake_write_records_exactly_one_audit_event()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var member = await NewMemberAsync(api);

        var create = await CreateClaimAsync(client, member);
        var claim = Id((await JsonOf(create)).GetProperty("claim"));
        var update = await client.PatchAsJsonAsync($"claims/{claim}", new { rowVersion = 0, claimedTotal = 990_000 }, Cancel);
        var upload = await UploadAsync(client, claim, TestFiles.Png(), "a.png");
        var removed = await UploadAsync(client, claim, TestFiles.Png(), "b.png");
        await api.PrepareDocumentsAsync();
        var delete = await client.DeleteAsync($"claims/{claim}/documents/{Id(await JsonOf(removed))}", Cancel);
        var submit = await client.PostAsync($"claims/{claim}/submit", null, Cancel);

        foreach (var (response, eventType) in new[]
                 {
                     (create, "CLAIM_CREATED"), (update, "CLAIM_UPDATED"), (upload, "DOCUMENT_UPLOADED"),
                     (delete, "DOCUMENT_REMOVED"), (submit, "CLAIM_STATUS_CHANGED"),
                 })
        {
            Assert.True(response.IsSuccessStatusCode, eventType);
            Assert.Equal(eventType, await api.Scalar<string>($"select string_agg(event_type, ',') from audit_event where request_id = '{RequestId(response)}'"));
        }
    }

    [Fact]
    public async Task Each_audited_read_records_one_event()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);
        await UploadedAsync(client, claim, TestFiles.Png(), "a.png");
        await api.PrepareDocumentsAsync();
        var page = Id((await DocumentsAsync(client, claim)).Single().GetProperty("pages")[0]);

        var reads = new[]
        {
            await client.PostAsJsonAsync("members/search", new { query = "nguyen van an" }, Cancel),
            await client.GetAsync($"policies/{AnPolicyId}/summary", Cancel),
            await client.GetAsync("claims?view=MY_DRAFTS", Cancel),
            await client.GetAsync($"claims/{claim}", Cancel),
            await client.GetAsync($"claims/{claim}/documents", Cancel),
            await client.GetAsync($"claims/{claim}/pages/{page}/image?variant=thumbnail", Cancel),
        };

        foreach (var response in reads)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, await api.Scalar<long>($"select count(*) from audit_event where request_id = '{RequestId(response)}'"));
        }
    }

    private async Task AssertNothingKept(string claim)
    {
        Assert.Equal(0, await api.Scalar<long>($"select count(*) from document where claim_id = '{claim}'"));
        Assert.DoesNotContain(api.Store.Keys, key => key.Contains(claim, StringComparison.Ordinal));
    }

    private async Task<int> Mine(string condition) =>
        (int)(await api.Scalar<long>($"select count(*) from claim where registered_by = '{Staff.IntakeId}' and {condition}"));

    private async Task<string> SomeoneElsesDraftAsync()
    {
        var member = await NewMemberAsync(api);
        var id = Guid.CreateVersion7();
        await api.Sql(
            $"""
            insert into staff_account (id, email, password_hash, full_name, initials, role)
            values ('{Staff.OtherIntakeId}', 'intake2@claimlens.test', 'x', 'Other Intake', 'OI', 'INTAKE_OFFICER')
            on conflict do nothing;
            insert into claim (id, claim_number, member_id, policy_id, provider_name, service_date, claimed_total, channel, registered_by)
            values ('{id}', 'O-{id.ToString("N")[20..]}', '{member.MemberId}', '{member.PolicyId}', 'x', '2026-10-12', 1, 'COUNTER', '{Staff.OtherIntakeId}');
            """);
        return id.ToString();
    }

    private static List<(int PageNo, int ClaimPageNo)> PageNumbers(List<JsonElement> documents, params string[] ids) =>
        ids.SelectMany(id => documents.Single(document => Id(document) == id).GetProperty("pages").EnumerateArray())
            .Select(page => (page.GetProperty("pageNo").GetInt32(), page.GetProperty("claimPageNo").GetInt32()))
            .ToList();

    private static (int Width, int Height) Size(byte[] png)
    {
        using var bitmap = SKBitmap.Decode(png);
        return (bitmap.Width, bitmap.Height);
    }

    private static bool IsRed(SKColor colour) => colour is { Red: > 200, Green: < 60, Blue: < 60 };

    private static bool IsBlue(SKColor colour) => colour is { Red: < 60, Green: < 60, Blue: > 200 };

    [GeneratedRegex(@"^(originals/[0-9a-f-]{36}/[0-9a-f-]{36}\.(jpg|png|pdf|docx|xlsx)|renditions/[0-9a-f-]{36}/[0-9a-f-]{36}\.pdf|(pages|thumbnails)/[0-9a-f-]{36}/[0-9a-f-]{36}/\d+\.png)$")]
    private static partial Regex StorageKey();
}
