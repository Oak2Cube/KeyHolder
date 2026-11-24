using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace KeyHolder.Desktop.Views;

public partial class VaultView : UserControl
{
    private bool _fitQueued;

    public VaultView()
    {
        InitializeComponent();
        AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrollChanged));
    }

    // Only the tools panel and the details/editor panel make the window grow.
    // The entry list always scrolls on its own, because it can hold many entries.
    // Only a change of the CONTENT size counts. A change of the viewport alone means the user resized the window,
    // and the window must stay as the user left it, even when the content no longer fits.
    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_fitQueued
            || e.ExtentWidthChange == 0 && e.ExtentHeightChange == 0
            || !ReferenceEquals(e.OriginalSource, ToolsScroll)
            && !ReferenceEquals(e.OriginalSource, DetailsScroll)
            && !ReferenceEquals(e.OriginalSource, EditorScroll))
        {
            return;
        }

        _fitQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, FitWindow);
    }

    private void FitWindow()
    {
        _fitQueued = false;

        if (Window.GetWindow(this) is not { } window)
        {
            return;
        }

        var right = DetailsScroll.IsVisible ? DetailsScroll : EditorScroll.IsVisible ? EditorScroll : null;
        var height = Math.Max(Overflow(ToolsScroll).Height, right is null ? 0 : Overflow(right).Height);
        var width = right is null ? 0 : Overflow(right).Width;

        AutoGrowWindow.Fit(window, width, height);
    }

    private static Size Overflow(ScrollViewer viewer) =>
        new(
            Math.Max(0, viewer.ExtentWidth - viewer.ViewportWidth),
            Math.Max(0, viewer.ExtentHeight - viewer.ViewportHeight));
}
