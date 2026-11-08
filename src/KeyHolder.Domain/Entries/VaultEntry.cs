namespace KeyHolder.Domain.Entries;

/// <summary>
/// A complete entry including its secret part.
/// </summary>
public sealed record VaultEntry(
    Guid Id,
    EntryMetadata Metadata,
    EntrySecret Secret,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>
    /// Creates a validated, normalized entry.
    /// </summary>
    /// <exception cref="EntryValidationException">The metadata or secret is invalid.</exception>
    public static VaultEntry Create(
        EntryMetadata metadata,
        EntrySecret secret,
        DateTimeOffset now,
        Guid? id = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(secret);

        var normalizedMetadata = EntryValidator.EnsureValid(metadata);
        var normalizedSecret = EntryValidator.EnsureValid(secret);

        return new VaultEntry(
            id ?? Guid.NewGuid(),
            normalizedMetadata,
            normalizedSecret,
            now,
            now);
    }

    public EntrySummary ToSummary() => new(Id, Metadata, CreatedAt, UpdatedAt);
}
