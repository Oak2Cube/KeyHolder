using KeyHolder.Domain.Locking;

namespace KeyHolder.UnitTests;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan amount) => UtcNow += amount;
}
