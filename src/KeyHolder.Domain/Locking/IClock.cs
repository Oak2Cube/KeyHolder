namespace KeyHolder.Domain.Locking;

/// <summary>
/// Source of the current time, so time-based rules can be tested without waiting.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>
/// The real system clock.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
