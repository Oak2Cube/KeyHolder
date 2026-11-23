using KeyHolder.Domain.Entries;
using KeyHolder.Domain.Locking;
using KeyHolder.Domain.Tags;
using KeyHolder.Infrastructure.FakeData;
using KeyHolder.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace KeyHolder.IntegrationTests.Persistence;

/// <summary>
/// Runs the shared contract on SQLite and adds SQLite-only behavior: restart persistence and rollback.
/// </summary>
public sealed class SqliteEntryRepositoryTests : EntryRepositoryContract
{
    private static readonly DateTimeOffset T0 = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly TempVault _vault = new();

    protected override IEntryRepository CreateRepository() => _vault.CreateMigratedRepository();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _vault.Dispose();
        }

        base.Dispose(disposing);
    }

    [Fact]
    public async Task EntriesSurviveRestart()
    {
        var kept = VaultEntry.Create(
            new EntryMetadata("Kept", "u", DTags: ["Email"], STags: ["Finance"], Importance: 4),
            new EntrySecret("FAKE-kept", "FAKE note"),
            T0);
        var edited = VaultEntry.Create(new EntryMetadata("Before"), new EntrySecret("FAKE-before"), T0);
        var removed = VaultEntry.Create(new EntryMetadata("Removed"), EntrySecret.Empty, T0);

        var firstRun = _vault.CreateMigratedRepository();
        await firstRun.AddAsync(kept);
        await firstRun.AddAsync(edited);
        await firstRun.AddAsync(removed);
        await firstRun.UpdateAsync(edited.Id, new EntryMetadata("After"), new EntrySecret("FAKE-after"), T0.AddDays(1));
        await firstRun.DeleteAsync(removed.Id);

        // A new factory, migrator, and repository on the same file behave like an app restart.
        var factory = _vault.CreateFactory();
        new SchemaMigrator(factory).Migrate();
        var secondRun = _vault.CreateRepository(factory);

        var summaries = await secondRun.ListSummariesAsync();
        Assert.Equal(["After", "Kept"], summaries.Select(s => s.Metadata.Title).Order().ToArray());

        var loadedKept = await secondRun.GetAsync(kept.Id);
        Assert.Equal("Kept", loadedKept?.Metadata.Title);
        Assert.Equal(4, loadedKept?.Metadata.Importance);
        Assert.Equal(["Email"], loadedKept?.Metadata.DTags);
        Assert.Equal(["Finance"], loadedKept?.Metadata.STags);
        Assert.Equal("FAKE-kept", loadedKept?.Secret.Password);

        var loadedEdited = await secondRun.GetAsync(edited.Id);
        Assert.Equal("After", loadedEdited?.Metadata.Title);
        Assert.Equal("FAKE-after", loadedEdited?.Secret.Password);
        Assert.Equal(T0.AddDays(1), loadedEdited?.UpdatedAt);
        Assert.Null(await secondRun.GetAsync(removed.Id));
    }

    [Fact]
    public async Task DirtyStateSurvivesRestart()
    {
        var firstRun = _vault.CreateMigratedRepository();
        await firstRun.AddAsync(VaultEntry.Create(new EntryMetadata("A"), EntrySecret.Empty, T0));

        var secondRun = _vault.CreateMigratedRepository();

        Assert.True(await secondRun.GetLocalChangedSinceReplicaAsync());
    }

    [Fact]
    public async Task SeedingIsNotRepeatedAfterRestart()
    {
        var firstRun = _vault.CreateMigratedRepository();
        Assert.True(await new FakeDataSeeder(firstRun, new SystemClock()).SeedIfNeededAsync());
        var count = (await firstRun.ListSummariesAsync()).Count;

        var secondRun = _vault.CreateMigratedRepository();

        Assert.False(await new FakeDataSeeder(secondRun, new SystemClock()).SeedIfNeededAsync());
        Assert.Equal(count, (await secondRun.ListSummariesAsync()).Count);
    }

    [Fact]
    public async Task FailedChangeRollsBackBothTheEntryAndTheDirtyFlag()
    {
        var repository = _vault.CreateMigratedRepository();

        // Make the dirty-flag update fail AFTER the entry insert succeeded.
        using (var connection = _vault.CreateFactory().Open())
        {
            using var trigger = connection.CreateCommand();
            trigger.CommandText = """
                CREATE TRIGGER fail_dirty BEFORE UPDATE ON replica_state
                BEGIN
                    SELECT RAISE(ABORT, 'injected failure');
                END;
                """;
            trigger.ExecuteNonQuery();
        }

        var entry = VaultEntry.Create(new EntryMetadata("Never stored"), EntrySecret.Empty, T0);

        await Assert.ThrowsAsync<SqliteException>(() => repository.AddAsync(entry));

        Assert.Empty(await repository.ListSummariesAsync());
        Assert.False(await repository.GetLocalChangedSinceReplicaAsync());
    }

    [Fact]
    public async Task FailedChangeRollsBackTheTagColorsToo()
    {
        var repository = _vault.CreateMigratedRepository();
        await repository.AddAsync(
            VaultEntry.Create(new EntryMetadata("Existing"), EntrySecret.Empty, T0),
            [new TagColorAssignment(TagKind.Data, "Email", TagColor.Blue)]);
        using (var connection = _vault.CreateFactory().Open())
        {
            using var trigger = connection.CreateCommand();
            trigger.CommandText = """
                CREATE TRIGGER fail_dirty BEFORE UPDATE ON replica_state
                BEGIN
                    SELECT RAISE(ABORT, 'injected failure');
                END;
                """;
            trigger.ExecuteNonQuery();
        }

        await Assert.ThrowsAsync<SqliteException>(
            () => repository.AddAsync(
                VaultEntry.Create(new EntryMetadata("Never stored"), EntrySecret.Empty, T0),
                [new TagColorAssignment(TagKind.Data, "Email", TagColor.Red), new TagColorAssignment(TagKind.Data, "New", TagColor.Green)]));

        var colors = await repository.ListTagColorsAsync();
        var kept = Assert.Single(colors);
        Assert.Equal(TagColor.Blue, kept.Color);
        Assert.Single(await repository.ListSummariesAsync());
    }

    [Fact]
    public async Task TagColorsSurviveRestart()
    {
        var firstRun = _vault.CreateMigratedRepository();
        await firstRun.AddAsync(
            VaultEntry.Create(new EntryMetadata("A"), EntrySecret.Empty, T0),
            [new TagColorAssignment(TagKind.Security, "Sensitive", TagColor.Red)]);

        var secondRun = _vault.CreateMigratedRepository();

        Assert.Equal(
            [new TagColorAssignment(TagKind.Security, "Sensitive", TagColor.Red)],
            await secondRun.ListTagColorsAsync());
    }

    [Fact]
    public async Task UnknownStoredColorFallsBackToDefaultAndIsNotListed()
    {
        var repository = _vault.CreateMigratedRepository();
        using (var connection = _vault.CreateFactory().Open())
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO tag_color (vault_id, kind, name_key, name, color)
                SELECT id, 'd', 'EMAIL', 'Email', 'Chartreuse' FROM vault
                """;
            command.ExecuteNonQuery();
        }

        Assert.Empty(await repository.ListTagColorsAsync());
    }

    [Fact]
    public async Task FailedUpdateKeepsTheOldValues()
    {
        var repository = _vault.CreateMigratedRepository();
        var entry = VaultEntry.Create(new EntryMetadata("Original"), new EntrySecret("FAKE-old"), T0);
        await repository.AddAsync(entry);

        using (var connection = _vault.CreateFactory().Open())
        {
            using var trigger = connection.CreateCommand();
            trigger.CommandText = """
                CREATE TRIGGER fail_dirty BEFORE UPDATE ON replica_state
                BEGIN
                    SELECT RAISE(ABORT, 'injected failure');
                END;
                """;
            trigger.ExecuteNonQuery();
        }

        await Assert.ThrowsAsync<SqliteException>(
            () => repository.UpdateAsync(entry.Id, new EntryMetadata("Changed"), new EntrySecret("FAKE-new"), T0.AddDays(1)));

        var loaded = await repository.GetAsync(entry.Id);
        Assert.Equal("Original", loaded?.Metadata.Title);
        Assert.Equal("FAKE-old", loaded?.Secret.Password);
        Assert.Equal(T0, loaded?.UpdatedAt);
    }

    [Fact]
    public async Task SqlSpecialCharactersAreStoredAsPlainText()
    {
        var repository = _vault.CreateMigratedRepository();
        const string Nasty = "'); DROP TABLE entry; --";

        // Tags cannot contain ";" (it was the old separator), so they get a different injection attempt.
        const string NastyTag = "') OR 1=1 --";
        var entry = VaultEntry.Create(
            new EntryMetadata(Nasty, DTags: [NastyTag]),
            new EntrySecret(Nasty, Nasty),
            T0);

        await repository.AddAsync(entry, [new TagColorAssignment(TagKind.Data, NastyTag, TagColor.Red)]);

        var loaded = await repository.GetAsync(entry.Id);
        Assert.Equal(Nasty, loaded?.Metadata.Title);
        Assert.Equal([NastyTag], loaded?.Metadata.DTags);
        Assert.Equal(NastyTag, Assert.Single(await repository.ListTagColorsAsync()).Name);
        Assert.Equal(Nasty, loaded?.Secret.Password);
    }

    [Fact]
    public async Task UpdateBumpsOnlyTheRevisionsOfChangedGroups()
    {
        var repository = _vault.CreateMigratedRepository();
        var entry = VaultEntry.Create(new EntryMetadata("A"), new EntrySecret("FAKE-1"), T0);
        await repository.AddAsync(entry);

        await repository.UpdateAsync(entry.Id, new EntryMetadata("B"), null, T0.AddMinutes(1));
        Assert.Equal((2L, 1L), ReadRevisions(entry.Id));

        await repository.UpdateAsync(entry.Id, new EntryMetadata("C"), new EntrySecret("FAKE-2"), T0.AddMinutes(2));
        Assert.Equal((3L, 2L), ReadRevisions(entry.Id));
    }

    private (long Metadata, long Secret) ReadRevisions(Guid id)
    {
        using var connection = _vault.CreateFactory().Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT metadata_revision, secret_revision FROM entry WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        return (reader.GetInt64(0), reader.GetInt64(1));
    }
}
