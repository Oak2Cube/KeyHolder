using System.Collections;
using System.ComponentModel;
using System.Globalization;
using KeyHolder.Cryptography;
using KeyHolder.Desktop.Mvvm;
using KeyHolder.Domain.Entries;
using KeyHolder.Domain.Locking;

namespace KeyHolder.Desktop.ViewModels;

public sealed class EditorCompletedEventArgs : EventArgs
{
    public EditorCompletedEventArgs(Guid? savedEntryId)
    {
        SavedEntryId = savedEntryId;
    }

    /// <summary>
    /// The saved entry id, or <see langword="null"/> when the user cancelled.
    /// </summary>
    public Guid? SavedEntryId { get; }
}

/// <summary>
/// Creates or edits one entry. Validation uses <see cref="EntryValidator"/>, so the rules live in the domain.
/// The password field is write-only: it never shows the stored password. When editing, an empty password
/// field means "keep the current password".
/// </summary>
public sealed class EntryEditorViewModel : ViewModelBase, INotifyDataErrorInfo
{
    private readonly IEntryRepository _repository;
    private readonly IClock _clock;
    private readonly IPasswordGenerator _generator;
    private readonly Guid? _entryId;
    private readonly string? _originalNotes;
    private readonly Dictionary<string, List<string>> _errors = [];

    // Held in memory only while the editor is open, so that saving edited notes does not erase the password.
    private string? _existingPassword;

    private string _title = string.Empty;
    private string _username = string.Empty;
    private string _email = string.Empty;
    private string _url = string.Empty;
    private string _dTagsText = string.Empty;
    private string _sTagsText = string.Empty;
    private string _importanceText = string.Empty;
    private string _password = string.Empty;
    private string _notes = string.Empty;
    private string _errorMessage = string.Empty;
    private string _generatorMessage = string.Empty;
    private int _generatorLength = PasswordGeneratorOptions.DefaultLength;
    private bool _generatorSymbols = true;
    private bool _generatorAvoidAmbiguous;

    private EntryEditorViewModel(
        IEntryRepository repository,
        IClock clock,
        IPasswordGenerator generator,
        EntrySummary? existing,
        EntrySecret? existingSecret)
    {
        _repository = repository;
        _clock = clock;
        _generator = generator;

        if (existing is not null)
        {
            _entryId = existing.Id;
            var metadata = existing.Metadata;
            _title = metadata.Title;
            _username = metadata.Username ?? string.Empty;
            _email = metadata.Email ?? string.Empty;
            _url = metadata.Url ?? string.Empty;
            _dTagsText = string.Join(", ", metadata.DTags);
            _sTagsText = string.Join(", ", metadata.STags);
            _importanceText = metadata.Importance?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
            _existingPassword = existingSecret?.Password;
            _originalNotes = existingSecret?.Notes;
            _notes = existingSecret?.Notes ?? string.Empty;
        }

        SaveCommand = new AsyncRelayCommand(SaveAsync, null, ReportError);
        CancelCommand = new RelayCommand(Cancel);
        GenerateCommand = new RelayCommand(Generate);
    }

    public event EventHandler<EditorCompletedEventArgs>? Completed;

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public AsyncRelayCommand SaveCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand GenerateCommand { get; }

    public bool IsNew => _entryId is null;

    public string Heading => IsNew ? "New entry" : "Edit entry";

    public string PasswordHint => IsNew
        ? "Optional. Use Generate for a random password."
        : "Leave empty to keep the current password.";

    public string Title
    {
        get => _title;
        set => SetValidated(ref _title, value);
    }

    public string Username
    {
        get => _username;
        set => SetValidated(ref _username, value);
    }

    public string Email
    {
        get => _email;
        set => SetValidated(ref _email, value);
    }

    public string Url
    {
        get => _url;
        set => SetValidated(ref _url, value);
    }

    /// <summary>
    /// d-tags (data type, for example Email or Account), separated by commas.
    /// </summary>
    public string DTagsText
    {
        get => _dTagsText;
        set => SetValidated(ref _dTagsText, value);
    }

    /// <summary>
    /// s-tags (security category, for example Finance or Sensitive), separated by commas.
    /// </summary>
    public string STagsText
    {
        get => _sTagsText;
        set => SetValidated(ref _sTagsText, value);
    }

    public string ImportanceText
    {
        get => _importanceText;
        set => SetValidated(ref _importanceText, value);
    }

    /// <summary>
    /// A new password. Write-only: never filled from storage.
    /// </summary>
    public string Password
    {
        get => _password;
        set => SetValidated(ref _password, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetValidated(ref _notes, value);
    }

    public int GeneratorLength
    {
        get => _generatorLength;
        set => SetProperty(ref _generatorLength, value);
    }

    public bool GeneratorSymbols
    {
        get => _generatorSymbols;
        set => SetProperty(ref _generatorSymbols, value);
    }

    public bool GeneratorAvoidAmbiguous
    {
        get => _generatorAvoidAmbiguous;
        set => SetProperty(ref _generatorAvoidAmbiguous, value);
    }

    public string GeneratorMessage
    {
        get => _generatorMessage;
        private set => SetProperty(ref _generatorMessage, value);
    }

    /// <summary>
    /// A save failure (not a field error), for example a database problem.
    /// </summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool HasErrors => _errors.Count > 0;

    /// <summary>
    /// All field errors in one text, for the summary under the form.
    /// </summary>
    public string ValidationSummary => string.Join(Environment.NewLine, _errors.Values.SelectMany(list => list));

    public static EntryEditorViewModel CreateNew(
        IEntryRepository repository,
        IClock clock,
        IPasswordGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(generator);

        return new EntryEditorViewModel(repository, clock, generator, existing: null, existingSecret: null);
    }

    /// <summary>
    /// Opens an entry for editing. Only this entry's secret is read, so notes can be edited
    /// and the password can be kept when it is not replaced.
    /// </summary>
    public static async Task<EntryEditorViewModel> CreateForEditAsync(
        IEntryRepository repository,
        IClock clock,
        IPasswordGenerator generator,
        EntrySummary entry)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(entry);

        var secret = await repository.GetSecretAsync(entry.Id);
        return new EntryEditorViewModel(repository, clock, generator, entry, secret);
    }

