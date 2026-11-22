namespace KeyHolder.Domain.Entries;

/// <summary>
/// An entry without its secret part. Lists, search, filter, and sort use only summaries.
/// </summary>
public sealed record EntrySummary(
    Guid Id,
    EntryMetadata Metadata,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>
    /// Whether the entry has a password. Only the presence is stored here, never the value.
    /// </summary>
    public bool HasPassword { get; init; }

    /// <summary>
    /// Whether the entry has notes. Only the presence is stored here, never the value.
    /// </summary>
    public bool HasNotes { get; init; }
}
