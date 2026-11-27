using System.Windows.Media;
using KeyHolder.Domain.Entries;

namespace KeyHolder.Desktop.Icons;

/// <summary>
/// One choice in the icon picker: the enum value, a short display name, and the frozen geometry.
/// </summary>
public sealed record EntryIconOption(EntryIcon Icon, string Name, Geometry Geometry);

/// <summary>
/// Built-in vector icons drawn on a 24×24 grid. Users cannot upload their own icons.
/// </summary>
public static class IconCatalog
{
    private static readonly Dictionary<EntryIcon, Geometry> Geometries = Build();

    /// <summary>
    /// Every icon in picker order, including Default (the key icon).
    /// </summary>
    public static IReadOnlyList<EntryIconOption> Options { get; } =
        [.. Enum.GetValues<EntryIcon>().Select(icon => new EntryIconOption(icon, DisplayName(icon), Geometries[icon]))];

    /// <summary>
    /// The frozen geometry for an icon. Unknown values use the default key.
    /// </summary>
    public static Geometry GetGeometry(EntryIcon icon) =>
        Geometries.TryGetValue(icon, out var geometry) ? geometry : Geometries[EntryIcon.Default];

    private static string DisplayName(EntryIcon icon) => icon switch
    {
        EntryIcon.Default => "Key (default)",
        _ => icon.ToString(),
    };

