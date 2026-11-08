using System.Globalization;
using Microsoft.Data.Sqlite;

namespace KeyHolder.Infrastructure.Persistence;

/// <summary>
/// One schema step. Versions start at 1 and increase by exactly 1.
/// </summary>
internal interface IMigration
{
    int Version { get; }

    void Apply(SqliteConnection connection, SqliteTransaction transaction);
}

/// <summary>
/// Thrown when the database was written by a newer version of the app.
/// </summary>
public sealed class UnsupportedSchemaVersionException : Exception
{
    public UnsupportedSchemaVersionException()
        : base("The database schema version is not supported.")
    {
    }

    public UnsupportedSchemaVersionException(string message)
        : base(message)
    {
    }

    public UnsupportedSchemaVersionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Applies ordered schema migrations. The applied version is stored in <c>PRAGMA user_version</c>.
/// Each migration and its version bump run in one transaction, so a failed migration changes nothing.
/// </summary>
public sealed class SchemaMigrator
{
    private readonly SqliteConnectionFactory _connections;
    private readonly IReadOnlyList<IMigration> _migrations;

    public SchemaMigrator(SqliteConnectionFactory connections)
        : this(connections, DefaultMigrations)
    {
    }

    internal SchemaMigrator(SqliteConnectionFactory connections, IReadOnlyList<IMigration> migrations)
    {
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(migrations);

        for (var i = 0; i < migrations.Count; i++)
        {
            if (migrations[i].Version != i + 1)
            {
                throw new ArgumentException(
                    "Migrations must be numbered 1, 2, 3, ... without gaps.",
                    nameof(migrations));
            }
        }

        _connections = connections;
        _migrations = migrations;
    }

    internal static IReadOnlyList<IMigration> DefaultMigrations { get; } = [new InitialSchemaMigration()];

    /// <summary>
    /// The newest schema version this build understands.
    /// </summary>
    public int LatestVersion => _migrations.Count;

    /// <summary>
    /// Brings the database up to <see cref="LatestVersion"/>. Safe to call repeatedly.
    /// </summary>
    /// <exception cref="UnsupportedSchemaVersionException">The database is newer than this app.</exception>
    public void Migrate()
    {
        using var connection = _connections.Open();

        var current = ReadVersion(connection);
        if (current > LatestVersion)
        {
            throw new UnsupportedSchemaVersionException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The database has schema version {current}, but this app only supports up to {LatestVersion}."));
        }

        foreach (var migration in _migrations.Where(m => m.Version > current))
        {
            using var transaction = connection.BeginTransaction();

            migration.Apply(connection, transaction);

            using var setVersion = connection.CreateCommand();
            setVersion.Transaction = transaction;

            // PRAGMA does not accept parameters. The value is an int we control, not user input.
#pragma warning disable CA2100
            setVersion.CommandText = string.Create(
                CultureInfo.InvariantCulture,
                $"PRAGMA user_version = {migration.Version}");
#pragma warning restore CA2100
            setVersion.ExecuteNonQuery();

            transaction.Commit();
        }
    }

    /// <summary>
    /// Reads the schema version of the database.
    /// </summary>
    public int GetCurrentVersion()
    {
        using var connection = _connections.Open();
        return ReadVersion(connection);
    }

    private static int ReadVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
}
