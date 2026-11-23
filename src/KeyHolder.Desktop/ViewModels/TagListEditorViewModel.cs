using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using KeyHolder.Desktop.Mvvm;
using KeyHolder.Desktop.Tags;
using KeyHolder.Domain.Entries;
using KeyHolder.Domain.Tags;

namespace KeyHolder.Desktop.ViewModels;

/// <summary>
/// One tag in the editor, with its color and a "-" command.
/// </summary>
public sealed class TagEditItemViewModel
{
    public TagEditItemViewModel(string name, Brush brush, ICommand removeCommand)
    {
        Name = name;
        Brush = brush;
        RemoveCommand = removeCommand;
    }

    public string Name { get; }

    public Brush Brush { get; }

    public ICommand RemoveCommand { get; }
}

/// <summary>
/// Edits one tag list (d-tags or s-tags) of an entry: the current tags with "-", and a "+" flow that opens an
/// add panel (name, existing-tag dropdown, color dropdown, OK). Colors are only staged here; they are saved
/// together with the entry, so cancelling the entry discards them.
/// </summary>
public sealed class TagListEditorViewModel : ViewModelBase
{
    private readonly TagKind _kind;
    private readonly TagCatalog _catalog;
    private readonly Dictionary<string, TagColorAssignment> _staged = [];

    private bool _isAdding;
    private string _newName = string.Empty;
    private string _newNameError = string.Empty;
    private TagDisplay? _selectedExisting;
    private TagColorOption _selectedColor = TagColorPalette.GetOption(TagColor.Default);
    private bool _colorChosen;
    private bool _syncing;

    public TagListEditorViewModel(TagKind kind, string title, IEnumerable<string> initialTags, TagCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(initialTags);
        ArgumentNullException.ThrowIfNull(catalog);

        _kind = kind;
        _catalog = catalog;
        Title = title;

        foreach (var name in initialTags)
        {
            Items.Add(CreateItem(name, catalog.GetColor(kind, name)));
        }

        AddCommand = new RelayCommand(StartAdding, () => !IsAdding && Items.Count < EntryLimits.MaxTagsPerList);
        OkCommand = new RelayCommand(Confirm, () => IsAdding && CurrentError is null);
        CancelAddCommand = new RelayCommand(CancelAdding);
    }

    /// <summary>
    /// Raised when a tag was added or removed.
    /// </summary>
    public event EventHandler? Changed;

    public string Title { get; }

    public ObservableCollection<TagEditItemViewModel> Items { get; } = [];

    /// <summary>
    /// Known tags that are not yet on this entry, for quick select.
    /// </summary>
    public ObservableCollection<TagDisplay> ExistingTags { get; } = [];

    public IReadOnlyList<TagColorOption> Colors { get; } = TagColorPalette.Options;

    public ICommand AddCommand { get; }

    public ICommand OkCommand { get; }

    public ICommand CancelAddCommand { get; }

    public IReadOnlyList<string> Names => [.. Items.Select(item => item.Name)];

    /// <summary>
    /// The color changes to save together with the entry.
    /// </summary>
    public IReadOnlyCollection<TagColorAssignment> StagedColors => [.. _staged.Values];

