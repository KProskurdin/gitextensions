using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Input;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The hand cursor over something that opens a link (a non-empty address), else the default one: upstream's grid shows the
///  hand over a build status that has a report (<c>RevisionGridControl.OnGridViewCellMouseMove</c>).
/// </summary>
public sealed class LinkCursorConverter : IValueConverter
{
    private static readonly Cursor _hand = new(StandardCursorType.Hand);

    public static LinkCursorConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string url && !string.IsNullOrWhiteSpace(url) ? _hand : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