    private static Dictionary<EntryIcon, Geometry> Build()
    {
        // Simple filled silhouettes on a 24×24 canvas. Default and Key share the same path.
        var key = Freeze("M7,14 A4,4 0 1 1 7,6 A4,4 0 1 1 7,14 M11,10 L21,10 L21,13 L18,13 L18,16 L15,16 L15,13 L11,13 Z");
        var map = new Dictionary<EntryIcon, Geometry>
        {
            [EntryIcon.Default] = key,
            [EntryIcon.Key] = key,
            [EntryIcon.Mail] = Freeze("M2,6 L22,6 L22,18 L2,18 Z M2,6 L12,13 L22,6"),
            [EntryIcon.Globe] = Freeze("M12,2 A10,10 0 1 0 12,22 A10,10 0 1 0 12,2 M2,12 L22,12 M12,2 C8,6 8,18 12,22 C16,18 16,6 12,2"),
            [EntryIcon.Bank] = Freeze("M2,10 L12,4 L22,10 L22,12 L2,12 Z M4,12 L4,18 L6,18 L6,12 Z M10,12 L10,18 L14,18 L14,12 Z M18,12 L18,18 L20,18 L20,12 Z M2,18 L22,18 L22,20 L2,20 Z"),
            [EntryIcon.Card] = Freeze("M2,6 L22,6 L22,18 L2,18 Z M2,10 L22,10 M4,14 L10,14 L10,16 L4,16 Z"),
            [EntryIcon.Shield] = Freeze("M12,2 L20,5 L20,11 C20,16 16,20 12,22 C8,20 4,16 4,11 L4,5 Z"),
            [EntryIcon.Lock] = Freeze("M7,10 L7,7 A5,5 0 0 1 17,7 L17,10 M5,10 L19,10 L19,20 L5,20 Z M12,13 L12,17"),
            [EntryIcon.User] = Freeze("M12,12 A4,4 0 1 0 12,4 A4,4 0 1 0 12,12 M4,20 C4,16 8,14 12,14 C16,14 20,16 20,20 L4,20"),
            [EntryIcon.Phone] = Freeze("M7,2 L17,2 L17,22 L7,22 Z M10,19 L14,19"),
            [EntryIcon.Cloud] = Freeze("M6,18 C3,18 2,16 2,14 C2,12 4,10 6,10 C6,7 8,5 12,5 C15,5 17,7 18,9 C20,9 22,11 22,13 C22,16 20,18 17,18 Z"),
            [EntryIcon.Server] = Freeze("M3,4 L21,4 L21,9 L3,9 Z M3,11 L21,11 L21,16 L3,16 Z M3,18 L21,18 L21,22 L3,22 Z M5,6.5 L7,6.5 M5,13.5 L7,13.5 M5,20 L7,20"),
            [EntryIcon.Wifi] = Freeze("M12,18 A1.5,1.5 0 1 0 12,21 A1.5,1.5 0 1 0 12,18 M5,14 C7,12 17,12 19,14 M2,10 C6,6 18,6 22,10 M8,16 C10,14.5 14,14.5 16,16"),
            [EntryIcon.Game] = Freeze("M4,10 C4,8 6,7 8,7 L16,7 C18,7 20,8 20,10 L21,15 C21,17 19,18 17,18 L15,18 L13,16 L11,16 L9,18 L7,18 C5,18 3,17 3,15 Z M8,11 L8,13 M7,12 L9,12 M15,11 L17,13 M17,11 L15,13"),
            [EntryIcon.Cart] = Freeze("M3,4 L5,4 L7,14 L19,14 L21,6 L6,6 M8,18 A1.5,1.5 0 1 0 8,21 A1.5,1.5 0 1 0 8,18 M17,18 A1.5,1.5 0 1 0 17,21 A1.5,1.5 0 1 0 17,18"),
            [EntryIcon.Work] = Freeze("M8,7 L8,5 L16,5 L16,7 M3,7 L21,7 L21,19 L3,19 Z M3,11 L21,11"),
            [EntryIcon.Home] = Freeze("M3,11 L12,3 L21,11 L21,20 L14,20 L14,14 L10,14 L10,20 L3,20 Z"),
            [EntryIcon.Health] = Freeze("M12,21 C12,21 3,15 3,9 C3,6 5,4 8,4 C10,4 11.5,5 12,6.5 C12.5,5 14,4 16,4 C19,4 21,6 21,9 C21,15 12,21 12,21"),
            [EntryIcon.Travel] = Freeze("M10,2 L14,2 L14,10 L22,14 L22,16 L14,14 L14,20 L17,22 L17,23 L12,21.5 L7,23 L7,22 L10,20 L10,14 L2,16 L2,14 L10,10 Z"),
            [EntryIcon.Social] = Freeze("M8,10 A3,3 0 1 0 8,4 A3,3 0 1 0 8,10 M16,11 A2.5,2.5 0 1 0 16,6 A2.5,2.5 0 1 0 16,11 M2,18 C2,15 5,13 8,13 C11,13 14,15 14,18 M13,18 C13,16 14.5,14.5 16,14.5 C18,14.5 20,16 20,18 L13,18 L2,18"),
            [EntryIcon.Code] = Freeze("M8,6 L3,12 L8,18 M16,6 L21,12 L16,18 M13,4 L11,20"),
            [EntryIcon.Doc] = Freeze("M6,2 L14,2 L18,6 L18,22 L6,22 Z M14,2 L14,6 L18,6 M8,10 L16,10 M8,14 L16,14 M8,18 L13,18"),
            [EntryIcon.Camera] = Freeze("M4,7 L8,7 L9,5 L15,5 L16,7 L20,7 L20,19 L4,19 Z M12,10 A3.5,3.5 0 1 0 12,17 A3.5,3.5 0 1 0 12,10"),
            [EntryIcon.Star] = Freeze("M12,2 L14.5,9 L22,9 L16,14 L18.5,21 L12,17 L5.5,21 L8,14 L2,9 L9.5,9 Z"),
            [EntryIcon.Folder] = Freeze("M2,6 L9,6 L11,8 L22,8 L22,20 L2,20 Z"),
        };

        if (map.Count != Enum.GetValues<EntryIcon>().Length)
        {
            throw new InvalidOperationException("IconCatalog is missing a geometry for at least one EntryIcon.");
        }

        return map;
    }

    private static Geometry Freeze(string pathData)
    {
        var geometry = Geometry.Parse(pathData);
        geometry.Freeze();
        return geometry;
    }
}
