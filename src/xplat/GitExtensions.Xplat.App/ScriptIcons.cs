using System.Diagnostics;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using GitExtensions.Xplat.Core.Scripts;

namespace GitExtensions.Xplat.App;

/// <summary>
///  A script's icon, as upstream's <c>ScriptInfo.GetIcon</c> finds it: the picture of <see cref="ScriptDefinition.IconFilePath"/>
///  when that file exists, otherwise the upstream image named <see cref="ScriptDefinition.Icon"/>. The names come from
///  upstream's own resource list (<c>GitUI/Properties/Images.resx</c>, linked into the app with the icon files), so both
///  apps show the same icon for a script.
/// </summary>
internal static class ScriptIcons
{
    private static readonly Dictionary<string, Bitmap?> _files = new(StringComparer.Ordinal);

    /// <summary>
    ///  The upstream image names a script can use, in upstream's order (by name).
    /// </summary>
    public static IReadOnlyList<string> Names => UpstreamImages.Names;

    /// <summary>
    ///  The upstream image named <paramref name="name"/>, or null when there is none. As upstream's resources, the name
    ///  matches in case.
    /// </summary>
    public static Bitmap? Named(string? name)
        => UpstreamImages.Named(name);

    /// <summary>
    ///  The icon of <paramref name="script"/>, or null. Upstream takes an .ico file's picture or the Windows "associated icon"
    ///  of any other file; the new shell takes the picture of an image file (.ico, .png, ...) and has no associated icons.
    /// </summary>
    public static Bitmap? For(ScriptDefinition script)
    {
        if (script.IconFilePath is { Length: > 0 } path && File.Exists(path))
        {
            if (!_files.TryGetValue(path, out Bitmap? picture))
            {
                picture = LoadFile(path);
                _files[path] = picture;
            }

            if (picture is not null)
            {
                return picture;
            }
        }

        return Named(script.Icon);
    }

    private static Bitmap? LoadFile(string path)
    {
        try
        {
            return new Bitmap(path);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"The icon file \"{path}\" cannot be shown: {ex.Message}");
            return null;
        }
    }
}

/// <summary>
///  One of the icons the script editor offers: an upstream image name with its picture, or no icon.
/// </summary>
public sealed record ScriptIconChoice(string Name, Bitmap? Picture);

/// <summary>
///  A script's icon from its icon name and icon file (in that order), for the script list.
/// </summary>
public sealed class ScriptIconConverter : IMultiValueConverter
{
    public static ScriptIconConverter Instance { get; } = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => ScriptIcons.For(new ScriptDefinition
        {
            Icon = values.ElementAtOrDefault(0) as string, IconFilePath = values.ElementAtOrDefault(1) as string,
        });
}
