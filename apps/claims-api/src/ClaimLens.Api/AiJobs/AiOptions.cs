using Microsoft.Extensions.Options;

namespace ClaimLens.Api.AiJobs;

/// <summary>
/// The `Ai` section. For now only the pipeline each queued task names, which is what an ai_run row records. The base
/// URL, service key and timeouts arrive with the AI Job Runner (docs/10-code-structure.md, section 4).
/// </summary>
public sealed class AiOptions
{
    public PipelineOptions PageCheck { get; set; } = new();
    public PipelineOptions FieldExtraction { get; set; } = new();
}

/// <summary>`model` names a pipeline, such as field-extractor v2.4 (S10).</summary>
public sealed class PipelineOptions
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
}

public sealed class AiOptionsValidator : IValidateOptions<AiOptions>
{
    // ai_run.model_name is varchar(80) and model_version varchar(40).
    private const int NameLength = 80;
    private const int VersionLength = 40;

    public ValidateOptionsResult Validate(string? name, AiOptions options)
    {
        var failures = new List<string>();
        Check("PageCheck", options.PageCheck, failures);
        Check("FieldExtraction", options.FieldExtraction, failures);
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
    }
}
