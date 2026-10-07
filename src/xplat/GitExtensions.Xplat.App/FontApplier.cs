using Avalonia;
using Avalonia.Media;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Applies upstream's four fonts to the running app as resources the views use: the application font through Fluent's own
///  font resources, the others through <c>CommitFontFamily</c>, <c>DiffFontFamily</c> and <c>MonospaceFontFamily</c> and
///  their sizes. Upstream needs a restart; here a change shows at once.
/// </summary>
internal static class FontApplier
{
    /// <summary>
    ///  The default code and monospace fonts: the first installed family wins (Windows, macOS, then the usual Linux fonts).
    /// </summary>
    public const string CodeFamilies = "Cascadia Mono, Consolas, Menlo, DejaVu Sans Mono, Liberation Mono, monospace";

    private const string ApplicationFamilyKey = "ContentControlThemeFontFamily";
    private const string ApplicationSizeKey = "ControlContentThemeFontSize";

    // Fluent's own application font, read before the first change so "(default)" can bring it back.
    private static (FontFamily Family, double Size)? _themeFont;

    public static void Apply(IAppPreferences preferences)
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        _themeFont ??= (
            app.TryGetResource(ApplicationFamilyKey, null, out object? family) && family is FontFamily themeFamily
                ? themeFamily
                : FontFamily.Default,
            app.TryGetResource(ApplicationSizeKey, null, out object? size) && size is double themeSize
                ? themeSize
                : 14);

        // Every resource is always set: a view whose dynamic resource disappears does not go back to its inherited size.
        FontSetting? application = preferences.GetFont(AppFont.Application);
        FontFamily applicationFamily =
            application is null ? _themeFont.Value.Family : new FontFamily(application.Family);
        double applicationSize = application?.SizeInPixels ?? _themeFont.Value.Size;
        app.Resources[ApplicationFamilyKey] = applicationFamily;
        app.Resources[ApplicationSizeKey] = applicationSize;

        // As upstream, the commit font defaults to the application's, the code and monospace fonts to a monospace one; a
        // family that is not installed falls back to the defaults after it, as upstream's font falls back to the system's.
        // Bold and italic are kept in the setting but not shown.
        Set(app, preferences.GetFont(AppFont.Commit), "CommitFontFamily", "CommitFontSize", applicationFamily,
            fallbacks: null, applicationSize);
        Set(app, preferences.GetFont(AppFont.Code), "DiffFontFamily", "DiffFontSize", new FontFamily(CodeFamilies),
            CodeFamilies, applicationSize);
        Set(app, preferences.GetFont(AppFont.Monospace), "MonospaceFontFamily", "MonospaceFontSize",
            new FontFamily(CodeFamilies), CodeFamilies, applicationSize);
    }

    private static void Set(Application app, FontSetting? setting, string familyKey, string sizeKey,
        FontFamily defaultFamily, string? fallbacks, double defaultSize)
    {
        app.Resources[familyKey] = setting is null ? defaultFamily
            : new FontFamily(fallbacks is null ? setting.Family : $"{setting.Family}, {fallbacks}");
        app.Resources[sizeKey] = setting?.SizeInPixels ?? defaultSize;
    }
}
