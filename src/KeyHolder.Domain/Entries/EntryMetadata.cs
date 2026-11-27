using KeyHolder.Domain.Tags;

namespace KeyHolder.Domain.Entries;

/// <summary>
/// The searchable, non-secret part of an entry. In Milestone 2 this becomes the
/// <c>metadata_envelope</c> payload.
/// </summary>
/// <param name="Title">Required display name.</param>
/// <param name="Username">Optional username.</param>
/// <param name="Email">Optional e-mail address.</param>
/// <param name="Url">Optional URL.</param>
/// <param name="DTags">Data Tags: the type of data (for example Email, Account).</param>
/// <param name="STags">Security Tags: category or risk (for example Finance, Sensitive).</param>
/// <param name="Importance">Optional number used for sorting.</param>
/// <param name="Icon">Built-in icon shown in the list and details. Default is the key icon.</param>
/// <param name="IconColor">Tint of the icon. Reuses the tag color palette (10 colors including Default).</param>
public sealed record EntryMetadata(
    string Title,
    string? Username = null,
    string? Email = null,
    string? Url = null,
    IReadOnlyList<string>? DTags = null,
    IReadOnlyList<string>? STags = null,
    int? Importance = null,
    EntryIcon Icon = EntryIcon.Default,
    TagColor IconColor = TagColor.Default)
{
    public IReadOnlyList<string> DTags { get; init; } = DTags ?? [];

    public IReadOnlyList<string> STags { get; init; } = STags ?? [];
}
