using KeyHolder.Domain.Entries;
using KeyHolder.Domain.Locking;

namespace KeyHolder.Desktop.Tests;

public sealed class LockingViewModelTests
{
    [Fact]
    public async Task ApplicationStartsLockedWithAnEmptyWorkspace()
    {
        var harness = await Harness.CreateAsync();

        Assert.True(harness.Shell.IsLocked);
        Assert.Empty(harness.Vault.Entries);
        Assert.Equal("Vault locked", harness.Shell.StatusText);
    }

    [Fact]
    public async Task UnlockLoadsTheEntries()
    {
        var harness = await Harness.CreateAsync();

        harness.Shell.UnlockCommand.Execute(null);

        Assert.True(harness.Shell.IsUnlocked);
        Assert.Equal(10, harness.Vault.Entries.Count);
        Assert.Equal("Vault unlocked.", harness.Shell.StatusText);
    }

    [Fact]
    public async Task UserLockForgetsEverythingTheScreenShowed()
    {
        var harness = await Harness.CreateAsync();
        harness.Shell.UnlockCommand.Execute(null);
        harness.Vault.SearchText = "bank";
        harness.Vault.SelectedEntry = harness.Vault.Entries.Single();
        harness.Secret.RevealPasswordCommand.Execute(null);
        harness.Vault.EditCommand.Execute(null);
        Assert.NotNull(harness.Vault.Editor);
        harness.Secret.CopyPasswordCommand.Execute(null);
        harness.Secret.RevealPasswordCommand.Execute(null);
        Assert.NotNull(harness.Clipboard.Text);
        Assert.True(harness.Secret.IsPasswordRevealed);

        harness.Shell.LockCommand.Execute(null);

        Assert.True(harness.Shell.IsLocked);
        Assert.Empty(harness.Vault.Entries);
        Assert.Empty(harness.Vault.DTagFilters);
        Assert.Null(harness.Vault.SelectedEntry);
        Assert.Null(harness.Vault.Editor);
        Assert.Equal(string.Empty, harness.Vault.SearchText);
        Assert.Equal(SecretPlaceholder.Value, harness.Secret.PasswordDisplay);
        Assert.Null(harness.Clipboard.Text);
        Assert.True(harness.Clipboard.ClearCalls > 0);
    }

    [Fact]
    public async Task IdleTimeoutLocksAndExplainsWhy()
    {
        var harness = await Harness.CreateAsync();
        harness.Shell.UnlockCommand.Execute(null);

        harness.Clock.Advance(TimeSpan.FromMinutes(5));
        harness.Policy.CheckIdle();

        Assert.True(harness.Shell.IsLocked);
        Assert.Empty(harness.Vault.Entries);
        Assert.Contains("idle", harness.Shell.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WindowsSessionLockLocksAndExplainsWhy()
    {
        var harness = await Harness.CreateAsync();
        harness.Shell.UnlockCommand.Execute(null);

        harness.Policy.Lock(LockReason.SessionLock);

        Assert.True(harness.Shell.IsLocked);
        Assert.Contains("Windows", harness.Shell.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnlockAfterLockShowsTheEntriesAgainWithoutOldState()
    {
        var harness = await Harness.CreateAsync();
        harness.Shell.UnlockCommand.Execute(null);
        harness.Vault.SearchText = "bank";
        harness.Shell.LockCommand.Execute(null);

        harness.Shell.UnlockCommand.Execute(null);

        Assert.Equal(10, harness.Vault.Entries.Count);
        Assert.Equal(string.Empty, harness.Vault.SearchText);
        Assert.Null(harness.Vault.SelectedEntry);
    }

    [Fact]
    public async Task BannerWarnsThatDataIsFake()
    {
        var harness = await Harness.CreateAsync();

        Assert.Contains("FAKE", harness.Shell.Banner, StringComparison.Ordinal);
    }
}
