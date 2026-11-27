namespace KeyHolder.Domain.Entries;

/// <summary>
/// The fixed set of built-in icons an entry can show. The UI draws each value as a vector path;
/// users cannot upload their own icons.
/// </summary>
public enum EntryIcon
{
    /// <summary>The default key icon. Not stored in the database.</summary>
    Default = 0,
    Key,
    Mail,
    Globe,
    Bank,
    Card,
    Shield,
    Lock,
    User,
    Phone,
    Cloud,
    Server,
    Wifi,
    Game,
    Cart,
    Work,
    Home,
    Health,
    Travel,
    Social,
    Code,
    Doc,
    Camera,
    Star,
    Folder,
}

/// <summary>
/// Helpers for reading and writing <see cref="EntryIcon"/> values from storage.
/// </summary>
public static class EntryIcons
{
    /// <summary>
    /// Reads a stored icon name. Missing or unknown values become <see cref="EntryIcon.Default"/>,
    /// so a newer database never crashes an older app.
    /// </summary>
    public static EntryIcon Parse(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && !char.IsDigit(text.Trim()[0])
        && Enum.TryParse<EntryIcon>(text.Trim(), ignoreCase: true, out var icon)
        && Enum.IsDefined(icon)
            ? icon
            : EntryIcon.Default;

    /// <summary>
    /// The text stored in the database for an icon. <see cref="EntryIcon.Default"/> is not written.
    /// </summary>
    public static string ToStorageText(EntryIcon icon) => icon.ToString();
}
