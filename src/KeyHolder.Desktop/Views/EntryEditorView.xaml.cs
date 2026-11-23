using System.Windows;
using System.Windows.Controls;

namespace KeyHolder.Desktop.Views;

public partial class EntryEditorView : UserControl
{
    public EntryEditorView()
    {
        InitializeComponent();
    }

    // When the add panel opens, put the cursor into the tag name box.
    private void OnTagNameVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox box && e.NewValue is true)
        {
            box.Dispatcher.BeginInvoke(() => box.Focus(), System.Windows.Threading.DispatcherPriority.Input);
        }
    }
}
