using KeyHolder.Desktop.Mvvm;

namespace KeyHolder.Desktop.ViewModels;

/// <summary>
/// One selectable tag in a filter list.
/// </summary>
public sealed class TagFilterItemViewModel : ViewModelBase
{
    private bool _isSelected;

    public TagFilterItemViewModel(string name, bool isSelected, EventHandler selectionChanged)
    {
        Name = name;
        _isSelected = isSelected;
        SelectionChanged = selectionChanged;
    }

    public string Name { get; }

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
