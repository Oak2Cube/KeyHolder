using KeyHolder.Desktop.Tags;
using KeyHolder.Desktop.ViewModels;
using KeyHolder.Domain.Tags;

namespace KeyHolder.Desktop.Tests;

internal static class TagEditorHelpers
{
    /// <summary>
    /// Does what the user does: press "+", type a name, optionally pick a color, press OK.
    /// </summary>
    public static void AddTag(TagListEditorViewModel list, string name, TagColor? color = null)
    {
        list.AddCommand.Execute(null);
        list.NewName = name;
        if (color is { } picked)
        {
            list.SelectedColor = TagColorPalette.GetOption(picked);
        }

        Assert.True(list.OkCommand.CanExecute(null));
        list.OkCommand.Execute(null);
    }
}

public sealed class TagListEditorViewModelTests
{
    private static async Task<(Harness Harness, EntryEditorViewModel Editor)> NewEditorAsync()
    {
        var harness = await Harness.CreateAsync();
        await harness.Vault.LoadAsync();
        harness.Vault.NewCommand.Execute(null);
        return (harness, harness.Vault.Editor!);
    }

    [Fact]
    public async Task PlusOpensTheAddPanelAndOkIsDisabledUntilTheNameIsValid()
    {
        var (_, editor) = await NewEditorAsync();
        var list = editor.DTags;
        Assert.False(list.IsAdding);
        Assert.True(list.ShowAddButton);

        list.AddCommand.Execute(null);

        Assert.True(list.IsAdding);
        Assert.False(list.ShowAddButton);
        Assert.False(list.OkCommand.CanExecute(null));
        Assert.False(list.HasNewNameError);

        list.NewName = "   ";
        Assert.False(list.OkCommand.CanExecute(null));

        list.NewName = "a,b";
        Assert.False(list.OkCommand.CanExecute(null));
        Assert.True(list.HasNewNameError);

        list.NewName = "Brand new tag";
        Assert.True(list.OkCommand.CanExecute(null));
        Assert.False(list.HasNewNameError);
    }

    [Fact]
    public async Task OkAddsAColoredTagAndMinusRemovesIt()
    {
        var (_, editor) = await NewEditorAsync();
        var list = editor.DTags;

        TagEditorHelpers.AddTag(list, "  Gaming   account ", TagColor.Purple);

        Assert.False(list.IsAdding);
        Assert.True(list.ShowAddButton);
        var item = Assert.Single(list.Items);
        Assert.Equal("Gaming account", item.Name);
        Assert.Same(TagColorPalette.GetBrush(TagColor.Purple), item.Brush);
        Assert.Single(list.StagedColors);

        item.RemoveCommand.Execute(null);

        Assert.Empty(list.Items);
        Assert.Empty(list.StagedColors);
    }

    [Fact]
    public async Task ADuplicateNameIgnoringCaseCannotBeAdded()
    {
        var (_, editor) = await NewEditorAsync();
        var list = editor.DTags;
        TagEditorHelpers.AddTag(list, "Account");

        list.AddCommand.Execute(null);
        list.NewName = "account";

        Assert.False(list.OkCommand.CanExecute(null));
        Assert.True(list.HasNewNameError);
    }

    [Fact]
    public async Task ChoosingAnExistingTagAddsItAtOnceWithItsStoredColor()
    {
        var (_, editor) = await NewEditorAsync();
        var list = editor.STags;

        // No "+" press needed: the picker sits next to "+" and works directly.
        Assert.True(list.CanPickExistingTag);
        list.SelectedExisting = list.ExistingTags.Single(tag => tag.Name == "Sensitive");

        var item = Assert.Single(list.Items);
        Assert.Equal("Sensitive", item.Name);
        Assert.Same(TagColorPalette.GetBrush(TagColor.Red), item.Brush);
        Assert.False(list.IsAdding);
        Assert.Null(list.SelectedExisting);

        // The stored color is reused, so nothing needs to be saved.
        Assert.Empty(list.StagedColors);
        Assert.DoesNotContain(list.ExistingTags, tag => tag.Name == "Sensitive");
    }

