using KeyHolder.Domain.Entries;
using KeyHolder.Domain.Tags;
using KeyHolder.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace KeyHolder.IntegrationTests.Persistence;

public sealed class SchemaMigratorTests : IDisposable
{
    private readonly TempVault _vault = new();

    public void Dispose() => _vault.Dispose();

    [Fact]
    public void FreshDatabaseIsMigratedToTheLatestVersion()
    {
        var migrator = new SchemaMigrator(_vault.CreateFactory());

        migrator.Migrate();

        Assert.Equal(migrator.LatestVersion, migrator.GetCurrentVersion());
        Assert.True(migrator.LatestVersion >= 1);
        Assert.Contains("entry", TableNames(), StringComparer.Ordinal);
        Assert.Contains("vault", TableNames(), StringComparer.Ordinal);
        Assert.Contains("replica_state", TableNames(), StringComparer.Ordinal);
    }

    [Fact]
    public void TagColorTableIsCreatedByMigrationTwo()
    {
        var migrator = new SchemaMigrator(_vault.CreateFactory());

        migrator.Migrate();

        Assert.True(migrator.LatestVersion >= 2);
        Assert.Contains("tag_color", TableNames(), StringComparer.Ordinal);
    }

    [Fact]
    public void VersionOneDatabaseUpgradesAndKeepsItsEntries()
    {
        new SchemaMigrator(_vault.CreateFactory(), [new InitialSchemaMigration()]).Migrate();
        var vaultId = ReadVaultId();
        using (var connection = _vault.CreateFactory().Open())
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO entry (id, vault_id, title, d_tags, created_at, updated_at)
                VALUES ('11111111-1111-1111-1111-111111111111', $vault, 'Old entry', '["Email"]', 'now', 'now')
                """;
            insert.Parameters.AddWithValue("$vault", vaultId);
            insert.ExecuteNonQuery();
        }

        Assert.DoesNotContain("tag_color", TableNames(), StringComparer.Ordinal);

        var migrator = new SchemaMigrator(_vault.CreateFactory());
        migrator.Migrate();

        Assert.Equal(migrator.LatestVersion, migrator.GetCurrentVersion());
        Assert.Contains("tag_color", TableNames(), StringComparer.Ordinal);
        Assert.Equal(1, CountRows("entry"));
        Assert.Equal(0, CountRows("tag_color"));
        Assert.Equal(vaultId, ReadVaultId());
    }

    [Fact]
    public void IconColumnsAreAddedByMigrationThree()
    {
        new SchemaMigrator(_vault.CreateFactory(), [new InitialSchemaMigration(), new TagColorMigration()]).Migrate();
        Assert.DoesNotContain("icon", EntryColumns(), StringComparer.Ordinal);
        Assert.DoesNotContain("icon_color", EntryColumns(), StringComparer.Ordinal);

        var migrator = new SchemaMigrator(_vault.CreateFactory());
        migrator.Migrate();

        Assert.True(migrator.LatestVersion >= 3);
        Assert.Contains("icon", EntryColumns(), StringComparer.Ordinal);
        Assert.Contains("icon_color", EntryColumns(), StringComparer.Ordinal);
    }

    [Fact]
    public async Task OldEntriesReadAsDefaultIconAfterMigrationThree()
    {
        new SchemaMigrator(_vault.CreateFactory(), [new InitialSchemaMigration(), new TagColorMigration()]).Migrate();
        var vaultId = ReadVaultId();
        using (var connection = _vault.CreateFactory().Open())
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO entry (id, vault_id, title, d_tags, s_tags, created_at, updated_at)
                VALUES ('22222222-2222-2222-2222-222222222222', $vault, 'Old', '[]', '[]', '2026-01-01T00:00:00.0000000Z', '2026-01-01T00:00:00.0000000Z')
                """;
            insert.Parameters.AddWithValue("$vault", vaultId);
            insert.ExecuteNonQuery();
        }

        new SchemaMigrator(_vault.CreateFactory()).Migrate();

        var repository = _vault.CreateMigratedRepository();
        var summary = await repository.GetSummaryAsync(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        Assert.Equal(EntryIcon.Default, summary?.Metadata.Icon);
        Assert.Equal(TagColor.Default, summary?.Metadata.IconColor);
    }

