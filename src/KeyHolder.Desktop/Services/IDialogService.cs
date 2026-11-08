using System.Windows;

namespace KeyHolder.Desktop.Services;

/// <summary>
/// Simple user prompts, behind an interface so view models do not depend on message boxes.
/// </summary>
public interface IDialogService
{
    /// <returns><see langword="true"/> when the user confirms.</returns>
    bool Confirm(string title, string message);
}

public sealed class WpfDialogService : IDialogService
{
    public bool Confirm(string title, string message) =>
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
}