    [Fact]
    public async Task ExistingTagPickerIsUnavailableWhileThePlusPanelIsOpen()
    {
        var (_, editor) = await NewEditorAsync();
        var list = editor.STags;

        list.AddCommand.Execute(null);

        Assert.False(list.CanPickExistingTag);
        list.SelectedExisting = list.ExistingTags.Single(tag => tag.Name == "Finance");
        Assert.Empty(list.Items);

        list.CancelAddCommand.Execute(null);

        Assert.True(list.CanPickExistingTag);
    }

    [Fact]
    public async Task ExistingTagListIsReadyBeforePlusIsPressed()
    {
        var (_, editor) = await NewEditorAsync();

        Assert.True(editor.STags.HasExistingTags);
        Assert.Contains(editor.STags.ExistingTags, tag => tag.Name == "Finance");
    }

    [Fact]
    public async Task RemovingATagReturnsItToTheExistingTagPicker()
    {
        var (_, editor) = await NewEditorAsync();
        var list = editor.STags;
        TagEditorHelpers.AddTag(list, "Sensitive");
        var sensitive = list.Items.Single(item => item.Name == "Sensitive");
        Assert.DoesNotContain(list.ExistingTags, tag => tag.Name == "Sensitive");

        sensitive.RemoveCommand.Execute(null);

        Assert.Contains(list.ExistingTags, tag => tag.Name == "Sensitive");
    }

    [Fact]
    public async Task TheExistingTagListHidesTagsAlreadyOnTheEntry()
    {
        var (_, editor) = await NewEditorAsync();
        var list = editor.STags;
        TagEditorHelpers.AddTag(list, "Sensitive");

        list.AddCommand.Execute(null);

        Assert.DoesNotContain(list.ExistingTags, tag => tag.Name == "Sensitive");
        Assert.Contains(list.ExistingTags, tag => tag.Name == "Finance");
    }

    [Fact]
    public async Task TypingAKnownNameBringsItsColorUntilAColorIsPicked()
    {
        var (_, editor) = await NewEditorAsync();
        var list = editor.STags;

        list.AddCommand.Execute(null);
        list.NewName = "finance";
        Assert.Equal(TagColor.Green, list.SelectedColor.Color);

        list.SelectedColor = TagColorPalette.GetOption(TagColor.Pink);
        list.NewName = "Finance";
        Assert.Equal(TagColor.Pink, list.SelectedColor.Color);

        list.OkCommand.Execute(null);

        // The known spelling is reused and the new color is staged.
        Assert.Equal("Finance", list.Items.Single().Name);
        var staged = Assert.Single(list.StagedColors);
        Assert.Equal(TagColor.Pink, staged.Color);
    }

    [Fact]
    public async Task CancelClosesThePanelWithoutAddingAnything()
    {
        var (_, editor) = await NewEditorAsync();
        var list = editor.DTags;

        list.AddCommand.Execute(null);
        list.NewName = "Maybe";
        list.CancelAddCommand.Execute(null);

        Assert.False(list.IsAdding);
        Assert.Empty(list.Items);
        Assert.Empty(list.StagedColors);
    }

    [Fact]
    public async Task SavingIsBlockedWhileTheAddPanelIsOpen()
    {
        var (harness, editor) = await NewEditorAsync();
        editor.Title = "Fake Pending Tag";
        editor.DTags.AddCommand.Execute(null);
        editor.DTags.NewName = "Pending";

        editor.SaveCommand.Execute(null);

        Assert.Same(editor, harness.Vault.Editor);
        Assert.NotEmpty(editor.ErrorMessage);
        Assert.Equal(10, harness.Vault.Entries.Count);
    }

    [Fact]
    public async Task CancellingTheEntryDiscardsStagedColors()
    {
        var (harness, editor) = await NewEditorAsync();
        editor.Title = "Fake Discarded";
        TagEditorHelpers.AddTag(editor.DTags, "Brand new", TagColor.Orange);

        editor.CancelCommand.Execute(null);

        Assert.DoesNotContain(
            await harness.Repository.ListTagColorsAsync(),
            color => color.Name == "Brand new");
        Assert.Equal(10, harness.Vault.Entries.Count);
    }

