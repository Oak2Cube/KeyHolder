using KeyHolder.Domain.Entries;

namespace KeyHolder.Infrastructure.FakeData;

/// <summary>
/// A store that can receive the one-time fake demo data without marking the vault as changed.
/// </summary>
public interface IFakeDataStore
{
    /// <summary>
    /// Adds the entries only if fake data was never added before.
    /// The seeded marker is saved in the same transaction. This does not set the dirty state.
    /// </summary>
    /// <returns><see langword="true"/> when the entries were added.</returns>
    Task<bool> SeedOnceAsync(IReadOnlyList<VaultEntry> entries, CancellationToken cancellationToken = default);
}
