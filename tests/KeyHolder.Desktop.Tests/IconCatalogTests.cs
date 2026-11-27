using KeyHolder.Desktop.Icons;
using KeyHolder.Domain.Entries;

namespace KeyHolder.Desktop.Tests;

public sealed class IconCatalogTests
{
    [Fact]
    public void EveryEntryIconHasAGeometry()
    {
        Assert.All(
            Enum.GetValues<EntryIcon>(),
            icon => Assert.NotNull(IconCatalog.GetGeometry(icon)));
    }

    [Fact]
    public void OptionsCoverEveryEntryIconOnce()
    {
        Assert.Equal(Enum.GetValues<EntryIcon>().Length, IconCatalog.Options.Count);
        Assert.Equal(
            Enum.GetValues<EntryIcon>().OrderBy(icon => icon),
            IconCatalog.Options.Select(option => option.Icon).OrderBy(icon => icon));
    }
}
