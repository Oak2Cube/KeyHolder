using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using KeyHolder.Cryptography;
using KeyHolder.Desktop.Mvvm;
using KeyHolder.Desktop.Services;
using KeyHolder.Domain.Entries;
using KeyHolder.Domain.Locking;
using KeyHolder.Domain.Query;
using SortKey = KeyHolder.Domain.Query.SortKey;

namespace KeyHolder.Desktop.ViewModels;

/// <summary>
/// The unlocked workspace: the entry list with search, filter, and sort, plus selection, editor, and secrets.
/// The list holds summaries only; secrets are loaded one entry at a time by <see cref="SecretViewModel"/>.
/// </summary>
public sealed class VaultViewModel : ViewModelBase
{
    private readonly IEntryRepository _repository;
    private readonly IVaultStateStore _state;
    private readonly IClock _clock;
    private readonly IPasswordGenerator _generator;
    private readonly IDialogService _dialogs;

    private IReadOnlyList<EntrySummary> _summaries = [];
    private bool _isRebuilding;
    private EntryListItemViewModel? _selectedEntry;
    private EntryEditorViewModel? _editor;
    private string _searchText = string.Empty;
    private SortKey _sortKey = SortKey.Name;
    private bool _isSortDescending;
    private string _statusMessage = string.Empty;
    private string _entryCountText = string.Empty;
    private string _dirtyText = string.Empty;
    private bool _isDirty;

    public VaultViewModel(
        IEntryRepository repository,
        IVaultStateStore state,
        IClock clock,
        IPasswordGenerator generator,
        IDialogService dialogs,
        SecretViewModel secret)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(secret);

        _repository = repository;
        _state = state;
        _clock = clock;
        _generator = generator;
        _dialogs = dialogs;
        Secret = secret;

        SearchOptions = new SearchOptionsViewModel();
        SearchOptions.SelectionChanged += (_, _) => ApplyQuery();

