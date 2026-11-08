namespace KeyHolder.Domain.Entries;

/// <summary>
/// Reports whether the working vault changed since the last durable replica export.
/// Clearing the flag belongs to Milestone 3 (export), so it is intentionally not exposed yet.
/// </summary>
public interface IVaultStateStore
{
    /// <summary>
    /// <see langword="true"/> when a successful local change happened after the last replica export.
    /// </summary>
    Task<bool> GetLocalChangedSinceReplicaAsync(CancellationToken cancellationToken = default);
}
