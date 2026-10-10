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
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public const string PanelBackgroundKey = "PanelBackgroundBrush";
    public const string EditorBackgroundKey = "EditorBackgroundBrush";
    public const string LineNumberBackgroundKey = "LineNumberBackgroundBrush";
    public const string SelectionKey = "SelectionBackgroundBrush";

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
