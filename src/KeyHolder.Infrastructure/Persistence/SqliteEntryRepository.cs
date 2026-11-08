using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using KeyHolder.Domain.Entries;
using KeyHolder.Infrastructure.FakeData;
using Microsoft.Data.Sqlite;

namespace KeyHolder.Infrastructure.Persistence;

/// <summary>
/// SQLite storage for the working vault. All SQL is parameterized.
/// Every successful change runs in one transaction that also sets
/// <c>replica_state.local_changed_since_replica</c>, so the dirty flag and the data never disagree.
/// Only <see cref="GetSecretAsync"/> and <see cref="GetAsync"/> read the <c>password</c> and <c>notes</c> columns.
/// </summary>
public sealed class SqliteEntryRepository : IEntryRepository, IVaultStateStore, IFakeDataStore
{
    private const string SummaryColumns =
        "id, title, username, email, url, d_tags, s_tags, importance, created_at, updated_at";

    private readonly SqliteConnectionFactory _connections;

    public SqliteEntryRepository(SqliteConnectionFactory connections)
    {
        ArgumentNullException.ThrowIfNull(connections);
        _connections = connections;
    }

    public async Task<IReadOnlyList<EntrySummary>> ListSummariesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SummaryColumns} FROM entry";

        var result = new List<EntrySummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadSummary(reader));
        }

        return result;
    }

    public async Task<EntrySummary?> GetSummaryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SummaryColumns} FROM entry WHERE id = $id";
        command.Parameters.AddWithValue("$id", IdText(id));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadSummary(reader) : null;
    }

    public async Task<EntrySecret?> GetSecretAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT password, notes FROM entry WHERE id = $id";
        command.Parameters.AddWithValue("$id", IdText(id));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadSecret(reader, 0) : null;
    }

    public async Task<VaultEntry?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SummaryColumns}, password, notes FROM entry WHERE id = $id";
        command.Parameters.AddWithValue("$id", IdText(id));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var summary = ReadSummary(reader);
        return new VaultEntry(summary.Id, summary.Metadata, ReadSecret(reader, 10), summary.CreatedAt, summary.UpdatedAt);
    }

    public async Task AddAsync(VaultEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var metadata = EntryValidator.EnsureValid(entry.Metadata);
        var secret = EntryValidator.EnsureValid(entry.Secret);

        await InTransactionAsync(
            async (connection, transaction) =>
            {
                var vaultId = await ReadVaultIdAsync(connection, transaction, cancellationToken);
                await InsertEntryAsync(connection, transaction, vaultId, entry, metadata, secret, cancellationToken);
                await MarkDirtyAsync(connection, transaction, cancellationToken);
                return true;
            },
            cancellationToken);
    }

    public Task<bool> UpdateAsync(
        Guid id,
        EntryMetadata metadata,
        EntrySecret? newSecret,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var normalizedMetadata = EntryValidator.EnsureValid(metadata);
        var normalizedSecret = newSecret is null ? null : EntryValidator.EnsureValid(newSecret);

        return InTransactionAsync(
            async (connection, transaction) =>
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;

                // The secret group is only touched when a new secret was supplied.
                command.CommandText = """
                    UPDATE entry SET
                        title = $title,
                        username = $username,
                        email = $email,
                        url = $url,
                        d_tags = $dTags,
                        s_tags = $sTags,
                        importance = $importance,
                        metadata_revision = metadata_revision + 1,
                        password = CASE WHEN $replaceSecret = 1 THEN $password ELSE password END,
                        notes = CASE WHEN $replaceSecret = 1 THEN $notes ELSE notes END,
                        secret_revision = CASE WHEN $replaceSecret = 1 THEN secret_revision + 1 ELSE secret_revision END,
                        updated_at = $updated
                    WHERE id = $id
                    """;
                AddMetadataParameters(command, normalizedMetadata);
                command.Parameters.AddWithValue("$replaceSecret", normalizedSecret is null ? 0 : 1);
                command.Parameters.AddWithValue("$password", (object?)normalizedSecret?.Password ?? DBNull.Value);
                command.Parameters.AddWithValue("$notes", (object?)normalizedSecret?.Notes ?? DBNull.Value);
                command.Parameters.AddWithValue("$updated", TimeText(now));
                command.Parameters.AddWithValue("$id", IdText(id));

                if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
                {
                    return false;
                }

                await MarkDirtyAsync(connection, transaction, cancellationToken);
                return true;
            },
            cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        InTransactionAsync(
            async (connection, transaction) =>
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM entry WHERE id = $id";
                command.Parameters.AddWithValue("$id", IdText(id));

                if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
                {
                    return false;
                }

                await MarkDirtyAsync(connection, transaction, cancellationToken);
                return true;
            },
            cancellationToken);

    public async Task<bool> GetLocalChangedSinceReplicaAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT local_changed_since_replica FROM replica_state LIMIT 1";

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is not null && Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0;
    }

    public Task<bool> SeedOnceAsync(
        IReadOnlyList<VaultEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return InTransactionAsync(
            async (connection, transaction) =>
            {
                await using var check = connection.CreateCommand();
                check.Transaction = transaction;
                check.CommandText = "SELECT id, fake_data_seeded FROM vault LIMIT 1";

                string vaultId;
                await using (var reader = await check.ExecuteReaderAsync(cancellationToken))
                {
                    if (!await reader.ReadAsync(cancellationToken))
                    {
                        throw new InvalidOperationException("The vault row is missing.");
                    }

                    if (reader.GetInt32(1) != 0)
                    {
                        return false;
                    }

                    vaultId = reader.GetString(0);
                }

                foreach (var entry in entries)
                {
                    var metadata = EntryValidator.EnsureValid(entry.Metadata);
                    var secret = EntryValidator.EnsureValid(entry.Secret);
                    await InsertEntryAsync(connection, transaction, vaultId, entry, metadata, secret, cancellationToken);
                }

                // Seeding is not a user change, so the dirty flag stays as it is.
                await using var mark = connection.CreateCommand();
                mark.Transaction = transaction;
                mark.CommandText = "UPDATE vault SET fake_data_seeded = 1";
                await mark.ExecuteNonQueryAsync(cancellationToken);

                return true;
            },
            cancellationToken);
    }

    private async Task<T> InTransactionAsync<T>(
        Func<SqliteConnection, SqliteTransaction, Task<T>> work,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        // Disposing an uncommitted transaction rolls it back, so any exception leaves no partial change.
        var result = await work(connection, transaction);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static async Task<string> ReadVaultIdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id FROM vault LIMIT 1";

        return await command.ExecuteScalarAsync(cancellationToken) as string
            ?? throw new InvalidOperationException("The vault row is missing.");
    }

    private static async Task InsertEntryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string vaultId,
        VaultEntry entry,
        EntryMetadata metadata,
        EntrySecret secret,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO entry (
                id, vault_id, title, username, email, url, d_tags, s_tags, importance,
                password, notes, created_at, updated_at)
            VALUES (
                $id, $vaultId, $title, $username, $email, $url, $dTags, $sTags, $importance,
                $password, $notes, $created, $updated)
            """;
        command.Parameters.AddWithValue("$id", IdText(entry.Id));
        command.Parameters.AddWithValue("$vaultId", vaultId);
        AddMetadataParameters(command, metadata);
        command.Parameters.AddWithValue("$password", (object?)secret.Password ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)secret.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", TimeText(entry.CreatedAt));
        command.Parameters.AddWithValue("$updated", TimeText(entry.UpdatedAt));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddMetadataParameters(SqliteCommand command, EntryMetadata metadata)
    {
        command.Parameters.AddWithValue("$title", metadata.Title);
        command.Parameters.AddWithValue("$username", (object?)metadata.Username ?? DBNull.Value);
        command.Parameters.AddWithValue("$email", (object?)metadata.Email ?? DBNull.Value);
        command.Parameters.AddWithValue("$url", (object?)metadata.Url ?? DBNull.Value);
        command.Parameters.AddWithValue("$dTags", JsonSerializer.Serialize(metadata.DTags));
        command.Parameters.AddWithValue("$sTags", JsonSerializer.Serialize(metadata.STags));
        command.Parameters.AddWithValue("$importance", (object?)metadata.Importance ?? DBNull.Value);
    }

    private static async Task MarkDirtyAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE replica_state SET local_changed_since_replica = 1";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static EntrySummary ReadSummary(DbDataReader reader)
    {
        var metadata = new EntryMetadata(
            reader.GetString(1),
            OptionalString(reader, 2),
            OptionalString(reader, 3),
            OptionalString(reader, 4),
            ReadTags(reader.GetString(5)),
            ReadTags(reader.GetString(6)),
            reader.IsDBNull(7) ? null : reader.GetInt32(7));

        return new EntrySummary(
            Guid.Parse(reader.GetString(0)),
            metadata,
            ParseTime(reader.GetString(8)),
            ParseTime(reader.GetString(9)));
    }

    private static EntrySecret ReadSecret(DbDataReader reader, int firstColumn) =>
        new(OptionalString(reader, firstColumn), OptionalString(reader, firstColumn + 1));

    private static string? OptionalString(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static List<string> ReadTags(string json) =>
        JsonSerializer.Deserialize<List<string>>(json) ?? [];

    private static string IdText(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);

    private static string TimeText(DateTimeOffset time) =>
        time.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTime(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
