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

    public string ImportanceText =>
        Summary.Metadata.Importance?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;

    /// <summary>
    /// The second line in the list: username, or e-mail when there is no username.
    /// </summary>
    public string Subtitle => !string.IsNullOrEmpty(Username) ? Username : Email;

    public string UpdatedText => Summary.UpdatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    public string CreatedText => Summary.CreatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
}
