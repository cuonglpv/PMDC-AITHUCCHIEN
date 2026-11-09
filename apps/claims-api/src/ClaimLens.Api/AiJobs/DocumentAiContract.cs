using System.Text.Json;
using System.Text.Json.Serialization;
using ClaimLens.Api.Domain.Enums;

namespace ClaimLens.Api.AiJobs;

// Requests and responses of docs/api/document-ai.yaml, written by hand. Only the tasks the runner calls are here; the
// others arrive with the slices that queue them.

public sealed record ModelRef(string Name, string Version);

public sealed record PageRef(Guid PageId, string ImageKey);

public sealed record PageCheckRequest(Guid RunId, ModelRef Model, IReadOnlyList<PageRef> Pages);

/// <summary>`pageType` is never UNCLASSIFIED: only the Claims API marks a page Unclassified.</summary>
public sealed record PageCheckResult(Guid PageId, PageType PageType, decimal TypeConfidence, decimal QualityScore);

/// <summary>A page that could not be read. The rest of the run still counts.</summary>
public sealed record PageFailure(Guid PageId, string Code);

public sealed record PageCheckResponse(
    Guid RunId,
    ModelRef Model,
    IReadOnlyList<PageCheckResult> Pages,
    IReadOnlyList<PageFailure> FailedPages);

/// <summary>`pageType` is the type in use, null for an Unclassified page.</summary>
public sealed record TypedPageRef(Guid PageId, string ImageKey, PageType? PageType);

public sealed record ExtractionDocument(Guid DocumentId, IReadOnlyList<TypedPageRef> Pages);

public sealed record FieldExtractionRequest(
    Guid RunId,
    ModelRef Model,
    IReadOnlyList<ExtractionDocument> Documents,
    IReadOnlyList<FieldKey> FieldCatalogue);

/// <summary>A region of a page, normalised to the page size.</summary>
public sealed record Bbox(decimal X, decimal Y, decimal W, decimal H);

/// <summary>`value`, `pageId` and `bbox` are null when the field was not found. Item 0 is the header.</summary>
public sealed record FieldSuggestion(FieldKey FieldKey, int ItemNo, string? Value, decimal Confidence, Guid? PageId, Bbox? Bbox);

public sealed record ExtractionDocumentResult(Guid DocumentId, IReadOnlyList<FieldSuggestion> Fields);

public sealed record FieldExtractionResponse(
    Guid RunId,
    ModelRef Model,
    IReadOnlyList<ExtractionDocumentResult> Documents,
    IReadOnlyList<PageFailure> FailedPages);

public sealed record PageRereadRequest(
    Guid RunId,
    ModelRef Model,
    Guid DocumentId,
    TypedPageRef Page,
    IReadOnlyList<FieldKey> FieldCatalogue);

/// <summary>The same suggestions as field extraction, for one page.</summary>
public sealed record PageRereadResponse(Guid RunId, ModelRef Model, Guid DocumentId, IReadOnlyList<FieldSuggestion> Fields);

/// <summary>An answer with the bytes it arrived as, which the runner keeps in object storage (ai_run.raw_output_key).</summary>
public sealed record DocumentAiAnswer<T>(T Body, byte[] Raw);

/// <summary>The JSON rules of the contract: camelCase names, enums by name only, required properties really required.</summary>
public static class DocumentAiJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        };
        json.Converters.Add(new JsonStringEnumConverter<FieldKey>(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
        json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false));
        json.MakeReadOnly(populateMissingResolver: true);
        return json;
    }
}
