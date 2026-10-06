using System.Reflection;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The services the windows share that touch user state outside the repository. The app uses the persistent ones; the
///  headless tests replace them before any window is created, so a test run never writes the user's settings.
/// </summary>
public static class AppServices
{
    private static IRecentRepositoryStore? _recentRepositories;
    private static IAppPreferences? _preferences;
    private static IWindowPlacementStore? _windowPlacements;
    private static ICommitMessageStore? _commitMessages;
    private static IAppThemeService? _themes;
    private static IScriptStore? _scripts;

    /// <summary>
    ///  The user scripts, in upstream's <c>ownScripts</c> setting. Created on first use, like the other settings stores.
    /// </summary>
    public static IScriptStore Scripts
    {
        get => _scripts ??= new UpstreamScriptStore();
        set => _scripts = value;
    }

    /// <summary>
    ///  The recent repositories list. Created on first use, because the settings store reads the settings file.
    /// </summary>
    public static IRecentRepositoryStore RecentRepositories
    {
        get => _recentRepositories ??= new SettingsRecentRepositoryStore();
        set => _recentRepositories = value;
    }

    /// <summary>
    ///  The app preferences, stored under the upstream setting keys. Created on first use, like the recent list.
    /// </summary>
    public static IAppPreferences Preferences
    {
        get => _preferences ??= new SettingsAppPreferences();
        set => _preferences = value;
    }

    /// <summary>
    ///  git config access. Tests replace it so they never write the user's global git config.
    /// </summary>
    public static IGitConfigService GitConfig { get; set; } = new GitConfigService();

    /// <summary>
    ///  The diff and merge tools git can start on this computer. Tests replace it, because the list depends on the machine.
    /// </summary>
    public static IDiffMergeToolCatalog DiffMergeTools { get; set; } = new GitDiffMergeToolCatalog();

    /// <summary>
    ///  Upstream's CSS themes: the built-in ones next to the app and the user's own. Created on first use, because finding
    ///  the user's folder reads the settings.
    /// </summary>
    public static IAppThemeService Themes
    {
        get => _themes ??= new UpstreamThemeService(AppThemePaths.Default());
        set => _themes = value;
    }

    /// <summary>
    ///  Window sizes and positions, in upstream's WindowPositions.xml. Created on first use.
    /// </summary>
    public static IWindowPlacementStore WindowPlacements
    {
        get => _windowPlacements ??= WindowPositionsFileStore.Default();
        set => _windowPlacements = value;
    }

    /// <summary>
    ///  The command git runs to edit a rebase todo list or a message: this app with the <c>fileeditor</c> verb. Tests
    ///  replace it, because the test host is not the app; null turns interactive rebase off.
    /// </summary>
    public static string? EditorCommand { get; set; } = DefaultEditorCommand();

    /// <summary>
    ///  The program ssh and git start to ask for a passphrase or password: this app (see <see cref="GitAskPass"/>). Null
    ///  when the app runs under the <c>dotnet</c> host, because <c>SSH_ASKPASS</c> must name one executable; tests set it
    ///  to null too.
    /// </summary>
    public static string? AskPassExecutable { get; set; } = DefaultAskPassExecutable();

    /// <summary>
    ///  The prepared commit message, kept where upstream keeps it (.git/COMMITMESSAGE).
    /// </summary>
    public static ICommitMessageStore CommitMessages
    {
        get => _commitMessages ??= new UpstreamCommitMessageStore();
        set => _commitMessages = value;
    }

    private static string? DefaultAskPassExecutable()
        => Environment.ProcessPath is { } process
           && !string.Equals(Path.GetFileNameWithoutExtension(process), "dotnet", StringComparison.OrdinalIgnoreCase)
            ? process
            : null;

    // A single-file app has no assembly location; it then runs as its own host, which is all the command needs.
    private static string? DefaultEditorCommand()
    {
        string? entryAssembly = Assembly.GetEntryAssembly()?.Location;
        return Environment.ProcessPath is { } process
            ? GitEditorCommand.Build(process, string.IsNullOrEmpty(entryAssembly) ? null : entryAssembly)
            : null;
    }
}
