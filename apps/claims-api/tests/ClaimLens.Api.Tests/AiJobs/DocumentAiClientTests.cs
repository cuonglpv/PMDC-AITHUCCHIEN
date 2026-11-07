using System.Net;
using System.Text;
using ClaimLens.Api.AiJobs;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.Tests.AiJobs;

/// <summary>The real client over a stub handler: what it sends, and how each kind of failure is classed.</summary>
public class DocumentAiClientTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static readonly PageCheckRequest Request =
        new(Guid.CreateVersion7(), new ModelRef("page-classifier", "v2.4"), [new PageRef(Guid.CreateVersion7(), "pages/a/b/1.png")]);

    [Fact]
    public async Task A_call_sends_the_service_key_to_the_tasks_path_and_keeps_the_raw_answer()
    {
        var body = $$"""
            {"runId":"{{Request.RunId}}","model":{"name":"page-classifier","version":"v2.5"},
             "pages":[{"pageId":"{{Request.Pages[0].PageId}}","pageType":"VISIT_SUMMARY","typeConfidence":0.91,"qualityScore":0.8}],
             "failedPages":[]}
            """;
        var handler = new StubHandler(HttpStatusCode.OK, body);

        var answer = await Client(handler).RunPageCheckAsync(Request, Cancel);

        Assert.Equal("http://document-ai.test/v1/tasks/page-check", handler.Seen!.RequestUri!.ToString());
        Assert.Equal("test-key", handler.Seen.Headers.GetValues(DocumentAiClient.ServiceKeyHeader).Single());
        Assert.Contains("\"imageKey\":\"pages/a/b/1.png\"", handler.SentBody, StringComparison.Ordinal);
        Assert.Equal(Domain.Enums.PageType.VisitSummary, answer.Body.Pages.Single().PageType);
        Assert.Equal(0.91m, answer.Body.Pages.Single().TypeConfidence);
        Assert.Equal(body, Encoding.UTF8.GetString(answer.Raw));
    }

    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity, "INVALID_REQUEST", false)]
    [InlineData(HttpStatusCode.Unauthorized, "SERVICE_KEY_INVALID", false)]
    [InlineData(HttpStatusCode.BadGateway, "BAD_MODEL_OUTPUT", true)]
    [InlineData(HttpStatusCode.GatewayTimeout, "MODEL_TIMEOUT", true)]
    [InlineData(HttpStatusCode.InternalServerError, "SERVICE_ERROR", true)]
    public async Task An_error_answer_carries_its_code_and_only_service_failures_are_tried_again(HttpStatusCode status, string code, bool transient)
    {
        var handler = new StubHandler(status, $$"""{"title":"x","status":{{(int)status}},"code":"{{code}}"}""", "application/problem+json");

        var error = await Assert.ThrowsAsync<DocumentAiException>(() => Client(handler).RunPageCheckAsync(Request, Cancel));

        Assert.Equal((code, transient), (error.Code, error.Transient));
    }

    [Theory]
    [InlineData("""{"runId":"00000000-0000-0000-0000-000000000000"}""")]
    [InlineData("""{"runId":"00000000-0000-0000-0000-000000000000","model":{"name":"a","version":"b"},"pages":[{"pageId":"00000000-0000-0000-0000-000000000000","pageType":"BILL","typeConfidence":1,"qualityScore":1}],"failedPages":[]}""")]
    [InlineData("not json")]
    public async Task An_answer_off_the_contract_is_a_bad_response_that_may_pass(string body)
    {
        var error = await Assert.ThrowsAsync<DocumentAiException>(() => Client(new StubHandler(HttpStatusCode.OK, body)).RunPageCheckAsync(Request, Cancel));

        Assert.Equal((DocumentAiException.BadResponse, true), (error.Code, error.Transient));
    }

    [Fact]
    public async Task No_answer_within_the_tasks_timeout_is_a_timeout_and_an_unreachable_service_is_unreachable()
    {
        var slow = await Assert.ThrowsAsync<DocumentAiException>(() => Client(new StubHandler(delay: TimeSpan.FromSeconds(5))).RunPageCheckAsync(Request, Cancel));
        var down = await Assert.ThrowsAsync<DocumentAiException>(() => Client(new StubHandler(refuse: true)).RunPageCheckAsync(Request, Cancel));

        Assert.Equal((DocumentAiException.Timeout, true), (slow.Code, slow.Transient));
        Assert.Equal((DocumentAiException.Unreachable, true), (down.Code, down.Transient));
    }

    private static DocumentAiClient Client(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("http://document-ai.test/v1/") },
            Options.Create(new AiOptions
            {
                BaseUrl = "http://document-ai.test",
                ServiceKey = "test-key",
                PageCheck = new PipelineOptions { Name = "page-classifier", Version = "v2.4", TimeoutSeconds = 1 },
            }));

    private sealed class StubHandler(
        HttpStatusCode status = HttpStatusCode.OK,
        string body = "{}",
        string mediaType = "application/json",
        TimeSpan? delay = null,
        bool refuse = false) : HttpMessageHandler
    {
        public HttpRequestMessage? Seen { get; private set; }

        public string SentBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen = request;
            SentBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (refuse)
            {
                throw new HttpRequestException("Connection refused (test)");
            }

            if (delay is { } wait)
            {
                await Task.Delay(wait, cancellationToken);
            }

            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) };
        }
    }
}
