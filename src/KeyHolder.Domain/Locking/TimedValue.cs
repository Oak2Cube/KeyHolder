namespace KeyHolder.Domain.Locking;

/// <summary>
/// Holds one value for a limited time. Used for Reveal and for the clipboard timeout.
/// After expiry or <see cref="Clear"/>, the value is dropped and cannot be read again.
/// </summary>
public sealed class TimedValue<T>
    where T : class
{
    private readonly IClock _clock;
    private T? _value;
    private DateTimeOffset _expiresAt;

    public TimedValue(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    /// <summary>
    /// <see langword="true"/> while a value is stored and not yet expired.
    /// </summary>
    public bool IsActive => TryGet(out _);

    public void Set(T value, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Lifetime must be positive.");
        }

        _value = value;
        _expiresAt = _clock.UtcNow + lifetime;
    }

    /// <summary>
    /// Returns the value while it is still valid. Drops it when it has expired.
    /// </summary>
    public bool TryGet(out T value)
    {
        if (_value is not null && _clock.UtcNow < _expiresAt)
        {
            value = _value;
            return true;
        }

        _value = null;
        value = null!;
        return false;
    }

    public void Clear() => _value = null;
}
