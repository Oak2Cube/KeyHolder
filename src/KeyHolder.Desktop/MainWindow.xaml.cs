using System.Windows;
using KeyHolder.Desktop.ViewModels;

namespace KeyHolder.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}