using Microsoft.Extensions.Options;

namespace ClaimLens.Api;

/// <summary>
/// The `Workers` section: each background worker can be switched off, so tests drive it directly
/// (docs/10-code-structure.md, section 4). The AI Job Runner joins this section when it is built.
/// </summary>
public sealed class WorkersOptions
{
    public WorkerSettings DocumentPreparation { get; set; } = new();
}

public sealed class WorkerSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>How long an idle worker waits before it looks again. A new upload wakes it sooner.</summary>
    public double PollIntervalSeconds { get; set; } = 2;

    public int ConcurrentRuns { get; set; } = 1;

    /// <summary>
    /// Document Preparation only: how many times a file is tried when something other than the file fails, such as the
    /// converter or storage being down, before the file is rejected.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>The wait after the first such failure; it doubles after each one, up to 15 minutes.</summary>
    public double RetryDelaySeconds { get; set; } = 10;
}

public sealed class WorkersOptionsValidator : IValidateOptions<WorkersOptions>
{
    public ValidateOptionsResult Validate(string? name, WorkersOptions options)
    {
        var failures = new List<string>();
        Check("DocumentPreparation", options.DocumentPreparation, failures);
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void Check(string worker, WorkerSettings? settings, List<string> failures)
    {
        if (settings is null)
        {
            failures.Add($"Workers:{worker} must be set.");
            return;
        }

        if (settings.PollIntervalSeconds is < 0.05 or > 3600)
        {
            failures.Add($"Workers:{worker}:PollIntervalSeconds must be between 0.05 and 3600.");
        }

        if (settings.ConcurrentRuns is < 1 or > 16)
        {
            failures.Add($"Workers:{worker}:ConcurrentRuns must be between 1 and 16.");
        }

        if (settings.MaxAttempts is < 1 or > 50)
        {
            failures.Add($"Workers:{worker}:MaxAttempts must be between 1 and 50.");
        }

        if (settings.RetryDelaySeconds is < 0 or > 3600)
        {
            failures.Add($"Workers:{worker}:RetryDelaySeconds must be between 0 and 3600.");
        }
    }
}
