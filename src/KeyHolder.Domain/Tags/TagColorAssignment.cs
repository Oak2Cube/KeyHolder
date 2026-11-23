using KeyHolder.Domain.Entries;

namespace KeyHolder.Domain.Tags;

/// <summary>
/// The color of one tag. One color is stored per tag name and tag list, shared by all entries.
/// Tag names are compared ignoring case.
/// </summary>
public sealed record TagColorAssignment(TagKind Kind, string Name, TagColor Color);

/// <summary>
/// Reads the stored tag colors.
/// </summary>
public interface ITagColorStore
{
    /// <summary>
    /// Lists every stored tag color. Tags with the <see cref="TagColor.Default"/> color are not stored.
    /// </summary>
    Task<IReadOnlyList<TagColorAssignment>> ListTagColorsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Cleans a set of color changes before it is stored.
/// </summary>
public static class TagColorAssignments
{
    /// <summary>
    /// Normalizes names, rejects invalid names, and keeps only the last assignment per tag.
    /// </summary>
    /// <exception cref="EntryValidationException">A tag name is invalid.</exception>
    public static IReadOnlyList<TagColorAssignment> Normalize(IEnumerable<TagColorAssignment>? assignments)
    {
        if (assignments is null)
        {
            return [];
        }

        var latest = new Dictionary<(TagKind Kind, string Key), TagColorAssignment>();
        var errors = new List<ValidationError>();

        foreach (var assignment in assignments)
        {
            var name = EntryValidator.NormalizeTag(assignment.Name);
            var error = EntryValidator.ValidateNewTag(name, []);
            if (error is not null)
            {
                errors.Add(new ValidationError("TagColors", error));
                continue;
            }

            if (!Enum.IsDefined(assignment.Color))
            {
                errors.Add(new ValidationError("TagColors", "The tag color is not supported."));
                continue;
            }

            latest[(assignment.Kind, TagColors.KeyOf(name))] = assignment with { Name = name };
        }

        return errors.Count > 0 ? throw new EntryValidationException(errors) : [.. latest.Values];
    }
}

/// <summary>
/// Case-insensitive lookup of tag colors by list and name.
/// </summary>
public sealed class TagColorLookup
{
    private readonly Dictionary<(TagKind Kind, string Name), TagColor> _colors = [];

    public static TagColorLookup Empty { get; } = new([]);

    public TagColorLookup(IEnumerable<TagColorAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        foreach (var assignment in assignments)
        {
            Set(assignment.Kind, assignment.Name, assignment.Color);
        }
    }

    /// <summary>
    /// The color of a tag, or <see cref="TagColor.Default"/> when it has none.
    /// </summary>
    public TagColor Get(TagKind kind, string name) =>
        _colors.TryGetValue((kind, TagColors.KeyOf(name)), out var color) ? color : TagColor.Default;

    private void Set(TagKind kind, string name, TagColor color)
    {
        var key = (kind, TagColors.KeyOf(name));
        if (color == TagColor.Default)
        {
            _colors.Remove(key);
        }
        else
        {
            _colors[key] = color;
        }
    }
}
