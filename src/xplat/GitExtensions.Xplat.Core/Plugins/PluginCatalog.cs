using System.Diagnostics;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Extensibility.Settings;
using GitUI;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Plugins;

/// <summary>
///  The plugins the app has loaded.
/// </summary>
public interface IPluginCatalog
{
    /// <summary>
    ///  Loads the plugins once and returns them; later calls return the same list. Loading may take a while (assembly
    ///  scanning), so callers run it off the UI thread, as upstream's <c>FormBrowse</c> does.
    /// </summary>
    IReadOnlyList<IGitPlugin> Load();
}

/// <summary>
///  The new shell's port of upstream's <c>PluginRegistry</c> loading: every <see cref="IGitPlugin"/> export, a stand-in
///  for each one that fails to construct, and each plugin's settings under upstream's keys.
/// </summary>
public static class PluginLoader
{
    /// <summary>
    ///  Creates the plugins from their exports, ordered by name as upstream's Plugins menu lists them.
    /// </summary>
    public static IReadOnlyList<IGitPlugin> Load(IEnumerable<Lazy<IGitPlugin>> exports)
    {
        List<IGitPlugin> plugins = [];
        foreach (Lazy<IGitPlugin> export in exports)
        {
            IGitPlugin plugin;
            try
            {
                plugin = export.Value;
            }
            catch (Exception ex)
            {
                plugin = new FailedPlugin(ex);
            }

            if (plugin.Description is null || plugins.Contains(plugin))
            {
                continue;
            }

            // As upstream: the description is the old settings key prefix, kept so older stored values still apply.
            plugin.SettingsContainer = new GitPluginSettingsContainer(plugin.Id, plugin.Description);
            plugins.Add(plugin);
        }

        return [.. plugins.OrderBy(plugin => plugin.Name, StringComparer.CurrentCultureIgnoreCase)];
    }
}

/// <summary>
///  Loads the plugins with upstream's <see cref="ManagedExtensibility"/>: the <c>GitExtensions.Plugins.*.dll</c> files in
///  the Plugins folder next to the app and the <c>GitExtensions.*.dll</c> files in the user's plugins folder
///  (<see cref="AppSettings.UserPluginsPath"/>), as the WinForms app does.
/// </summary>
public sealed class UpstreamPluginCatalog : IPluginCatalog
{
    private static readonly Lock _initLock = new();
    private static bool _initialized;

    private readonly Lazy<IReadOnlyList<IGitPlugin>> _plugins = new(LoadPlugins);

    public IReadOnlyList<IGitPlugin> Load() => _plugins.Value;

    /// <summary>
    ///  Initializes upstream's <see cref="ManagedExtensibility"/> with the app's plugin folders, once: it is process-wide and
    ///  accepts one initialization. The build server integration reads its exports too.
    /// </summary>
    public static void EnsureInitialized()
    {
        lock (_initLock)
        {
            if (!_initialized)
            {
                ManagedExtensibility.Initialise(userPluginsPath: AppSettings.UserPluginsPath);
                _initialized = true;
            }
        }
    }

    private static IReadOnlyList<IGitPlugin> LoadPlugins()
    {
        try
        {
            EnsureInitialized();
            return PluginLoader.Load(ManagedExtensibility.GetExports<IGitPlugin>());
        }
        catch (Exception ex)
        {
            // As upstream: a broken plugin folder must not stop the app.
            Trace.WriteLine($"Fail to load plugins. Error: {ex}");
            return [];
        }
    }
}

/// <summary>
///  A fixed list of plugins. Tests use it, so they never load the plugins next to the test host.
/// </summary>
public sealed class FixedPluginCatalog(params IGitPlugin[] plugins) : IPluginCatalog
{
    private readonly Lazy<IReadOnlyList<IGitPlugin>> _plugins = new(() => PluginLoader.Load(
        plugins.Select(plugin => new Lazy<IGitPlugin>(() => plugin))));

    public IReadOnlyList<IGitPlugin> Load() => _plugins.Value;
}

/// <summary>
///  Stands in for a plugin that failed to load, as upstream's <c>FailedPluginWrapper</c>: listed under upstream's name, and
///  its error shown when run.
/// </summary>
public sealed class FailedPlugin : IGitPlugin
{
    /// <summary>
    ///  Upstream's <c>FailedToLoadPlugin</c> text.
    /// </summary>
    public const string FailedToLoadPlugin = "Plugin loading failure";

    public FailedPlugin(Exception loadingException)
    {
        Error = loadingException.ToString();
        Name = FailedToLoadPlugin;
    }

    /// <summary>
    ///  The load error, as upstream shows it.
    /// </summary>
    public string Error { get; }

    public Guid Id { get; } = Guid.NewGuid();

    public string? Name { get; }

    public string Description => FailedToLoadPlugin;

    public Image? Icon => null;

    public IGitPluginSettingsContainer? SettingsContainer { get; set; }

    public bool HasSettings => false;

    public IEnumerable<ISetting> GetSettings() => [];

    public void Register(IGitUICommands gitUiCommands)
    {
    }

    public void Unregister(IGitUICommands gitUiCommands)
    {
    }

    // The host shows Error itself; upstream's message box and clipboard copy are WinForms calls.
    public bool Execute(GitUIEventArgs args) => false;
}
