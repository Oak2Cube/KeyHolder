using Microsoft.Data.Sqlite;

namespace KeyHolder.Infrastructure.Persistence;

/// <summary>
/// Adds the color of tags. One color is stored per tag name and tag list (<c>d</c> or <c>s</c>) in a vault.
/// <c>name_key</c> is the case-insensitive key made by the app (so non-ASCII letters work too);
/// <c>name</c> keeps the spelling the user typed last. Tags without a color have no row.
/// Tag colors are metadata, so Milestone 2 moves them into the encrypted metadata.
/// </summary>
internal sealed class TagColorMigration : IMigration
{
    public int Version => 2;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var create = connection.CreateCommand();
        create.Transaction = transaction;
        create.CommandText = """
            CREATE TABLE tag_color (
                vault_id TEXT NOT NULL REFERENCES vault (id),
                kind     TEXT NOT NULL CHECK (kind IN ('d', 's')),
                name_key TEXT NOT NULL CHECK (length(name_key) > 0),
                name     TEXT NOT NULL,
                color    TEXT NOT NULL,
                PRIMARY KEY (vault_id, kind, name_key)
            );
            """;
        create.ExecuteNonQuery();
    }
}
