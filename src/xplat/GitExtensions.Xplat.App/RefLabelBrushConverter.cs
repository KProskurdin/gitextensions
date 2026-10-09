using System.Globalization;
using Avalonia.Data.Converters;
using GitExtensions.Xplat.Core.CommitHistory;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Colors a ref label by its kind, as upstream's revision grid does: HEAD, local branches, remote branches, tags and
///  bisect marks.
/// </summary>
public sealed class RefLabelBrushConverter : IValueConverter
{
    public static RefLabelBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        ThemeBrushes.Palette palette = ThemeBrushes.Current;
        return value switch
        {
            RefKind.Head => palette.BranchLabel,
            RefKind.Tag => palette.TagLabel,
            RefKind.RemoteBranch => palette.RemoteBranchLabel,
            RefKind.BisectGood => palette.BisectGoodLabel,
            RefKind.BisectBad => palette.BisectBadLabel,
            _ => palette.BranchLabel,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
