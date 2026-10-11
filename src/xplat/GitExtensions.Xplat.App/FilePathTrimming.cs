using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's "Truncate long filenames" (<c>TruncatePathMethod</c>) for the file lists: the trimming the path text uses,
///  as the <c>FilePathTrimming</c> resource, and the path text itself (<see cref="Text"/>).
/// </summary>
public static class FilePathTrimming
{
    public const string ResourceKey = "FilePathTrimming";

    /// <summary>
    ///  The path text of a file list item: the file name only when the setting asks for it.
    /// </summary>
    public static IValueConverter Text { get; } = new FuncValueConverter<string?, string>(path =>
        FilePathDisplay.Text(path ?? "", AppServices.Preferences.TruncatePathMethod));

    /// <summary>
    ///  Sets the resource for the setting: upstream's "Compact" keeps both ends of the path (as Windows'
    ///  <c>PathCompactPathEx</c>), "Trim start" drops its start; "None" cuts nothing. A list shows the new text when it is
    ///  filled again.
    /// </summary>
    public static void Apply(IAppPreferences preferences)
    {
        if (Application.Current is { } app)
        {
            app.Resources[ResourceKey] = preferences.TruncatePathMethod switch
            {
                TruncatePathMethod.Compact => TextTrimming.PathSegmentEllipsis,
                TruncatePathMethod.TrimStart => TextTrimming.LeadingCharacterEllipsis,
                TruncatePathMethod.FileNameOnly => TextTrimming.CharacterEllipsis,
                _ => TextTrimming.None,
            };
        }
    }
}

/// <summary>
///  One of upstream's truncation choices, named as upstream's list names them.
/// </summary>
public sealed record TruncatePathOption(TruncatePathMethod Method, string Text)
{
    public static IReadOnlyList<TruncatePathOption> All { get; } =
    [
        new(TruncatePathMethod.None, "None"),
        new(TruncatePathMethod.Compact, "Compact"),
        new(TruncatePathMethod.TrimStart, "Trim start"),
        new(TruncatePathMethod.FileNameOnly, "Filename only"),
    ];

    public override string ToString() => Text;
}
