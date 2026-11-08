using GitExtUtils.GitUI.Theming;
using GitUI.Theming;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  The colors of the active theme, by upstream <see cref="AppColor"/>. <see cref="IsDark"/> picks the light or dark controls;
///  <see cref="Error"/> is set when the chosen theme could not be read and the default one is used instead.
///  <see cref="SystemColors"/> holds the system colors the theme overrides (upstream's <c>SysColorValues</c>).
/// </summary>
public sealed record AppThemeColors(
    ThemeId Id,
    bool IsDark,
    IReadOnlyDictionary<AppColor, Color> Colors,
    string? Error = null,
    IReadOnlyDictionary<KnownColor, Color>? SystemColors = null)
{
    private const float DarkBrightness = 0.5f;

    // What Windows' SystemColorMode.Dark gives for the system colors, as upstream's dark.css lists them. Upstream's dark themes
    // rely on it: WinForms switches the mode on from the panel color, and .NET's SystemColors then answer with these. Without
    // WinForms the mode stays off on every OS, and they answer with the light values.
    private static readonly IReadOnlyDictionary<KnownColor, Color> _darkModeSystemColors = new Dictionary<KnownColor, Color>
    {
        [KnownColor.ActiveBorder] = Color.FromArgb(0x46, 0x46, 0x46),
        [KnownColor.ActiveCaption] = Color.FromArgb(0x3c, 0x5f, 0x78),
        [KnownColor.ActiveCaptionText] = Color.FromArgb(0xff, 0xff, 0xff),
        [KnownColor.AppWorkspace] = Color.FromArgb(0x3c, 0x3c, 0x3c),
        [KnownColor.ButtonFace] = Color.FromArgb(0x20, 0x20, 0x20),
        [KnownColor.ButtonHighlight] = Color.FromArgb(0x10, 0x10, 0x10),
        [KnownColor.ButtonShadow] = Color.FromArgb(0x46, 0x46, 0x46),
        [KnownColor.Control] = Color.FromArgb(0x20, 0x20, 0x20),
        [KnownColor.ControlDark] = Color.FromArgb(0x4a, 0x4a, 0x4a),
        [KnownColor.ControlDarkDark] = Color.FromArgb(0x5a, 0x5a, 0x5a),
        [KnownColor.ControlLight] = Color.FromArgb(0x2e, 0x2e, 0x2e),
        [KnownColor.ControlLightLight] = Color.FromArgb(0x1f, 0x1f, 0x1f),
        [KnownColor.ControlText] = Color.FromArgb(0xff, 0xff, 0xff),
        [KnownColor.Desktop] = Color.FromArgb(0x10, 0x10, 0x10),
        [KnownColor.GradientActiveCaption] = Color.FromArgb(0x41, 0x64, 0x82),
        [KnownColor.GradientInactiveCaption] = Color.FromArgb(0x55, 0x73, 0x96),
        [KnownColor.GrayText] = Color.FromArgb(0x96, 0x96, 0x96),
        [KnownColor.Highlight] = Color.FromArgb(0x28, 0x64, 0xb4),
        [KnownColor.HighlightText] = Color.FromArgb(0x00, 0x00, 0x00),
        [KnownColor.HotTrack] = Color.FromArgb(0x2d, 0x5f, 0xaf),
        [KnownColor.InactiveBorder] = Color.FromArgb(0x3c, 0x3f, 0x41),
        [KnownColor.InactiveCaption] = Color.FromArgb(0x37, 0x4b, 0x5a),
        [KnownColor.InactiveCaptionText] = Color.FromArgb(0xbe, 0xbe, 0xbe),
        [KnownColor.Info] = Color.FromArgb(0x50, 0x50, 0x3c),
        [KnownColor.InfoText] = Color.FromArgb(0xbe, 0xbe, 0xbe),
        [KnownColor.Menu] = Color.FromArgb(0x37, 0x37, 0x37),
        [KnownColor.MenuBar] = Color.FromArgb(0x37, 0x37, 0x37),
        [KnownColor.MenuHighlight] = Color.FromArgb(0x2a, 0x80, 0xd2),
        [KnownColor.MenuText] = Color.FromArgb(0xf0, 0xf0, 0xf0),
        [KnownColor.ScrollBar] = Color.FromArgb(0x50, 0x50, 0x50),
        [KnownColor.Window] = Color.FromArgb(0x32, 0x32, 0x32),
        [KnownColor.WindowFrame] = Color.FromArgb(0x28, 0x28, 0x28),
        [KnownColor.WindowText] = Color.FromArgb(0xf0, 0xf0, 0xf0),
    };

    /// <summary>
    ///  The theme these colors come from, for upstream's color helpers (<see cref="ColorHelper.ThemeSettings"/>).
    /// </summary>
    public Theme? Theme { get; init; }

    /// <summary>
    ///  The color, or <see cref="Color.Empty"/> where the theme leaves a slot unused (e.g. dark's GraphBranch8).
    /// </summary>
    public Color Get(AppColor name) => Colors.TryGetValue(name, out Color color) ? color : Color.Empty;

    /// <summary>
    ///  The graph's lane colors as upstream's <c>RevisionGraphLaneColor</c> builds them: the distinct, non-empty colors of
    ///  GraphBranch1 to GraphBranch8 in order, or upstream's four fallback colors when the theme gives fewer than four.
    /// </summary>
    public IReadOnlyList<Color> LaneColors()
    {
        const int MinBranchColors = 4;
        Color[] colors =
        [
            .. Enumerable.Range(0, 8).Select(i => Get(AppColor.GraphBranch1 + i)).Where(color => !color.IsEmpty).Distinct()
        ];
        return colors.Length < MinBranchColors ? [Color.Cyan, Color.Magenta, Color.Yellow, Color.Lime] : colors;
    }

    /// <summary>
    ///  The system color the theme sets, or <see cref="Color.Empty"/> where it keeps the system's.
    /// </summary>
    public Color Get(KnownColor name)
        => SystemColors?.TryGetValue(name, out Color color) == true ? color : Color.Empty;

    /// <summary>
    ///  Upstream's <c>Theme.GetNonEmptyColor</c>: the system color the theme sets, else the one the system has in the theme's
    ///  light or dark mode.
    /// </summary>
    public Color GetNonEmpty(KnownColor name)
        => Get(name) is { IsEmpty: false } themed ? themed
            : IsDark && _darkModeSystemColors.TryGetValue(name, out Color dark) ? dark
            : Color.FromKnownColor(name);

    /// <summary>
    ///  The colors of <paramref name="theme"/>. A color the theme does not set comes from upstream's defaults, except the
    ///  graph branch slots, which a theme leaves empty on purpose. A theme is dark when its panel background is.
    /// </summary>
    public static AppThemeColors From(Theme theme, IReadOnlyList<string> variations, string? error = null)
    {
        Dictionary<AppColor, Color> colors = [];
        foreach (AppColor name in Enum.GetValues<AppColor>())
        {
            Color color = theme.GetColor(name);
            if (color.IsEmpty && !IsGraphBranch(name))
            {
                color = AppColorDefaults.GetBy(name, [.. variations]);
            }

            colors[name] = color;
        }

        Dictionary<KnownColor, Color> systemColors = [];
        foreach (KnownColor name in Enum.GetValues<KnownColor>().Where(name => Color.FromKnownColor(name).IsSystemColor))
        {
            if (theme.GetColor(name) is { IsEmpty: false } color)
            {
                systemColors[name] = color;
            }
        }

        bool isDark = colors[AppColor.PanelBackground].GetBrightness() < DarkBrightness;
        return new AppThemeColors(theme.Id, isDark, colors, error, systemColors) { Theme = theme };
    }

    private static bool IsGraphBranch(AppColor name) => name is >= AppColor.GraphBranch1 and <= AppColor.GraphBranch8;
}

