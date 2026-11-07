using System.Collections.Concurrent;

namespace ClaimLens.Api;

/// <summary>
/// Failures of a background item that were not the item's own fault, such as a service or storage being down. A failed
/// item waits, doubling each time, while the worker goes on with the items behind it; after MaxAttempts the worker
/// gives up on it.
/// <para>
/// Kept in memory: the MVP runs one API instance, and a restart only lets an item start counting again. Several
/// instances would each count on their own, which needs a column instead.
/// </para>
/// </summary>
public abstract class FailedAttempts(Func<WorkerSettings> settings, TimeProvider clock)
{
    /// <summary>However many times it failed, an item is tried again within this.</summary>
    private static readonly TimeSpan LongestWait = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<Guid, (int Failures, DateTimeOffset RetryAt)> failed = new();

    /// <summary>The items still waiting out a failure, which the next pass skips.</summary>
    public Guid[] Waiting()
    {
        var now = clock.GetUtcNow();
        return failed.Where(entry => entry.Value.RetryAt > now).Select(entry => entry.Key).ToArray();
    }

    /// <summary>Records a failure. True when the item has now used all its attempts and the worker gives up on it.</summary>
    public bool Failed(Guid id)
    {
        var current = settings();
        var failures = failed.TryGetValue(id, out var known) ? known.Failures + 1 : 1;
        if (failures >= current.MaxAttempts)
        {
            failed.TryRemove(id, out _);
            return true;
        }

        var wait = TimeSpan.FromSeconds(current.RetryDelaySeconds * Math.Pow(2, failures - 1));
        failed[id] = (failures, clock.GetUtcNow() + (wait < LongestWait ? wait : LongestWait));
        return false;
    }

    /// <summary>The item was done or given up, so its failures no longer matter.</summary>
    public void Done(Guid id) => failed.TryRemove(id, out _);
}
