using KeyHolder.Domain.Entries;

namespace KeyHolder.Domain.Query;

/// <summary>
/// Filters entries by d-tags and s-tags.
/// Within one tag list an entry matches when it has ANY selected tag.
/// Between the two lists both must match (AND). An empty list does not filter.
/// Tag comparison ignores case.
/// </summary>
public sealed class EntryFilter
{
    public static EntryFilter None { get; } = new();

    public EntryFilter(IEnumerable<string>? dTags = null, IEnumerable<string>? sTags = null)
    {
        DTags = EntryValidator.NormalizeTags(dTags).ToHashSet(StringComparer.OrdinalIgnoreCase);
        STags = EntryValidator.NormalizeTags(sTags).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlySet<string> DTags { get; }

    public IReadOnlySet<string> STags { get; }

    public bool IsEmpty => DTags.Count == 0 && STags.Count == 0;

    public bool Matches(EntrySummary entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return MatchesAny(DTags, entry.Metadata.DTags) && MatchesAny(STags, entry.Metadata.STags);
    }

    private static bool MatchesAny(IReadOnlySet<string> selected, IReadOnlyList<string> entryTags) =>
        selected.Count == 0 || entryTags.Any(selected.Contains);
}
