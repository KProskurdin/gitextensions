using GitExtUtils.GitUI.Theming;
using GitUI.Theming;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  The colors of the active theme, by upstream <see cref="AppColor"/>. <see cref="IsDark"/> picks the light or dark controls;
///  <see cref="Error"/> is set when the chosen theme could not be read and the default one is used instead.
/// </summary>
public sealed record AppThemeColors(
    ThemeId Id,
    bool IsDark,
    IReadOnlyDictionary<AppColor, Color> Colors,
    string? Error = null)
{
    private const float DarkBrightness = 0.5f;

    /// <summary>
    ///  The color, or <see cref="Color.Empty"/> where the theme leaves a slot unused (e.g. dark's GraphBranch8).
    /// </summary>
    public Color Get(AppColor name) => Colors.TryGetValue(name, out Color color) ? color : Color.Empty;

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

        bool isDark = colors[AppColor.PanelBackground].GetBrightness() < DarkBrightness;
        return new AppThemeColors(theme.Id, isDark, colors, error);
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
