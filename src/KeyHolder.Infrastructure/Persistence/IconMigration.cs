using Microsoft.Data.Sqlite;

namespace KeyHolder.Infrastructure.Persistence;

/// <summary>
/// Adds the built-in icon and icon tint on each entry. NULL means the default key icon
/// and the neutral (Default) color. Icon and color are metadata, so Milestone 2 moves them
/// into the encrypted metadata envelope.
/// </summary>
internal sealed class IconMigration : IMigration
{
    public int Version => 3;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using (var icon = connection.CreateCommand())
        {
            icon.Transaction = transaction;
            icon.CommandText = "ALTER TABLE entry ADD COLUMN icon TEXT";
            icon.ExecuteNonQuery();
        }

        using var color = connection.CreateCommand();
        color.Transaction = transaction;
        color.CommandText = "ALTER TABLE entry ADD COLUMN icon_color TEXT";
        color.ExecuteNonQuery();
    }
}
