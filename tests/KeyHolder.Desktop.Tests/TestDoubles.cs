using KeyHolder.Cryptography;
using KeyHolder.Desktop.Services;
using KeyHolder.Desktop.ViewModels;
using KeyHolder.Domain.Entries;
using KeyHolder.Domain.Locking;
using KeyHolder.Infrastructure.FakeData;
using KeyHolder.Infrastructure.InMemory;

namespace KeyHolder.Desktop.Tests;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan amount) => UtcNow += amount;
}

internal sealed class FakeClipboard : IClipboardService
{
    public string? Text { get; private set; }

    public TimeSpan? ClearAfter { get; private set; }

    public int ClearCalls { get; private set; }

    public void SetSecret(string text, TimeSpan clearAfter)
    {
        Text = text;
        ClearAfter = clearAfter;
    }

    public void ClearIfOwned()
    {
        ClearCalls++;
        Text = null;
    }
}

internal sealed class FakeDialogs : IDialogService
{
    public bool Answer { get; set; } = true;

    public int Asked { get; private set; }

    public bool Confirm(string title, string message)
    {
        Asked++;
        return Answer;
    }
}

/// <summary>
/// A complete view model graph over an in-memory repository holding the fake demo data.
/// </summary>
internal sealed class Harness
{
    private Harness()
    {
    }

    public FakeClock Clock { get; } = new();

    public FakeClipboard Clipboard { get; } = new();

    public FakeDialogs Dialogs { get; } = new();

    public InMemoryEntryRepository Repository { get; } = new();

    public VaultLockPolicy Policy { get; private set; } = null!;

    public SecretViewModel Secret { get; private set; } = null!;

    public VaultViewModel Vault { get; private set; } = null!;

    public MainWindowViewModel Shell { get; private set; } = null!;

    public static async Task<Harness> CreateAsync(bool seed = true)
    {
        var harness = new Harness();
        if (seed)
        {
            await new FakeDataSeeder(harness.Repository, harness.Clock).SeedIfNeededAsync();
        }

        harness.Policy = new VaultLockPolicy(harness.Clock, TimeSpan.FromMinutes(5));
        harness.Secret = new SecretViewModel(harness.Repository, harness.Clipboard, harness.Clock);
        harness.Vault = new VaultViewModel(
            harness.Repository,
            harness.Repository,
            harness.Clock,
            new PasswordGenerator(),
            harness.Dialogs,
            harness.Secret);
        harness.Shell = new MainWindowViewModel(harness.Policy, harness.Vault, harness.Clipboard);
        return harness;
    }

    public async Task<EntryListItemViewModel> SelectAsync(string title)
    {
        await Vault.LoadAsync();
        var item = Vault.Entries.Single(entry => entry.Title == title);
        Vault.SelectedEntry = item;
        return item;
    }
}
