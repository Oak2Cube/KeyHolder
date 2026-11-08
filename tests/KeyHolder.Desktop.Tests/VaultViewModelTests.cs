using KeyHolder.Desktop.ViewModels;
using KeyHolder.Domain.Query;

namespace KeyHolder.Desktop.Tests;

public sealed class VaultViewModelTests
{
    [Fact]
    public async Task LoadShowsAllSeededEntriesSortedByName()
    {
        var harness = await Harness.CreateAsync();

        await harness.Vault.LoadAsync();

        Assert.Equal(10, harness.Vault.Entries.Count);
        var titles = harness.Vault.Entries.Select(entry => entry.Title).ToList();
        Assert.Equal(titles.Order(StringComparer.OrdinalIgnoreCase), titles);
        Assert.Equal("10 of 10 entries", harness.Vault.EntryCountText);
    }

    [Fact]
    public async Task SearchTextFiltersTheList()
    {
        var harness = await Harness.CreateAsync();
        await harness.Vault.LoadAsync();

        harness.Vault.SearchText = "bank";

        Assert.Equal(["Fake Bank"], harness.Vault.Entries.Select(entry => entry.Title));
    }

    [Fact]
    public async Task SearchOnlyUsesTheChosenFields()
    {
        var harness = await Harness.CreateAsync();
        await harness.Vault.LoadAsync();

        // "Finance" is an s-tag. The default fields are title, username, and email.
        harness.Vault.SearchText = "Finance";
        Assert.Empty(harness.Vault.Entries);

        harness.Vault.SearchOptions.Options.Single(option => option.Field == SearchField.STags).IsSelected = true;
        Assert.Equal(2, harness.Vault.Entries.Count);
    }

    [Fact]
    public async Task SecretTextCanNeverBeSearched()
    {
        var harness = await Harness.CreateAsync();
        await harness.Vault.LoadAsync();
        foreach (var option in harness.Vault.SearchOptions.Options)
        {
            option.IsSelected = true;
        }

        harness.Vault.SearchText = "FAKE-bank-password";

        Assert.Empty(harness.Vault.Entries);
    }

    [Fact]
    public async Task TagFiltersAreBuiltFromEntriesAndFilterTheList()
    {
        var harness = await Harness.CreateAsync();
        await harness.Vault.LoadAsync();

        Assert.Contains(harness.Vault.DTagFilters, tag => tag.Name == "Email");
        Assert.Contains(harness.Vault.STagFilters, tag => tag.Name == "Finance");

        harness.Vault.STagFilters.Single(tag => tag.Name == "Finance").IsSelected = true;
        Assert.Equal(["Fake Bank", "Fake Tax Portal"], harness.Vault.Entries.Select(entry => entry.Title));
        Assert.True(harness.Vault.HasActiveFilters);

        harness.Vault.DTagFilters.Single(tag => tag.Name == "Document").IsSelected = true;
        Assert.Equal(["Fake Tax Portal"], harness.Vault.Entries.Select(entry => entry.Title));

        harness.Vault.ClearFiltersCommand.Execute(null);
        Assert.Equal(10, harness.Vault.Entries.Count);
        Assert.False(harness.Vault.HasActiveFilters);
    }

    [Fact]
    public async Task SortByImportanceDescendingPutsMissingImportanceLast()
    {
        var harness = await Harness.CreateAsync();
        await harness.Vault.LoadAsync();

        harness.Vault.SortKey = SortKey.Importance;
        harness.Vault.IsSortDescending = true;

        var importances = harness.Vault.Entries.Select(entry => entry.Summary.Metadata.Importance).ToList();
        Assert.Equal(10, importances[0]);
        Assert.Equal(9, importances[1]);
        Assert.Equal([null, null, null], importances.TakeLast(3));
    }

