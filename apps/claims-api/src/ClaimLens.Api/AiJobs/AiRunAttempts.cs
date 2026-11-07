using Microsoft.Extensions.Options;

namespace ClaimLens.Api.AiJobs;

/// <summary>
/// Failed calls of an ai_run that may pass: the service or its provider failed, timed out or could not be reached. The
/// run goes back to QUEUED and waits while the runner goes on with other runs; after Workers:AiJobs:MaxAttempts it is
/// FAILED and the claim follows the manual fallback.
/// </summary>
public sealed class AiRunAttempts(IOptions<WorkersOptions> options, TimeProvider clock)
    : FailedAttempts(() => options.Value.AiJobs, clock);
