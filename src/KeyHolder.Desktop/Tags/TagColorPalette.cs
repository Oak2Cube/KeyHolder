using System.Windows.Media;
using KeyHolder.Domain.Tags;

namespace KeyHolder.Desktop.Tags;

/// <summary>
/// One choice in the color dropdown: the color, its display name, and the frozen brush for the swatch and the text.
/// </summary>
public sealed record TagColorOption(TagColor Color, string Name, Brush Brush);

/// <summary>
/// Maps the domain's <see cref="TagColor"/> names to brushes that are readable on the dark theme.
/// The domain knows only the names; shades live here.
/// </summary>
public static class TagColorPalette
{
    private static readonly Dictionary<TagColor, Brush> Brushes = new()
    {
        [TagColor.Default] = Create(0xF8, 0xFA, 0xFC),
        [TagColor.Red] = Create(0xF8, 0x71, 0x71),
        [TagColor.Orange] = Create(0xFB, 0x92, 0x3C),
        [TagColor.Yellow] = Create(0xFB, 0xBF, 0x24),
        [TagColor.Green] = Create(0x4A, 0xDE, 0x80),
        [TagColor.Teal] = Create(0x2D, 0xD4, 0xBF),
        [TagColor.Blue] = Create(0x60, 0xA5, 0xFA),
        [TagColor.Purple] = Create(0xC0, 0x84, 0xFC),
        [TagColor.Pink] = Create(0xF4, 0x72, 0xB6),
        [TagColor.Gray] = Create(0x94, 0xA3, 0xB8),
    };

    /// <summary>
    /// Every color in dropdown order, starting with Default.
    /// </summary>
    public static IReadOnlyList<TagColorOption> Options { get; } =
        [.. Enum.GetValues<TagColor>().Select(color => new TagColorOption(color, color.ToString(), Brushes[color]))];

    /// <summary>
    /// The text brush for a color. Unknown values use the Default brush.
    /// </summary>
    public static Brush GetBrush(TagColor color) =>
        Brushes.TryGetValue(color, out var brush) ? brush : Brushes[TagColor.Default];

    /// <summary>
    /// The dropdown option for a color.
    /// </summary>
    public static TagColorOption GetOption(TagColor color) =>
        Options.FirstOrDefault(option => option.Color == color) ?? Options[0];

    private static SolidColorBrush Create(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
