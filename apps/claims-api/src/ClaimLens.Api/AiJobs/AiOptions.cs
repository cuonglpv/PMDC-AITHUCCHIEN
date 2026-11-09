using Microsoft.Extensions.Options;

namespace ClaimLens.Api.AiJobs;

/// <summary>
/// The `Ai` section: where the Document AI Service is, the key it expects, and per task the pipeline an ai_run names
/// and how long the AI Job Runner waits for an answer (docs/10-code-structure.md, section 4).
/// </summary>
public sealed class AiOptions
{
    /// <summary>The service's address without its /v1, such as http://document-ai:8001.</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Sent as X-Service-Key. A secret: it comes from the environment only.</summary>
    public string ServiceKey { get; set; } = "";

    public PipelineOptions PageCheck { get; set; } = new() { TimeoutSeconds = 30 };
    public PipelineOptions FieldExtraction { get; set; } = new() { TimeoutSeconds = 120 };
    public PipelineOptions PageReread { get; set; } = new() { TimeoutSeconds = 60 };

    public PipelineOptions For(Domain.Enums.AiTask task) =>
        task switch
        {
            Domain.Enums.AiTask.PageCheck => PageCheck,
            Domain.Enums.AiTask.FieldExtraction => FieldExtraction,
            Domain.Enums.AiTask.PageReread => PageReread,
            _ => throw new ArgumentOutOfRangeException(nameof(task), task, "This task is not built yet."),
        };
}

/// <summary>`model` names a pipeline, such as field-extractor v2.4 (S10).</summary>
public sealed class PipelineOptions
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";

    /// <summary>How long one call may take (document-ai.yaml). A run still RUNNING well after it is failed as TIMEOUT.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class AiOptionsValidator : IValidateOptions<AiOptions>
{
    // ai_run.model_name is varchar(80) and model_version varchar(40).
    private const int NameLength = 80;
    private const int VersionLength = 40;

    public ValidateOptionsResult Validate(string? name, AiOptions options)
    {
        var failures = new List<string>();

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
        {
            failures.Add("Ai:BaseUrl must be an http or https URL.");
        }

        if (string.IsNullOrWhiteSpace(options.ServiceKey))
        {
            failures.Add("Ai:ServiceKey must be set.");
        }

        Check("PageCheck", options.PageCheck, failures);
        Check("FieldExtraction", options.FieldExtraction, failures);
        Check("PageReread", options.PageReread, failures);
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void Check(string task, PipelineOptions? pipeline, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(pipeline?.Name) || pipeline.Name.Length > NameLength)
        {
            failures.Add($"Ai:{task}:Name must name a pipeline in at most {NameLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(pipeline?.Version) || pipeline.Version.Length > VersionLength)
        {
            failures.Add($"Ai:{task}:Version must be set in at most {VersionLength} characters.");
        }

        if (pipeline is null || pipeline.TimeoutSeconds is < 1 or > 600)
        {
            failures.Add($"Ai:{task}:TimeoutSeconds must be between 1 and 600.");
        }
    }
}
