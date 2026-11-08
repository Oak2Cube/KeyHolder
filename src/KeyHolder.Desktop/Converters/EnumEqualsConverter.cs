using System.Globalization;
using System.Windows.Data;

namespace KeyHolder.Desktop.Converters;

/// <summary>
/// Binds a RadioButton to one value of an enum property. The ConverterParameter is the enum value of the button.
/// </summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && value.Equals(parameter);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is not null ? parameter : Binding.DoNothing;
}
