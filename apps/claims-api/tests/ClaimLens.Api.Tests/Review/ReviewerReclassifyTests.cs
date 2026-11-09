using System.Net;
using System.Net.Http.Json;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Tests.AiJobs;
using static ClaimLens.Api.Tests.AiJobs.AiData;
using static ClaimLens.Api.Tests.Features.IntakeData;
using static ClaimLens.Api.Tests.Review.ReviewData;

namespace ClaimLens.Api.Tests.Review;

/// <summary>US06: the reviewer chooses a page's type, which creates the empty fields of that type.</summary>
public sealed class ReviewerReclassifyTests(ApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>A claim whose one page stayed Unclassified, taken by this reviewer: NEEDS_VERIFICATION with no fields.</summary>
    private async Task<(string Claim, ReadyFile File)> UnclassifiedAsync(HttpClient reviewer)
    {
        var intake = await api.SignedInAs(Staff.Intake);
        var (claim, file) = await DraftWithFileAsync(api, intake);
        api.Ai.AnswerPage(file.Page, PageType.Receipt, 0.64m);
        await api.RunAiJobsAsync();
        await SubmitAsync(intake, claim);
        await api.RunAiJobsAsync();
        Assert.Equal("NEEDS_VERIFICATION", await ClaimStatus(api, claim));
        await StartAsync(reviewer, claim);
        return (claim, file);
    }

    private static Task<HttpResponseMessage> SetTypeAsync(HttpClient client, string claim, Guid page, string type) =>
        client.PutAsJsonAsync($"claims/{claim}/pages/{page}/type", new { pageType = type }, Cancel);

    [Fact]
    public async Task Choosing_a_type_records_it_as_theirs_and_creates_the_empty_fields_of_that_type()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await UnclassifiedAsync(reviewer);
        Assert.Empty(await FieldsAsync(reviewer, claim));

        var response = await SetTypeAsync(reviewer, claim, file.Page, "INVOICE");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await JsonOf(response);
        Assert.Equal("INVOICE", page.GetProperty("pageType").GetString());
        Assert.Equal("USER", page.GetProperty("pageTypeSource").GetString());
        Assert.Equal(Staff.ReviewerId, page.GetProperty("pageTypeSetBy").GetProperty("id").GetString());

        var fields = await FieldsAsync(reviewer, claim);
        Assert.Equal(
            ["insured_name", "invoice_number", "invoice_total", "provider_name", "provider_tax_code", "service_date"],
            fields.Select(field => field.GetProperty("fieldKey").GetString()).Order());
        Assert.All(fields, field =>
        {
            Assert.Equal("NEEDS_ENTRY", Status(field));
            Assert.Equal(file.DocumentId.ToString(), field.GetProperty("documentId").GetString());
            Assert.False(field.GetProperty("aiMarker").GetBoolean());
        });
        Assert.Equal("NEEDS_VERIFICATION", await ClaimStatus(api, claim));
        Assert.Equal(1, await EventsAsync(api, claim, "PAGE_RECLASSIFIED"));
    }

    [Fact]
    public async Task Fields_a_document_already_has_stay_as_they_are()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await TakenAsync(api, reviewer);
        var before = await FieldOf(reviewer, claim, "invoice_total");

        var response = await SetTypeAsync(reviewer, claim, file.Page, "VISIT_SUMMARY");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fields = await FieldsAsync(reviewer, claim);
        var total = FieldOf(fields, "invoice_total");
        Assert.Equal(Version(before), Version(total));
        Assert.Equal("AUTO_ACCEPTED", Status(total));
        Assert.Equal("NEEDS_ENTRY", Status(FieldOf(fields, "diagnosis")));

        // Only the fields of the type in use are required now: a visit summary needs its diagnosis, not an invoice total.
        Assert.True(FieldOf(fields, "diagnosis").GetProperty("required").GetBoolean());
        Assert.False(total.GetProperty("required").GetBoolean());
    }

    [Fact]
    public async Task A_type_with_no_fields_clears_the_last_page_and_moves_the_claim_on()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await UnclassifiedAsync(reviewer);

        var response = await SetTypeAsync(reviewer, claim, file.Page, "OTHER");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("IN_REVIEW", await ClaimStatus(api, claim));
        Assert.Empty(await FieldsAsync(reviewer, claim));
    }

    [Fact]
    public async Task The_same_choice_twice_writes_one_event_and_no_second_set_of_fields()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await UnclassifiedAsync(reviewer);

        await SetTypeAsync(reviewer, claim, file.Page, "RECEIPT");
        var fields = (await FieldsAsync(reviewer, claim)).Count;
        await SetTypeAsync(reviewer, claim, file.Page, "RECEIPT");

        Assert.Equal(fields, (await FieldsAsync(reviewer, claim)).Count);
        Assert.Equal(1, await EventsAsync(api, claim, "PAGE_RECLASSIFIED"));
    }

    [Fact]
    public async Task Unclassified_cannot_be_chosen_and_a_page_of_another_claim_is_not_found()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var (claim, file) = await UnclassifiedAsync(reviewer);
        var (_, elsewhere) = await UnclassifiedAsync(reviewer);

        var unclassified = await SetTypeAsync(reviewer, claim, file.Page, "UNCLASSIFIED");
        var foreign = await SetTypeAsync(reviewer, claim, elsewhere.Page, "INVOICE");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, unclassified.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task Only_the_assignee_can_choose()
    {
        var reviewer = await api.SignedInAs(Staff.Reviewer);
        var other = await api.SignedInAs(Staff.OtherReviewer);
        var (claim, file) = await UnclassifiedAsync(reviewer);

        var response = await SetTypeAsync(other, claim, file.Page, "INVOICE");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("CLAIM_OUT_OF_SCOPE", await Code(response));
    }
}
