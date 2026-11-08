using KeyHolder.Domain.Entries;
using KeyHolder.Domain.Locking;
using KeyHolder.Infrastructure.FakeData;

namespace KeyHolder.IntegrationTests.Persistence;

/// <summary>
/// Behavior every repository must share. Run against the in-memory and the SQLite repository.
/// </summary>
public abstract class EntryRepositoryContract : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Creates a fresh, empty repository.
    /// </summary>
    protected abstract IEntryRepository CreateRepository();

    protected IEntryRepository Repository => _repository ??= CreateRepository();

    private IEntryRepository? _repository;

    private IVaultStateStore State => (IVaultStateStore)Repository;

    private IFakeDataStore Seedable => (IFakeDataStore)Repository;

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }

    private static VaultEntry NewEntry(string title = "Fake Mail", string password = "FAKE-pw", string? notes = "FAKE note") =>
        VaultEntry.Create(
            new EntryMetadata(
                title,
                "fake.user",
                "fake@example.test",
                "https://mail.example.test",
                ["Email", "Account"],
                ["Sensitive"],
                7),
            new EntrySecret(password, notes),
            T0);

    [Fact]
    public async Task AddedEntryCanBeReadBack()
    {
        var entry = NewEntry();

        await Repository.AddAsync(entry);
        var loaded = await Repository.GetAsync(entry.Id);

        Assert.NotNull(loaded);
        Assert.Equal(entry.Id, loaded.Id);
        Assert.Equal("Fake Mail", loaded.Metadata.Title);
        Assert.Equal("fake.user", loaded.Metadata.Username);
        Assert.Equal("fake@example.test", loaded.Metadata.Email);
        Assert.Equal("https://mail.example.test", loaded.Metadata.Url);
        Assert.Equal(["Email", "Account"], loaded.Metadata.DTags);
        Assert.Equal(["Sensitive"], loaded.Metadata.STags);
        Assert.Equal(7, loaded.Metadata.Importance);
        Assert.Equal("FAKE-pw", loaded.Secret.Password);
        Assert.Equal("FAKE note", loaded.Secret.Notes);
        Assert.Equal(T0, loaded.CreatedAt);
        Assert.Equal(T0, loaded.UpdatedAt);
    }

    [Fact]
    public async Task TitleOnlyEntryRoundTripsWithEmptyOptionalFields()
    {
        var entry = VaultEntry.Create(new EntryMetadata("Only Title"), EntrySecret.Empty, T0);

        await Repository.AddAsync(entry);
        var loaded = await Repository.GetAsync(entry.Id);

        Assert.NotNull(loaded);
        Assert.Null(loaded.Metadata.Username);
        Assert.Null(loaded.Metadata.Importance);
        Assert.Empty(loaded.Metadata.DTags);
        Assert.Empty(loaded.Metadata.STags);
        Assert.Null(loaded.Secret.Password);
        Assert.Null(loaded.Secret.Notes);
    }

    [Fact]
    public async Task MissingEntryReturnsNull()
    {
        Assert.Null(await Repository.GetAsync(Guid.NewGuid()));
        Assert.Null(await Repository.GetSummaryAsync(Guid.NewGuid()));
        Assert.Null(await Repository.GetSecretAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task InvalidEntryIsRejectedAndNothingIsStored()
    {
        var invalid = new VaultEntry(Guid.NewGuid(), new EntryMetadata("  "), EntrySecret.Empty, T0, T0);

        await Assert.ThrowsAsync<EntryValidationException>(() => Repository.AddAsync(invalid));

        Assert.Empty(await Repository.ListSummariesAsync());
        Assert.False(await State.GetLocalChangedSinceReplicaAsync());
    }

    [Fact]
    public async Task SummariesDoNotContainSecrets()
    {
        await Repository.AddAsync(NewEntry(password: "FAKE-super-secret", notes: "FAKE-secret-note"));

        var summaries = await Repository.ListSummariesAsync();
        var text = string.Join(' ', summaries.Select(summary => summary.ToString()));

        Assert.DoesNotContain("FAKE-super-secret", text, StringComparison.Ordinal);
        Assert.DoesNotContain("FAKE-secret-note", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSecretReturnsOnlyTheRequestedEntrySecret()
    {
        var first = NewEntry("First", "FAKE-one");
        var second = NewEntry("Second", "FAKE-two");
        await Repository.AddAsync(first);
        await Repository.AddAsync(second);

        var secret = await Repository.GetSecretAsync(second.Id);

        Assert.Equal("FAKE-two", secret?.Password);
    }

    [Fact]
    public async Task UpdateWithoutSecretKeepsTheStoredSecret()
    {
        var entry = NewEntry();
        await Repository.AddAsync(entry);
        var later = T0.AddHours(1);

        var updated = await Repository.UpdateAsync(
            entry.Id,
            new EntryMetadata("Renamed", DTags: ["Phone number"], Importance: 2),
            newSecret: null,
            later);

        Assert.True(updated);
        var loaded = await Repository.GetAsync(entry.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Renamed", loaded.Metadata.Title);
        Assert.Null(loaded.Metadata.Username);
        Assert.Equal(["Phone number"], loaded.Metadata.DTags);
        Assert.Equal(2, loaded.Metadata.Importance);
        Assert.Equal("FAKE-pw", loaded.Secret.Password);
        Assert.Equal("FAKE note", loaded.Secret.Notes);
        Assert.Equal(T0, loaded.CreatedAt);
        Assert.Equal(later, loaded.UpdatedAt);
    }

    [Fact]
    public async Task UpdateWithSecretReplacesIt()
    {
        var entry = NewEntry();
        await Repository.AddAsync(entry);

        await Repository.UpdateAsync(
            entry.Id,
            entry.Metadata,
            new EntrySecret("FAKE-new", null),
            T0.AddHours(1));

        var secret = await Repository.GetSecretAsync(entry.Id);
        Assert.Equal("FAKE-new", secret?.Password);
        Assert.Null(secret?.Notes);
    }

    [Fact]
    public async Task UpdateWithInvalidMetadataThrowsAndChangesNothing()
    {
        var entry = NewEntry();
        await Repository.AddAsync(entry);

        await Assert.ThrowsAsync<EntryValidationException>(
            () => Repository.UpdateAsync(entry.Id, new EntryMetadata(""), null, T0.AddHours(1)));

        var loaded = await Repository.GetSummaryAsync(entry.Id);
        Assert.Equal("Fake Mail", loaded?.Metadata.Title);
    }

    [Fact]
    public async Task UpdateOfMissingEntryReturnsFalseAndStaysClean()
    {
        var updated = await Repository.UpdateAsync(Guid.NewGuid(), new EntryMetadata("X"), null, T0);

        Assert.False(updated);
        Assert.False(await State.GetLocalChangedSinceReplicaAsync());
    }

    [Fact]
    public async Task DeleteRemovesTheEntry()
    {
        var entry = NewEntry();
        await Repository.AddAsync(entry);

        Assert.True(await Repository.DeleteAsync(entry.Id));

        Assert.Null(await Repository.GetAsync(entry.Id));
        Assert.Empty(await Repository.ListSummariesAsync());
    }

    [Fact]
    public async Task DeleteOfMissingEntryReturnsFalseAndStaysClean()
    {
        Assert.False(await Repository.DeleteAsync(Guid.NewGuid()));
        Assert.False(await State.GetLocalChangedSinceReplicaAsync());
    }

    [Fact]
    public async Task NewVaultIsNotDirty()
    {
        Assert.False(await State.GetLocalChangedSinceReplicaAsync());
    }

    [Fact]
    public async Task AddMarksTheVaultDirty()
    {
        await Repository.AddAsync(NewEntry());

        Assert.True(await State.GetLocalChangedSinceReplicaAsync());
    }

    [Fact]
    public async Task UpdateMarksTheVaultDirty()
    {
        var entry = NewEntry();
        await Seedable.SeedOnceAsync([entry]);
        Assert.False(await State.GetLocalChangedSinceReplicaAsync());

        await Repository.UpdateAsync(entry.Id, new EntryMetadata("Changed"), null, T0.AddMinutes(1));

        Assert.True(await State.GetLocalChangedSinceReplicaAsync());
    }

    [Fact]
    public async Task DeleteMarksTheVaultDirty()
    {
        var entry = NewEntry();
        await Seedable.SeedOnceAsync([entry]);
        Assert.False(await State.GetLocalChangedSinceReplicaAsync());

        await Repository.DeleteAsync(entry.Id);

        Assert.True(await State.GetLocalChangedSinceReplicaAsync());
    }

    [Fact]
    public async Task DuplicateIdIsRejectedAndKeepsTheOriginal()
    {
        var entry = NewEntry();
        await Seedable.SeedOnceAsync([entry]);

        await Assert.ThrowsAnyAsync<Exception>(
            () => Repository.AddAsync(entry with { Metadata = new EntryMetadata("Imposter") }));

        Assert.False(await State.GetLocalChangedSinceReplicaAsync());
        var summaries = await Repository.ListSummariesAsync();
        Assert.Equal("Fake Mail", Assert.Single(summaries).Metadata.Title);
    }

    [Fact]
    public async Task SeederAddsFakeDataOnlyOnce()
    {
        var seeder = new FakeDataSeeder(Seedable, new SystemClock());

        Assert.True(await seeder.SeedIfNeededAsync());
        var count = (await Repository.ListSummariesAsync()).Count;
        Assert.True(count >= 8);

        Assert.False(await seeder.SeedIfNeededAsync());
        Assert.Equal(count, (await Repository.ListSummariesAsync()).Count);
    }

    [Fact]
    public async Task SeedingDoesNotMarkTheVaultDirty()
    {
        await new FakeDataSeeder(Seedable, new SystemClock()).SeedIfNeededAsync();

        Assert.False(await State.GetLocalChangedSinceReplicaAsync());
    }

    [Fact]
    public async Task SeededDataIsNotRestoredAfterTheUserDeletesIt()
    {
        var seeder = new FakeDataSeeder(Seedable, new SystemClock());
        await seeder.SeedIfNeededAsync();
        foreach (var summary in await Repository.ListSummariesAsync())
        {
            await Repository.DeleteAsync(summary.Id);
        }

        await seeder.SeedIfNeededAsync();

        Assert.Empty(await Repository.ListSummariesAsync());
    }

    [Fact]
    public void FakeDataIsObviouslyFake()
    {
        var entries = FakeDataSeeder.CreateEntries(T0);

        Assert.All(entries, entry =>
        {
            Assert.StartsWith("Fake ", entry.Metadata.Title, StringComparison.Ordinal);
            Assert.True(entry.Secret.Password is null || entry.Secret.Password.StartsWith("FAKE-", StringComparison.Ordinal));
            Assert.True(entry.Metadata.Email is null || entry.Metadata.Email.EndsWith(".test", StringComparison.Ordinal));
        });
        Assert.Equal(entries.Count, entries.Select(entry => entry.Id).Distinct().Count());
    }
}
