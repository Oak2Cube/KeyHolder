namespace KeyHolder.Domain.Entries;

/// <summary>
/// Stores vault entries. The UI depends on this interface only, so the storage format
/// (plain columns now, encrypted envelopes later) never leaks into view models.
/// </summary>
public interface IEntryRepository
{
    /// <summary>
    /// Lists every entry without secrets. This is the only list read path.
    /// </summary>
    Task<IReadOnlyList<EntrySummary>> ListSummariesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one entry without its secret, or <see langword="null"/> when it does not exist.
    /// </summary>
    Task<EntrySummary?> GetSummaryAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the secret of exactly one entry, or <see langword="null"/> when it does not exist.
    /// </summary>
    Task<EntrySecret?> GetSecretAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a complete entry including its secret, or <see langword="null"/> when it does not exist.
    /// </summary>
    Task<VaultEntry?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entry.
    /// </summary>
    /// <exception cref="EntryValidationException">The entry is invalid.</exception>
    Task AddAsync(VaultEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the metadata of an entry. The secret is replaced only when <paramref name="newSecret"/> is not
    /// <see langword="null"/>; otherwise the stored secret stays unchanged.
    /// </summary>
    /// <returns><see langword="false"/> when the entry does not exist.</returns>
    /// <exception cref="EntryValidationException">The metadata or secret is invalid.</exception>
    Task<bool> UpdateAsync(
        Guid id,
        EntryMetadata metadata,
        EntrySecret? newSecret,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes an entry.
    /// </summary>
    /// <returns><see langword="false"/> when the entry does not exist.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
