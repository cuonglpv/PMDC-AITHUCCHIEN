using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.DocumentPreparation;

/// <summary>
/// Failed preparation attempts that were not the file's fault, such as the converter or storage being down. A file that
/// failed waits, doubling each time, while the worker goes on with the files behind it; after
/// Workers:DocumentPreparation:MaxAttempts it is rejected so the officer can upload it again.
/// <para>
/// Kept in memory: the MVP runs one API instance, and a restart only lets a file start counting again. Several
/// instances would each count on their own, which needs a column on document instead.
/// </para>
/// </summary>
public sealed class PreparationAttempts(IOptions<WorkersOptions> options, TimeProvider clock)
{
    /// <summary>However many times it failed, a file is tried again within this.</summary>
    private static readonly TimeSpan LongestWait = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<Guid, (int Failures, DateTimeOffset RetryAt)> failed = new();

    private WorkerSettings Settings => options.Value.DocumentPreparation;

    /// <summary>The files still waiting out a failure, which the next pass skips.</summary>
    public Guid[] Waiting()
    {
        var now = clock.GetUtcNow();
        return failed.Where(entry => entry.Value.RetryAt > now).Select(entry => entry.Key).ToArray();
    }

    /// <summary>Records a failure. True when the file has now used all its attempts and is to be rejected.</summary>
    public bool Failed(Guid documentId)
    {
        var failures = failed.TryGetValue(documentId, out var known) ? known.Failures + 1 : 1;
        if (failures >= Settings.MaxAttempts)
        {
            failed.TryRemove(documentId, out _);
            return true;
        }

        var wait = TimeSpan.FromSeconds(Settings.RetryDelaySeconds * Math.Pow(2, failures - 1));
        failed[documentId] = (failures, clock.GetUtcNow() + (wait < LongestWait ? wait : LongestWait));
        return false;
    }

    /// <summary>The file was prepared or rejected, so its failures no longer matter.</summary>
    public void Done(Guid documentId) => failed.TryRemove(documentId, out _);
}
