using KeyHolder.Desktop.Mvvm;
using KeyHolder.Domain.Query;

namespace KeyHolder.Desktop.ViewModels;

/// <summary>
/// One checkbox in the "search in" list.
/// </summary>
public sealed class SearchFieldOptionViewModel : ViewModelBase
{
    private readonly SearchOptionsViewModel _owner;
    private bool _isSelected;

    internal SearchFieldOptionViewModel(SearchOptionsViewModel owner, SearchField field, string label, bool isSelected)
    {
        _owner = owner;
        Field = field;
        Label = label;
        _isSelected = isSelected;
    }

    public SearchField Field { get; }

    public string Label { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                _owner.OnOptionChanged(this);
            }
        }
    }

    /// <summary>
    /// The last selected field cannot be switched off, because search needs at least one field.
    /// </summary>
    public bool CanToggle => !_isSelected || _owner.SelectedCount > SearchFieldSelection.MinFields;

    internal void RefreshCanToggle() => OnPropertyChanged(nameof(CanToggle));

    /// <summary>
    /// Puts the checkbox back to selected after a rejected change.
    /// </summary>
    internal void Restore()
    {
        _isSelected = true;
        OnPropertyChanged(nameof(IsSelected));
        OnPropertyChanged(nameof(CanToggle));
    }
}

/// <summary>
/// The customizable list of fields search looks in (1 to 6 of title, username, email, URL, d-tags, s-tags).
/// </summary>
public sealed class SearchOptionsViewModel : ViewModelBase
{
    private SearchFieldSelection _selection = SearchFieldSelection.Default;

    public SearchOptionsViewModel()
    {
        Options =
        [
            Create(SearchField.Title, "Title"),
            Create(SearchField.Username, "Username"),
            Create(SearchField.Email, "Email"),
            Create(SearchField.Url, "URL"),
            Create(SearchField.DTags, "d-tags (data)"),
            Create(SearchField.STags, "s-tags (security)"),
        ];
    }

    public event EventHandler? SelectionChanged;

    public IReadOnlyList<SearchFieldOptionViewModel> Options { get; }

    public SearchFieldSelection Selection => _selection;

    public int SelectedCount => Options.Count(option => option.IsSelected);

    public string Summary => string.Join(", ", Options.Where(option => option.IsSelected).Select(option => option.Label));

    internal void OnOptionChanged(SearchFieldOptionViewModel changed)
    {
        var selected = Options.Where(option => option.IsSelected).Select(option => option.Field).ToList();

        // The UI disables unchecking the last field. This guard keeps the rule even if code sets it directly.
        if (selected.Count < SearchFieldSelection.MinFields)
        {
            changed.Restore();
            return;
        }

        _selection = SearchFieldSelection.Create(selected);

        foreach (var option in Options)
        {
            option.RefreshCanToggle();
        }

        OnPropertyChanged(nameof(Selection));
        OnPropertyChanged(nameof(Summary));
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private SearchFieldOptionViewModel Create(SearchField field, string label) =>
        new(this, field, label, SearchFieldSelection.Default.Contains(field));
}
