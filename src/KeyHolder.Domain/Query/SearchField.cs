namespace KeyHolder.Domain.Query;

/// <summary>
/// Metadata fields that search can look in. Secret fields are never searchable.
/// </summary>
public enum SearchField
{
    Title,
    Username,
    Email,
    Url,
    DTags,
    STags,
}