    [Fact]
    public async Task SavingStoresTagsAndColorsAndEveryViewShowsTheSameBrush()
    {
        var (harness, editor) = await NewEditorAsync();
        editor.Title = "Fake Colorful";
        TagEditorHelpers.AddTag(editor.DTags, "Gaming", TagColor.Purple);
        TagEditorHelpers.AddTag(editor.STags, "Risky", TagColor.Orange);

        editor.SaveCommand.Execute(null);

        Assert.Null(harness.Vault.Editor);
        var purple = TagColorPalette.GetBrush(TagColor.Purple);
        var orange = TagColorPalette.GetBrush(TagColor.Orange);
        var stored = await harness.Repository.ListTagColorsAsync();
        Assert.Contains(stored, color => color.Kind == TagKind.Data && color.Name == "Gaming" && color.Color == TagColor.Purple);

        // List line and detail chips.
        var selected = harness.Vault.SelectedEntry!;
        Assert.Equal("Fake Colorful", selected.Title);
        Assert.Same(purple, Assert.Single(selected.DTagList).Brush);
        Assert.Same(orange, Assert.Single(selected.STagList).Brush);

        // Filter lists.
        Assert.Same(purple, harness.Vault.DTagFilters.Single(filter => filter.Name == "Gaming").Brush);
        Assert.Same(orange, harness.Vault.STagFilters.Single(filter => filter.Name == "Risky").Brush);
    }

    [Fact]
    public async Task RecoloringATagChangesItOnEveryEntry()
    {
        var harness = await Harness.CreateAsync();
        await harness.SelectAsync("Fake Mail");
        harness.Vault.EditCommand.Execute(null);
        var editor = harness.Vault.Editor!;
        var sensitive = editor.STags.Items.Single(item => item.Name == "Sensitive");
        Assert.Same(TagColorPalette.GetBrush(TagColor.Red), sensitive.Brush);

        sensitive.RemoveCommand.Execute(null);
        TagEditorHelpers.AddTag(editor.STags, "Sensitive", TagColor.Blue);
        editor.SaveCommand.Execute(null);

        var blue = TagColorPalette.GetBrush(TagColor.Blue);
        foreach (var title in new[] { "Fake Mail", "Fake Bank", "Fake Phone PIN" })
        {
            var item = harness.Vault.Entries.Single(entry => entry.Title == title);
            Assert.Same(blue, item.STagList.Single(tag => tag.Name == "Sensitive").Brush);
        }

        Assert.Same(blue, harness.Vault.STagFilters.Single(filter => filter.Name == "Sensitive").Brush);
    }

    [Fact]
    public async Task EditingAnEntryShowsItsTagsWithTheirStoredColors()
    {
        var harness = await Harness.CreateAsync();
        await harness.SelectAsync("Fake Bank");

        harness.Vault.EditCommand.Execute(null);

        var finance = harness.Vault.Editor!.STags.Items.Single(item => item.Name == "Finance");
        Assert.Same(TagColorPalette.GetBrush(TagColor.Green), finance.Brush);
        Assert.Equal(["Account"], harness.Vault.Editor.DTags.Names);
    }

    [Fact]
    public async Task ACancelledEditLeavesTheStoredColorsAlone()
    {
        var harness = await Harness.CreateAsync();
        await harness.SelectAsync("Fake Bank");
        harness.Vault.EditCommand.Execute(null);
        var editor = harness.Vault.Editor!;

        editor.STags.Items.Single(item => item.Name == "Finance").RemoveCommand.Execute(null);
        TagEditorHelpers.AddTag(editor.STags, "Finance", TagColor.Pink);
        editor.CancelCommand.Execute(null);

        var colors = await harness.Repository.ListTagColorsAsync();
        Assert.Equal(TagColor.Green, colors.Single(color => color.Name == "Finance").Color);
    }

    [Fact]
    public void ThePaletteHasABrushForEveryColor()
    {
        Assert.Equal(Enum.GetValues<TagColor>().Length, TagColorPalette.Options.Count);
        Assert.All(Enum.GetValues<TagColor>(), color => Assert.NotNull(TagColorPalette.GetBrush(color)));
        Assert.Equal(TagColor.Default, TagColorPalette.Options[0].Color);
        Assert.True(TagColorPalette.GetBrush(TagColor.Red).IsFrozen);
    }
}
