using System.Windows;
using System.Windows.Controls;

namespace KeyHolder.Desktop.Views;

/// <summary>
/// Lets a <see cref="PasswordBox"/> bind to a view model string (PasswordBox.Password is not bindable).
/// </summary>
public static class PasswordBoxBinding
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordBoxBinding),
            new FrameworkPropertyMetadata(

                // The default must differ from the view model's initial empty text, or the first binding
                // would not raise the change callback and the PasswordChanged handler would never attach.
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnBoundPasswordChanged));

    private static readonly DependencyProperty IsUpdatingProperty =
        DependencyProperty.RegisterAttached(
            "IsUpdating",
            typeof(bool),
            typeof(PasswordBoxBinding),
            new PropertyMetadata(false));

    public static string GetBoundPassword(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (string?)element.GetValue(BoundPasswordProperty) ?? string.Empty;
    }

    public static void SetBoundPassword(DependencyObject element, string value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(BoundPasswordProperty, value);
    }

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box)
        {
            return;
        }

        box.PasswordChanged -= OnPasswordChanged;

        if (!(bool)box.GetValue(IsUpdatingProperty))
        {
            box.Password = e.NewValue as string ?? string.Empty;
        }

        box.PasswordChanged += OnPasswordChanged;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        box.SetValue(IsUpdatingProperty, true);
        SetBoundPassword(box, box.Password);
        box.SetValue(IsUpdatingProperty, false);
    }
}
