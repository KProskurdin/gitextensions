using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using GitCommands;
using GitCommands.Config;
using GitCommands.Remotes;
using GitCommands.Settings;
using GitExtensions.Extensibility.BuildServerIntegration;
using GitExtensions.Extensibility.Configurations;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitUIPluginInterfaces.BuildServerIntegration;

namespace GitExtensions.Xplat.Core.BuildServer;

/// <summary>
///  What the watcher needs from the window that shows the build results.
/// </summary>
/// <param name="IsCommitShown">Whether a commit is in the grid; adapters skip builds of other commits.</param>
/// <param name="OnBuildInfo">Called on a background thread with each build result as it arrives.</param>
/// <param name="OpenSettings">Opens the build server settings; an adapter asks for it when its settings are wrong.</param>
/// <param name="AskCredentials">Asks the user for the credentials of the server named by the key; null when cancelled.</param>
public sealed record BuildServerWatcherHost(
    Func<ObjectId, bool> IsCommitShown,
    Action<BuildInfo> OnBuildInfo,
    Action OpenSettings,
    Func<string, IBuildServerCredentials, IBuildServerCredentials?> AskCredentials);

/// <summary>
///  The new shell's version of upstream's <c>BuildServerWatcher</c>, without its grid: chooses the repository's build
///  server adapter as upstream does (the configured type, or one detected from the remotes when nothing is configured),
///  polls it with upstream's intervals and passes the results to the window, and keeps the credentials adapters ask for.
/// </summary>
public sealed class BuildServerWatcher : IBuildServerWatcher, IDisposable
{
    private const string CredentialsConfigName = "Credentials";
    private const string UseGuestAccessKey = "UseGuestAccess";
    private const string BuildServerCredentialsTypeKey = "BuildServerCredentialsType";
    private const string UsernameKey = "Username";
    private const string PasswordKey = "Password";
    private const string BearerTokenKey = "BearerToken";

    private static readonly TimeSpan _shortPollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _longPollInterval = TimeSpan.FromSeconds(120);

    private readonly IGitModule _module;
    private readonly Func<SettingsSource> _settings;
    private readonly IBuildServerCatalog _catalog;
    private readonly IBuildServerCredentialStore _credentials;
    private readonly BuildServerWatcherHost _host;
    private readonly IRepoNameExtractor _repoNameExtractor;
    private readonly Lock _credentialsLock = new();
    private readonly Lock _observerLock = new();
    private IDisposable? _subscription;
    private IBuildServerAdapter? _adapter;
    private int _launchVersion;

    /// <param name="settings">The repository's effective settings, read again at each launch.</param>
    public BuildServerWatcher(IGitModule module, Func<SettingsSource> settings, IBuildServerCatalog catalog,
        IBuildServerCredentialStore credentials, BuildServerWatcherHost host)
    {
        _module = module;
        _settings = settings;
        _catalog = catalog;
        _credentials = credentials;
        _host = host;
        _repoNameExtractor = new RepoNameExtractor(() => _module);
    }

    /// <summary>
    ///  The build server type whose results are shown, or null when there is none.
    /// </summary>
    public string? ActiveType { get; private set; }

    Task IBuildServerWatcher.LaunchBuildServerInfoFetchOperationAsync() => LaunchAsync();

    /// <summary>
    ///  Upstream's <c>LaunchBuildServerInfoFetchOperationAsync</c>: stops the previous polling, chooses the adapter and starts
    ///  polling it. Returns the build server type, or null when the repository has none.
    /// </summary>
    public async Task<string?> LaunchAsync()
    {
        int version = Interlocked.Increment(ref _launchVersion);
        CancelBuildStatusFetchOperation();
        (IBuildServerAdapter? adapter, string? type) = await Task.Run(CreateAdapter).ConfigureAwait(false);
        if (version != Volatile.Read(ref _launchVersion))
        {
            adapter?.Dispose();
            return null;
        }

        Interlocked.Exchange(ref _adapter, adapter)?.Dispose();
        ActiveType = adapter is null ? null : type;
        if (adapter is not null)
        {
            Subscribe(adapter);
        }

        return ActiveType;
    }

    public void CancelBuildStatusFetchOperation()
    {
        lock (_observerLock)
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }

