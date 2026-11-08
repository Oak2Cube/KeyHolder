using Microsoft.Data.Sqlite;

namespace KeyHolder.Infrastructure.Persistence;

/// <summary>
/// Opens connections to the working database. Pooling is off so the file is released
/// as soon as a connection closes (important for restart tests and for later file replacement).
/// </summary>
public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(VaultPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        DatabasePath = paths.WorkingDatabasePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            ForeignKeys = true,
        }.ToString();
    }

    public string DatabasePath { get; }

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        PrepareDirectory();

        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public SqliteConnection Open()
    {
        PrepareDirectory();

        var connection = new SqliteConnection(_connectionString);
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private void PrepareDirectory()
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(DatabasePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
