using ClaimLens.Api.AccessControl;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.Tests.Features;

/// <summary>Failed sign-ins with made-up emails must not pile up in memory for the life of the process.</summary>
public class SignInThrottleTests
{
    [Fact]
    public void Windows_that_have_ended_are_dropped()
    {
        var clock = new TestClock();
        var throttle = new SignInThrottle(Options.Create(new AuthOptions()), clock);
        for (var attempt = 0; attempt < 1000; attempt++)
        {
            throttle.RecordFailure($"random-{attempt}@example.test", "203.0.113.9");
        }

        Assert.Equal(1001, throttle.TrackedWindows);

        clock.Advance(TimeSpan.FromMinutes(16));
        throttle.RecordFailure("one-more@example.test", "198.51.100.77");

        Assert.Equal(2, throttle.TrackedWindows);
    }

    [Fact]
    public void A_window_still_running_keeps_counting()
    {
        var clock = new TestClock();
        var throttle = new SignInThrottle(Options.Create(new AuthOptions { SignInFailuresPerEmail = 2 }), clock);
        throttle.RecordFailure("someone@example.test", "203.0.113.9");
        throttle.RecordFailure("someone@example.test", "203.0.113.9");

        clock.Advance(TimeSpan.FromMinutes(10));
        throttle.RecordFailure("other@example.test", "198.51.100.77");

        Assert.NotNull(throttle.RetryAfter("someone@example.test", "192.0.2.1"));
    }
}
