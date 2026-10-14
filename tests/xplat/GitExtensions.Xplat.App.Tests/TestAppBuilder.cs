using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.App;
using GitExtensions.Xplat.Core.Avatars;
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

    public static InMemoryRemoteUrlHistory RemoteUrls { get; } = new();

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

    /// <summary>
    ///  The avatar images the tests offer, by address.
    /// </summary>
    public static FakeAvatarDownloader AvatarDownloads { get; } = new();

    /// <summary>
    ///  An empty folder outside any repository, the tests' current directory.
    /// </summary>
    public static string TestCurrentDirectory { get; } =
        Path.Combine(Path.GetTempPath(), "xplat-app-tests-cwd-" + Guid.NewGuid().ToString("N"));

    public static string AvatarCacheFolder { get; } =
        Path.Combine(Path.GetTempPath(), "xplat-avatars-" + Guid.NewGuid().ToString("N"));

    public static AppBuilder BuildAvaloniaApp()
    {
        // A git command run without a repository (a plugin acting after an open failed) runs in the current directory, so it
        // must never be the repository the tests are started from: a failed run once fetched a branch and set a proxy there.
        // git does not look for a repository above the temporary folder either (the test repositories are inside it).
        Directory.CreateDirectory(TestCurrentDirectory);
        Environment.CurrentDirectory = TestCurrentDirectory;
        Environment.SetEnvironmentVariable("GIT_CEILING_DIRECTORIES",
            Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar));

        // No plugin from the folder next to the test host is loaded, and plugin settings stay in memory.
        UsePlugins();
        UseRevisionLinks();

        // Replaced before the app starts, so no window ever touches the user's settings file.
        AppServices.RecentRepositories = new InMemoryRecentRepositoryStore();
        AppServices.RemoteUrls = RemoteUrls;
        AppServices.Preferences = Preferences;
        AppServices.GitConfig = GitConfig;
        AppServices.WindowPlacements = WindowPlacements;

        // The test host is not the app, so git must never be told to start it as an editor.
        AppServices.EditorCommand = null;
        AppServices.AskPassExecutable = null;
        AppServices.DiffMergeTools = new FixedDiffMergeToolCatalog();
        AppServices.Scripts = Scripts;

        // Avatars are never downloaded and their cache is a folder of the run's own.
        AppServices.Avatars = new AvatarService(() => Preferences.Avatars, AvatarCacheFolder, AvatarDownloads);

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

    // As git, the effective value is the repository's, else the global, else the system one.
    public Task<string> GetAsync(ConfigScope scope, string key, string? repositoryPath = null)
        => Task.FromResult(scope == ConfigScope.Effective
            ? new[] { ConfigScope.Local, ConfigScope.Global, ConfigScope.System }
                .Select(level => _values.GetValueOrDefault((level, key), ""))
                .FirstOrDefault(value => value.Length > 0) ?? ""
            : _values.GetValueOrDefault((scope, key), ""));

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

/// <summary>
///  Avatar images by address, and the addresses asked for.
/// </summary>
public sealed class FakeAvatarDownloader : IAvatarDownloader
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Uri, byte[]> _images = new();

    public System.Collections.Concurrent.ConcurrentQueue<Uri> Requests { get; } = new();

    public void Offer(Uri uri, byte[] image) => _images[uri] = image;

    public Task<byte[]?> DownloadAsync(Uri uri)
    {
        Requests.Enqueue(uri);
        return Task.FromResult(_images.TryGetValue(uri, out byte[]? image) ? image : null);
    }
}
