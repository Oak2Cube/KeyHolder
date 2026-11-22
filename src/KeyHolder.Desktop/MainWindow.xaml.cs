using System.Windows;
using KeyHolder.Desktop.ViewModels;

namespace KeyHolder.Desktop;

public partial class MainWindow : Window
{
    // Segoe MDL2 Assets glyphs for the caption buttons.
    private const string MaximizeGlyph = "\uE922";
    private const string RestoreGlyph = "\uE923";

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        StateChanged += OnStateChanged;
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnStateChanged(object? sender, EventArgs e)
    {
        var maximized = WindowState == WindowState.Maximized;
        MaximizeButton.Content = maximized ? RestoreGlyph : MaximizeGlyph;
        MaximizeButton.ToolTip = maximized ? "Restore" : "Maximize";
    }
}