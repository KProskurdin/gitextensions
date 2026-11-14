using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Shows the image file a view model names (a path) as a bitmap; a missing path or a file that cannot be read shows
///  nothing, as upstream lists a language whose flag cannot be read without it.
/// </summary>
public sealed class BitmapFileConverter : IValueConverter
{
    public static BitmapFileConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            return value is string path && File.Exists(path) ? new Bitmap(path) : null;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
