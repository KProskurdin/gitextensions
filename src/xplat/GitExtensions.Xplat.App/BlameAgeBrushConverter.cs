using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using GitExtensions.Xplat.Core.CommitHistory;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's blame age colors (<c>BlameControl.GetAgeBucketGradientColors</c>, ColorBrewer's seven greens), from the
///  oldest bucket to the newest. Upstream adapts them to a dark theme; here their lightness is turned around.
/// </summary>
public sealed class BlameAgeBrushConverter : IValueConverter
{
    private static readonly Color[] _gradient =
    [
        Color.FromRgb(247, 252, 245),
        Color.FromRgb(199, 233, 192),
        Color.FromRgb(161, 217, 155),
        Color.FromRgb(116, 196, 118),
        Color.FromRgb(65, 171, 93),
        Color.FromRgb(35, 139, 69),
        Color.FromRgb(0, 68, 27),
    ];

    public static BlameAgeBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int bucket = value is int index ? Math.Clamp(index, 0, BlameOptions.AgeBucketCount - 1) : 0;
        Color color = _gradient[bucket];
        if (ThemeBrushes.Current.IsDark)
        {
            HslColor hsl = color.ToHsl();
            color = new HslColor(hsl.A, hsl.H, hsl.S, 1 - hsl.L).ToRgb();
        }

        return new SolidColorBrush(color);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
