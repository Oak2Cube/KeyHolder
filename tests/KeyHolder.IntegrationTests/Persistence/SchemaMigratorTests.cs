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
