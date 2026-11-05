using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.AccessControl;

/// <summary>
/// Counts failed sign-ins per email and per address, in memory. With several API instances each counts on its own,
/// which is enough for the MVP.
/// </summary>
public sealed class SignInThrottle(IOptions<AuthOptions> options, TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, Window> windows = new(StringComparer.Ordinal);

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
        Add($"email:{email}");
        Add($"address:{address}");
    }

    public void RecordSuccess(string email) => windows.TryRemove($"email:{email}", out _);

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

    private TimeSpan Length => TimeSpan.FromMinutes(options.Value.SignInWindowMinutes);

    private static int? Longest(int? first, int? second) =>
        first is null ? second : second is null ? first : Math.Max(first.Value, second.Value);
}
