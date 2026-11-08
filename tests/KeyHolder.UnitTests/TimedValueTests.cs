using KeyHolder.Domain.Locking;

namespace KeyHolder.UnitTests;

public sealed class TimedValueTests
{
    [Fact]
    public void ValueIsAvailableUntilItExpires()
    {
        var clock = new FakeClock();
        var timed = new TimedValue<string>(clock);

        timed.Set("FAKE-secret", TimeSpan.FromSeconds(10));
        clock.Advance(TimeSpan.FromSeconds(9));

        Assert.True(timed.TryGet(out var value));
        Assert.Equal("FAKE-secret", value);
        Assert.True(timed.IsActive);
    }

    [Fact]
    public void ValueIsDroppedAtExpiry()
    {
        var clock = new FakeClock();
        var timed = new TimedValue<string>(clock);
        timed.Set("FAKE-secret", TimeSpan.FromSeconds(10));

        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.False(timed.TryGet(out _));
        Assert.False(timed.IsActive);
    }

    [Fact]
    public void ExpiredValueStaysGoneEvenIfTimeWentBackwards()
    {
        var clock = new FakeClock();
        var timed = new TimedValue<string>(clock);
        timed.Set("FAKE-secret", TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.False(timed.TryGet(out _));

        clock.Advance(TimeSpan.FromSeconds(-2));

        Assert.False(timed.TryGet(out _));
    }

    [Fact]
    public void ClearDropsTheValue()
    {
        var timed = new TimedValue<string>(new FakeClock());
        timed.Set("FAKE-secret", TimeSpan.FromMinutes(1));

        timed.Clear();

        Assert.False(timed.IsActive);
    }

    [Fact]
    public void NonPositiveLifetimeIsRejected()
    {
        var timed = new TimedValue<string>(new FakeClock());

        Assert.Throws<ArgumentOutOfRangeException>(() => timed.Set("x", TimeSpan.Zero));
    }
}
