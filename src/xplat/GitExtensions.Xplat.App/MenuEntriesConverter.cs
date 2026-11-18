using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.App;

/// <summary>
///  A view model's <see cref="MenuEntry"/> list as menu items, for a menu item's <c>ItemsSource</c> bound in XAML
///  (<see cref="MenuEntries"/>); the dimmed texts take the application's theme.
/// </summary>
public sealed class MenuEntriesConverter : IValueConverter
{
    private MenuEntriesConverter()
    {
    }

    public static MenuEntriesConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is IEnumerable<MenuEntry> entries && Application.Current is { } application
            ? entries.Select(entry => MenuEntries.Item(entry, application)).ToList()
            : Array.Empty<Control>();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
