using KeyHolder.Infrastructure.Persistence;

namespace KeyHolder.IntegrationTests.Persistence;

/// <summary>
/// A throwaway working database in a temp folder, deleted on dispose.
/// </summary>
internal sealed class TempVault : IDisposable
{
    private readonly string _directory;

    public TempVault()
    {
        _directory = Path.Combine(Path.GetTempPath(), "KeyHolderTests", Guid.NewGuid().ToString("N"));
        Paths = VaultPaths.ForDirectory(_directory);
    }

    public VaultPaths Paths { get; }

    /// <summary>
    /// Creates a new factory for the same file, like an app restart.
    /// </summary>
    public SqliteConnectionFactory CreateFactory() => new(Paths);

    public SqliteEntryRepository CreateRepository(SqliteConnectionFactory? factory = null) =>
        new(factory ?? CreateFactory());

    /// <summary>
    /// Migrates the database and returns a repository.
    /// </summary>
    public SqliteEntryRepository CreateMigratedRepository()
    {
        var factory = CreateFactory();
        new SchemaMigrator(factory).Migrate();
        return CreateRepository(factory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