    /// <summary>
    ///  Upstream's credentials: the stored ones when asked for and present, otherwise the user is asked (with the stored
    ///  ones filled in) and the answer is stored. Null when the user cancels.
    /// </summary>
    public IBuildServerCredentials? GetBuildServerCredentials(IBuildServerAdapter buildServerAdapter,
        bool useStoredCredentialsIfExisting)
    {
        lock (_credentialsLock)
        {
            BuildServerCredentials credentials =
                new() { BuildServerCredentialsType = BuildServerCredentialsType.Guest };
            if (_credentials.Load(buildServerAdapter.UniqueKey) is { } text)
            {
                ConfigFile config = new(fileName: "");
                config.LoadFromString(text);
                if (config.FindConfigSection(CredentialsConfigName) is { } section)
                {
                    Read(section, credentials);
                    if (useStoredCredentialsIfExisting)
                    {
                        return credentials;
                    }
                }
            }

            IBuildServerCredentials? answer = _host.AskCredentials(buildServerAdapter.UniqueKey, credentials);
            if (answer is not null)
            {
                _credentials.Save(buildServerAdapter.UniqueKey, Write(answer));
            }

            return answer;
        }
    }

    public string ReplaceVariables(string projectNames)
    {
        (string repoProject, string repoName) = _repoNameExtractor.Get();
        if (!string.IsNullOrWhiteSpace(repoProject))
        {
            projectNames = projectNames.Replace("{cRepoProject}", repoProject);
        }

        if (!string.IsNullOrWhiteSpace(repoName))
        {
            projectNames = projectNames.Replace("{cRepoShortName}", repoName);
        }

        return projectNames;
    }

    /// <summary>
    ///  Upstream's <c>OnRepositoryChanged</c>.
    /// </summary>
    public void OnRepositoryChanged() => _adapter?.OnRepositoryChanged();

    public void Dispose()
    {
        Interlocked.Increment(ref _launchVersion);
        CancelBuildStatusFetchOperation();
        Interlocked.Exchange(ref _adapter, null)?.Dispose();
    }

    /// <summary>
    ///  Upstream's <c>GetBuildServerAdapterAsync</c>: a configured type is used unless integration is turned off; with no
    ///  type, one is detected from the remotes only when the user has not touched the integration settings.
    /// </summary>
    private (IBuildServerAdapter? Adapter, string? Type) CreateAdapter()
    {
        SettingsSource effectiveSettings = _settings();
        string? buildServerName = BuildServerSettings.ServerName[effectiveSettings];
        if (!string.IsNullOrEmpty(buildServerName))
        {
            if (BuildServerSettings.IntegrationEnabled[effectiveSettings] is false)
            {
                return (null, null);
            }
        }
        else
        {
            if (BuildServerSettings.IntegrationEnabled[effectiveSettings] is not null)
            {
                return (null, null);
            }

            buildServerName = TryAutoDetect(BuildServerSettings.GetSettingsSource(effectiveSettings));
            if (string.IsNullOrEmpty(buildServerName))
            {
                return (null, null);
            }
        }

        SettingsSource serverSettings = BuildServerSettings.GetSettingsSource(effectiveSettings);
        TryPopulateSettings(buildServerName, serverSettings);
        try
        {
            IBuildServerAdapter? adapter = _catalog.CreateAdapter(buildServerName);
            adapter?.Initialize(this, serverSettings, _host.OpenSettings, _host.IsCommitShown);
            return (adapter, buildServerName);
        }
        catch (InvalidOperationException ex)
        {
            // As upstream: an adapter whose settings are incomplete shows nothing.
            Debug.Write(ex);
            return (null, null);
        }
    }

    // Upstream's polling: running builds first (they may start queries), the last three days' finished builds, then all
    // of them; finished builds are polled again every two minutes, running ones every ten seconds while there are any.
    private void Subscribe(IBuildServerAdapter adapter)
    {
        NewThreadScheduler scheduler = NewThreadScheduler.Default;
        IObservable<BuildInfo> runningBuilds = adapter.GetRunningBuilds(scheduler);
        IObservable<BuildInfo> lastDays =
            adapter.GetFinishedBuildsSince(scheduler, DateTime.Today - TimeSpan.FromDays(3));
        IObservable<BuildInfo> allFinished = adapter.GetFinishedBuildsSince(scheduler);
        bool anyRunningBuilds = false;
        IObservable<BuildInfo> delay = Observable.Defer(() => Observable.Empty<BuildInfo>()
            .DelaySubscription(anyRunningBuilds ? _shortPollInterval : _longPollInterval));
        bool shouldLookForNewlyFinishedBuilds = false;
        DateTime nowFrozen = DateTime.Now;
        IObservable<BuildInfo> fromNow = Observable.If(() => shouldLookForNewlyFinishedBuilds,
            adapter.GetFinishedBuildsSince(scheduler, nowFrozen)
                .Finally(() => shouldLookForNewlyFinishedBuilds = false));

        lock (_observerLock)
        {
            _subscription?.Dispose();
            _subscription = new CompositeDisposable
            {
                lastDays.OnErrorResumeNext(allFinished)
                    .OnErrorResumeNext(Observable.Empty<BuildInfo>()
                        .DelaySubscription(_longPollInterval)
                        .OnErrorResumeNext(fromNow)
                        .Retry()
                        .Repeat())
                    .Subscribe(Report),
                runningBuilds.Do(_ =>
                    {
                        anyRunningBuilds = true;
                        shouldLookForNewlyFinishedBuilds = true;
                    })
                    .OnErrorResumeNext(delay)
                    .Retry()
                    .Finally(() => anyRunningBuilds = false)
                    .Repeat()
                    .Subscribe(Report),
            };
        }
    }

