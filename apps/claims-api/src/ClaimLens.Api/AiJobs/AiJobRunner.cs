using Microsoft.Extensions.Options;

namespace ClaimLens.Api.AiJobs;

/// <summary>
/// Works through ai_run rows in QUEUED (docs/10-code-structure.md, section 4). Workers:AiJobs switches it off, which the
/// tests do so they can call <see cref="AiRunProcessor"/> themselves.
/// </summary>
public sealed class AiJobRunner(
    IServiceScopeFactory scopes,
    IOptions<WorkersOptions> options,
    ILogger<AiJobRunner> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value.AiJobs;
        if (!settings.Enabled)
        {
            logger.LogInformation("The AI Job Runner is switched off (Workers:AiJobs:Enabled)");
            return Task.CompletedTask;
        }

        var idle = TimeSpan.FromSeconds(settings.PollIntervalSeconds);
        return Task.WhenAll(Enumerable.Range(0, settings.ConcurrentRuns).Select(_ => RunAsync(idle, stoppingToken)));
    }

    private async Task RunAsync(TimeSpan idle, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            bool handled;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                handled = await scope.ServiceProvider.GetRequiredService<AiRunProcessor>().RunNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                // A run whose failure could not even be recorded stays RUNNING, and the stuck-run check fails it later.
                logger.LogError(error, "The AI Job Runner failed on a run; it goes on with the next");
                handled = false;
            }

            if (!handled)
            {
                try
                {
                    await Task.Delay(idle, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
