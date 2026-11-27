using KeyHolder.Desktop.Tags;
using KeyHolder.Domain.Entries;
using KeyHolder.Domain.Tags;

namespace KeyHolder.Desktop.Tests;

public sealed class TagCatalogTests
{
    private static readonly DateTimeOffset T0 = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void KnownTagsIncludeOnlyNamesUsedByEntries()
    {
        var summary = new EntrySummary(
            Guid.NewGuid(),
            new EntryMetadata("Mail", DTags: ["Email"], STags: ["Sensitive"]),
            T0,
            T0);

        var catalog = TagCatalog.Create(
            [summary],
            [
                new TagColorAssignment(TagKind.Data, "Email", TagColor.Blue),
                new TagColorAssignment(TagKind.Data, "Orphan", TagColor.Pink),
                new TagColorAssignment(TagKind.Security, "Sensitive", TagColor.Red),
                new TagColorAssignment(TagKind.Security, "Unused", TagColor.Orange),
            ]);

        Assert.Equal(["Email"], catalog.KnownTags(TagKind.Data));
        Assert.Equal(["Sensitive"], catalog.KnownTags(TagKind.Security));
        Assert.DoesNotContain(catalog.KnownTags(TagKind.Data), name => name == "Orphan");
        Assert.DoesNotContain(catalog.KnownTags(TagKind.Security), name => name == "Unused");
        Assert.Equal(TagColor.Blue, catalog.GetColor(TagKind.Data, "Email"));
    }

    [Fact]
    public void ColorOnlyNamesDoNotAppearInKnownTags()
    {
        var catalog = TagCatalog.Create(
            [],
            [new TagColorAssignment(TagKind.Data, "Ghost", TagColor.Teal)]);

        Assert.Empty(catalog.KnownTags(TagKind.Data));
        Assert.Empty(catalog.KnownTags(TagKind.Security));
    }
}
