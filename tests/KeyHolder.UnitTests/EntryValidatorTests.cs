using KeyHolder.Domain.Entries;

namespace KeyHolder.UnitTests;

public sealed class EntryValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TitleOnlyEntryIsValid()
    {
        Assert.Empty(EntryValidator.Validate(new EntryMetadata("Example Mail")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankTitleIsRejected(string title)
    {
        var errors = EntryValidator.Validate(new EntryMetadata(title));

        Assert.Contains(errors, error => error.Field == EntryFields.Title);
    }

    [Fact]
    public void TitleIsTrimmed()
    {
        var entry = VaultEntry.Create(new EntryMetadata("  Example Mail  "), EntrySecret.Empty, Now);

        Assert.Equal("Example Mail", entry.Metadata.Title);
    }

    [Fact]
    public void BlankOptionalFieldsBecomeNull()
    {
        var metadata = EntryValidator.Normalize(new EntryMetadata("A", "  ", "", " "));

        Assert.Null(metadata.Username);
        Assert.Null(metadata.Email);
        Assert.Null(metadata.Url);
    }

    [Fact]
    public void TagsAreTrimmedDeduplicatedIgnoringCaseAndBlankDropped()
    {
        var tags = EntryValidator.NormalizeTags([" Email ", "email", "", "  ", "Account", "EMAIL"]);

        Assert.Equal(["Email", "Account"], tags);
    }

    [Fact]
    public void TooManyTagsAreRejected()
    {
        var tags = Enumerable.Range(0, EntryLimits.MaxTagsPerList + 1).Select(i => $"tag{i}").ToList();

        var errors = EntryValidator.Validate(new EntryMetadata("A", DTags: tags));

        Assert.Contains(errors, error => error.Field == EntryFields.DTags);
    }

    [Fact]
    public void TooLongTagIsRejected()
    {
        var errors = EntryValidator.Validate(
            new EntryMetadata("A", STags: [new string('x', EntryLimits.MaxTagLength + 1)]));

        Assert.Contains(errors, error => error.Field == EntryFields.STags);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void ImportanceMustBeInRange(int importance, bool valid)
    {
        var errors = EntryValidator.Validate(new EntryMetadata("A", Importance: importance));

        Assert.Equal(valid, errors.Count == 0);
    }

    [Fact]
    public void ImportanceIsOptional()
    {
        Assert.Empty(EntryValidator.Validate(new EntryMetadata("A", Importance: null)));
    }

    [Fact]
    public void TooLongTitleIsRejected()
    {
        var errors = EntryValidator.Validate(new EntryMetadata(new string('x', EntryLimits.MaxTitleLength + 1)));

        Assert.Contains(errors, error => error.Field == EntryFields.Title);
    }

    [Fact]
    public void TooLongNotesAreRejected()
    {
        var errors = EntryValidator.Validate(new EntrySecret(Notes: new string('x', EntryLimits.MaxNotesLength + 1)));

        Assert.Contains(errors, error => error.Field == EntryFields.Notes);
    }

    [Fact]
    public void PasswordIsNotTrimmed()
    {
        var secret = EntryValidator.Normalize(new EntrySecret("  FAKE-spaces  "));

        Assert.Equal("  FAKE-spaces  ", secret.Password);
    }

    [Fact]
    public void CreateThrowsForInvalidEntry()
    {
        var exception = Assert.Throws<EntryValidationException>(
            () => VaultEntry.Create(new EntryMetadata(" "), EntrySecret.Empty, Now));

        Assert.Contains(exception.Errors, error => error.Field == EntryFields.Title);
    }

    [Fact]
    public void CreateSetsIdAndTimes()
    {
        var entry = VaultEntry.Create(new EntryMetadata("A"), EntrySecret.Empty, Now);

        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal(Now, entry.CreatedAt);
        Assert.Equal(Now, entry.UpdatedAt);
    }

    [Fact]
    public void SecretToStringDoesNotLeakValues()
    {
        var text = new EntrySecret("FAKE-password", "FAKE-note").ToString();

        Assert.DoesNotContain("FAKE", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PlaceholderIsFixedAndShowsNoLength()
    {
        Assert.Equal("••••••••", SecretPlaceholder.Value);
    }
}
