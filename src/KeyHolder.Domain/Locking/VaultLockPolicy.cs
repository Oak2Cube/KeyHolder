namespace KeyHolder.Domain.Locking;

/// <summary>
/// Why the vault became locked.
/// </summary>
public enum LockReason
{
    /// <summary>No user activity for the idle timeout.</summary>
    Idle,

    /// <summary>Windows locked the session or switched user.</summary>
    SessionLock,

    /// <summary>The user pressed Lock.</summary>
    User,
}

public sealed class LockedEventArgs : EventArgs
{
    public LockedEventArgs(LockReason reason)
    {
        Reason = reason;
    }

    public LockReason Reason { get; }
}

/// <summary>
/// Tracks locked or unlocked state and locks the vault after idle time.
/// The vault starts locked. This class is pure logic: the desktop app feeds it activity,
/// periodic ticks, and Windows session events.
/// </summary>
public sealed class VaultLockPolicy
{
    public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromMinutes(5);

    private readonly IClock _clock;
    private DateTimeOffset _lastActivity;

    public VaultLockPolicy(IClock clock, TimeSpan? idleTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var timeout = idleTimeout ?? DefaultIdleTimeout;
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(idleTimeout), "Idle timeout must be positive.");
        }

        _clock = clock;
        IdleTimeout = timeout;
        _lastActivity = clock.UtcNow;
        IsLocked = true;
    }

    public event EventHandler? Unlocked;

    public event EventHandler<LockedEventArgs>? Locked;

    public TimeSpan IdleTimeout { get; }

    public bool IsLocked { get; private set; }

    /// <summary>
    /// Why the vault was last locked, or <see langword="null"/> before the first lock after an unlock.
    /// </summary>
    public LockReason? LastLockReason { get; private set; }

    public void Unlock()
    {
        if (!IsLocked)
        {
            return;
        }

        IsLocked = false;
        LastLockReason = null;
        _lastActivity = _clock.UtcNow;
        Unlocked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Locks the vault. Does nothing when it is already locked.
    /// </summary>
    public void Lock(LockReason reason)
    {
        if (IsLocked)
        {
            return;
        }

        IsLocked = true;
        LastLockReason = reason;
        Locked?.Invoke(this, new LockedEventArgs(reason));
    }

    /// <summary>
    /// Records user activity and restarts the idle countdown. Ignored while locked.
    /// </summary>
    public void RecordActivity()
    {
        if (!IsLocked)
        {
            _lastActivity = _clock.UtcNow;
        }
    }

    /// <summary>
    /// Locks the vault when the idle timeout has passed.
    /// </summary>
    /// <returns><see langword="true"/> when this call locked the vault.</returns>
    public bool CheckIdle()
    {
        if (IsLocked || _clock.UtcNow - _lastActivity < IdleTimeout)
        {
            return false;
        }

        Lock(LockReason.Idle);
        return true;
    }
}
