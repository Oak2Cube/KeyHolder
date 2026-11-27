using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeyHolder.Desktop.Icons;
using KeyHolder.Desktop.Tags;
using KeyHolder.Domain.Entries;
using KeyHolder.Domain.Tags;

namespace KeyHolder.Desktop.Views;

/// <summary>
/// Draws one built-in entry icon at a chosen size (default 40×40) in the chosen tint.
/// </summary>
public partial class EntryIconView : UserControl
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(EntryIcon),
        typeof(EntryIconView),
        new PropertyMetadata(EntryIcon.Default, OnAppearanceChanged));

    public static readonly DependencyProperty IconColorProperty = DependencyProperty.Register(
        nameof(IconColor),
        typeof(TagColor),
        typeof(EntryIconView),
        new PropertyMetadata(TagColor.Default, OnAppearanceChanged));

    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(
        nameof(IconSize),
        typeof(double),
        typeof(EntryIconView),
        new PropertyMetadata(40.0));

    public static readonly DependencyProperty GeometryProperty = DependencyProperty.Register(
        nameof(Geometry),
        typeof(Geometry),
        typeof(EntryIconView),
        new PropertyMetadata(IconCatalog.GetGeometry(EntryIcon.Default)));

    public static readonly DependencyProperty IconBrushProperty = DependencyProperty.Register(
        nameof(IconBrush),
        typeof(Brush),
        typeof(EntryIconView),
        new PropertyMetadata(TagColorPalette.GetBrush(TagColor.Default)));

    public EntryIconView()
    {
        InitializeComponent();
        RefreshAppearance();
    }

    public EntryIcon Icon
    {
        get => (EntryIcon)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public TagColor IconColor
    {
        get => (TagColor)GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
    }

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public Geometry Geometry
    {
        get => (Geometry)GetValue(GeometryProperty);
        private set => SetValue(GeometryProperty, value);
    }

    public Brush IconBrush
    {
        get => (Brush)GetValue(IconBrushProperty);
        private set => SetValue(IconBrushProperty, value);
    }

    private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EntryIconView view)
        {
            view.RefreshAppearance();
        }
    }

    private void RefreshAppearance()
    {
        Geometry = IconCatalog.GetGeometry(Icon);
        IconBrush = TagColorPalette.GetBrush(IconColor);
    }
}
