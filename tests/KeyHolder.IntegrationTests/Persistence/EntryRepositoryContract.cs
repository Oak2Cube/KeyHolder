using KeyHolder.Domain.Entries;
using KeyHolder.Domain.Locking;
using KeyHolder.Domain.Tags;
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

    private ITagColorStore Colors => (ITagColorStore)Repository;

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
    public async Task SummariesReportWhichSecretFieldsAreSet()
    {
        var full = NewEntry("Full", "FAKE-pw", "FAKE note");
        var titleOnly = VaultEntry.Create(new EntryMetadata("Only Title"), EntrySecret.Empty, T0);
        await Repository.AddAsync(full);
        await Repository.AddAsync(titleOnly);

        var summaries = await Repository.ListSummariesAsync();

        var withSecrets = summaries.Single(summary => summary.Id == full.Id);
        Assert.True(withSecrets.HasPassword);
        Assert.True(withSecrets.HasNotes);
        var withoutSecrets = summaries.Single(summary => summary.Id == titleOnly.Id);
        Assert.False(withoutSecrets.HasPassword);
        Assert.False(withoutSecrets.HasNotes);
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
    public async Task NewVaultHasNoTagColors()
    {
        Assert.Empty(await Colors.ListTagColorsAsync());
    }

    [Fact]
    public async Task ColorsAreSavedWithANewEntry()
    {
        await Repository.AddAsync(NewEntry(), [new TagColorAssignment(TagKind.Data, "Email", TagColor.Blue)]);

        var colors = await Colors.ListTagColorsAsync();

        var color = Assert.Single(colors);
        Assert.Equal(new TagColorAssignment(TagKind.Data, "Email", TagColor.Blue), color);
    }

    [Fact]
    public async Task ColorsAreSavedWithAnUpdate()
    {
        var entry = NewEntry();
        await Repository.AddAsync(entry);

        await Repository.UpdateAsync(
            entry.Id,
            entry.Metadata,
            null,
            T0.AddHours(1),
            [new TagColorAssignment(TagKind.Security, "Sensitive", TagColor.Red)]);

        Assert.Equal(
            [new TagColorAssignment(TagKind.Security, "Sensitive", TagColor.Red)],
            await Colors.ListTagColorsAsync());
    }

    [Fact]
    public async Task TagColorsIgnoreCaseAndKeepOneColorPerTagName()
    {
        var first = VaultEntry.Create(
            new EntryMetadata("A", DTags: ["Email", "Финансы"]),
            EntrySecret.Empty,
            T0);
        var second = VaultEntry.Create(
            new EntryMetadata("B", DTags: ["Email", "финансы"]),
            EntrySecret.Empty,
            T0);

        await Repository.AddAsync(
            first,
            [new TagColorAssignment(TagKind.Data, "Email", TagColor.Blue), new TagColorAssignment(TagKind.Data, "Финансы", TagColor.Green)]);
        await Repository.AddAsync(
            second,
            [new TagColorAssignment(TagKind.Data, "EMAIL", TagColor.Red), new TagColorAssignment(TagKind.Data, "финансы", TagColor.Pink)]);

        var colors = await Colors.ListTagColorsAsync();

        Assert.Equal(2, colors.Count);
        Assert.Equal(TagColor.Red, colors.Single(c => string.Equals(c.Name, "email", StringComparison.OrdinalIgnoreCase)).Color);
        Assert.Equal(TagColor.Pink, colors.Single(c => string.Equals(c.Name, "финансы", StringComparison.OrdinalIgnoreCase)).Color);
    }

    [Fact]
    public async Task TheSameNameInBothListsHasSeparateColors()
    {
        var entry = VaultEntry.Create(
            new EntryMetadata("Both", DTags: ["Account"], STags: ["Account"]),
            EntrySecret.Empty,
            T0);

        await Repository.AddAsync(
            entry,
            [new TagColorAssignment(TagKind.Data, "Account", TagColor.Teal), new TagColorAssignment(TagKind.Security, "Account", TagColor.Orange)]);

        var colors = await Colors.ListTagColorsAsync();

        Assert.Equal(TagColor.Teal, colors.Single(c => c.Kind == TagKind.Data).Color);
        Assert.Equal(TagColor.Orange, colors.Single(c => c.Kind == TagKind.Security).Color);
    }

    [Fact]
    public async Task DefaultColorRemovesTheStoredColor()
    {
        await Repository.AddAsync(NewEntry("A"), [new TagColorAssignment(TagKind.Data, "Email", TagColor.Blue)]);

        await Repository.AddAsync(NewEntry("B"), [new TagColorAssignment(TagKind.Data, "email", TagColor.Default)]);

        Assert.Empty(await Colors.ListTagColorsAsync());
    }

    [Fact]
    public async Task ColorsOfAMissingEntryAreNotSaved()
    {
        var updated = await Repository.UpdateAsync(
            Guid.NewGuid(),
            new EntryMetadata("X"),
            null,
            T0,
            [new TagColorAssignment(TagKind.Data, "Email", TagColor.Blue)]);

        Assert.False(updated);
        Assert.Empty(await Colors.ListTagColorsAsync());
    }

    [Fact]
    public async Task InvalidEntryDoesNotSaveItsColors()
    {
        var invalid = new VaultEntry(Guid.NewGuid(), new EntryMetadata("  "), EntrySecret.Empty, T0, T0);

        await Assert.ThrowsAsync<EntryValidationException>(
            () => Repository.AddAsync(invalid, [new TagColorAssignment(TagKind.Data, "Email", TagColor.Blue)]));

        Assert.Empty(await Colors.ListTagColorsAsync());
    }

    [Fact]
    public async Task InvalidTagNameIsRejectedAndNothingIsStored()
    {
        await Assert.ThrowsAsync<EntryValidationException>(
            () => Repository.AddAsync(NewEntry(), [new TagColorAssignment(TagKind.Data, "a,b", TagColor.Blue)]));

        Assert.Empty(await Repository.ListSummariesAsync());
        Assert.Empty(await Colors.ListTagColorsAsync());
    }

    [Fact]
    public async Task SavingColorsMarksTheVaultDirty()
    {
        var entry = NewEntry();
        await Seedable.SeedOnceAsync([entry]);
        Assert.False(await State.GetLocalChangedSinceReplicaAsync());

        await Repository.UpdateAsync(
            entry.Id,
            entry.Metadata,
            null,
            T0.AddMinutes(1),
            [new TagColorAssignment(TagKind.Data, "Email", TagColor.Blue)]);

        Assert.True(await State.GetLocalChangedSinceReplicaAsync());
    }

    [Fact]
    public async Task SeederAddsDemoTagColorsWithoutMarkingTheVaultDirty()
    {
        await new FakeDataSeeder(Seedable, new SystemClock()).SeedIfNeededAsync();

        var colors = await Colors.ListTagColorsAsync();

        Assert.Contains(colors, c => c.Kind == TagKind.Security && c.Name == "Sensitive" && c.Color == TagColor.Red);
        Assert.False(await State.GetLocalChangedSinceReplicaAsync());
    }

    [Fact]
    public async Task ColorsOfTagsNoLongerUsedArePruned()
    {
        var entry = NewEntry();
        await Repository.AddAsync(entry, [new TagColorAssignment(TagKind.Data, "Email", TagColor.Blue)]);

        await Repository.DeleteAsync(entry.Id);

        Assert.Empty(await Colors.ListTagColorsAsync());
    }

    [Fact]
    public async Task RemovingATagFromTheLastEntryPrunesItsColor()
    {
        var entry = NewEntry();
        await Repository.AddAsync(entry, [new TagColorAssignment(TagKind.Data, "Email", TagColor.Blue)]);

        var withoutEmail = entry.Metadata with { DTags = ["Account"] };
        await Repository.UpdateAsync(entry.Id, withoutEmail, null, T0.AddMinutes(1));

        var colors = await Colors.ListTagColorsAsync();
        Assert.DoesNotContain(colors, color => color.Name == "Email");
    }

    [Fact]
    public async Task IconAndColorRoundTripWithTheEntry()
    {
        var entry = VaultEntry.Create(
            new EntryMetadata("Bank", Icon: EntryIcon.Bank, IconColor: TagColor.Blue),
            EntrySecret.Empty,
            T0);

        await Repository.AddAsync(entry);

        var loaded = await Repository.GetAsync(entry.Id);
        Assert.Equal(EntryIcon.Bank, loaded?.Metadata.Icon);
        Assert.Equal(TagColor.Blue, loaded?.Metadata.IconColor);

        var summary = await Repository.GetSummaryAsync(entry.Id);
        Assert.Equal(EntryIcon.Bank, summary?.Metadata.Icon);
        Assert.Equal(TagColor.Blue, summary?.Metadata.IconColor);
    }

    [Fact]
    public async Task UpdatingIconChangesTheStoredValues()
    {
        var entry = NewEntry();
        await Repository.AddAsync(entry);

        await Repository.UpdateAsync(
            entry.Id,
            entry.Metadata with { Icon = EntryIcon.Shield, IconColor = TagColor.Red },
            null,
            T0.AddMinutes(1));

        var loaded = await Repository.GetAsync(entry.Id);
        Assert.Equal(EntryIcon.Shield, loaded?.Metadata.Icon);
        Assert.Equal(TagColor.Red, loaded?.Metadata.IconColor);
    }

    [Fact]
    public async Task DefaultIconAndColorAreNotRequired()
    {
        await Repository.AddAsync(NewEntry());

        var loaded = await Repository.GetAsync((await Repository.ListSummariesAsync())[0].Id);
        Assert.Equal(EntryIcon.Default, loaded?.Metadata.Icon);
        Assert.Equal(TagColor.Default, loaded?.Metadata.IconColor);
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
