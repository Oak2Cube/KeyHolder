using KeyHolder.Domain.Entries;
using KeyHolder.Domain.Tags;

namespace KeyHolder.Desktop.Tests;

public sealed class EntryListItemViewModelTests
{
    [Fact]
    public async Task SeededEntriesExposeTheDefaultIcon()
    {
        var harness = await Harness.CreateAsync();

        var item = await harness.SelectAsync("Fake Mail");

        Assert.Equal(EntryIcon.Default, item.Icon);
        Assert.Equal(TagColor.Default, item.IconColor);
    }

    [Fact]
    public async Task AnEntryWithAllFieldsReportsAllOfThem()
    {
        var harness = await Harness.CreateAsync();

        var item = await harness.SelectAsync("Fake Mail");

        Assert.True(item.HasUsername);
        Assert.True(item.HasEmail);
        Assert.True(item.HasUrl);
        Assert.True(item.HasDTags);
        Assert.True(item.HasSTags);
        Assert.True(item.HasImportance);
    }

    [Fact]
    public async Task EmptyContactFieldsAreReportedAsMissing()
    {
        var harness = await Harness.CreateAsync();

        var item = await harness.SelectAsync("Fake Phone PIN");

        Assert.False(item.HasUsername);
        Assert.False(item.HasEmail);
        Assert.False(item.HasUrl);
        Assert.True(item.HasDTags);
    }

    [Fact]
    public async Task EmptyTagListsAreReportedAsMissing()
    {
        var harness = await Harness.CreateAsync();

        var item = await harness.SelectAsync("Fake Cloud Storage");

        Assert.True(item.HasUsername);
        Assert.False(item.HasEmail);
        Assert.True(item.HasDTags);
        Assert.False(item.HasSTags);
        Assert.Empty(item.STagList);
    }

    [Fact]
    public async Task EmptySecretFieldsAreReportedAsMissing()
    {
        var harness = await Harness.CreateAsync();

        var notesOnly = await harness.SelectAsync("Fake Notes Only");
        Assert.False(notesOnly.HasPassword);
        Assert.True(notesOnly.HasNotes);
        Assert.True(notesOnly.HasSecretFields);

        var passwordOnly = await harness.SelectAsync("Fake Cloud Storage");
        Assert.True(passwordOnly.HasPassword);
        Assert.False(passwordOnly.HasNotes);
        Assert.True(passwordOnly.HasSecretFields);
    }

    [Fact]
    public async Task MissingImportanceIsReportedAsMissing()
    {
        var harness = await Harness.CreateAsync();

        var item = await harness.SelectAsync("Fake Notes Only");

        Assert.False(item.HasImportance);
    }
}
