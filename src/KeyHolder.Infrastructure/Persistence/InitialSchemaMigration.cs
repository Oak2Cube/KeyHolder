using System.Globalization;
using Microsoft.Data.Sqlite;

namespace KeyHolder.Infrastructure.Persistence;

/// <summary>
/// Milestone 1 schema. Metadata and secret fields are plain columns in separate groups
/// (with separate revisions) so Milestone 2 can replace them with
/// <c>metadata_envelope</c> and <c>secret_envelope</c> in a later migration.
/// This schema holds FAKE data only.
/// </summary>
internal sealed class InitialSchemaMigration : IMigration
{
    public int Version => 1;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var create = connection.CreateCommand();
        create.Transaction = transaction;
        create.CommandText = """
            CREATE TABLE vault (
                id               TEXT PRIMARY KEY NOT NULL,
                created_at       TEXT NOT NULL,
                fake_data_seeded INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE entry (
                id                TEXT PRIMARY KEY NOT NULL,
                vault_id          TEXT NOT NULL REFERENCES vault (id),

                -- metadata group
                title             TEXT NOT NULL CHECK (length(trim(title)) > 0),
                username          TEXT,
                email             TEXT,
                url               TEXT,
                d_tags            TEXT NOT NULL DEFAULT '[]',
                s_tags            TEXT NOT NULL DEFAULT '[]',
                importance        INTEGER,
                metadata_revision INTEGER NOT NULL DEFAULT 1,

                -- secret group
                password          TEXT,
                notes             TEXT,
                secret_revision   INTEGER NOT NULL DEFAULT 1,

                created_at        TEXT NOT NULL,
                updated_at        TEXT NOT NULL
            );

            CREATE INDEX ix_entry_vault ON entry (vault_id);

            CREATE TABLE replica_state (
                vault_id                    TEXT PRIMARY KEY NOT NULL REFERENCES vault (id),
                replica_path                TEXT,
                last_exported_at            TEXT,
                local_changed_since_replica INTEGER NOT NULL DEFAULT 0
            );
            """;
        create.ExecuteNonQuery();

        var vaultId = Guid.NewGuid().ToString("D");
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        using var insertVault = connection.CreateCommand();
        insertVault.Transaction = transaction;
        insertVault.CommandText = "INSERT INTO vault (id, created_at) VALUES ($id, $created)";
        insertVault.Parameters.AddWithValue("$id", vaultId);
        insertVault.Parameters.AddWithValue("$created", now);
        insertVault.ExecuteNonQuery();

        using var insertState = connection.CreateCommand();
        insertState.Transaction = transaction;
        insertState.CommandText = "INSERT INTO replica_state (vault_id) VALUES ($id)";
        insertState.Parameters.AddWithValue("$id", vaultId);
        insertState.ExecuteNonQuery();
    }
}
