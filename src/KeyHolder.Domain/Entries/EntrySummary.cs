namespace KeyHolder.Domain.Entries;

/// <summary>
/// An entry without its secret part. Lists, search, filter, and sort use only summaries.
/// </summary>
public sealed record EntrySummary(
    Guid Id,
    EntryMetadata Metadata,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
