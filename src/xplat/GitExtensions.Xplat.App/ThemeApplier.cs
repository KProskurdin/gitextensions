using Avalonia;
using Avalonia.Platform;
using Avalonia.Styling;
using GitExtensions.Xplat.Core.Settings;
using GitExtUtils.GitUI.Theming;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Applies the theme preference to the running app: loads the upstream theme's colors (<see cref="IAppThemeService"/>),
///  hands them to <see cref="ThemeBrushes"/>, and sets Avalonia's light or dark variant to match the theme. With
///  "follow the operating system" the colors are loaded again whenever the system switches between light and dark.
/// </summary>
internal static class ThemeApplier
{
    private static bool _followsSystem;
    private static bool _listening;

    /// <summary>
    ///  Raised after new theme colors are in <see cref="ThemeBrushes"/>, so views that cached brushes rebuild them.
    /// </summary>
    public static event EventHandler? Changed;

    /// <summary>
    ///  The message when the chosen theme could not be read and upstream's default theme is used instead.
    /// </summary>
    public static string? LastError { get; private set; }

    public static void Apply(IAppPreferences preferences)
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        _followsSystem = preferences.Theme == ThemeId.WindowsAppColorModeId;
        if (!_listening && app.PlatformSettings is { } platform)
        {
            _listening = true;
            platform.ColorValuesChanged += (_, _) =>
            {
                if (_followsSystem)
                {
                    Apply(AppServices.Preferences);
                }
            };
        }

        AppThemeColors colors =
            AppServices.Themes.Load(preferences.Theme, preferences.ThemeVariations, SystemIsDark(app));
        LastError = colors.Error;
        ThemeBrushes.Use(colors);
        app.RequestedThemeVariant = colors.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;

        // Upstream's panel, editor, line number and selection colors: windows and panels, the diff and file views, their
        // line numbers, and the selected lines of a diff.
        SetBrush(app, PanelBackgroundKey, colors.Get(AppColor.PanelBackground));
        SetBrush(app, EditorBackgroundKey, colors.Get(AppColor.EditorBackground));
        SetBrush(app, LineNumberBackgroundKey, colors.Get(AppColor.LineNumberBackground));
        SetBrush(app, SelectionKey, colors.Get(AppColor.Selection));

        // Upstream's system color overrides, where the theme sets them, on the Fluent resources that play their part: the
        // window text, the highlight (Fluent's accent, which selections and focus use) and the disabled text.
        Override(app, TextKey, colors.Get(System.Drawing.KnownColor.WindowText), brush: true);
        Override(app, DisabledTextKey, colors.Get(System.Drawing.KnownColor.GrayText), brush: true);
        Override(app, AccentKey, colors.Get(System.Drawing.KnownColor.Highlight), brush: false);

        // Upstream's revision grid rows (RevisionDataGridView): the system highlight for the selected row, the
        // InactiveSelectionHighlight while the grid has no focus, AuthoredHighlight for the author's commits, every other
        // row a shade darker than the panel, and the commit body in the gray text color.
        System.Drawing.Color panel = colors.Get(AppColor.PanelBackground) is { IsEmpty: false } themedPanel
            ? themedPanel
            : colors.IsDark
                ? System.Drawing.Color.FromArgb(0x20, 0x20, 0x20)
                : System.Drawing.SystemColors.Control;
        SetBrush(app, GridSelectionKey, SystemColor(colors, System.Drawing.KnownColor.Highlight));
        SetBrush(app, GridSelectionTextKey, SystemColor(colors, System.Drawing.KnownColor.HighlightText));
        SetBrush(app, GridInactiveSelectionKey, colors.Get(AppColor.InactiveSelectionHighlight));
        SetBrush(app, AuthoredHighlightKey, colors.Get(AppColor.AuthoredHighlight));
        SetBrush(app, AlternateRowKey, panel.MakeDarkerBy(colors.IsDark ? -0.018 : 0.025));
        SetBrush(app, GridBodyKey, SystemColor(colors, System.Drawing.KnownColor.GrayText));
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public const string PanelBackgroundKey = "PanelBackgroundBrush";
    public const string EditorBackgroundKey = "EditorBackgroundBrush";
    public const string LineNumberBackgroundKey = "LineNumberBackgroundBrush";
    public const string SelectionKey = "SelectionBackgroundBrush";
    public const string TextKey = "SystemControlForegroundBaseHighBrush";
    public const string DisabledTextKey = "SystemControlDisabledBaseMediumLowBrush";
    public const string AccentKey = "SystemAccentColor";
    public const string GridSelectionKey = "GridSelectionBrush";
    public const string GridSelectionTextKey = "GridSelectionTextBrush";
    public const string GridInactiveSelectionKey = "GridInactiveSelectionBrush";
    public const string AuthoredHighlightKey = "AuthoredHighlightBrush";
    public const string AlternateRowKey = "AlternateRowBrush";
    public const string GridBodyKey = "GridBodyBrush";

    // A system color as the theme sets it, else the system's (on Windows the user's, elsewhere .NET's Windows defaults).
    private static System.Drawing.Color SystemColor(AppThemeColors colors, System.Drawing.KnownColor name)
        => colors.Get(name) is { IsEmpty: false } themed ? themed : System.Drawing.Color.FromKnownColor(name);

    // The app's values of the keys a theme overrides now (Avalonia keeps the platform's accent there), put back when a theme
    // without the override is applied. A key no theme overrides is left as it is.
    private static readonly Dictionary<string, object?> _systemValues = [];

    private static void Override(Application app, string key, System.Drawing.Color color, bool brush)
    {
        if (!color.IsEmpty)
        {
            if (!_systemValues.ContainsKey(key))
            {
                _systemValues[key] = app.Resources.TryGetValue(key, out object? value) ? value : null;
            }

            Avalonia.Media.Color themed = Avalonia.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
            app.Resources[key] = brush ? new Avalonia.Media.SolidColorBrush(themed).ToImmutable() : themed;
        }
        else if (_systemValues.Remove(key, out object? system))
        {
            if (system is null)
            {
                app.Resources.Remove(key);
            }
            else
            {
                app.Resources[key] = system;
            }
        }
    }

    // A slot the theme leaves empty keeps Fluent's color for the variant.
    private static void SetBrush(Application app, string key, System.Drawing.Color color)
    {
        if (color.IsEmpty)
        {
            app.Resources.Remove(key);
            return;
        }

        app.Resources[key] = new Avalonia.Media.SolidColorBrush(
            Avalonia.Media.Color.FromArgb(color.A, color.R, color.G, color.B)).ToImmutable();
    }

    private static bool SystemIsDark(Application app)
        => app.PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark;
}