    public bool IsAdding
    {
        get => _isAdding;
        private set
        {
            if (SetProperty(ref _isAdding, value))
            {
                OnPropertyChanged(nameof(ShowAddButton));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>
    /// The "+" button is shown whenever the add panel is closed.
    /// </summary>
    public bool ShowAddButton => !IsAdding;

    public bool HasExistingTags => ExistingTags.Count > 0;

    public string NewName
    {
        get => _newName;
        set
        {
            if (!SetProperty(ref _newName, value ?? string.Empty))
            {
                return;
            }

            if (!_syncing)
            {
                OnTypedNameChanged();
            }

            RefreshError();
        }
    }

    /// <summary>
    /// Why the typed name cannot be added. Empty while nothing is typed or when the name is valid.
    /// </summary>
    public string NewNameError
    {
        get => _newNameError;
        private set
        {
            if (SetProperty(ref _newNameError, value))
            {
                OnPropertyChanged(nameof(HasNewNameError));
            }
        }
    }

    public bool HasNewNameError => NewNameError.Length > 0;

    /// <summary>
    /// The chosen existing tag. Choosing one fills the name and its stored color.
    /// </summary>
    public TagDisplay? SelectedExisting
    {
        get => _selectedExisting;
        set
        {
            if (!SetProperty(ref _selectedExisting, value) || value is null)
            {
                return;
            }

            _syncing = true;
            try
            {
                NewName = value.Name;
                SetColor(_catalog.GetColor(_kind, value.Name));
                _colorChosen = false;
            }
            finally
            {
                _syncing = false;
            }
        }
    }

    public TagColorOption SelectedColor
    {
        get => _selectedColor;
        set
        {
            if (value is not null && SetProperty(ref _selectedColor, value) && !_syncing)
            {
                _colorChosen = true;
            }
        }
    }

    private string? CurrentError => EntryValidator.ValidateNewTag(NewName, Names);

    private void StartAdding()
    {
        _syncing = true;
        try
        {
            NewName = string.Empty;
            SelectedExisting = null;
            SetColor(TagColor.Default);
            _colorChosen = false;
        }
        finally
        {
            _syncing = false;
        }

        var used = Items.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        ExistingTags.Clear();
        foreach (var name in _catalog.KnownTags(_kind).Where(name => !used.Contains(name)))
        {
            ExistingTags.Add(_catalog.GetDisplay(_kind, name));
        }

        OnPropertyChanged(nameof(HasExistingTags));
        RefreshError();
        IsAdding = true;
    }

    private void CancelAdding()
    {
        NewNameError = string.Empty;
        IsAdding = false;
    }

    private void Confirm()
    {
        if (CurrentError is not null)
        {
            return;
        }

        var typed = EntryValidator.NormalizeTag(NewName);

        // Reuse the spelling of a known tag, so "email" does not create a second spelling of "Email".
        var name = _catalog.KnownTags(_kind)
            .FirstOrDefault(known => string.Equals(known, typed, StringComparison.OrdinalIgnoreCase))
            ?? typed;
        var color = SelectedColor.Color;

        Items.Add(CreateItem(name, color));

        var key = TagColors.KeyOf(name);
        if (color == _catalog.GetColor(_kind, name))
        {
            _staged.Remove(key);
        }
        else
        {
            _staged[key] = new TagColorAssignment(_kind, name, color);
        }

        IsAdding = false;
        NewNameError = string.Empty;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private TagEditItemViewModel CreateItem(string name, TagColor color)
    {
        TagEditItemViewModel? item = null;
        item = new TagEditItemViewModel(
            name,
            TagColorPalette.GetBrush(color),
            new RelayCommand(() => Remove(item!)));
        return item;
    }

    private void Remove(TagEditItemViewModel item)
    {
        if (!Items.Remove(item))
        {
            return;
        }

        // A removed tag leaves no color change behind.
        _staged.Remove(TagColors.KeyOf(item.Name));
        CommandManager.InvalidateRequerySuggested();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnTypedNameChanged()
    {
        var typed = EntryValidator.NormalizeTag(NewName);

        // Typing a different name than the chosen existing tag clears that choice.
        if (_selectedExisting is not null
            && !string.Equals(_selectedExisting.Name, typed, StringComparison.OrdinalIgnoreCase))
        {
            _syncing = true;
            try
            {
                SelectedExisting = null;
            }
            finally
            {
                _syncing = false;
            }
        }

        // Until the user picks a color, a known tag name brings its stored color, so OK never wipes it by accident.
        if (!_colorChosen)
        {
            _syncing = true;
            try
            {
                SetColor(_catalog.GetColor(_kind, typed));
            }
            finally
            {
                _syncing = false;
            }
        }
    }

    private void SetColor(TagColor color) => SelectedColor = TagColorPalette.GetOption(color);

    private void RefreshError()
    {
        NewNameError = string.IsNullOrWhiteSpace(NewName) ? string.Empty : CurrentError ?? string.Empty;
        CommandManager.InvalidateRequerySuggested();
    }
}
