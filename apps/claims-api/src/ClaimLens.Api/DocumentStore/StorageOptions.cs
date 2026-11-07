using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.DocumentStore;

/// <summary>The `Storage` section: one S3 bucket with one prefix per kind of file (docs/10-code-structure.md, section 8).</summary>
public sealed class StorageOptions
{
    public string ServiceUrl { get; set; } = "";
    public string Bucket { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Region { get; set; } = "us-east-1";

    /// <summary>Local and test only: create the bucket when the API starts. A real bucket is made by whoever hosts it.</summary>
    public bool CreateBucket { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}

public sealed partial class StorageOptionsValidator : IValidateOptions<StorageOptions>
{
    public ValidateOptionsResult Validate(string? name, StorageOptions options)
    {
        var failures = new List<string>();

        if (!Uri.TryCreate(options.ServiceUrl, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
        {
            failures.Add("Storage:ServiceUrl must be an http or https URL.");
        }

        if (!BucketName().IsMatch(options.Bucket ?? ""))
        {
            failures.Add("Storage:Bucket must be a bucket name: 3 to 63 lowercase letters, digits, dots or hyphens.");
        }

        if (string.IsNullOrWhiteSpace(options.AccessKey) || string.IsNullOrWhiteSpace(options.SecretKey))
        {
            failures.Add("Storage:AccessKey and Storage:SecretKey must be set.");
        }

        if (string.IsNullOrWhiteSpace(options.Region))
        {
            failures.Add("Storage:Region must be set.");
        }

        if (options.TimeoutSeconds is < 1 or > 600)
        {
            failures.Add("Storage:TimeoutSeconds must be between 1 and 600.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex BucketName();
}
