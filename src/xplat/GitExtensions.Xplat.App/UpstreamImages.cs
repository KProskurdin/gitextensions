using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's images by their resource name (<c>GitUI.Properties.Images</c>): its resource list
///  (<c>GitUI/Properties/Images.resx</c>) and icon files are linked into the app, so a toolbar, menu or tree shows the
///  picture upstream's designer gives it, under the same name.
/// </summary>
public static class UpstreamImages
{
    private const string AssetsRoot = "avares://GitExtensions/Assets/Upstream/";

    // Upstream's resource list points at its files relative to itself (..\Resources\Icons\Name.png); the linked folders, by
    // the folder the list names.
    private static readonly (string Folder, string Asset)[] _folders =
    [
        (@"Resources\Icons\", "Icons/"),
        (@"Resources\Backgrounds\", "Backgrounds/"),
        (@"setup\assets\Logo\", "Logo/"),
    ];

    private static readonly Lazy<IReadOnlyDictionary<string, Uri>> _icons = new(LoadIcons);
    private static readonly Dictionary<string, Bitmap?> _loaded = new(StringComparer.Ordinal);

    /// <summary>
    ///  Every upstream image name, by name.
    /// </summary>
    public static IReadOnlyList<string> Names => [.. _icons.Value.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    ///  The upstream image named <paramref name="name"/> (matched in case, as upstream's resources), or null when there is
    ///  none. A picture is read once and shared, as bitmaps do not change.
    /// </summary>
    public static Bitmap? Named(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || !_icons.Value.TryGetValue(name, out Uri? uri))
        {
            return null;
        }

        lock (_loaded)
        {
            if (!_loaded.TryGetValue(name, out Bitmap? picture))
            {
                picture = Load(uri);
                _loaded[name] = picture;
            }

            return picture;
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
        foreach ((_, string folder) in _folders)
        {
            foreach (Uri asset in AssetLoader.GetAssets(new Uri(AssetsRoot + folder), null))
            {
                files[folder + Path.GetFileName(asset.AbsolutePath)] = asset;
            }
        }

        using Stream resx = AssetLoader.Open(new Uri(AssetsRoot + "Images.resx"));
        Dictionary<string, Uri> icons = new(StringComparer.Ordinal);
        foreach (XElement data in XDocument.Load(resx).Root?.Elements("data") ?? [])
        {
            string? name = (string?)data.Attribute("name");
            string? value = (string?)data.Element("value");
            if (name is null || value is null
                             || _folders.FirstOrDefault(folder =>
                                     value.Contains(folder.Folder, StringComparison.OrdinalIgnoreCase))
                                 is not { Asset: { } assetFolder })
            {
                continue;
            }

            string file = Path.GetFileName(value.Split(';')[0].Replace('\\', '/'));
            if (files.TryGetValue(assetFolder + file, out Uri? uri))
            {
                icons[name] = uri;
            }
        }

        return icons;
    }
}

/// <summary>
///  <c>{local:UpstreamImage Pull}</c>: upstream's picture of that name, for an <see cref="Image"/>'s source.
/// </summary>
public sealed class UpstreamImageExtension(string name) : MarkupExtension
{
    // A name that is not upstream's leaves the property unset.
    public override object ProvideValue(IServiceProvider serviceProvider)
        => (object?)UpstreamImages.Named(name) ?? AvaloniaProperty.UnsetValue;
}

/// <summary>
///  <c>Icon="{local:UpstreamIcon Pull}"</c>: upstream's picture of that name as a 16-pixel icon, for a menu item, as
///  upstream's menus show it.
/// </summary>
public sealed class UpstreamIconExtension(string name) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider)
        => UpstreamImages.Named(name) is { } picture ? ToolStripContent.Icon(picture) : AvaloniaProperty.UnsetValue;
}

/// <summary>
///  The content of a toolstrip item as upstream's <c>ToolStripItem</c> draws it: the image, then the text when there is
///  one (upstream's <c>ImageAndText</c>), or only the image.
/// </summary>
internal static class ToolStripContent
{
    // Upstream's toolstrip images are 16 by 16 pixels at 100 %.
    public const double IconSize = 16;

    public static Image Icon(Bitmap picture) => new()
    {
        Source = picture, Width = IconSize, Height = IconSize, VerticalAlignment = VerticalAlignment.Center,
    };

    // The text is upstream's English; it shows in the current language, also when a view sets it after the window was
    // translated (e.g. the Commit button's count).
    public static object? Build(string? image, string? text)
    {
        string? shown = string.IsNullOrEmpty(text) ? text : UpstreamTranslation.ByText(text);
        Bitmap? picture = UpstreamImages.Named(image);
        if (picture is null)
        {
            return shown;
        }

        if (string.IsNullOrEmpty(shown))
        {
            return Icon(picture);
        }

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children =
            {
                Icon(picture), new AccessText { Text = shown, VerticalAlignment = VerticalAlignment.Center }
            },
        };
    }
}

/// <summary>
///  A toolstrip button (upstream's <c>ToolStripButton</c>): flat, upstream's image and an optional text.
/// </summary>
public class ToolButton : Button
{
    public static readonly StyledProperty<string?> ImageProperty =
        AvaloniaProperty.Register<ToolButton, string?>(nameof(Image));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ToolButton, string?>(nameof(Text));

