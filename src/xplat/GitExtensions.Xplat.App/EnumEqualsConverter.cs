using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Binds a radio button of a group to one value of an enum (MVVM.md): checked while the bound value equals the converter
///  parameter, and checking it sets the value to the parameter; unchecking it changes nothing, as another button of the
///  group sets the value then.
/// </summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public static EnumEqualsConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Equals(value, parameter);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? parameter : BindingOperations.DoNothing;
}
