using WaterCoolerApp.Services;
using Xunit;

namespace WaterCoolerApp.Tests;

public sealed class DiagnosticMessageThrottleTests
{
    [Fact]
    public void ShouldWrite_SuppressesIdenticalMessageWithinInterval()
    {
        var throttle = new DiagnosticMessageThrottle(TimeSpan.FromMinutes(1), maxTrackedMessages: 8);
        var now = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(throttle.ShouldWrite("device missing", now));
        Assert.False(throttle.ShouldWrite("device missing", now.AddSeconds(59)));
    }

    [Fact]
    public void ShouldWrite_AllowsIdenticalMessageAfterInterval()
    {
        var throttle = new DiagnosticMessageThrottle(TimeSpan.FromMinutes(1), maxTrackedMessages: 8);
        var now = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(throttle.ShouldWrite("device missing", now));
        Assert.True(throttle.ShouldWrite("device missing", now.AddMinutes(1)));
    }

    [Fact]
    public void ShouldWrite_TracksDifferentMessagesIndependently()
    {
        var throttle = new DiagnosticMessageThrottle(TimeSpan.FromMinutes(1), maxTrackedMessages: 8);
        var now = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(throttle.ShouldWrite("device missing", now));
        Assert.True(throttle.ShouldWrite("write failed", now.AddSeconds(1)));
        Assert.False(throttle.ShouldWrite("device missing", now.AddSeconds(2)));
    }

    [Fact]
    public void ShouldWrite_ClearsCacheWhenCapacityIsReached()
    {
        var throttle = new DiagnosticMessageThrottle(TimeSpan.FromMinutes(1), maxTrackedMessages: 2);
        var now = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(throttle.ShouldWrite("first", now));
        Assert.True(throttle.ShouldWrite("second", now));
        Assert.True(throttle.ShouldWrite("third", now.AddSeconds(1)));

        // The bounded cache was cleared when the third distinct key arrived.
        Assert.True(throttle.ShouldWrite("first", now.AddSeconds(2)));
    }

    [Fact]
    public void Constructor_RejectsInvalidLimits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DiagnosticMessageThrottle(TimeSpan.Zero, maxTrackedMessages: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DiagnosticMessageThrottle(TimeSpan.FromMinutes(1), maxTrackedMessages: 0));
    }
}
