using System.Globalization;
using KeyHolder.Domain.Entries;

namespace KeyHolder.Desktop.ViewModels;

/// <summary>
/// Display wrapper for one entry in the list and the detail pane. It never holds secrets.
/// </summary>
public sealed class EntryListItemViewModel
{
    public EntryListItemViewModel(EntrySummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        Summary = summary;
    }

    public EntrySummary Summary { get; }

    public Guid Id => Summary.Id;

    public string Title => Summary.Metadata.Title;

    public string Username => Summary.Metadata.Username ?? string.Empty;

    public string Email => Summary.Metadata.Email ?? string.Empty;

    public string Url => Summary.Metadata.Url ?? string.Empty;

    public string DTagsText => string.Join(", ", Summary.Metadata.DTags);

    public string STagsText => string.Join(", ", Summary.Metadata.STags);

    /// <summary>
    /// The d-tags as separate values, shown as chips in the detail pane.
    /// </summary>
    public IReadOnlyList<string> DTagList => Summary.Metadata.DTags;

    /// <summary>
    /// The s-tags as separate values, shown as chips in the detail pane.
    /// </summary>
    public IReadOnlyList<string> STagList => Summary.Metadata.STags;

    public string ImportanceText =>
        Summary.Metadata.Importance?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;

    // The detail pane hides fields without a value (and tag groups without tags).
    public bool HasUsername => !string.IsNullOrWhiteSpace(Username);

    public bool HasEmail => !string.IsNullOrWhiteSpace(Email);

    public bool HasUrl => !string.IsNullOrWhiteSpace(Url);

    public bool HasDTags => Summary.Metadata.DTags.Count > 0;

    public bool HasSTags => Summary.Metadata.STags.Count > 0;

    public bool HasImportance => Summary.Metadata.Importance is not null;

    public bool HasPassword => Summary.HasPassword;

    public bool HasNotes => Summary.HasNotes;

    /// <summary>
    /// The Deep access level group is shown only when there is a password or notes.
    /// </summary>
    public bool HasSecretFields => HasPassword || HasNotes;

    /// <summary>
    /// The second line in the list: username, or e-mail when there is no username.
    /// </summary>
    public string Subtitle => !string.IsNullOrEmpty(Username) ? Username : Email;

    public string UpdatedText => Summary.UpdatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    public string CreatedText => Summary.CreatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
}