    [Fact]
    public void TagColorTableRejectsAnUnknownKind()
    {
        new SchemaMigrator(_vault.CreateFactory()).Migrate();
        var vaultId = ReadVaultId();
        using var connection = _vault.CreateFactory().Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO tag_color (vault_id, kind, name_key, name, color)
            VALUES ($vault, 'x', 'EMAIL', 'Email', 'Blue')
            """;
        command.Parameters.AddWithValue("$vault", vaultId);

        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }

    [Fact]
    public void MigratingTwiceKeepsTheSameVaultId()
    {
        var migrator = new SchemaMigrator(_vault.CreateFactory());
        migrator.Migrate();
        var firstId = ReadVaultId();

        migrator.Migrate();
        new SchemaMigrator(_vault.CreateFactory()).Migrate();

        Assert.Equal(firstId, ReadVaultId());
        Assert.Equal(1, CountRows("vault"));
        Assert.Equal(1, CountRows("replica_state"));
    }

    [Fact]
    public void MigrationsRunInOrderOnlyOnce()
    {
        var log = new List<int>();
        var migrations = new IMigration[]
        {
            new RecordingMigration(1, log),
            new RecordingMigration(2, log),
            new RecordingMigration(3, log),
        };

        new SchemaMigrator(_vault.CreateFactory(), migrations).Migrate();
        new SchemaMigrator(_vault.CreateFactory(), migrations).Migrate();

        Assert.Equal([1, 2, 3], log);
    }

    [Fact]
    public void OnlyNewerMigrationsRunOnAnExistingDatabase()
    {
        var log = new List<int>();
        new SchemaMigrator(_vault.CreateFactory(), [new RecordingMigration(1, log)]).Migrate();
        log.Clear();

        new SchemaMigrator(
            _vault.CreateFactory(),
            [new RecordingMigration(1, log), new RecordingMigration(2, log)]).Migrate();

        Assert.Equal([2], log);
    }

    [Fact]
    public void DatabaseNewerThanTheAppIsRefused()
    {
        var factory = _vault.CreateFactory();
        new SchemaMigrator(factory).Migrate();
        SetUserVersion(99);

        var exception = Assert.Throws<UnsupportedSchemaVersionException>(
            () => new SchemaMigrator(_vault.CreateFactory()).Migrate());

        Assert.Contains("99", exception.Message, StringComparison.Ordinal);
        Assert.Equal(99, new SchemaMigrator(factory).GetCurrentVersion());
    }

    [Fact]
    public void FailedMigrationChangesNothing()
    {
        var log = new List<int>();
        var migrations = new IMigration[]
        {
            new RecordingMigration(1, log),
            new FailingMigration(2),
        };

        Assert.Throws<InvalidOperationException>(
            () => new SchemaMigrator(_vault.CreateFactory(), migrations).Migrate());

        var migrator = new SchemaMigrator(_vault.CreateFactory(), migrations);
        Assert.Equal(1, migrator.GetCurrentVersion());
        Assert.DoesNotContain("half_done", TableNames(), StringComparer.Ordinal);
    }

    [Fact]
    public void MigrationNumbersMustHaveNoGaps()
    {
        Assert.Throws<ArgumentException>(
            () => new SchemaMigrator(_vault.CreateFactory(), [new RecordingMigration(2, [])]));
    }

    [Fact]
    public void ForeignKeysAreEnforced()
    {
        new SchemaMigrator(_vault.CreateFactory()).Migrate();
        using var connection = _vault.CreateFactory().Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO entry (id, vault_id, title, created_at, updated_at)
            VALUES ('x', 'no-such-vault', 'T', 'now', 'now')
            """;

        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }

    [Fact]
    public void BlankTitleIsRejectedByTheDatabaseToo()
    {
        new SchemaMigrator(_vault.CreateFactory()).Migrate();
        var vaultId = ReadVaultId();
        using var connection = _vault.CreateFactory().Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO entry (id, vault_id, title, created_at, updated_at)
            VALUES ('x', $vault, '   ', 'now', 'now')
            """;
        command.Parameters.AddWithValue("$vault", vaultId);

        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }

    private List<string> EntryColumns()
    {
        using var connection = _vault.CreateFactory().Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(entry)";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(1));
        }

        return names;
    }

    private List<string> TableNames()
    {
        using var connection = _vault.CreateFactory().Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private string ReadVaultId()
    {
        using var connection = _vault.CreateFactory().Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM vault";
        return (string)command.ExecuteScalar()!;
    }

    private int CountRows(string table)
    {
        using var connection = _vault.CreateFactory().Open();
        using var command = connection.CreateCommand();

        // The table name comes from this test file, never from input.
#pragma warning disable CA2100
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
#pragma warning restore CA2100
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private void SetUserVersion(int version)
    {
        using var connection = _vault.CreateFactory().Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA user_version = {version}";
        command.ExecuteNonQuery();
    }

    private sealed class RecordingMigration(int version, List<int> log) : IMigration
    {
        public int Version => version;

        public void Apply(SqliteConnection connection, SqliteTransaction transaction) => log.Add(version);
    }

    private sealed class FailingMigration(int version) : IMigration
    {
        public int Version => version;

        public void Apply(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "CREATE TABLE half_done (id INTEGER)";
            command.ExecuteNonQuery();

            throw new InvalidOperationException("injected migration failure");
        }
    }
}
