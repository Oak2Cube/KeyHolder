namespace KeyHolder.Domain.Query;

/// <summary>
/// What the entry list is sorted by.
/// </summary>
public enum SortKey
{
    /// <summary>Title, ignoring case.</summary>
    Name,

    /// <summary>Last update time.</summary>
    Date,

    /// <summary>Importance. Entries without importance always come last.</summary>
    Importance,
}

public enum SortDirection
{
    Ascending,
    Descending,
}
