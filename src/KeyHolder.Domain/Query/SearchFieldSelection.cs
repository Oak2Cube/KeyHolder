namespace KeyHolder.Domain.Query;

/// <summary>
/// The customizable list of fields search looks in: between 1 and 6 distinct fields.
/// </summary>
public sealed class SearchFieldSelection
{
    public const int MinFields = 1;
    public const int MaxFields = 6;

    private SearchFieldSelection(IReadOnlyList<SearchField> fields)
    {
        Fields = fields;
    }

    /// <summary>
    /// All six searchable fields.
    /// </summary>
    public static SearchFieldSelection All { get; } = new(Enum.GetValues<SearchField>());

    /// <summary>
    /// The default selection: title, username, and e-mail.
    /// </summary>
    public static SearchFieldSelection Default { get; } = new([SearchField.Title, SearchField.Username, SearchField.Email]);

    /// <summary>
    /// The selected fields, distinct and in enum order.
    /// </summary>
    public IReadOnlyList<SearchField> Fields { get; }

    /// <summary>
    /// Creates a selection. Duplicates are ignored.
    /// </summary>
    /// <exception cref="ArgumentException">No field is selected, or a value is not a known field.</exception>
    public static SearchFieldSelection Create(IEnumerable<SearchField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var distinct = fields.Distinct().Order().ToArray();

        if (distinct.Any(field => !Enum.IsDefined(field)))
        {
            throw new ArgumentException("Unknown search field.", nameof(fields));
        }

        if (distinct.Length is < MinFields or > MaxFields)
        {
            throw new ArgumentException(
                $"Search needs between {MinFields} and {MaxFields} distinct fields.",
                nameof(fields));
        }

        return new SearchFieldSelection(distinct);
    }

    public bool Contains(SearchField field) => Fields.Contains(field);
}
