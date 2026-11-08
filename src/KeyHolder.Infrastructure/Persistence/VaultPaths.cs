namespace KeyHolder.Infrastructure.Persistence;

/// <summary>
/// Where the working vault database lives.
/// </summary>
public sealed record VaultPaths(string WorkingDatabasePath)
{
    public const string WorkingDatabaseFileName = "working.sqlite";

    /// <summary>
    /// <c>%LOCALAPPDATA%\KeyHolder\working.sqlite</c>.
    /// </summary>
    public static VaultPaths Default() =>
        ForDirectory(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KeyHolder"));

    public static VaultPaths ForDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        return new VaultPaths(Path.Combine(directory, WorkingDatabaseFileName));
    }
}