    public ToolButton() => Classes.Add("tool");

    /// <summary>
    ///  Upstream's image name (<c>GitUI.Properties.Images</c>).
    /// </summary>
    public string? Image
    {
        get => GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(Button);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ImageProperty || change.Property == TextProperty)
        {
            Content = ToolStripContent.Build(Image, Text);
        }
    }
}

/// <summary>
///  A toolstrip button that stays pressed while checked (upstream's <c>ToolStripButton</c> with <c>Checked</c>).
/// </summary>
public class ToolToggleButton : ToggleButton
{
    public static readonly StyledProperty<string?> ImageProperty =
        AvaloniaProperty.Register<ToolToggleButton, string?>(nameof(Image));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ToolToggleButton, string?>(nameof(Text));

    public ToolToggleButton() => Classes.Add("tool");

    public string? Image
    {
        get => GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ToggleButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ImageProperty || change.Property == TextProperty)
        {
            Content = ToolStripContent.Build(Image, Text);
        }
    }
}

/// <summary>
///  A toolstrip drop-down button (upstream's <c>ToolStripDropDownButton</c>): the image and text open the flyout.
/// </summary>
public class ToolDropDownButton : DropDownButton
{
    public static readonly StyledProperty<string?> ImageProperty =
        AvaloniaProperty.Register<ToolDropDownButton, string?>(nameof(Image));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ToolDropDownButton, string?>(nameof(Text));

    public ToolDropDownButton() => Classes.Add("tool");

    public string? Image
    {
        get => GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(DropDownButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ImageProperty || change.Property == TextProperty)
        {
            Content = ToolStripContent.Build(Image, Text);
        }
    }
}

/// <summary>
///  A toolstrip split button (upstream's <c>ToolStripSplitButton</c>): the image and text run the default action, the
///  arrow opens the other choices.
/// </summary>
public class ToolSplitButton : SplitButton
{
    public static readonly StyledProperty<string?> ImageProperty =
        AvaloniaProperty.Register<ToolSplitButton, string?>(nameof(Image));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ToolSplitButton, string?>(nameof(Text));

    public ToolSplitButton() => Classes.Add("tool");

    public string? Image
    {
        get => GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(SplitButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ImageProperty || change.Property == TextProperty)
        {
            Content = ToolStripContent.Build(Image, Text);
        }
    }
}

/// <summary>
///  Upstream's image by its resource name, for a binding (the left panel's node images).
/// </summary>
public sealed class UpstreamImageConverter : IValueConverter
{
    public static UpstreamImageConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => UpstreamImages.Named(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
///  Bold for true, as upstream's tree nodes set <c>FontStyle.Bold</c> for the checked-out branch.
/// </summary>
public sealed class BoldConverter : IValueConverter
{
    public static BoldConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? FontWeight.Bold : FontWeight.Normal;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