    [Fact]
    public async Task SortByDateUsesLastChange()
    {
        var harness = await Harness.CreateAsync();
        await harness.Vault.LoadAsync();

        harness.Vault.SortKey = SortKey.Date;
        harness.Vault.IsSortDescending = true;

        var dates = harness.Vault.Entries.Select(entry => entry.Summary.UpdatedAt).ToList();
        Assert.Equal(dates.OrderDescending(), dates);
    }

    [Fact]
    public async Task SelectionSurvivesChangingTheSort()
    {
        var harness = await Harness.CreateAsync();
        var item = await harness.SelectAsync("Fake Mail");

        harness.Vault.SortKey = SortKey.Date;

        Assert.Equal(item.Id, harness.Vault.SelectedEntry?.Id);
        Assert.True(harness.Vault.IsViewing);
    }

    [Fact]
    public async Task SelectionIsDroppedWhenTheEntryIsFilteredOut()
    {
        var harness = await Harness.CreateAsync();
        await harness.SelectAsync("Fake Mail");

        harness.Vault.SearchText = "Bank";

        Assert.Null(harness.Vault.SelectedEntry);
        Assert.True(harness.Vault.ShowEmptyHint);
    }

    [Fact]
    public async Task DeleteAsksForConfirmationAndRemovesTheEntry()
    {
        var harness = await Harness.CreateAsync();
        await harness.SelectAsync("Fake Gym Membership");

        harness.Vault.DeleteCommand.Execute(null);

        Assert.Equal(1, harness.Dialogs.Asked);
        Assert.DoesNotContain(harness.Vault.Entries, entry => entry.Title == "Fake Gym Membership");
        Assert.Equal(9, harness.Vault.Entries.Count);
        Assert.Null(harness.Vault.SelectedEntry);
        Assert.True(harness.Vault.IsDirty);
        Assert.Equal("Changed since last replica export", harness.Vault.DirtyText);
    }

    [Fact]
    public async Task DeclinedDeleteKeepsTheEntry()
    {
        var harness = await Harness.CreateAsync();
        harness.Dialogs.Answer = false;
        await harness.SelectAsync("Fake Gym Membership");

        harness.Vault.DeleteCommand.Execute(null);

        Assert.Equal(10, harness.Vault.Entries.Count);
        Assert.False(harness.Vault.IsDirty);
    }

    [Fact]
    public async Task FreshSeededVaultIsNotDirty()
    {
        var harness = await Harness.CreateAsync();

        await harness.Vault.LoadAsync();

        Assert.False(harness.Vault.IsDirty);
        Assert.Equal("No local changes", harness.Vault.DirtyText);
    }

    [Fact]
    public async Task NewEntryFlowAddsAndSelectsTheEntry()
    {
        var harness = await Harness.CreateAsync();
        await harness.Vault.LoadAsync();

        harness.Vault.NewCommand.Execute(null);
        var editor = harness.Vault.Editor;
        Assert.NotNull(editor);
        Assert.True(harness.Vault.IsEditing);
        editor.Title = "Fake Brand New";
        editor.DTagsText = "Account, account, Email";
        editor.ImportanceText = "42";
        editor.SaveCommand.Execute(null);

        Assert.Null(harness.Vault.Editor);
        Assert.Equal(11, harness.Vault.Entries.Count);
        Assert.Equal("Fake Brand New", harness.Vault.SelectedEntry?.Title);
        Assert.Equal("Account, Email", harness.Vault.SelectedEntry?.DTagsText);
        Assert.Equal("42", harness.Vault.SelectedEntry?.ImportanceText);
        Assert.True(harness.Vault.IsDirty);
    }

    [Fact]
    public async Task CancelledEditorChangesNothing()
    {
        var harness = await Harness.CreateAsync();
        await harness.Vault.LoadAsync();

        harness.Vault.NewCommand.Execute(null);
        harness.Vault.Editor!.Title = "Never saved";
        harness.Vault.Editor.CancelCommand.Execute(null);

        Assert.Null(harness.Vault.Editor);
        Assert.Equal(10, harness.Vault.Entries.Count);
        Assert.False(harness.Vault.IsDirty);
    }
}
