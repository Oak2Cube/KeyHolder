using KeyHolder.Domain.Entries;
using KeyHolder.Domain.Query;

namespace KeyHolder.UnitTests;

public sealed class EntryListQueryTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static int _counter;

    private static EntrySummary Entry(
        string title,
        string? username = null,
        string? email = null,
        string? url = null,
        string[]? dTags = null,
        string[]? sTags = null,
        int? importance = null,
        int day = 0)
    {
        var id = new Guid(Interlocked.Increment(ref _counter), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var updated = Start.AddDays(day);
        return new EntrySummary(
            id,
            new EntryMetadata(title, username, email, url, dTags, sTags, importance),
            updated,
            updated);
    }

    private static string[] Titles(IEnumerable<EntrySummary> entries) =>
        entries.Select(entry => entry.Metadata.Title).ToArray();

    [Fact]
    public void SearchOnlyLooksInChosenFields()
    {
        var entries = new[]
        {
            Entry("Bank", username: "alex"),
            Entry("Mail", username: "bank-user"),
        };

        var titleOnly = new EntryListQuery
        {
            SearchText = "bank",
            SearchFields = SearchFieldSelection.Create([SearchField.Title]),
        };
        var usernameOnly = titleOnly with { SearchFields = SearchFieldSelection.Create([SearchField.Username]) };

        Assert.Equal(["Bank"], Titles(titleOnly.Apply(entries)));
        Assert.Equal(["Mail"], Titles(usernameOnly.Apply(entries)));
    }

    [Fact]
    public void SearchIgnoresCase()
    {
        var result = new EntryListQuery { SearchText = "EXAMPLE" }.Apply([Entry("Example Mail")]);

        Assert.Single(result);
    }

    [Theory]
    [InlineData(SearchField.Title, "Alpha")]
    [InlineData(SearchField.Username, "user-x")]
    [InlineData(SearchField.Email, "x@example.test")]
    [InlineData(SearchField.Url, "site.example.test")]
    [InlineData(SearchField.DTags, "DataTag")]
    [InlineData(SearchField.STags, "SecTag")]
    public void EachFieldIsSearchable(SearchField field, string term)
    {
        var entries = new[]
        {
            Entry(
                "Alpha",
                "user-x",
                "x@example.test",
                "https://site.example.test",
                ["DataTag"],
                ["SecTag"]),
            Entry("Other"),
        };
        var query = new EntryListQuery
        {
            SearchText = term,
            SearchFields = SearchFieldSelection.Create([field]),
        };

        Assert.Equal(["Alpha"], Titles(query.Apply(entries)));
    }

    [Fact]
    public void AllTermsMustMatchSomeChosenField()
    {
        var entries = new[]
        {
            Entry("Bank", username: "alex"),
            Entry("Bank", username: "sam"),
        };

        var result = new EntryListQuery { SearchText = "bank alex" }.Apply(entries);

        Assert.Single(result);
    }

    [Fact]
    public void EmptySearchReturnsEverything()
    {
        Assert.Equal(2, new EntryListQuery().Apply([Entry("A"), Entry("B")]).Count);
    }

    [Fact]
    public void SearchSelectionRequiresBetweenOneAndSixDistinctFields()
    {
        Assert.Throws<ArgumentException>(() => SearchFieldSelection.Create([]));
        Assert.Throws<ArgumentException>(() => SearchFieldSelection.Create([(SearchField)99]));

        Assert.Single(SearchFieldSelection.Create([SearchField.Title, SearchField.Title]).Fields);
        Assert.Equal(6, SearchFieldSelection.Create(Enum.GetValues<SearchField>()).Fields.Count);
        Assert.Equal(6, SearchFieldSelection.All.Fields.Count);
    }

    [Fact]
    public void FilterByDTagMatchesAnySelectedTag()
    {
        var entries = new[]
        {
            Entry("A", dTags: ["Email"]),
            Entry("B", dTags: ["Account"]),
            Entry("C", dTags: ["Phone"]),
        };
        var query = new EntryListQuery { Filter = new EntryFilter(dTags: ["email", "ACCOUNT"]) };

        Assert.Equal(["A", "B"], Titles(query.Apply(entries)));
    }

    [Fact]
    public void FilterCombinesDTagsAndSTagsWithAnd()
    {
        var entries = new[]
        {
            Entry("A", dTags: ["Email"], sTags: ["Sensitive"]),
            Entry("B", dTags: ["Email"], sTags: ["Finance"]),
            Entry("C", dTags: ["Account"], sTags: ["Sensitive"]),
        };
        var query = new EntryListQuery { Filter = new EntryFilter(["Email"], ["Sensitive"]) };

        Assert.Equal(["A"], Titles(query.Apply(entries)));
    }

    [Fact]
    public void DTagFilterDoesNotMatchSTags()
    {
        var entries = new[] { Entry("A", sTags: ["Email"]) };
        var query = new EntryListQuery { Filter = new EntryFilter(dTags: ["Email"]) };

        Assert.Empty(query.Apply(entries));
    }

    [Fact]
    public void EmptyFilterDoesNotFilter()
    {
        Assert.True(EntryFilter.None.IsEmpty);
        Assert.Single(new EntryListQuery().Apply([Entry("A")]));
    }

    [Fact]
    public void SortByNameIgnoresCase()
    {
        var entries = new[] { Entry("banana"), Entry("Apple"), Entry("cherry") };

        var ascending = new EntryListQuery { SortKey = SortKey.Name }.Apply(entries);
        var descending = new EntryListQuery { SortKey = SortKey.Name, SortDirection = SortDirection.Descending }
            .Apply(entries);

        Assert.Equal(["Apple", "banana", "cherry"], Titles(ascending));
        Assert.Equal(["cherry", "banana", "Apple"], Titles(descending));
    }

    [Fact]
    public void SortByDateUsesUpdatedAt()
    {
        var entries = new[] { Entry("B", day: 2), Entry("A", day: 3), Entry("C", day: 1) };

        var ascending = new EntryListQuery { SortKey = SortKey.Date }.Apply(entries);
        var descending = new EntryListQuery { SortKey = SortKey.Date, SortDirection = SortDirection.Descending }
            .Apply(entries);

        Assert.Equal(["C", "B", "A"], Titles(ascending));
        Assert.Equal(["A", "B", "C"], Titles(descending));
    }

    [Theory]
    [InlineData(SortDirection.Ascending, new[] { "Low", "High", "None" })]
    [InlineData(SortDirection.Descending, new[] { "High", "Low", "None" })]
    public void SortByImportanceAlwaysPutsMissingImportanceLast(SortDirection direction, string[] expected)
    {
        var entries = new[]
        {
            Entry("None"),
            Entry("High", importance: 90),
            Entry("Low", importance: 1),
        };

        var result = new EntryListQuery { SortKey = SortKey.Importance, SortDirection = direction }.Apply(entries);

        Assert.Equal(expected, Titles(result));
    }

    [Fact]
    public void TiesAreBrokenByTitleThenIdSoOrderIsStable()
    {
        var entries = new[]
        {
            Entry("B", importance: 5),
            Entry("A", importance: 5),
            Entry("C", importance: 5),
        };

        var result = new EntryListQuery { SortKey = SortKey.Importance }.Apply(entries);
        var reversedInput = new EntryListQuery { SortKey = SortKey.Importance }.Apply(entries.Reverse());

        Assert.Equal(["A", "B", "C"], Titles(result));
        Assert.Equal(Titles(result), Titles(reversedInput));
    }

    [Fact]
    public void CollectTagsReturnsDistinctSortedTags()
    {
        var entries = new[]
        {
            Entry("A", dTags: ["Email", "account"], sTags: ["Sensitive"]),
            Entry("B", dTags: ["EMAIL"], sTags: ["Finance"]),
        };

        var (dTags, sTags) = EntryListQuery.CollectTags(entries);

        Assert.Equal(["account", "Email"], dTags);
        Assert.Equal(["Finance", "Sensitive"], sTags);
    }
}
