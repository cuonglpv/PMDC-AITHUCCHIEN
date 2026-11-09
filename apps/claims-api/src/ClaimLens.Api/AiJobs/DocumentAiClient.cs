using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.AiJobs;

/// <summary>
/// The Document AI Service (docs/api/document-ai.yaml). Only the AI Job Runner calls it; an architecture test keeps
/// every other folder away from it. Tests replace it with a fake.
/// </summary>
public interface IDocumentAiClient
{
    /// <summary>runPageCheck. Throws <see cref="DocumentAiException"/> when the call fails.</summary>
    Task<DocumentAiAnswer<PageCheckResponse>> RunPageCheckAsync(PageCheckRequest request, CancellationToken cancel);

    /// <summary>runFieldExtraction. Throws <see cref="DocumentAiException"/> when the call fails.</summary>
    Task<DocumentAiAnswer<FieldExtractionResponse>> RunFieldExtractionAsync(FieldExtractionRequest request, CancellationToken cancel);

    /// <summary>runPageReread. Throws <see cref="DocumentAiException"/> when the call fails.</summary>
    Task<DocumentAiAnswer<PageRereadResponse>> RunPageRereadAsync(PageRereadRequest request, CancellationToken cancel);
}

/// <summary>
/// A call that failed. <see cref="Code"/> becomes ai_run.error_code. A transient failure (the service or its provider
/// failed, timed out or could not be reached, or the answer broke the contract) is tried again; any other one, such as
/// a request the service refused as invalid, fails the run at once.
/// </summary>
public sealed class DocumentAiException(string code, bool transient, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public const string Timeout = "TIMEOUT";
    public const string Unreachable = "SERVICE_UNREACHABLE";
    public const string BadResponse = "BAD_RESPONSE";

    public string Code { get; } = code;

    public bool Transient { get; } = transient;
}

public sealed partial class DocumentAiClient(HttpClient http, IOptions<AiOptions> options) : IDocumentAiClient
{
    public const string ServiceKeyHeader = "X-Service-Key";

    public Task<DocumentAiAnswer<PageCheckResponse>> RunPageCheckAsync(PageCheckRequest request, CancellationToken cancel) =>
        PostAsync<PageCheckRequest, PageCheckResponse>("tasks/page-check", request, options.Value.PageCheck.TimeoutSeconds, cancel);

    public Task<DocumentAiAnswer<FieldExtractionResponse>> RunFieldExtractionAsync(FieldExtractionRequest request, CancellationToken cancel) =>
        PostAsync<FieldExtractionRequest, FieldExtractionResponse>(
            "tasks/field-extraction",
            request,
            options.Value.FieldExtraction.TimeoutSeconds,
            cancel);

    public Task<DocumentAiAnswer<PageRereadResponse>> RunPageRereadAsync(PageRereadRequest request, CancellationToken cancel) =>
        PostAsync<PageRereadRequest, PageRereadResponse>("tasks/page-reread", request, options.Value.PageReread.TimeoutSeconds, cancel);

    private async Task<DocumentAiAnswer<TResponse>> PostAsync<TRequest, TResponse>(
        string path,
        TRequest request,
        int timeoutSeconds,
        CancellationToken cancel)
        where TResponse : class
    {
        // Calls are synchronous and the runner holds them open, so each task has its own limit (document-ai.yaml).
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        using var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent(request),
        };
        message.Headers.Add(ServiceKeyHeader, options.Value.ServiceKey);

        HttpStatusCode status;
        byte[] body;
        try
        {
            using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            status = response.StatusCode;
            body = await response.Content.ReadAsByteArrayAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            throw new DocumentAiException(DocumentAiException.Timeout, transient: true, $"No answer within {timeoutSeconds} s.");
        }
        catch (HttpRequestException error)
        {
            throw new DocumentAiException(DocumentAiException.Unreachable, transient: true, "The Document AI Service could not be reached.", error);
        }

        if ((int)status is < 200 or > 299)
        {
            throw Failure(status, body);
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<TResponse>(body, DocumentAiJson.Options)
                ?? throw new JsonException("The body is null.");
            return new DocumentAiAnswer<TResponse>(parsed, body);
        }
        catch (JsonException error)
        {
            throw new DocumentAiException(DocumentAiException.BadResponse, transient: true, "The answer does not match the contract.", error);
        }
    }

    private static ByteArrayContent JsonContent<T>(T value)
    {
        var content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(value, DocumentAiJson.Options));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        return content;
    }

    /// <summary>
    /// 500, 502 and 504 are the service or its provider failing, and may pass. 401 and 422 are this API's mistake (a
    /// wrong key, a request off the contract), so trying again would only fail again.
    /// </summary>
    private static DocumentAiException Failure(HttpStatusCode status, byte[] body)
    {
        var code = ProblemCode(body) ?? (int)status switch
        {
            401 => "SERVICE_KEY_INVALID",
            422 => "INVALID_REQUEST",
            502 => "MODEL_ERROR",
            504 => "MODEL_TIMEOUT",
            >= 500 => "SERVICE_ERROR",
            _ => $"HTTP_{(int)status}",
        };
        var transient = (int)status >= 500 || status == HttpStatusCode.TooManyRequests;
        return new DocumentAiException(code, transient, $"The Document AI Service answered {(int)status} {code}.");
    }

    /// <summary>The problem body's `code`, when it is one this API can store as error_code (varchar(60)).</summary>
    private static string? ProblemCode(byte[] body)
    {
        try
        {
            using var problem = JsonDocument.Parse(body);
            return problem.RootElement.ValueKind == JsonValueKind.Object
                   && problem.RootElement.TryGetProperty("code", out var code)
                   && code.ValueKind == JsonValueKind.String
                   && ErrorCode().IsMatch(code.GetString()!)
                ? code.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_]{0,59}$")]
    private static partial Regex ErrorCode();
}
