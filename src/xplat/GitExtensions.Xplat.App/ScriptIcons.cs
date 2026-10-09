using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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
    private const string AssetsRoot = "avares://GitExtensions/Assets/Upstream/";

    // Upstream's resource list points at the icon files relative to itself (..\Resources\Icons\Name.png).
    private const string IconsFolder = @"Resources\Icons\";

    private static readonly Lazy<IReadOnlyDictionary<string, Uri>> _icons = new(LoadIcons);
    private static readonly Dictionary<string, Bitmap?> _files = new(StringComparer.Ordinal);

    /// <summary>
    ///  The upstream image names a script can use, in upstream's order (by name).
    /// </summary>
    public static IReadOnlyList<string> Names => [.. _icons.Value.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    ///  The upstream image named <paramref name="name"/>, or null when there is none. As upstream's resources, the name
    ///  matches in case.
    /// </summary>
    public static Bitmap? Named(string? name)
        => string.IsNullOrWhiteSpace(name) || !_icons.Value.TryGetValue(name, out Uri? uri) ? null : Load(uri);

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

    private static Bitmap? Load(Uri uri)
    {
        try
        {
            using Stream stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"The icon \"{uri}\" cannot be shown: {ex.Message}");
            return null;
        }
    }

    // The ResXFileRef entries of upstream's list whose file is one of the linked icons; a name's file is matched without
    // case, as on Windows, where upstream's list was written (one entry is lowercase).
    private static IReadOnlyDictionary<string, Uri> LoadIcons()
    {
        Dictionary<string, Uri> files = new(StringComparer.OrdinalIgnoreCase);
        foreach (Uri asset in AssetLoader.GetAssets(new Uri(AssetsRoot + "Icons/"), null))
        {
            files[Path.GetFileName(asset.AbsolutePath)] = asset;
        }

        using Stream resx = AssetLoader.Open(new Uri(AssetsRoot + "Images.resx"));
        Dictionary<string, Uri> icons = new(StringComparer.Ordinal);
        foreach (XElement data in XDocument.Load(resx).Root?.Elements("data") ?? [])
        {
            string? name = (string?)data.Attribute("name");
            string? value = (string?)data.Element("value");
            if (name is null || value is null || !value.Contains(IconsFolder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string file = Path.GetFileName(value.Split(';')[0].Replace('\\', '/'));
            if (files.TryGetValue(file, out Uri? uri))
            {
                icons[name] = uri;
            }
        }

        return icons;
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
