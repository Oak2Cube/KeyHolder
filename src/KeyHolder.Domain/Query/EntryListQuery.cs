using KeyHolder.Domain.Entries;

namespace KeyHolder.Domain.Query;

/// <summary>
/// Search, filter, and sort over entry summaries. It never sees secrets.
/// </summary>
public sealed record EntryListQuery
{
    public string SearchText { get; init; } = string.Empty;

    public SearchFieldSelection SearchFields { get; init; } = SearchFieldSelection.Default;

    public EntryFilter Filter { get; init; } = EntryFilter.None;

    public SortKey SortKey { get; init; } = SortKey.Name;

    public SortDirection SortDirection { get; init; } = SortDirection.Ascending;

    /// <summary>
    /// Searches the chosen fields, then filters by tags, then sorts.
    /// Search splits the text on whitespace; every term must be found in at least one chosen field.
    /// Ties are broken by title (ignoring case), then by id, so the order is always stable.
    /// </summary>
    public IReadOnlyList<EntrySummary> Apply(IEnumerable<EntrySummary> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var terms = (SearchText ?? string.Empty).Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var matching = entries.Where(entry => Filter.Matches(entry) && MatchesSearch(entry, terms));

        return Sort(matching).ToList();
    }

    /// <summary>
    /// Collects the distinct tags used by the entries, sorted ignoring case, for building filter lists.
    /// </summary>
    public static (IReadOnlyList<string> DTags, IReadOnlyList<string> STags) CollectTags(
        IEnumerable<EntrySummary> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var all = entries.ToList();
        return (
            Distinct(all.SelectMany(entry => entry.Metadata.DTags)),
            Distinct(all.SelectMany(entry => entry.Metadata.STags)));

        static List<string> Distinct(IEnumerable<string> tags) =>
            tags.GroupBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    private bool MatchesSearch(EntrySummary entry, string[] terms) =>
        terms.All(term => SearchFields.Fields.Any(field => FieldContains(entry.Metadata, field, term)));

    private static bool FieldContains(EntryMetadata metadata, SearchField field, string term) =>
        field switch
        {
            SearchField.Title => Contains(metadata.Title, term),
            SearchField.Username => Contains(metadata.Username, term),
            SearchField.Email => Contains(metadata.Email, term),
            SearchField.Url => Contains(metadata.Url, term),
            SearchField.DTags => metadata.DTags.Any(tag => Contains(tag, term)),
            SearchField.STags => metadata.STags.Any(tag => Contains(tag, term)),
            _ => false,
        };

    private static bool Contains(string? value, string term) =>
        value is not null && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private IOrderedEnumerable<EntrySummary> Sort(IEnumerable<EntrySummary> entries)
    {
        var descending = SortDirection == SortDirection.Descending;

        var primary = SortKey switch
        {
            SortKey.Date => descending
                ? entries.OrderByDescending(entry => entry.UpdatedAt)
                : entries.OrderBy(entry => entry.UpdatedAt),

            // Entries without importance are always last, whichever direction is chosen.
            SortKey.Importance => entries
                .OrderBy(entry => entry.Metadata.Importance is null)
                .ThenBy(
                    entry => descending ? -(entry.Metadata.Importance ?? 0) : (entry.Metadata.Importance ?? 0)),

            _ => descending
                ? entries.OrderByDescending(entry => entry.Metadata.Title, StringComparer.OrdinalIgnoreCase)
                : entries.OrderBy(entry => entry.Metadata.Title, StringComparer.OrdinalIgnoreCase),
        };

        return primary
            .ThenBy(entry => entry.Metadata.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id);
    }
}
