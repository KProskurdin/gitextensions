using GitExtensions.Extensibility.Extensions;
using GitExtensions.Xplat.Core.Plugins;
using GitUIPluginInterfaces;
using GitUIPluginInterfaces.BuildServerIntegration;

namespace GitExtensions.Xplat.Core.BuildServer;

/// <summary>
///  A build server type a plugin provides, with upstream's reason when its adapter cannot be loaded.
/// </summary>
public sealed record BuildServerType(string Name, string? CanBeLoaded = null)
{
    /// <summary>
    ///  Upstream's entry in the build server type list: the name, then " - " and the reason it cannot be loaded.
    /// </summary>
    public string DisplayName => Name.Combine(" - ", CanBeLoaded)!;
}

/// <summary>
///  The build server plugins: their adapters, settings controls and auto-detectors, by build server type.
/// </summary>
public interface IBuildServerCatalog
{
    IReadOnlyList<BuildServerType> Types { get; }

    IReadOnlyList<IBuildServerAutoDetector> AutoDetectors { get; }

    /// <summary>
    ///  A new adapter for <paramref name="type"/>, or null when no plugin provides one or it cannot be loaded.
    /// </summary>
    IBuildServerAdapter? CreateAdapter(string type);

    /// <summary>
    ///  A new settings control for <paramref name="type"/>, or null when the plugin has none.
    /// </summary>
    IBuildServerSettingsUserControl? CreateSettingsControl(string type);
}

/// <summary>
///  The exports of the plugins the app loads, through upstream's <see cref="ManagedExtensibility"/>, as upstream's
///  <c>BuildServerWatcher</c> and settings page read them.
/// </summary>
public sealed class UpstreamBuildServerCatalog : IBuildServerCatalog
{
    public IReadOnlyList<BuildServerType> Types
        => [.. AdapterExports().Select(export => new BuildServerType(export.Metadata.BuildServerType, export.Metadata.CanBeLoaded))];

    public IReadOnlyList<IBuildServerAutoDetector> AutoDetectors
    {
        get
        {
            UpstreamPluginCatalog.EnsureInitialized();
            return [.. ManagedExtensibility.GetExports<IBuildServerAutoDetector>().Select(export => export.Value)];
        }
    }

    public IBuildServerAdapter? CreateAdapter(string type)
    {
        Lazy<IBuildServerAdapter, IBuildServerTypeMetadata>? export =
            AdapterExports().SingleOrDefault(candidate => candidate.Metadata.BuildServerType == type);
        return export is null || !string.IsNullOrEmpty(export.Metadata.CanBeLoaded) ? null : export.Value;
    }

    public IBuildServerSettingsUserControl? CreateSettingsControl(string type)
    {
        UpstreamPluginCatalog.EnsureInitialized();
        return ManagedExtensibility.GetExports<IBuildServerSettingsUserControl, IBuildServerTypeMetadata>()
            .SingleOrDefault(export => export.Metadata.BuildServerType == type)?.Value;
    }

    private static IEnumerable<Lazy<IBuildServerAdapter, IBuildServerTypeMetadata>> AdapterExports()
    {
        UpstreamPluginCatalog.EnsureInitialized();
        return ManagedExtensibility.GetExports<IBuildServerAdapter, IBuildServerTypeMetadata>();
    }
}

/// <summary>
///  Fixed adapters and auto-detectors. Tests use it, so they never load the plugins next to the test host.
/// </summary>
public sealed class FixedBuildServerCatalog : IBuildServerCatalog
{
    private readonly IReadOnlyDictionary<string, Func<IBuildServerAdapter>> _adapters;
    private readonly IReadOnlyDictionary<string, Func<IBuildServerSettingsUserControl>> _settingsControls;

    public FixedBuildServerCatalog(IReadOnlyDictionary<string, Func<IBuildServerAdapter>>? adapters = null,
        IReadOnlyList<IBuildServerAutoDetector>? autoDetectors = null,
        IReadOnlyDictionary<string, Func<IBuildServerSettingsUserControl>>? settingsControls = null)
    {
        _adapters = adapters ?? new Dictionary<string, Func<IBuildServerAdapter>>();
        _settingsControls = settingsControls ?? new Dictionary<string, Func<IBuildServerSettingsUserControl>>();
        AutoDetectors = autoDetectors ?? [];
    }

    public IReadOnlyList<BuildServerType> Types => [.. _adapters.Keys.Select(name => new BuildServerType(name))];

    public IReadOnlyList<IBuildServerAutoDetector> AutoDetectors { get; }

    public IBuildServerAdapter? CreateAdapter(string type) => _adapters.GetValueOrDefault(type)?.Invoke();

    public IBuildServerSettingsUserControl? CreateSettingsControl(string type)
        => _settingsControls.GetValueOrDefault(type)?.Invoke();
}
