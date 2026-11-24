using System.Windows;
using KeyHolder.Desktop.Views;

namespace KeyHolder.Desktop.Tests;

public sealed class AutoGrowWindowTests
{
    private static readonly Rect WorkArea = new(0, 0, 1920, 1040);

    [Fact]
    public void ZeroDeficitChangesNothing()
    {
        var window = new Rect(1800, 1000, 500, 300);

        Assert.Equal(window, AutoGrowWindow.Compute(window, new Size(0, 0), WorkArea));
    }

    [Fact]
    public void TheWindowGrowsByTheDeficit()
    {
        var result = AutoGrowWindow.Compute(new Rect(100, 50, 1200, 700), new Size(40, 120), WorkArea);

        Assert.Equal(new Rect(100, 50, 1240, 820), result);
    }

    [Fact]
    public void GrowthStopsAtTheWorkArea()
    {
        var result = AutoGrowWindow.Compute(new Rect(0, 0, 1200, 700), new Size(5000, 5000), WorkArea);

        Assert.Equal(1920, result.Width);
        Assert.Equal(1040, result.Height);
    }

    [Fact]
    public void AWindowThatWouldCrossAnEdgeIsMovedBackInside()
    {
        var result = AutoGrowWindow.Compute(new Rect(900, 600, 1000, 400), new Size(200, 300), WorkArea);

        Assert.Equal(1200, result.Width);
        Assert.Equal(700, result.Height);
        Assert.Equal(WorkArea.Right - 1200, result.X);
        Assert.Equal(WorkArea.Bottom - 700, result.Y);
    }

    [Fact]
    public void OnlyTheNeededDirectionGrows()
    {
        var window = new Rect(10, 10, 1200, 700);

        var result = AutoGrowWindow.Compute(window, new Size(0, 50), WorkArea);

        Assert.Equal(1200, result.Width);
        Assert.Equal(750, result.Height);
    }

    [Fact]
    public void AWindowLargerThanTheWorkAreaIsNeverShrunk()
    {
        var result = AutoGrowWindow.Compute(new Rect(0, 0, 2000, 700), new Size(10, 0), WorkArea);

        Assert.Equal(2000, result.Width);
    }

    [Fact]
    public void ANegativeWorkAreaOriginIsRespected()
    {
        var left = new Rect(-1920, 0, 1920, 1040);

        var result = AutoGrowWindow.Compute(new Rect(-500, 100, 400, 300), new Size(300, 0), left);

        Assert.Equal(-1920 + 1920 - 700, result.X);
        Assert.Equal(700, result.Width);
    }
}
