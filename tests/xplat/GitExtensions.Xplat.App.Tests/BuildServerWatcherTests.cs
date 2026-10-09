using System.Reactive.Concurrency;
using System.Reactive.Linq;
using GitCommands;
using GitCommands.Settings;
using GitExtensions.Extensibility.BuildServerIntegration;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Core.BuildServer;
using GitExtensions.Xplat.Core.Plugins;
using GitExtensions.Xplat.Core.Repository;
using GitUIPluginInterfaces.BuildServerIntegration;
using Avalonia.Headless.NUnit;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// The repository is read with real git, which needs ThreadHelper.JoinableTaskContext; AvaloniaTest runs the app, which sets it.
internal sealed class BuildServerWatcherTests
{
    private const string FakeType = "Fake CI";
    private static readonly ObjectId _commit = ObjectId.Parse("0123456789abcdef0123456789abcdef01234567");

    private TestRepository _repo = null!;
    private GitModule _module = null!;
    private InMemorySettingsSource _settings = null!;
    private readonly List<BuildInfo> _reported = [];
    private readonly List<string> _asked = [];

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _repo.Run("remote", "add", "origin", "https://example.com/fork/project.git");
        _repo.Run("remote", "add", "upstream", "https://example.com/team/project.git");
        _module = GitModules.Open(_repo.Path);
        _settings = new InMemorySettingsSource();
        _reported.Clear();
        _asked.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void LaunchAsync_should_poll_the_configured_build_server_and_report_its_builds()
    {
        BuildServerSettings.ServerName[_settings] = FakeType;
        FakeAdapter adapter = new([new BuildInfo { CommitHashList = [_commit], Status = BuildStatus.Success }]);
        using BuildServerWatcher watcher = Watcher(adapter);

        string? type = watcher.LaunchAsync().GetAwaiter().GetResult();

        type.Should().Be(FakeType);
        WaitUntil(() => _reported.Count > 0);
        _reported[0].Status.Should().Be(BuildStatus.Success);
        adapter.Settings!.GetString("Key", null).Should().Be("value", "the adapter gets the settings of its type");
    }

    [AvaloniaTest]
    public void LaunchAsync_should_show_nothing_when_the_integration_is_turned_off()
    {
        BuildServerSettings.ServerName[_settings] = FakeType;
        BuildServerSettings.IntegrationEnabled[_settings] = false;
        using BuildServerWatcher watcher = Watcher(new FakeAdapter([]));

        (watcher.LaunchAsync().GetAwaiter().GetResult()).Should().BeNull();
    }

    [AvaloniaTest]
    public void LaunchAsync_should_detect_the_build_server_from_the_remotes_in_upstreams_priority_order()
    {
        FakeDetector detector = new();
        using BuildServerWatcher watcher = Watcher(new FakeAdapter([]), detector);

        string? type = watcher.LaunchAsync().GetAwaiter().GetResult();

        type.Should().Be(FakeType);
        detector.RemoteUrls.Should().Equal("https://example.com/team/project.git", "https://example.com/fork/project.git");
    }

    [AvaloniaTest]
    public void LaunchAsync_should_not_detect_once_the_user_chose_the_integration_setting()
    {
        BuildServerSettings.IntegrationEnabled[_settings] = true;
        using BuildServerWatcher watcher = Watcher(new FakeAdapter([]), new FakeDetector());

        (watcher.LaunchAsync().GetAwaiter().GetResult()).Should().BeNull();
    }

    [AvaloniaTest]
    public void GetBuildServerCredentials_should_ask_once_and_then_use_the_stored_credentials()
    {
        SessionBuildServerCredentialStore store = new();
        FakeAdapter adapter = new([]);
        using BuildServerWatcher watcher = Watcher(adapter, credentials: store);

        IBuildServerCredentials? first = watcher.GetBuildServerCredentials(adapter, useStoredCredentialsIfExisting: true);
        IBuildServerCredentials? second = watcher.GetBuildServerCredentials(adapter, useStoredCredentialsIfExisting: true);
        IBuildServerCredentials? third = watcher.GetBuildServerCredentials(adapter, useStoredCredentialsIfExisting: false);

        _asked.Should().Equal("https://ci.example.com", "https://ci.example.com");
        first!.Username.Should().Be("ann");
        second!.BuildServerCredentialsType.Should().Be(BuildServerCredentialsType.UsernameAndPassword);
        second.Password.Should().Be("secret");
        third!.Username.Should().Be("ann");
        store.Load("https://ci.example.com").Should().Contain("[Credentials]").And.Contain("Username = ann");
    }

    [AvaloniaTest]
    public void ReplaceVariables_should_use_upstreams_repository_names()
    {
        using BuildServerWatcher watcher = Watcher(new FakeAdapter([]));

        watcher.ReplaceVariables("{cRepoProject}/{cRepoShortName}").Should().Be("fork/project", "a branch without upstream uses the first remote, as upstream");
    }

    private BuildServerWatcher Watcher(FakeAdapter adapter, FakeDetector? detector = null,
        IBuildServerCredentialStore? credentials = null)
    {
        SettingsSource typeSettings = new SettingsPath(_settings, $"BuildServer.{FakeType}");
        typeSettings.SetString("Key", "value");
        FixedBuildServerCatalog catalog = new(
            new Dictionary<string, Func<IBuildServerAdapter>> { [FakeType] = () => adapter },
            detector is null ? [] : [detector]);
        return new BuildServerWatcher(_module, () => _settings, catalog, credentials ?? new SessionBuildServerCredentialStore(),
            new BuildServerWatcherHost(
                _ => true,
                info =>
                {
                    lock (_reported)
                    {
                        _reported.Add(info);
                    }
                },
                () => { },
                (key, known) =>
                {
                    _asked.Add(key);
                    return new BuildServerCredentials
                    {
                        BuildServerCredentialsType = BuildServerCredentialsType.UsernameAndPassword,
                        Username = known.Username ?? "ann",
                        Password = "secret",
                    };
                }));
    }

    private static void WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            DateTime.UtcNow.Should().BeBefore(deadline, "the condition should become true");
            Thread.Sleep(20);
        }
    }

    private sealed class FakeAdapter(IReadOnlyList<BuildInfo> builds) : IBuildServerAdapter
    {
        public SettingsSource? Settings { get; private set; }

        public string UniqueKey => "https://ci.example.com";

        public void Initialize(IBuildServerWatcher buildServerWatcher, SettingsSource config, Action openSettings,
            Func<ObjectId, bool>? isCommitInRevisionGrid = null)
            => Settings = config;

        public IObservable<BuildInfo> GetFinishedBuildsSince(IScheduler scheduler, DateTime? sinceDate = null)
            => builds.ToObservable(scheduler);

        public IObservable<BuildInfo> GetRunningBuilds(IScheduler scheduler) => Observable.Empty<BuildInfo>();

        public void Dispose()
        {
        }
    }

    private sealed class FakeDetector : IBuildServerAutoDetector
    {
        public IReadOnlyList<string> RemoteUrls { get; private set; } = [];

        public string BuildServerType => FakeType;

        public bool TryDetect(IReadOnlyList<string> remoteUrls, SettingsSource? settingsSource)
        {
            RemoteUrls = remoteUrls;
            return true;
        }
    }
}
