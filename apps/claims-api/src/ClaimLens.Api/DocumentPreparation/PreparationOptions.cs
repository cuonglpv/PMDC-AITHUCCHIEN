using Microsoft.Extensions.Options;

namespace ClaimLens.Api.DocumentPreparation;

/// <summary>The `Converter` section: Gotenberg, which runs LibreOffice in its own container (S11).</summary>
public sealed class ConverterOptions
{
    public string BaseUrl { get; set; } = "";

    /// <summary>Longer than Gotenberg's own --api-timeout, so Gotenberg reports a slow file instead of the API giving up.</summary>
    public int TimeoutSeconds { get; set; } = 90;
}

/// <summary>The `Limits` section (US03). The database repeats both: ck_document_size_20mb and ck_page_limit_30.</summary>
public sealed class LimitsOptions
{
    public const long DatabaseMaxFileBytes = 20 * 1024 * 1024;
    public const int DatabaseMaxPagesPerClaim = 30;

    public long MaxFileBytes { get; set; } = DatabaseMaxFileBytes;
    public int MaxPagesPerClaim { get; set; } = DatabaseMaxPagesPerClaim;
}

public sealed class ConverterOptionsValidator : IValidateOptions<ConverterOptions>
{
    public ValidateOptionsResult Validate(string? name, ConverterOptions options)
    {
        var failures = new List<string>();

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
        {
            failures.Add("Converter:BaseUrl must be an http or https URL.");
        }

        if (options.TimeoutSeconds is < 1 or > 600)
        {
            failures.Add("Converter:TimeoutSeconds must be between 1 and 600.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

public sealed class LimitsOptionsValidator : IValidateOptions<LimitsOptions>
{
    public ValidateOptionsResult Validate(string? name, LimitsOptions options)
    {
        var failures = new List<string>();

        if (options.MaxFileBytes is < 1 or > LimitsOptions.DatabaseMaxFileBytes)
        {
            failures.Add($"Limits:MaxFileBytes must be between 1 and {LimitsOptions.DatabaseMaxFileBytes}, which the database allows.");
        }

        if (options.MaxPagesPerClaim is < 1 or > LimitsOptions.DatabaseMaxPagesPerClaim)
        {
            failures.Add($"Limits:MaxPagesPerClaim must be between 1 and {LimitsOptions.DatabaseMaxPagesPerClaim}, which the database allows.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
