using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Shows a view model's <see cref="System.Drawing.Color"/> (the colors upstream's rules compute) as a brush.
/// </summary>
public sealed class DrawingColorBrushConverter : IValueConverter
{
    public static DrawingColorBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is System.Drawing.Color color
            ? new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B))
            : BindingOperations.DoNothing;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
