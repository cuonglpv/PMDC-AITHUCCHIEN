using Microsoft.Extensions.Options;

namespace ClaimLens.Api.DocumentPreparation;

/// <summary>Lets an upload wake the worker at once instead of after its poll interval.</summary>
public sealed class PreparationSignal
{
    private readonly SemaphoreSlim pending = new(0, int.MaxValue);

    public void Notify() => pending.Release();

    public async Task WaitAsync(TimeSpan timeout, CancellationToken cancel)
    {
        if (await pending.WaitAsync(timeout, cancel))
        {
            // One wake-up is enough for any number of uploads; the worker empties the queue before it waits again.
            while (pending.CurrentCount > 0 && pending.Wait(0))
            {
            }
        }
    }
}

/// <summary>
/// Works through documents in CONVERTING (docs/10-code-structure.md, section 4). Workers:DocumentPreparation switches it
/// off, which the tests do so they can call <see cref="DocumentPreparer"/> themselves.
/// </summary>
public sealed class DocumentPreparationWorker(
    IServiceScopeFactory scopes,
    PreparationSignal signal,
    IOptions<WorkersOptions> options,
    ILogger<DocumentPreparationWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value.DocumentPreparation;
        if (!settings.Enabled)
        {
            logger.LogInformation("Document preparation is switched off (Workers:DocumentPreparation:Enabled)");
            return Task.CompletedTask;
        }

        var idle = TimeSpan.FromSeconds(settings.PollIntervalSeconds);
        return Task.WhenAll(Enumerable.Range(0, settings.ConcurrentRuns).Select(_ => RunAsync(idle, stoppingToken)));
    }

    private async Task RunAsync(TimeSpan idle, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            bool prepared;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                prepared = await scope.ServiceProvider.GetRequiredService<DocumentPreparer>().PrepareNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                // The file stays CONVERTING and its lock is gone, so the next pass tries it again: storage or the
                // converter being down for a moment is not the file's fault.
                logger.LogWarning(error, "Document preparation failed; it will be tried again");
                prepared = false;
            }

            if (!prepared)
            {
                try
                {
                    await signal.WaitAsync(idle, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
