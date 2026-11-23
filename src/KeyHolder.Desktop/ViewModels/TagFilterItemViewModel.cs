using System.Windows.Media;
using KeyHolder.Desktop.Mvvm;

namespace KeyHolder.Desktop.ViewModels;

/// <summary>
/// One selectable tag in a filter list.
/// </summary>
public sealed class TagFilterItemViewModel : ViewModelBase
{
    private bool _isSelected;

    public TagFilterItemViewModel(string name, Brush brush, bool isSelected, EventHandler selectionChanged)
    {
        Name = name;
        Brush = brush;
        _isSelected = isSelected;
        SelectionChanged = selectionChanged;
    }

    public string Name { get; }

    /// <summary>
    /// The tag color, used as the text color of the checkbox label.
    /// </summary>
    public Brush Brush { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                SelectionChanged(this, EventArgs.Empty);
            }
        }
    }

    private EventHandler SelectionChanged { get; }
}
