using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using GitExtensions.Extensibility.BuildServerIntegration;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Colors a build status as upstream's <c>BuildStatusColumnProvider</c> does: green for success, red for failure, blue
///  while running, orange red when unstable, gray when stopped, and the text color when unknown. Upstream uses its lighter
///  colors on a selected row; here they are used on a dark theme, where the darker ones cannot be read.
/// </summary>
public sealed class BuildStatusBrushConverter : IValueConverter
{
    private static readonly IBrush _lightBlue = new SolidColorBrush(Color.FromRgb(130, 180, 240)).ToImmutable();

    public static BuildStatusBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        ThemeBrushes.Palette palette = ThemeBrushes.Current;
        bool dark = palette.IsDark;
        return value switch
        {
            BuildStatus.Success => dark ? Brushes.LightGreen : Brushes.DarkGreen,
            BuildStatus.Failure => dark ? Brushes.Red : Brushes.DarkRed,
            BuildStatus.InProgress => dark ? _lightBlue : Brushes.Blue,
            BuildStatus.Unstable => Brushes.OrangeRed,
            BuildStatus.Stopped => dark ? Brushes.LightGray : Brushes.Gray,
            _ => palette.Context,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