        NewCommand = new RelayCommand(StartNew, () => Editor is null);
        EditCommand = new AsyncRelayCommand(StartEditAsync, () => SelectedEntry is not null && Editor is null, ReportError);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedEntry is not null && Editor is null, ReportError);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
    }

    public ObservableCollection<EntryListItemViewModel> Entries { get; } = [];

    public ObservableCollection<TagFilterItemViewModel> DTagFilters { get; } = [];

    public ObservableCollection<TagFilterItemViewModel> STagFilters { get; } = [];

    public SearchOptionsViewModel SearchOptions { get; }

    public SecretViewModel Secret { get; }

    public ICommand NewCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand ClearFiltersCommand { get; }

    public EntryListItemViewModel? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            // While the list is rebuilt the ListBox writes null into the binding. Ignore that.
            if (_isRebuilding || _selectedEntry == value)
            {
                return;
            }

            _selectedEntry = value;
            Secret.SetEntry(value?.Id);
            OnPropertyChanged();
            NotifyModeChanged();
        }
    }

    public EntryEditorViewModel? Editor
    {
        get => _editor;
        private set
        {
            if (SetProperty(ref _editor, value))
            {
                NotifyModeChanged();
            }
        }
    }

    public bool IsEditing => Editor is not null;

    public bool IsViewing => Editor is null && SelectedEntry is not null;

    public bool ShowEmptyHint => Editor is null && SelectedEntry is null;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                ApplyQuery();
            }
        }
    }

    public SortKey SortKey
    {
        get => _sortKey;
        set
        {
            if (SetProperty(ref _sortKey, value))
            {
                ApplyQuery();
            }
        }
    }

    public bool IsSortDescending
    {
        get => _isSortDescending;
        set
        {
            if (SetProperty(ref _isSortDescending, value))
            {
                ApplyQuery();
            }
        }
    }

    public bool HasActiveFilters =>
        DTagFilters.Any(tag => tag.IsSelected) || STagFilters.Any(tag => tag.IsSelected);

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string EntryCountText
    {
        get => _entryCountText;
        private set => SetProperty(ref _entryCountText, value);
    }

    /// <summary>
    /// <see langword="true"/> when the vault changed since the last durable replica export.
    /// </summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    public string DirtyText
    {
        get => _dirtyText;
        private set => SetProperty(ref _dirtyText, value);
    }

    /// <summary>
    /// Loads the entry summaries and the dirty state. Called after unlock and after every change.
    /// </summary>
    public async Task LoadAsync(Guid? selectId = null)
    {
        _summaries = await _repository.ListSummariesAsync();
        var dirty = await _state.GetLocalChangedSinceReplicaAsync();

        IsDirty = dirty;
        DirtyText = dirty ? "Changed since last replica export" : "No local changes";

        RebuildTagFilters();
        ApplyQuery(selectId ?? SelectedEntry?.Id);
    }

    /// <summary>
    /// Forgets everything the workspace showed: list, selection, search, editor draft, revealed values,
    /// and the app's clipboard value. Called whenever the vault locks.
    /// </summary>
    public void Clear()
    {
        _isRebuilding = true;
        try
        {
            if (_editor is not null)
            {
                _editor.Completed -= OnEditorCompleted;
                _editor.Discard();
            }

            _editor = null;
            _selectedEntry = null;
            _summaries = [];
            _searchText = string.Empty;
            Entries.Clear();
            DTagFilters.Clear();
            STagFilters.Clear();
        }
        finally
        {
            _isRebuilding = false;
        }

        Secret.Clear();
        StatusMessage = string.Empty;
        EntryCountText = string.Empty;
        DirtyText = string.Empty;
        IsDirty = false;

        OnPropertyChanged(nameof(Editor));
        OnPropertyChanged(nameof(SelectedEntry));
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(HasActiveFilters));
        NotifyModeChanged();
    }

    /// <summary>
    /// Shows a message in the status bar.
    /// </summary>
    public void SetStatus(string message) => StatusMessage = message;

    private void StartNew()
    {
        Secret.Clear();
        OpenEditor(EntryEditorViewModel.CreateNew(_repository, _clock, _generator));
    }

    private async Task StartEditAsync()
    {
        if (SelectedEntry is not { } selected)
        {
            return;
        }

        Secret.Clear();
        Secret.SetEntry(selected.Id);

        var editor = await EntryEditorViewModel.CreateForEditAsync(_repository, _clock, _generator, selected.Summary);
        OpenEditor(editor);
    }

    private void OpenEditor(EntryEditorViewModel editor)
    {
        editor.Completed += OnEditorCompleted;
        Editor = editor;
        StatusMessage = editor.IsNew ? "Creating a new entry." : "Editing entry.";
    }

    private async void OnEditorCompleted(object? sender, EditorCompletedEventArgs e)
    {
        if (sender is EntryEditorViewModel finished)
        {
            finished.Completed -= OnEditorCompleted;
        }

        Editor = null;

        try
        {
            if (e.SavedEntryId is { } savedId)
            {
                await LoadAsync(savedId);
                StatusMessage = "Entry saved.";
            }
            else
            {
                StatusMessage = "Edit cancelled.";
            }
        }
        catch (Exception exception)
        {
            ReportError(exception);
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedEntry is not { } selected)
        {
            return;
        }

        var confirmed = _dialogs.Confirm(
            "Delete entry",
            $"Delete \"{selected.Title}\" permanently? This cannot be undone.");

        if (!confirmed)
        {
            return;
        }

        var deleted = await _repository.DeleteAsync(selected.Id);

        // The deleted entry is no longer selectable.
        _selectedEntry = null;
        Secret.SetEntry(null);

        await LoadAsync();
        StatusMessage = deleted ? $"Deleted \"{selected.Title}\"." : "The entry was already deleted.";
    }

    private void ClearFilters()
    {
        _isRebuilding = true;
        try
        {
            foreach (var tag in DTagFilters.Concat(STagFilters))
            {
                tag.IsSelected = false;
            }
        }
        finally
        {
            _isRebuilding = false;
        }

        ApplyQuery();
    }

    private void RebuildTagFilters()
    {
        var selectedD = DTagFilters.Where(tag => tag.IsSelected).Select(tag => tag.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedS = STagFilters.Where(tag => tag.IsSelected).Select(tag => tag.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var (dTags, sTags) = EntryListQuery.CollectTags(_summaries);

        _isRebuilding = true;
        try
        {
            Replace(DTagFilters, dTags, selectedD);
            Replace(STagFilters, sTags, selectedS);
        }
        finally
        {
            _isRebuilding = false;
        }
    }

    private void Replace(
        ObservableCollection<TagFilterItemViewModel> target,
        IReadOnlyList<string> tags,
        HashSet<string> selected)
    {
        target.Clear();
        foreach (var tag in tags)
        {
            target.Add(new TagFilterItemViewModel(tag, selected.Contains(tag), OnTagFilterChanged));
        }
    }

    private void OnTagFilterChanged(object? sender, EventArgs e)
    {
        if (!_isRebuilding)
        {
            ApplyQuery();
        }
    }

    private void ApplyQuery(Guid? keepSelectionId = null)
    {
        if (_isRebuilding)
        {
            return;
        }

        var selectedId = keepSelectionId ?? _selectedEntry?.Id;

        var query = new EntryListQuery
        {
            SearchText = SearchText,
            SearchFields = SearchOptions.Selection,
            Filter = new EntryFilter(
                DTagFilters.Where(tag => tag.IsSelected).Select(tag => tag.Name),
                STagFilters.Where(tag => tag.IsSelected).Select(tag => tag.Name)),
            SortKey = SortKey,
            SortDirection = IsSortDescending ? SortDirection.Descending : SortDirection.Ascending,
        };

        var result = query.Apply(_summaries);

        _isRebuilding = true;
        EntryListItemViewModel? reselected = null;
        try
        {
            Entries.Clear();
            foreach (var summary in result)
            {
                var item = new EntryListItemViewModel(summary);
                Entries.Add(item);
                if (summary.Id == selectedId)
                {
                    reselected = item;
                }
            }
        }
        finally
        {
            _isRebuilding = false;
        }

        var previousId = _selectedEntry?.Id;
        _selectedEntry = reselected;
        if (previousId != reselected?.Id)
        {
            // The selected entry was filtered out or deleted: its revealed secret must disappear.
            Secret.SetEntry(reselected?.Id);
        }

        EntryCountText = string.Create(
            CultureInfo.CurrentCulture,
            $"{Entries.Count} of {_summaries.Count} entries");

        OnPropertyChanged(nameof(SelectedEntry));
        OnPropertyChanged(nameof(HasActiveFilters));
        NotifyModeChanged();
    }

    private void NotifyModeChanged()
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(IsViewing));
        OnPropertyChanged(nameof(ShowEmptyHint));
        CommandManager.InvalidateRequerySuggested();
    }

    private void ReportError(Exception exception) => StatusMessage = $"Error: {exception.Message}";
}
