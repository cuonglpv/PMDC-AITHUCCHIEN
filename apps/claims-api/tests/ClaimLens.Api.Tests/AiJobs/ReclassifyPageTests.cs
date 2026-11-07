using System.Net;
using System.Net.Http.Json;
using static ClaimLens.Api.Tests.AiJobs.AiData;
using static ClaimLens.Api.Tests.Features.IntakeData;

namespace ClaimLens.Api.Tests.AiJobs;

/// <summary>US04: the Intake Officer chooses a page's type before submission.</summary>
public sealed class ReclassifyPageTests(ApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_officer_sets_a_page_type_which_is_recorded_as_theirs_and_audited()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, file) = await DraftWithFileAsync(api, client);

        var response = await client.PutAsJsonAsync($"claims/{claim}/pages/{file.Page}/type", new { pageType = "VISIT_SUMMARY" }, Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await JsonOf(response);
        Assert.Equal("VISIT_SUMMARY", page.GetProperty("pageType").GetString());
        Assert.Equal("USER", page.GetProperty("pageTypeSource").GetString());
        Assert.Equal(Staff.IntakeId, page.GetProperty("pageTypeSetBy").GetProperty("id").GetString());
        Assert.Equal(1, await api.Scalar<long>(
            $"""
            select count(*) from audit_event
            where event_type = 'PAGE_RECLASSIFIED' and entity_id = '{file.Page}' and actor_type = 'USER' and actor_id = '{Staff.IntakeId}'
              and before_value->>'pageType' = 'UNCLASSIFIED' and after_value->>'pageType' = 'VISIT_SUMMARY'
              and request_id = '{RequestId(response)}'
            """));
    }

    [Theory]
    [InlineData("{\"pageType\":\"UNCLASSIFIED\"}")]
    [InlineData("{\"pageType\":\"BILL\"}")]
    [InlineData("{}")]
    [InlineData("not json")]
    public async Task Only_a_real_page_type_can_be_chosen(string body)
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, file) = await DraftWithFileAsync(api, client);

        var response = await client.PutAsync(
            $"claims/{claim}/pages/{file.Page}/type",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            Cancel);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", await Code(response));
        Assert.Null(await api.Scalar<string>($"select page_type_source::text from document_page where id = '{file.Page}'"));
    }

    [Fact]
    public async Task After_submission_the_officer_can_no_longer_choose()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, file) = await DraftWithFileAsync(api, client);
        await SubmitAsync(client, claim);

        var response = await client.PutAsJsonAsync($"claims/{claim}/pages/{file.Page}/type", new { pageType = "INVOICE" }, Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("CLAIM_STATE_CONFLICT", await Code(response));
    }

    [Fact]
    public async Task A_page_of_another_claim_is_not_found()
    {
        var client = await api.SignedInAs(Staff.Intake);
        var (claim, _) = await DraftWithFileAsync(api, client);
        var (_, elsewhere) = await DraftWithFileAsync(api, client);

        var response = await client.PutAsJsonAsync($"claims/{claim}/pages/{elsewhere.Page}/type", new { pageType = "INVOICE" }, Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(await api.Scalar<string>($"select page_type_source::text from document_page where id = '{elsewhere.Page}'"));
    }
}
