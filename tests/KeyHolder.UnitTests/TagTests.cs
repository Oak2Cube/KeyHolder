using KeyHolder.Domain.Entries;
using KeyHolder.Domain.Tags;

namespace KeyHolder.UnitTests;

public sealed class TagTests
{
    [Fact]
    public void ANormalTagIsValid()
    {
        Assert.Null(EntryValidator.ValidateNewTag("Email", []));
        Assert.Null(EntryValidator.ValidateNewTag("Phone number", ["Email"]));
        Assert.Null(EntryValidator.ValidateNewTag("Финансы", ["Email"]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyTagIsRejected(string? name)
    {
        Assert.NotNull(EntryValidator.ValidateNewTag(name, []));
    }

    [Fact]
    public void ATooLongTagIsRejectedButTheLimitItselfIsFine()
    {
        Assert.Null(EntryValidator.ValidateNewTag(new string('x', EntryLimits.MaxTagLength), []));
        Assert.NotNull(EntryValidator.ValidateNewTag(new string('x', EntryLimits.MaxTagLength + 1), []));
    }

    [Theory]
    [InlineData("a,b")]
    [InlineData("a;b")]
    [InlineData("bad\u0007bell")]
    [InlineData("bad\u0000nul")]
    public void SeparatorsAndControlCharactersAreRejected(string name)
    {
        Assert.NotNull(EntryValidator.ValidateNewTag(name, []));
    }

    [Theory]
    [InlineData("email")]
    [InlineData("EMAIL")]
    [InlineData("  Email  ")]
    public void ADuplicateIgnoringCaseAndSpacesIsRejected(string name)
    {
        Assert.NotNull(EntryValidator.ValidateNewTag(name, ["Email"]));
    }

    [Fact]
    public void InnerWhiteSpaceIsCollapsedBeforeComparing()
    {
        Assert.Equal("Phone number", EntryValidator.NormalizeTag("  Phone    number "));
        Assert.NotNull(EntryValidator.ValidateNewTag("phone   NUMBER", ["Phone number"]));
    }

    [Fact]
    public void AFullListRejectsAnotherTag()
    {
        var full = Enumerable.Range(0, EntryLimits.MaxTagsPerList).Select(i => $"tag{i}").ToList();

        Assert.NotNull(EntryValidator.ValidateNewTag("one more", full));
        Assert.Null(EntryValidator.ValidateNewTag("one more", full.Take(EntryLimits.MaxTagsPerList - 1).ToList()));
    }

    [Fact]
    public void TabsAndLineBreaksInsideATagBecomeOneSpace()
    {
        Assert.Equal("bad tab", EntryValidator.NormalizeTag("bad\ttab"));
        Assert.Equal("two lines", EntryValidator.NormalizeTag("two\r\nlines"));
    }

    [Fact]
    public void EntryValidationRejectsTagsWithSeparators()
    {
        var errors = EntryValidator.Validate(new EntryMetadata("A", DTags: ["a,b"], STags: ["c;d"]));

        Assert.Contains(errors, error => error.Field == EntryFields.DTags);
        Assert.Contains(errors, error => error.Field == EntryFields.STags);
    }

    [Fact]
    public void NormalizedTagListsCollapseWhiteSpaceAndDuplicates()
    {
        var tags = EntryValidator.NormalizeTags(["Phone  number", "phone number", " Email"]);

        Assert.Equal(["Phone number", "Email"], tags);
    }

    [Theory]
    [InlineData("Blue", TagColor.Blue)]
    [InlineData("blue", TagColor.Blue)]
    [InlineData(" Red ", TagColor.Red)]
    [InlineData("Default", TagColor.Default)]
    [InlineData("Chartreuse", TagColor.Default)]
    [InlineData("", TagColor.Default)]
    [InlineData(null, TagColor.Default)]
    [InlineData("3", TagColor.Default)]
    [InlineData("999", TagColor.Default)]
    public void StoredColorNamesParseWithFallbackToDefault(string? text, TagColor expected)
    {
        Assert.Equal(expected, TagColors.Parse(text));
    }

    [Fact]
    public void EveryColorSurvivesAStorageRoundTrip()
    {
        Assert.All(
            Enum.GetValues<TagColor>(),
            color => Assert.Equal(color, TagColors.Parse(TagColors.ToStorageText(color))));
    }

    [Fact]
    public void KindCodesRoundTripAndUnknownCodesAreNull()
    {
        Assert.Equal(TagKind.Data, TagColors.ParseKindCode(TagColors.ToKindCode(TagKind.Data)));
        Assert.Equal(TagKind.Security, TagColors.ParseKindCode(TagColors.ToKindCode(TagKind.Security)));
        Assert.Null(TagColors.ParseKindCode("x"));
    }

    [Fact]
    public void ColorLookupIsCaseInsensitiveAndSeparatePerList()
    {
        var lookup = new TagColorLookup(
        [
            new TagColorAssignment(TagKind.Data, "Email", TagColor.Blue),
            new TagColorAssignment(TagKind.Security, "Email", TagColor.Red),
        ]);

        Assert.Equal(TagColor.Blue, lookup.Get(TagKind.Data, "EMAIL"));
        Assert.Equal(TagColor.Red, lookup.Get(TagKind.Security, "email"));
        Assert.Equal(TagColor.Default, lookup.Get(TagKind.Data, "Unknown"));
    }

    [Fact]
    public void NormalizingAssignmentsKeepsTheLastOnePerTag()
    {
        var result = TagColorAssignments.Normalize(
        [
            new TagColorAssignment(TagKind.Data, " Email ", TagColor.Blue),
            new TagColorAssignment(TagKind.Data, "EMAIL", TagColor.Red),
        ]);

        var single = Assert.Single(result);
        Assert.Equal(TagColor.Red, single.Color);
        Assert.Equal("EMAIL", single.Name);
    }

    [Fact]
    public void NormalizingAssignmentsRejectsInvalidNamesAndUnknownColors()
    {
        Assert.Throws<EntryValidationException>(
            () => TagColorAssignments.Normalize([new TagColorAssignment(TagKind.Data, " ", TagColor.Blue)]));
        Assert.Throws<EntryValidationException>(
            () => TagColorAssignments.Normalize([new TagColorAssignment(TagKind.Data, "a;b", TagColor.Blue)]));
        Assert.Throws<EntryValidationException>(
            () => TagColorAssignments.Normalize([new TagColorAssignment(TagKind.Data, "ok", (TagColor)999)]));
    }
}
