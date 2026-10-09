using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.App;
using GitExtensions.Xplat.Core.BuildServer;
using GitExtensions.Xplat.Core.Plugins;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;

[assembly: AvaloniaTestApplication(typeof(GitExtensions.Xplat.App.Tests.TestAppBuilder))]

namespace GitExtensions.Xplat.App.Tests;

public static class TestAppBuilder
{
    /// <summary>
    ///  The preferences the windows see during the tests; a test may change them and reset them afterwards.
    /// </summary>
    public static InMemoryAppPreferences Preferences { get; } = new();

    /// <summary>
    ///  The git config the windows see during the tests, so no test writes the user's global git config.
    /// </summary>
    public static FakeGitConfigService GitConfig { get; } = new();

    /// <summary>
    ///  Window sizes and positions during the tests, so no test writes the user's WindowPositions.xml.
    /// </summary>
    public static InMemoryWindowPlacementStore WindowPlacements { get; } = new();

    /// <summary>
    ///  The user scripts during the tests, so no test reads or writes the user's scripts.
    /// </summary>
    public static InMemoryScriptStore Scripts { get; } = new();

    /// <summary>
    ///  The plugin settings the Settings window edits during the tests, so no test writes the user's settings files.
    /// </summary>
    public static InMemorySettingsSource PluginSettings { get; private set; } = new();

    /// <summary>
    ///  The settings file the revision link definitions go to during the tests; <see cref="UseRevisionLinks"/> starts a new one.
    /// </summary>
    public static string RevisionLinksFile { get; private set; } = "";

    /// <summary>
    ///  Gives the windows an empty settings file of their own for revision links.
    /// </summary>
    public static void UseRevisionLinks()
    {
        RevisionLinksFile = Path.Combine(Path.GetTempPath(), $"xplat-links-{Guid.NewGuid():N}.settings");
        AppServices.RevisionLinks = new FileRevisionLinkStore(RevisionLinksFile);
    }

    /// <summary>
    ///  Gives the windows <paramref name="plugins"/> and empty plugin settings; with no plugins, as at the start, none load.
    /// </summary>
    public static void UsePlugins(params IGitPlugin[] plugins)
    {
        AppServices.Plugins = new FixedPluginCatalog(plugins);
        PluginSettings = new InMemorySettingsSource();
        AppServices.PluginSettings = new InMemoryPluginSettingsStore(PluginSettings);
    }

    /// <summary>
    ///  Gives the windows <paramref name="catalog"/> as the build server plugins, and credentials kept in memory.
    /// </summary>
    public static void UseBuildServers(IBuildServerCatalog catalog)
    {
        AppServices.BuildServers = catalog;
        AppServices.BuildServerCredentials = new SessionBuildServerCredentialStore();
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        // No plugin from the folder next to the test host is loaded, and plugin settings stay in memory.
        UsePlugins();
        UseRevisionLinks();

        // Replaced before the app starts, so no window ever touches the user's settings file.
        AppServices.RecentRepositories = new InMemoryRecentRepositoryStore();
        AppServices.Preferences = Preferences;
        AppServices.GitConfig = GitConfig;
        AppServices.WindowPlacements = WindowPlacements;

        // The test host is not the app, so git must never be told to start it as an editor.
        AppServices.EditorCommand = null;
        AppServices.AskPassExecutable = null;
        AppServices.DiffMergeTools = new FixedDiffMergeToolCatalog();
        AppServices.Scripts = Scripts;

        // No build server plugin from the folder next to the test host, and no credentials written to the user's storage.
        UseBuildServers(new FixedBuildServerCatalog());

        // Upstream's English texts, whatever language the user's settings file has; a test may choose one and reset it.
        GitCommands.AppSettings.CurrentTranslation = "";

        // Upstream's theme files are copied next to the app; no user themes, so the user's own never change a test.
        AppServices.Themes = new UpstreamThemeService(new AppThemePaths(
            Path.Combine(AppContext.BaseDirectory, "Themes"),
            Path.Combine(Path.GetTempPath(), "xplat-no-user-themes")));
        return AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}

/// <summary>
///  git config values in memory, by scope and key.
/// </summary>
public sealed class FakeGitConfigService : IGitConfigService
{
    private readonly Dictionary<(ConfigScope, string), string> _values = [];
    private readonly Dictionary<(ConfigScope, string), List<string>> _multiValues = [];

    public Task<string> GetAsync(ConfigScope scope, string key, string? repositoryPath = null)
        => Task.FromResult(_values.GetValueOrDefault((scope, key), ""));

    public Task<IReadOnlyList<string>> GetAllAsync(ConfigScope scope, string key, string? repositoryPath = null)
        => Task.FromResult<IReadOnlyList<string>>(
            _multiValues.TryGetValue((scope, key), out List<string>? values) ? values
            : _values.TryGetValue((scope, key), out string? value) ? [value]
            : []);

    /// <summary>
    ///  Gives <paramref name="key"/> several values, as git keeps for <c>credential.helper</c>.
    /// </summary>
    public void SetMultiple(ConfigScope scope, string key, params string[] values)
    {
        _multiValues[(scope, key)] = [.. values];
        _values[(scope, key)] = values[^1];
    }

    public Task SetAsync(ConfigScope scope, string key, string value, string? repositoryPath = null)
    {
        if (value.Length == 0)
        {
            _values.Remove((scope, key));
        }
        else
        {
            _values[(scope, key)] = value;
        }

        return Task.CompletedTask;
    }

    public void Set(ConfigScope scope, string key, string value) => _values[(scope, key)] = value;

    public string Get(ConfigScope scope, string key) => _values.GetValueOrDefault((scope, key), "");

    public void Clear()
    {
        _values.Clear();
        _multiValues.Clear();
    }
}

/// <summary>
///  A fixed list of tools, so the settings tests do not depend on what is installed on the machine.
/// </summary>
public sealed class FixedDiffMergeToolCatalog : IDiffMergeToolCatalog
{
    public Task<IReadOnlyList<string>> GetAvailableAsync(bool diff, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(diff ? ["meld", "vscode"] : ["kdiff3", "meld"]);
}
