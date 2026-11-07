using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.AccessControl;

/// <summary>
/// Counts failed sign-ins per email and per address, in memory. With several API instances each counts on its own,
/// which is enough for the MVP. Ended windows are swept out at most once a minute, so emails nobody uses again do not
/// stay in memory: what is held is bounded by the failures of one window.
/// </summary>
public sealed class SignInThrottle(IOptions<AuthOptions> options, TimeProvider clock)
{
    private static readonly TimeSpan SweepEvery = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, Window> windows = new(StringComparer.Ordinal);
    private long lastSweepTicks = clock.GetUtcNow().UtcTicks;

    private readonly record struct Window(int Failures, DateTimeOffset StartedAt);

    /// <summary>Seconds to wait when this attempt must be refused, or null when it may go ahead.</summary>
    public int? RetryAfter(string email, string address)
    {
        var settings = options.Value;
        return Longest(
            Check($"email:{email}", settings.SignInFailuresPerEmail),
            Check($"address:{address}", settings.SignInFailuresPerAddress));
    }

    public void RecordFailure(string email, string address)
    {
        SweepIfDue();
        Add($"email:{email}");
        Add($"address:{address}");
    }

    public void RecordSuccess(string email) => windows.TryRemove($"email:{email}", out _);

    /// <summary>Emails and addresses with a window in memory.</summary>
    public int TrackedWindows => windows.Count;

    private int? Check(string key, int limit)
    {
        if (!windows.TryGetValue(key, out var window) || window.Failures < limit)
        {
            return null;
        }

        var left = window.StartedAt + Length - clock.GetUtcNow();
        return left <= TimeSpan.Zero ? null : (int)Math.Ceiling(left.TotalSeconds);
    }

    private void Add(string key)
    {
        var now = clock.GetUtcNow();
        windows.AddOrUpdate(
            key,
            _ => new Window(1, now),
            (_, window) => now - window.StartedAt > Length ? new Window(1, now) : window with { Failures = window.Failures + 1 });
    }

    private void SweepIfDue()
    {
        var now = clock.GetUtcNow();
        var last = Interlocked.Read(ref lastSweepTicks);
        if (now.UtcTicks - last < SweepEvery.Ticks
            || Interlocked.CompareExchange(ref lastSweepTicks, now.UtcTicks, last) != last)
        {
            return;
        }

        foreach (var entry in windows)
        {
            if (now - entry.Value.StartedAt > Length)
            {
                // Removes only the window seen here, not one a concurrent failure has just started.
                windows.TryRemove(entry);
            }
        }
    }

    private TimeSpan Length => TimeSpan.FromMinutes(options.Value.SignInWindowMinutes);

    private static int? Longest(int? first, int? second) =>
        first is null ? second : second is null ? first : Math.Max(first.Value, second.Value);
}