    private void Report(BuildInfo buildInfo)
    {
        lock (_observerLock)
        {
            if (_subscription is null)
            {
                return;
            }
        }

        try
        {
            _host.OnBuildInfo(buildInfo);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Build status update failed: {ex}");
        }
    }

    private string? TryAutoDetect(SettingsSource settingsSource)
    {
        try
        {
            List<string> remoteUrls = OrderedRemoteUrls();
            if (remoteUrls.Count == 0)
            {
                return null;
            }

            return _catalog.AutoDetectors.FirstOrDefault(detector => detector.TryDetect(remoteUrls, settingsSource))
                ?.BuildServerType;
        }
        catch (Exception ex)
        {
            Debug.Write($"Auto-detect build server failed: {ex}");
            return null;
        }
    }

    // Upstream's TryPopulateSettingsForBuildServer: the matching detector fills in what the remotes tell (owner, project).
    private void TryPopulateSettings(string buildServerName, SettingsSource settingsSource)
    {
        try
        {
            List<string> remoteUrls = OrderedRemoteUrls();
            if (remoteUrls.Count > 0)
            {
                _catalog.AutoDetectors.FirstOrDefault(detector => detector.BuildServerType == buildServerName)
                    ?.TryDetect(remoteUrls, settingsSource);
            }
        }
        catch (Exception ex)
        {
            Debug.Write($"Populate build server settings failed: {ex}");
        }
    }

    // Upstream's GetOrderedRemoteUrls: the remotes in AppSettings.PrioritizedBuildServerRemoteNames' order, so a fork finds
    // the upstream project's builds.
    private List<string> OrderedRemoteUrls()
    {
        string[] prioritizedNames = AppSettings.PrioritizedBuildServerRemoteNames
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<string> remoteUrls = [];
        foreach (string remoteName in _module.GetRemoteNames().OrderBy(remote =>
                 {
                     int index = Array.FindIndex(prioritizedNames,
                         name => string.Equals(name, remote, StringComparison.OrdinalIgnoreCase));
                     return index >= 0 ? index : prioritizedNames.Length;
                 }))
        {
            string remoteUrl = _module.GetSetting(string.Format(SettingKeyString.RemoteUrl, remoteName));
            if (!string.IsNullOrWhiteSpace(remoteUrl))
            {
                remoteUrls.Add(remoteUrl);
            }
        }

        return remoteUrls;
    }

    private static void Read(IConfigSection section, BuildServerCredentials credentials)
    {
        string? type = section.GetValue(BuildServerCredentialsTypeKey);
        if (!string.IsNullOrWhiteSpace(type))
        {
            credentials.BuildServerCredentialsType =
                Enum.TryParse(type, ignoreCase: true, out BuildServerCredentialsType parsed)
                    ? parsed
                    : BuildServerCredentialsType.Guest;
        }
        else
        {
            // Upstream's older format.
            credentials.BuildServerCredentialsType = section.GetValueAsBool(UseGuestAccessKey, true)
                ? BuildServerCredentialsType.Guest
                : BuildServerCredentialsType.UsernameAndPassword;
        }

        credentials.Username = section.GetValue(UsernameKey);
        credentials.Password = section.GetValue(PasswordKey);
        credentials.BearerToken = section.GetValue(BearerTokenKey);
    }

    private static string Write(IBuildServerCredentials credentials)
    {
        ConfigFile config = new(fileName: "");
        IConfigSection section = config.FindOrCreateConfigSection(CredentialsConfigName);
        section.SetValue(BuildServerCredentialsTypeKey, credentials.BuildServerCredentialsType.ToString());
        section.SetValue(UsernameKey, credentials.Username);
        section.SetValue(PasswordKey, credentials.Password);
        section.SetValue(BearerTokenKey, credentials.BearerToken);
        return config.GetAsString();
    }
}
