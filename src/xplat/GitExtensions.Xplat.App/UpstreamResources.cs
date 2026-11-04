using System.Xml.Linq;
using Avalonia.Platform;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's string resources (<c>GitUI.Properties.Resources</c>), read from its linked <c>Resources.resx</c>, whose
///  generated class needs WinForms.
/// </summary>
internal static class UpstreamResources
{
    private const string ResourcesUri = "avares://GitExtensions/Assets/Upstream/Resources.resx";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> _strings = new(Load);

    public static string Team => Text(nameof(Team));

    public static string Coders => Text(nameof(Coders));

    public static string Translators => Text(nameof(Translators));

    public static string Designers => Text(nameof(Designers));

    /// <summary>
    ///  The string resource <paramref name="name"/>, or empty when there is none.
    /// </summary>
    public static string Text(string name) => _strings.Value.GetValueOrDefault(name, "");

    // Only the plain strings: the file references (images) have a type attribute.
    private static IReadOnlyDictionary<string, string> Load()
    {
        using Stream resx = AssetLoader.Open(new Uri(ResourcesUri));
        Dictionary<string, string> strings = new(StringComparer.Ordinal);
        foreach (XElement data in XDocument.Load(resx).Root?.Elements("data") ?? [])
        {
            if ((string?)data.Attribute("name") is { } name && data.Attribute("type") is null
                                                           && (string?)data.Element("value") is { } value)
            {
                strings[name] = value;
            }
        }

        return strings;
    }
}