/// <summary>
///  Lists and loads upstream's themes: the CSS files in the app's Themes folder and the user's own (see upstream
///  <see cref="ThemeRepository"/>).
/// </summary>
public interface IAppThemeService
{
    /// <summary>
    ///  The themes to choose from: "follow the operating system" (upstream's <see cref="ThemeId.WindowsAppColorModeId"/>)
    ///  first, then the built-in themes, then the user's.
    /// </summary>
    IReadOnlyList<ThemeId> GetThemeIds();

    /// <summary>
    ///  The colors of <paramref name="id"/> with <paramref name="variations"/> (e.g. colorblind). The operating system mode
    ///  resolves to dark or light by <paramref name="systemIsDark"/>, as upstream resolves it by the Windows color mode.
    /// </summary>
    AppThemeColors Load(ThemeId id, IReadOnlyList<string> variations, bool systemIsDark);
}

/// <summary>
///  Reads the themes through upstream's <see cref="IThemeRepository"/>, used as is; loads them as upstream's
///  <c>ThemeModule.LoadThemeSettings</c> does: the light theme is upstream's default theme, and a theme that fails to load
///  falls back to it.
/// </summary>
public sealed class UpstreamThemeService(IThemeRepository repository) : IAppThemeService
{
    public UpstreamThemeService(IThemePathProvider paths)
        : this(new ThemeRepository(
            new ThemePersistence(new ThemeLoader(new ThemeCssUrlResolver(paths), new ThemeFileReader())), paths))
    {
    }

