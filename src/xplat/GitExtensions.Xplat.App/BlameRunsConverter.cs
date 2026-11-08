using System.Globalization;
using Avalonia.Data.Converters;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;

namespace GitExtensions.Xplat.App;

/// <summary>
///  A blame row's syntax colors as the colored parts of its text, in the theme's colors.
/// </summary>
public sealed class BlameRunsConverter : IValueConverter
{
    public static BlameRunsConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not BlameRow { Syntax: { } syntax } row)
        {
            return null;
        }

        return syntax.Select(run => new DiffTextRun(row.Content.Substring(run.Start, run.Length),
            run.Style?.Color is { } color ? ThemeBrushes.Syntax(color) : null,
            run.Style?.Bold == true, run.Style?.Italic == true)).ToList();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