    public IEnumerable GetErrors(string? propertyName) =>
        propertyName is not null && _errors.TryGetValue(propertyName, out var list) ? list : Array.Empty<string>();

    /// <summary>
    /// Drops the in-memory secret copies. Called when the editor closes or the vault locks.
    /// </summary>
    public void Discard()
    {
        _existingPassword = null;
        _password = string.Empty;
        _notes = string.Empty;
    }

    private void SetValidated(ref string field, string? value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (SetProperty(ref field, value ?? string.Empty, name))
        {
            ErrorMessage = string.Empty;
            Validate();
        }
    }

    private async Task SaveAsync()
    {
        ErrorMessage = string.Empty;
        Validate();
        if (HasErrors)
        {
            return;
        }

        var metadata = EntryValidator.Normalize(BuildMetadata(out _));
        var now = _clock.UtcNow;

        if (_entryId is null)
        {
            var entry = VaultEntry.Create(metadata, new EntrySecret(Password, Notes), now);
            await _repository.AddAsync(entry);
            Finish(entry.Id);
            return;
        }

        var normalizedNotes = EntryValidator.Normalize(new EntrySecret(null, Notes)).Notes;
        var replaceSecret = !string.IsNullOrEmpty(Password)
            || !string.Equals(normalizedNotes, _originalNotes, StringComparison.Ordinal);

        EntrySecret? newSecret = replaceSecret
            ? new EntrySecret(string.IsNullOrEmpty(Password) ? _existingPassword : Password, Notes)
            : null;

        if (await _repository.UpdateAsync(_entryId.Value, metadata, newSecret, now))
        {
            Finish(_entryId.Value);
        }
        else
        {
            ErrorMessage = "This entry no longer exists.";
        }
    }

    private void Cancel()
    {
        Discard();
        Completed?.Invoke(this, new EditorCompletedEventArgs(null));
    }

    private void Finish(Guid id)
    {
        Discard();
        Completed?.Invoke(this, new EditorCompletedEventArgs(id));
    }

    private void Generate()
    {
        try
        {
            var options = new PasswordGeneratorOptions
            {
                Length = GeneratorLength,
                IncludeSymbols = GeneratorSymbols,
                ExcludeAmbiguous = GeneratorAvoidAmbiguous,
            };

            Password = _generator.Generate(options);
            GeneratorMessage = "A new password was generated and is hidden. Save, then use Reveal or Copy.";
        }
        catch (ArgumentException exception)
        {
            GeneratorMessage = exception.Message;
        }
    }

    private EntryMetadata BuildMetadata(out string? importanceError)
    {
        importanceError = null;
        int? importance = null;

        var importanceText = ImportanceText.Trim();
        if (importanceText.Length > 0)
        {
            if (int.TryParse(importanceText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var parsed))
            {
                importance = parsed;
            }
            else
            {
                importanceError = "Importance must be a whole number.";
            }
        }

        return new EntryMetadata(
            Title,
            Username,
            Email,
            Url,
            SplitTags(DTagsText),
            SplitTags(STagsText),
            importance);
    }

    private static string[] SplitTags(string text) =>
        text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private void Validate()
    {
        var found = new Dictionary<string, List<string>>();

        var metadata = BuildMetadata(out var importanceError);
        foreach (var error in EntryValidator.Validate(metadata))
        {
            Add(found, MapField(error.Field), error.Message);
        }

        if (importanceError is not null)
        {
            Add(found, nameof(ImportanceText), importanceError);
        }

        foreach (var error in EntryValidator.Validate(new EntrySecret(Password, Notes)))
        {
            Add(found, MapField(error.Field), error.Message);
        }

        var changed = _errors.Keys.Union(found.Keys).ToList();
        _errors.Clear();
        foreach (var (key, value) in found)
        {
            _errors[key] = value;
        }

        foreach (var property in changed)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(property));
        }

        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(ValidationSummary));
    }

    private static void Add(Dictionary<string, List<string>> errors, string property, string message)
    {
        if (!errors.TryGetValue(property, out var list))
        {
            list = [];
            errors[property] = list;
        }

        list.Add(message);
    }

    private static string MapField(string field) => field switch
    {
        EntryFields.DTags => nameof(DTagsText),
        EntryFields.STags => nameof(STagsText),
        EntryFields.Importance => nameof(ImportanceText),
        _ => field,
    };

    private void ReportError(Exception exception) =>
        ErrorMessage = exception is EntryValidationException
            ? exception.Message
            : $"Could not save: {exception.Message}";
}
