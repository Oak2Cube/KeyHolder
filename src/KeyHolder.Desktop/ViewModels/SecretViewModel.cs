using System.Windows.Threading;
using KeyHolder.Desktop.Mvvm;
using KeyHolder.Desktop.Services;
using KeyHolder.Domain.Entries;
using KeyHolder.Domain.Locking;

namespace KeyHolder.Desktop.ViewModels;

/// <summary>
/// Reveal and Copy for the selected entry only. The secret is read lazily when the user asks,
/// shown for a short time, and then replaced by the fixed placeholder again.
/// </summary>
public sealed class SecretViewModel : ViewModelBase, IDisposable
{
    public static readonly TimeSpan RevealTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan ClipboardTimeout = TimeSpan.FromSeconds(20);

    private const string NoValueText = "(none)";

    private readonly IEntryRepository _repository;
    private readonly IClipboardService _clipboard;
    private readonly TimedValue<string> _revealedPassword;
    private readonly TimedValue<string> _revealedNotes;
    private readonly DispatcherTimer _timer;
    private Guid? _entryId;
    private string _message = string.Empty;

    public SecretViewModel(IEntryRepository repository, IClipboardService clipboard, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(clock);

        _repository = repository;
        _clipboard = clipboard;
        _revealedPassword = new TimedValue<string>(clock);
        _revealedNotes = new TimedValue<string>(clock);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => RefreshDisplays();

        RevealPasswordCommand = new AsyncRelayCommand(
            () => TogglePasswordAsync(),
            () => _entryId is not null,
            ReportError);
        RevealNotesCommand = new AsyncRelayCommand(
            () => ToggleNotesAsync(),
            () => _entryId is not null,
            ReportError);
        CopyPasswordCommand = new AsyncRelayCommand(
            () => CopyPasswordAsync(),
            () => _entryId is not null,
            ReportError);
    }

    public AsyncRelayCommand RevealPasswordCommand { get; }

    public AsyncRelayCommand RevealNotesCommand { get; }

    public AsyncRelayCommand CopyPasswordCommand { get; }

    /// <summary>
    /// The fixed placeholder, or the real password while a Reveal is active.
    /// </summary>
    public string PasswordDisplay =>
        _revealedPassword.TryGet(out var value) ? value : SecretPlaceholder.Value;

    public string NotesDisplay =>
        _revealedNotes.TryGet(out var value) ? value : SecretPlaceholder.Value;

    public bool IsPasswordRevealed => _revealedPassword.IsActive;

    public bool IsNotesRevealed => _revealedNotes.IsActive;

    public string RevealPasswordLabel => IsPasswordRevealed ? "Hide" : "Reveal";

    public string RevealNotesLabel => IsNotesRevealed ? "Hide" : "Reveal";

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    /// <summary>
    /// Selects the entry that Reveal and Copy work on. Changing the entry hides everything.
    /// </summary>
    public void SetEntry(Guid? entryId)
    {
        if (_entryId == entryId)
        {
            return;
        }

        HideAll();
        _entryId = entryId;
        Message = string.Empty;
    }

    /// <summary>
    /// Hides revealed values and removes the app's clipboard value.
    /// </summary>
    public void Clear()
    {
        HideAll();
        _clipboard.ClearIfOwned();
        _entryId = null;
        Message = string.Empty;
    }

    public void Dispose() => _timer.Stop();

    private void HideAll()
    {
        _revealedPassword.Clear();
        _revealedNotes.Clear();
        _timer.Stop();
        NotifyDisplays();
    }

    private async Task TogglePasswordAsync()
    {
        if (_revealedPassword.IsActive)
        {
            _revealedPassword.Clear();
            NotifyDisplays();
            return;
        }

        var secret = await LoadSelectedSecretAsync();
        if (secret is null)
        {
            return;
        }

        _revealedPassword.Set(secret.Password ?? NoValueText, RevealTimeout);
        Message = $"Password shown for {RevealTimeout.TotalSeconds:0} seconds.";
        StartTimer();
        NotifyDisplays();
    }

    private async Task ToggleNotesAsync()
    {
        if (_revealedNotes.IsActive)
        {
            _revealedNotes.Clear();
            NotifyDisplays();
            return;
        }

        var secret = await LoadSelectedSecretAsync();
        if (secret is null)
        {
            return;
        }

        _revealedNotes.Set(secret.Notes ?? NoValueText, RevealTimeout);
        Message = $"Notes shown for {RevealTimeout.TotalSeconds:0} seconds.";
        StartTimer();
        NotifyDisplays();
    }

    private async Task CopyPasswordAsync()
    {
        var secret = await LoadSelectedSecretAsync();
        if (secret is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(secret.Password))
        {
            Message = "This entry has no password to copy.";
            return;
        }

        _clipboard.SetSecret(secret.Password, ClipboardTimeout);
        Message = $"Password copied. The clipboard is cleared in {ClipboardTimeout.TotalSeconds:0} seconds.";
    }

    /// <summary>
    /// Reads the secret of the selected entry. Returns <see langword="null"/> when the selection changed
    /// while waiting or the entry is gone, so a late answer can never show another entry's secret.
    /// </summary>
    private async Task<EntrySecret?> LoadSelectedSecretAsync()
    {
        if (_entryId is not { } id)
        {
            return null;
        }

        var secret = await _repository.GetSecretAsync(id);

        if (_entryId != id)
        {
            return null;
        }

        if (secret is null)
        {
            Message = "The entry no longer exists.";
        }

        return secret;
    }

    private void StartTimer()
    {
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private void RefreshDisplays()
    {
        if (!_revealedPassword.IsActive && !_revealedNotes.IsActive)
        {
            _timer.Stop();
        }

        NotifyDisplays();
    }

    private void NotifyDisplays()
    {
        OnPropertyChanged(nameof(PasswordDisplay));
        OnPropertyChanged(nameof(NotesDisplay));
        OnPropertyChanged(nameof(IsPasswordRevealed));
        OnPropertyChanged(nameof(IsNotesRevealed));
        OnPropertyChanged(nameof(RevealPasswordLabel));
        OnPropertyChanged(nameof(RevealNotesLabel));
    }

    private void ReportError(Exception exception) => Message = $"Could not read the secret: {exception.Message}";
}
