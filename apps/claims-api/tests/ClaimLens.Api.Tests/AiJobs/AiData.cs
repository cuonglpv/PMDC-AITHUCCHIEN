using System.Net;
using System.Text.Json;
using ClaimLens.Api.AiJobs;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Tests.Documents;
using static ClaimLens.Api.Tests.Features.IntakeData;

namespace ClaimLens.Api.Tests.AiJobs;

/// <summary>A file of a claim with its pages, in file order.</summary>
public sealed record ReadyFile(Guid DocumentId, IReadOnlyList<Guid> Pages)
{
    public Guid Page => Pages[0];
}

/// <summary>Claims with files ready and their AI runs, for the AI Job Runner tests.</summary>
public static class AiData
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public static readonly Bbox Box = new(0.1m, 0.2m, 0.3m, 0.04m);

    /// <summary>A draft with one file of this many pages, prepared, its page check still queued.</summary>
    public static async Task<(string Claim, ReadyFile File)> DraftWithFileAsync(ApiFactory api, HttpClient client, int pages = 1)
    {
        var claim = await NewDraftAsync(api, client);
        var file = await AddFileAsync(api, client, claim, pages);
        return (claim, file);
    }

    public static async Task<ReadyFile> AddFileAsync(ApiFactory api, HttpClient client, string claim, int pages = 1)
    {
        var document = await UploadedAsync(client, claim, TestFiles.Pdf(pages), "file.pdf");
        await api.PrepareDocumentsAsync();
        var ids = await api.Column<Guid>($"select id from document_page where document_id = '{document}' order by page_no");
        return new ReadyFile(Guid.Parse(document), ids);
    }

    /// <summary>A submitted claim whose files had their page checks answered with these types at 0.99.</summary>
    public static async Task<(string Claim, ReadyFile[] Files)> SubmittedAsync(ApiFactory api, HttpClient client, params PageType[] types)
    {
        var claim = await NewDraftAsync(api, client);
        var files = new List<ReadyFile>();
        foreach (var type in types)
        {
            var file = await AddFileAsync(api, client, claim);
            api.Ai.AnswerPage(file.Page, type, 0.99m);
            files.Add(file);
        }

        await api.RunAiJobsAsync();
        await SubmitAsync(client, claim);
        return (claim, files.ToArray());
    }

    public static async Task SubmitAsync(HttpClient client, string claim)
    {
        var response = await client.PostAsync($"claims/{claim}/submit", null, Cancel);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    public static FieldSuggestion Field(FieldKey key, string? value, decimal confidence, Guid page, int itemNo = 0) =>
        new(key, itemNo, value, confidence, value is null ? null : page, value is null ? null : Box);

    /// <summary>Every required invoice header field, auto-accepted, plus one line whose required fields are too.</summary>
    public static FieldSuggestion[] CleanInvoice(Guid page) =>
    [
        Field(FieldKey.InsuredName, "Nguyễn Văn An", 0.99m, page),
        Field(FieldKey.ProviderName, "Phòng khám Đa khoa Hồng Ngọc", 0.99m, page),
        Field(FieldKey.InvoiceNumber, "HN-0045821", 0.99m, page),
        Field(FieldKey.ServiceDate, "12/10/2026", 0.99m, page),
        Field(FieldKey.InvoiceTotal, "300.000", 0.99m, page),
        Field(FieldKey.LineDescription, "Khám bệnh", 0.99m, page, itemNo: 1),
        Field(FieldKey.LineAmount, "300.000", 0.99m, page, itemNo: 1),
    ];

    public static Task<string?> ClaimStatus(ApiFactory api, string claim) =>
        api.Scalar<string>($"select status::text from claim where id = '{claim}'");

    public static Task<string?> RunStatus(ApiFactory api, string claim, string task) =>
        api.Scalar<string>($"select status::text from ai_run where claim_id = '{claim}' and task = '{task}' order by requested_at desc limit 1");

    public static Task<string?> Slot(ApiFactory api, Guid document, string column, string fieldKey, int itemNo = 0) =>
        api.Scalar<string>($"select {column}::text from claim_field where document_id = '{document}' and field_key = '{fieldKey}' and item_no = {itemNo}");

    public static async Task<bool> FallbackActiveAsync(HttpClient client, string claim)
    {
        var status = await JsonOf(await client.GetAsync($"claims/{claim}/status", Cancel));
        var detail = await JsonOf(await client.GetAsync($"claims/{claim}", Cancel));
        var fromStatus = status.GetProperty("fallbackActive").GetBoolean();
        Assert.Equal(fromStatus, detail.GetProperty("ai").GetProperty("fallbackActive").GetBoolean());
        return fromStatus;
    }

    public static async Task<JsonElement> PageAsync(HttpClient client, string claim, Guid page) =>
        (await DocumentsAsync(client, claim))
            .SelectMany(document => document.GetProperty("pages").EnumerateArray())
            .Single(item => item.GetProperty("id").GetGuid() == page);
}
