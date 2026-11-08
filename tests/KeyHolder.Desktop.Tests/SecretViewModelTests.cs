using KeyHolder.Desktop.ViewModels;
using KeyHolder.Domain.Entries;

namespace KeyHolder.Desktop.Tests;

public sealed class SecretViewModelTests
{
    [Fact]
    public async Task PasswordShowsTheFixedPlaceholderByDefault()
    {
        var harness = await Harness.CreateAsync();
        await harness.SelectAsync("Fake Bank");

        Assert.Equal(SecretPlaceholder.Value, harness.Secret.PasswordDisplay);
        Assert.Equal(SecretPlaceholder.Value, harness.Secret.NotesDisplay);
        Assert.False(harness.Secret.IsPasswordRevealed);
    }

    [Fact]
    public async Task RevealShowsThePasswordThenHidesItAfterTheTimeout()
    {
        var harness = await Harness.CreateAsync();
        await harness.SelectAsync("Fake Bank");

        harness.Secret.RevealPasswordCommand.Execute(null);

        Assert.Equal("FAKE-bank-password-2", harness.Secret.PasswordDisplay);
        Assert.True(harness.Secret.IsPasswordRevealed);
        Assert.Equal("Hide", harness.Secret.RevealPasswordLabel);

        harness.Clock.Advance(SecretViewModel.RevealTimeout - TimeSpan.FromSeconds(1));
        Assert.Equal("FAKE-bank-password-2", harness.Secret.PasswordDisplay);

        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(SecretPlaceholder.Value, harness.Secret.PasswordDisplay);
        Assert.False(harness.Secret.IsPasswordRevealed);
    }

    [Fact]
    public async Task RevealTwiceHidesAgain()
    {
        var harness = await Harness.CreateAsync();
        await harness.SelectAsync("Fake Bank");

        harness.Secret.RevealPasswordCommand.Execute(null);
        harness.Secret.RevealPasswordCommand.Execute(null);

        Assert.Equal(SecretPlaceholder.Value, harness.Secret.PasswordDisplay);
    }

    [Fact]
    public async Task RevealingThePasswordDoesNotShowTheNotes()
    {
        var harness = await Harness.CreateAsync();
        await harness.SelectAsync("Fake Bank");

        harness.Secret.RevealPasswordCommand.Execute(null);

        Assert.Equal(SecretPlaceholder.Value, harness.Secret.NotesDisplay);
    }

    [Fact]
    public async Task RevealedPasswordIsHiddenWhenAnotherEntryIsSelected()
    {
        var harness = await Harness.CreateAsync();
        await harness.SelectAsync("Fake Bank");
        harness.Secret.RevealPasswordCommand.Execute(null);

        harness.Vault.SelectedEntry = harness.Vault.Entries.Single(entry => entry.Title == "Fake Mail");

        Assert.Equal(SecretPlaceholder.Value, harness.Secret.PasswordDisplay);
    }

    [Fact]
    public async Task CopyPutsThePasswordOnTheClipboardWithATimeout()
    {
        var harness = await Harness.CreateAsync();
        await harness.SelectAsync("Fake Mail");

        harness.Secret.CopyPasswordCommand.Execute(null);

        Assert.Equal("FAKE-mail-password-1", harness.Clipboard.Text);
        Assert.Equal(SecretViewModel.ClipboardTimeout, harness.Clipboard.ClearAfter);
        Assert.Equal(SecretPlaceholder.Value, harness.Secret.PasswordDisplay);
    }

    [Fact]
    public async Task CopyOfAnEntryWithoutPasswordCopiesNothing()
    {
        var harness = await Harness.CreateAsync();
        await harness.SelectAsync("Fake Notes Only");

        harness.Secret.CopyPasswordCommand.Execute(null);

        Assert.Null(harness.Clipboard.Text);
        Assert.Contains("no password", harness.Secret.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NotesCanBeRevealedSeparately()
    {
        var harness = await Harness.CreateAsync();
        await harness.SelectAsync("Fake Notes Only");

        harness.Secret.RevealNotesCommand.Execute(null);

        Assert.Equal("FAKE DATA: an entry with no password.", harness.Secret.NotesDisplay);
        Assert.Equal(SecretPlaceholder.Value, harness.Secret.PasswordDisplay);
    }

    [Fact]
    public async Task RevealAndCopyDoNothingWithoutASelection()
    {
        var harness = await Harness.CreateAsync();
        await harness.Vault.LoadAsync();

        Assert.False(harness.Secret.RevealPasswordCommand.CanExecute(null));
        Assert.False(harness.Secret.CopyPasswordCommand.CanExecute(null));
    }

    [Fact]
    public async Task OnlyTheSelectedEntrySecretIsEverRead()
    {
        var harness = await Harness.CreateAsync();
        var counting = new CountingRepository(harness.Repository);
        var secret = new SecretViewModel(counting, harness.Clipboard, harness.Clock);
        var bank = (await harness.Repository.ListSummariesAsync()).Single(entry => entry.Metadata.Title == "Fake Bank");

        secret.SetEntry(bank.Id);
        secret.RevealPasswordCommand.Execute(null);
        secret.CopyPasswordCommand.Execute(null);

        Assert.Equal([bank.Id], counting.SecretReads.Distinct());
        Assert.Equal(0, counting.FullReads);
    }

    private sealed class CountingRepository(IEntryRepository inner) : IEntryRepository
    {
        public List<Guid> SecretReads { get; } = [];

        public int FullReads { get; private set; }

        public Task<IReadOnlyList<EntrySummary>> ListSummariesAsync(CancellationToken cancellationToken = default) =>
            inner.ListSummariesAsync(cancellationToken);

        public Task<EntrySummary?> GetSummaryAsync(Guid id, CancellationToken cancellationToken = default) =>
            inner.GetSummaryAsync(id, cancellationToken);

        public Task<EntrySecret?> GetSecretAsync(Guid id, CancellationToken cancellationToken = default)
        {
            SecretReads.Add(id);
            return inner.GetSecretAsync(id, cancellationToken);
        }

        public Task<VaultEntry?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            FullReads++;
            return inner.GetAsync(id, cancellationToken);
        }

        public Task AddAsync(VaultEntry entry, CancellationToken cancellationToken = default) =>
            inner.AddAsync(entry, cancellationToken);

        public Task<bool> UpdateAsync(
            Guid id,
            EntryMetadata metadata,
            EntrySecret? newSecret,
            DateTimeOffset now,
            CancellationToken cancellationToken = default) =>
            inner.UpdateAsync(id, metadata, newSecret, now, cancellationToken);

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            inner.DeleteAsync(id, cancellationToken);
    }
}
