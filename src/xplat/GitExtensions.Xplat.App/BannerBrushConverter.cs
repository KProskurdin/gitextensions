using System.Globalization;
using Avalonia.Data.Converters;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The colors of upstream's notification bars above the grid (<c>InteractiveGitActionControl</c>) in the current theme:
///  the background of a bar with conflicts or without, and the bar's text.
/// </summary>
public sealed class BannerBrushConverter : IValueConverter
{
    private readonly bool _text;

    private BannerBrushConverter(bool text) => _text = text;

    /// <summary>
    ///  A bar's background, from its <see cref="GitActionBanner"/>.
    /// </summary>
    public static BannerBrushConverter Background { get; } = new(text: false);

    /// <summary>
    ///  A bar's text color.
    /// </summary>
    public static BannerBrushConverter Text { get; } = new(text: true);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        ThemeBrushes.Palette palette = ThemeBrushes.Current;
        return _text ? palette.Context
            : value is GitActionBanner { HasConflicts: true } ? palette.ConflictBanner
            : palette.InfoBanner;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
