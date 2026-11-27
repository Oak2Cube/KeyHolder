using System.Windows.Media;
using KeyHolder.Domain.Entries;
using KeyHolder.Domain.Query;
using KeyHolder.Domain.Tags;

namespace KeyHolder.Desktop.Tags;

/// <summary>
/// A tag text with the brush it is drawn in. Used wherever a tag is shown: list lines, detail chips,
/// filter lists, and the editor.
/// </summary>
public sealed record TagDisplay(string Name, Brush Brush)
{
    public TagColor Color { get; init; }
}

/// <summary>
/// A snapshot of the tags that exist in the vault and their colors. Built after every load, so a color change
/// shows everywhere at once. Only tags still used by at least one entry appear in the catalog.
/// </summary>
public sealed class TagCatalog
{
    private readonly TagColorLookup _colors;
    private readonly IReadOnlyList<string> _dTags;
    private readonly IReadOnlyList<string> _sTags;

    public static TagCatalog Empty { get; } = new([], [], []);

    public TagCatalog(
        IEnumerable<TagColorAssignment> colors,
        IReadOnlyList<string> dTags,
        IReadOnlyList<string> sTags)
    {
        ArgumentNullException.ThrowIfNull(colors);
        ArgumentNullException.ThrowIfNull(dTags);
        ArgumentNullException.ThrowIfNull(sTags);

        _colors = new TagColorLookup(colors);
        _dTags = DistinctSorted(dTags);
        _sTags = DistinctSorted(sTags);
    }

    /// <summary>
    /// Builds the catalog from the current entries and the stored colors.
    /// </summary>
    public static TagCatalog Create(IEnumerable<EntrySummary> summaries, IEnumerable<TagColorAssignment> colors)
    {
        ArgumentNullException.ThrowIfNull(summaries);
        var (dTags, sTags) = EntryListQuery.CollectTags(summaries);
        return new TagCatalog(colors, dTags, sTags);
    }

    /// <summary>
    /// Every tag of a list that at least one entry still uses, sorted, without duplicates ignoring case.
    /// </summary>
    public IReadOnlyList<string> KnownTags(TagKind kind) => kind == TagKind.Data ? _dTags : _sTags;

    public TagColor GetColor(TagKind kind, string name) => _colors.Get(kind, name);

    public TagDisplay GetDisplay(TagKind kind, string name)
    {
        var color = GetColor(kind, name);
        return new TagDisplay(name, TagColorPalette.GetBrush(color)) { Color = color };
    }

    public IReadOnlyList<TagDisplay> GetDisplays(TagKind kind, IEnumerable<string> names) =>
        [.. names.Select(name => GetDisplay(kind, name))];

    private static List<string> DistinctSorted(IReadOnlyList<string> names) =>
        [.. names
            .GroupBy(TagColors.KeyOf)
            .Select(group => group.First())
            .Order(StringComparer.OrdinalIgnoreCase)];
}