    public IReadOnlyList<ThemeId> GetThemeIds()
    {
        List<ThemeId> ids = [ThemeId.WindowsAppColorModeId];
        try
        {
            ids.AddRange(repository.GetThemeIds().Where(id => !ids.Contains(id)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Without the Themes folder only the built-in default and the system mode remain.
        }

        if (!ids.Contains(ThemeId.DefaultLight))
        {
            ids.Insert(1, ThemeId.DefaultLight);
        }

        return ids;
    }

    public AppThemeColors Load(ThemeId id, IReadOnlyList<string> variations, bool systemIsDark)
    {
        ThemeId resolved = id == ThemeId.WindowsAppColorModeId
            ? systemIsDark ? ThemeId.DefaultDark : ThemeId.DefaultLight
            : id;
        if (resolved == ThemeId.DefaultLight)
        {
            return AppThemeColors.From(Theme.CreateDefaultTheme([.. variations]), variations);
        }

        try
        {
            return AppThemeColors.From(repository.GetTheme(resolved, variations), variations);
        }
        catch (Exception ex) when (ex is ThemeException or InvalidOperationException)
        {
            return AppThemeColors.From(Theme.CreateDefaultTheme([.. variations]), variations,
                $"Failed to load {(resolved.IsBuiltin ? "preinstalled" : "user-defined")} theme {resolved.Name}: {ex.Message}");
        }
    }
}

/// <summary>
///  Where the theme files are: upstream's <c>ThemePathProvider</c> rules (built-in themes in the app's Themes folder, user
///  themes in the user's Git Extensions folder, none in portable mode, where both folders are the same), reimplemented
///  because upstream's class checks in Debug builds that the app is GitExtensions.exe, which fails on Linux and macOS.
/// </summary>
public sealed class AppThemePaths(string appThemesDirectory, string? userThemesDirectory) : IThemePathProvider
{
    private const string Subdirectory = "Themes";

    public string AppThemesDirectory => appThemesDirectory;

    public string? UserThemesDirectory => userThemesDirectory;

    public string ThemeExtension => ".css";

    /// <summary>
    ///  The folders of the running app and its user.
    /// </summary>
    public static AppThemePaths Default()
    {
        string appDirectory = AppContext.BaseDirectory;
        string? userDirectory = GitCommands.AppSettings.ApplicationDataPath.Value;
        bool portable = userDirectory is null
                        || string.Equals(Path.TrimEndingDirectorySeparator(appDirectory),
                            Path.TrimEndingDirectorySeparator(userDirectory), StringComparison.OrdinalIgnoreCase);
        return new AppThemePaths(Path.Join(appDirectory, Subdirectory), portable ? null : Path.Join(userDirectory, Subdirectory));
    }

    public string GetThemePath(ThemeId id)
    {
        if (id.IsBuiltin)
        {
            string name = id == ThemeId.DefaultLight ? ThemeId.InvariantThemeFileName : id.Name;
            return Path.Join(appThemesDirectory, name + ThemeExtension);
        }

        return Path.Join(userThemesDirectory ?? throw new InvalidOperationException("Portable mode only supports local themes"),
            id.Name + ThemeExtension);
    }
}
