using KeyHolder.Desktop.ViewModels;
using KeyHolder.Domain.Query;

namespace KeyHolder.Desktop.Tests;

public sealed class SearchOptionsViewModelTests
{
    [Fact]
    public void DefaultSelectionIsTitleUsernameAndEmail()
    {
        var options = new SearchOptionsViewModel();

        Assert.Equal(
            [SearchField.Title, SearchField.Username, SearchField.Email],
            options.Selection.Fields);
        Assert.Equal(6, options.Options.Count);
    }

    [Fact]
    public void TheLastSelectedFieldCannotBeSwitchedOff()
    {
        var options = new SearchOptionsViewModel();
        options.Options.Single(o => o.Field == SearchField.Username).IsSelected = false;
        options.Options.Single(o => o.Field == SearchField.Email).IsSelected = false;

        var title = options.Options.Single(o => o.Field == SearchField.Title);

        Assert.False(title.CanToggle);
        Assert.Single(options.Selection.Fields);

        // Even a direct write cannot leave search without a field.
        title.IsSelected = false;
        Assert.Single(options.Selection.Fields);
    }

    [Fact]
    public void AllSixFieldsCanBeSelected()
    {
        var options = new SearchOptionsViewModel();

        foreach (var option in options.Options)
        {
            option.IsSelected = true;
        }

        Assert.Equal(6, options.Selection.Fields.Count);
    }

    [Fact]
    public void ChangingTheSelectionRaisesAnEvent()
    {
        var options = new SearchOptionsViewModel();
        var raised = 0;
        options.SelectionChanged += (_, _) => raised++;

        options.Options.Single(o => o.Field == SearchField.Url).IsSelected = true;

        Assert.Equal(1, raised);
    }
}
