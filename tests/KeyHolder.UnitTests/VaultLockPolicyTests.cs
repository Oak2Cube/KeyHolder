using KeyHolder.Domain.Locking;

namespace KeyHolder.UnitTests;

public sealed class VaultLockPolicyTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    [Fact]
    public void VaultStartsLocked()
    {
        var policy = new VaultLockPolicy(new FakeClock(), Timeout);

        Assert.True(policy.IsLocked);
    }

    [Fact]
    public void UnlockRaisesEventAndClearsLastReason()
    {
        var policy = new VaultLockPolicy(new FakeClock(), Timeout);
        var raised = 0;
        policy.Unlocked += (_, _) => raised++;

        policy.Unlock();
        policy.Unlock();

        Assert.False(policy.IsLocked);
        Assert.Null(policy.LastLockReason);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void IdleTimeoutLocksTheVault()
    {
        var clock = new FakeClock();
        var policy = new VaultLockPolicy(clock, Timeout);
        policy.Unlock();
        LockReason? reason = null;
        policy.Locked += (_, args) => reason = args.Reason;

        clock.Advance(Timeout - TimeSpan.FromSeconds(1));
        Assert.False(policy.CheckIdle());

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(policy.CheckIdle());

        Assert.True(policy.IsLocked);
        Assert.Equal(LockReason.Idle, reason);
        Assert.Equal(LockReason.Idle, policy.LastLockReason);
    }

    [Fact]
    public void ActivityRestartsTheIdleCountdown()
    {
        var clock = new FakeClock();
        var policy = new VaultLockPolicy(clock, Timeout);
        policy.Unlock();

        clock.Advance(TimeSpan.FromMinutes(4));
        policy.RecordActivity();
        clock.Advance(TimeSpan.FromMinutes(4));

        Assert.False(policy.CheckIdle());
        Assert.False(policy.IsLocked);
    }

    [Theory]
    [InlineData(LockReason.SessionLock)]
    [InlineData(LockReason.User)]
    public void ExplicitLockRecordsTheReason(LockReason reason)
    {
        var policy = new VaultLockPolicy(new FakeClock(), Timeout);
        policy.Unlock();

        policy.Lock(reason);

        Assert.True(policy.IsLocked);
        Assert.Equal(reason, policy.LastLockReason);
    }

    [Fact]
    public void LockWhileLockedDoesNotRaiseAgain()
    {
        var policy = new VaultLockPolicy(new FakeClock(), Timeout);
        policy.Unlock();
        var raised = 0;
        policy.Locked += (_, _) => raised++;

        policy.Lock(LockReason.User);
        policy.Lock(LockReason.SessionLock);

        Assert.Equal(1, raised);
        Assert.Equal(LockReason.User, policy.LastLockReason);
    }

    [Fact]
    public void ActivityWhileLockedDoesNotShortenTheNextIdlePeriod()
    {
        var clock = new FakeClock();
        var policy = new VaultLockPolicy(clock, Timeout);

        clock.Advance(TimeSpan.FromMinutes(10));
        policy.Unlock();

        Assert.False(policy.CheckIdle());
    }

    [Fact]
    public void NonPositiveTimeoutIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new VaultLockPolicy(new FakeClock(), TimeSpan.Zero));
    }
}
