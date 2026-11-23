namespace KeyHolder.Domain.Tags;

/// <summary>
/// Which of the two tag lists a tag belongs to.
/// </summary>
public enum TagKind
{
    /// <summary>d-tags: the type of data (shown as "Record type").</summary>
    Data,

    /// <summary>s-tags: the category or risk (shown as "Security category").</summary>
    Security,
}

/// <summary>
/// The fixed set of colors a tag can have. The UI decides the exact shade; the domain only stores the name.
/// </summary>
public enum TagColor
{
    /// <summary>The neutral text color. A tag with this color has no stored color.</summary>
    Default,
    Red,
    Orange,
    Yellow,
    Green,
    Teal,
    Blue,
    Purple,
    Pink,
    Gray,
}

/// <summary>
/// Helpers for reading and writing <see cref="TagColor"/> values from storage.
/// </summary>
public static class TagColors
{
    /// <summary>
    /// Reads a stored color name. Missing, unknown, or numeric values become <see cref="TagColor.Default"/>,
    /// so a newer database never crashes an older app.
    /// </summary>
    public static TagColor Parse(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && !char.IsDigit(text.Trim()[0])
        && Enum.TryParse<TagColor>(text.Trim(), ignoreCase: true, out var color)
        && Enum.IsDefined(color)
            ? color
            : TagColor.Default;

    /// <summary>
    /// The case-insensitive key of a tag name. Every store uses this same key, so names that differ only in
    /// case (including non-ASCII letters) always share one color.
    /// </summary>
    public static string KeyOf(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// The text stored in the database for a color.
    /// </summary>
    public static string ToStorageText(TagColor color) => color.ToString();

    /// <summary>
    /// The single-letter kind code stored in the database: <c>d</c> or <c>s</c>.
    /// </summary>
    public static string ToKindCode(TagKind kind) => kind == TagKind.Data ? "d" : "s";

    /// <summary>
    /// Reads a kind code from the database. Returns <see langword="null"/> for anything but <c>d</c> or <c>s</c>.
    /// </summary>
    public static TagKind? ParseKindCode(string? code) => code switch
    {
        "d" => TagKind.Data,
        "s" => TagKind.Security,
        _ => null,
    };
}
