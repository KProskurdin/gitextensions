using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitCommands.Settings;
using GitExtensions.Extensibility.BuildServerIntegration;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Core.BuildServer;
using GitExtensions.Xplat.Core.CommitHistory;
using GitUIPluginInterfaces.BuildServerIntegration;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// The build server plugins built for the new shell (src/xplat/plugins/BuildServerIntegration), loaded from the Plugins folder:
// the grid's build status column fed by a real adapter, and the Settings tab with a real plugin's settings control.
internal sealed class BuildServerUiTests
{
    private const string GitHubActions = "GitHub Actions";
    private const string Jenkins = "Jenkins";
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        TestAppBuilder.UseRevisionLinks();
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.UseBuildServers(new FixedBuildServerCatalog());
        TestAppBuilder.Preferences.ShowBuildStatusIconColumn = true;
        TestAppBuilder.Preferences.ShowBuildStatusTextColumn = false;
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_grid_shows_the_build_status_the_configured_build_server_reports()
    {
        string head = _repo.Run("rev-parse", "HEAD").Trim();
        using FakeGitHubApi api = new(head);
        _repo.Run("remote", "add", "origin", "https://github.com/owner/repo.git");
        Assembly plugin = PluginAssembly("GitHubActionsIntegration");
        TestAppBuilder.UseBuildServers(new FixedBuildServerCatalog(
            new Dictionary<string, Func<IBuildServerAdapter>>
            {
                [GitHubActions] = () => (IBuildServerAdapter)Activator.CreateInstance(
                    plugin.GetType("GitExtensions.Plugins.GitHubActionsIntegration.GitHubActionsAdapter",
                        throwOnError: true)!)!,
            },
            [
                (IBuildServerAutoDetector)Activator.CreateInstance(
                    plugin.GetType("GitExtensions.Plugins.GitHubActionsIntegration.GitHubActionsAutoDetector",
                        throwOnError: true)!,
                    nonPublic: true)!,
            ]));

        // The owner and repository come from the GitHub remote through the plugin's detector, as upstream; only the API is a
        // local one.
        DistributedSettings settings = AppServices.RevisionLinks.Open(_repo.Path);
        BuildServerSettings.ServerName[settings] = GitHubActions;
        settings.SetString($"BuildServer.{GitHubActions}.GitHubActionsApiUrl", api.Url);
        BuildServerSettings.ShowBuildResultPage[settings] = true;
        AppServices.RevisionLinks.Save(settings);

        MainWindow window = OpenWindow();
        WaitUntil(() => HeadBuild(window).Symbol == "✔");

        window.BuildServerType.Should().Be(GitHubActions);
        HeadBuild(window).Status.Should().Be(BuildStatus.Success);
        HeadBuild(window).Url.Should().Be("https://github.com/owner/repo/actions/runs/7");
        HeadBuild(window).Tooltip.Should().NotBeNullOrEmpty();
        window.Resources["BuildStatusColumnWidth"].Should().Be(24.0, "the symbol alone, as upstream's icon column");
        api.Requests.Should().Contain(request =>
            request.Contains("/repos/owner/repo/actions/runs", StringComparison.Ordinal));

        // Upstream's build report tab, for the selected commit whose build has a report.
        TabItem report = window.FindControl<TabItem>("BuildReportTab")!;
        report.IsVisible.Should().BeFalse();
        window.FindControl<ListBox>("CommitList")!.SelectedIndex = 0;
        report.IsVisible.Should().BeTrue();
        ToolTip.GetTip(window.FindControl<HyperlinkButton>("OpenBuildReportLink")!).Should()
            .Be("https://github.com/owner/repo/actions/runs/7");

        Click(window, "ShowBuildStatusTextMenuItem");
        window.Resources["BuildStatusColumnWidth"].Should().Be(150.0);
        TestAppBuilder.Preferences.ShowBuildStatusTextColumn.Should().BeTrue();
        Click(window, "ShowBuildStatusIconMenuItem");
        Click(window, "ShowBuildStatusTextMenuItem");
        window.Resources["BuildStatusColumnWidth"].Should().Be(0.0, "neither the symbol nor the text is chosen");
    }

    [AvaloniaTest]
    public void The_grid_has_no_build_status_column_when_the_integration_is_turned_off()
    {
        TestAppBuilder.UseBuildServers(new FixedBuildServerCatalog(
            new Dictionary<string, Func<IBuildServerAdapter>>
            {
                [Jenkins] = () => throw new InvalidOperationException("not used")
            }));
        DistributedSettings settings = AppServices.RevisionLinks.Open(_repo.Path);
        BuildServerSettings.ServerName[settings] = Jenkins;
        BuildServerSettings.IntegrationEnabled[settings] = false;
        AppServices.RevisionLinks.Save(settings);

        MainWindow window = OpenWindow();

        window.BuildServerType.Should().BeNull();
        window.Resources["BuildStatusColumnWidth"].Should().Be(0.0);
    }

    [AvaloniaTest]
    public void The_settings_tab_stores_the_build_server_and_its_plugins_settings()
    {
        Assembly plugin = PluginAssembly("JenkinsIntegration");
        Type controlType =
            plugin.GetType("JenkinsIntegration.Settings.JenkinsSettingsUserControl", throwOnError: true)!;
        TestAppBuilder.UseBuildServers(new FixedBuildServerCatalog(
            new Dictionary<string, Func<IBuildServerAdapter>>
            {
                [Jenkins] = () => (IBuildServerAdapter)Activator.CreateInstance(
                    plugin.GetType("JenkinsIntegration.JenkinsAdapter", throwOnError: true)!)!,
            },
            settingsControls: new Dictionary<string, Func<IBuildServerSettingsUserControl>>
            {
                [Jenkins] = () => (IBuildServerSettingsUserControl)Activator.CreateInstance(controlType)!,
            }));
        MainWindow window = OpenWindow();
        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settingsWindow = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settingsWindow.IsGitConfigLoaded && settingsWindow.IsBuildServerPageLoaded);

        ComboBox types = Find<ComboBox>(settingsWindow, "BuildServerTypeBox");
        types.Items.OfType<ComboBoxItem>().Select(item => item.Content).Should().Equal("None", Jenkins);
        Find<CheckBox>(settingsWindow, "EnableBuildServerCheck").IsChecked.Should().BeNull("nothing is configured");
        types.SelectedItem = types.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Tag, Jenkins));
        WaitUntil(() => settingsWindow.BuildServerControl is not null);
        Control control = (Control)settingsWindow.BuildServerControl!;
        control.FindControl<TextBox>("JenkinsProjectName")!.Text.Should().Be(Path.GetFileName(_repo.Path),
            "the plugin suggests the repository's folder name");
        control.FindControl<TextBox>("JenkinsServerUrl")!.Text = "http://127.0.0.1:1/jenkins";
        Find<CheckBox>(settingsWindow, "EnableBuildServerCheck").IsChecked = true;
        Find<CheckBox>(settingsWindow, "ShowBuildResultPageCheck").IsChecked = false;
        Click(settingsWindow, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        DistributedSettings stored = AppServices.RevisionLinks.Open(_repo.Path);
        BuildServerSettings.ServerName[stored].Should().Be(Jenkins);
        BuildServerSettings.IntegrationEnabled[stored].Should().BeTrue();
        BuildServerSettings.ShowBuildResultPage[stored].Should().BeFalse();
        stored.GetString($"BuildServer.{Jenkins}.BuildServerUrl", null).Should().Be("http://127.0.0.1:1/jenkins");
        stored.GetString($"BuildServer.{Jenkins}.ProjectName", null).Should().Be(Path.GetFileName(_repo.Path));
    }

    [AvaloniaTest]
    public void The_credentials_window_returns_the_chosen_access_and_keeps_every_box()
    {
        BuildServerCredentialsWindow window = new("https://ci.example.com",
            new BuildServerCredentials
            {
                BuildServerCredentialsType = BuildServerCredentialsType.Guest, Username = "ann"
            });
        window.Show();

        Find<TextBlock>(window, "labelHeader").Text.Should()
            .Be("Please enter the credentials for the build server at https://ci.example.com.");
        Find<TextBox>(window, "textBoxUserName").IsEnabled.Should().BeFalse("guest access needs no user");
        Find<RadioButton>(window, "radioButtonBearerToken").IsChecked = true;
        Find<TextBox>(window, "textBoxBearerToken").IsEnabled.Should().BeTrue();
        Find<TextBox>(window, "textBoxBearerToken").Text = "token";
        Click(window, "buttonOK");

        window.Credentials!.BuildServerCredentialsType.Should().Be(BuildServerCredentialsType.BearerToken);
        window.Credentials.BearerToken.Should().Be("token");
        window.Credentials.Username.Should().Be("ann");
    }

    private static BuildStatusCell HeadBuild(MainWindow window)
        => ((CommitListItem)Find<ListBox>(window, "CommitList").Items[0]!).Build;

    // The tests do not reference the plugins: they load them from the app's Plugins folder, as the app does.
    private static Assembly PluginAssembly(string name)
    {
        string assemblyName = "GitExtensions.Plugins." + name;
        return Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Plugins", assemblyName,
            assemblyName + ".dll"));
    }

    private MainWindow OpenWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        Find<TextBox>(window, "PathBox").Text = _repo.Path;
        Click(window, "OpenButton");
        WaitUntil(() =>
            Find<ListBox>(window, "CommitList").ItemCount == 2 && Find<Button>(window, "OpenButton").IsEnabled);
        return window;
    }

    private static void Click(Window window, string name)
    {
        Control control = Find<Control>(window, name);
        RoutedEvent click = control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent;
        control.RaiseEvent(new RoutedEventArgs(click));
    }

    private static T Find<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name) ?? throw new InvalidOperationException($"Control {name} not found");

    private static void WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out waiting for the UI");
            }

            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
    }

    /// <summary>
    ///  GitHub's REST API for one finished workflow run of <paramref name="headSha"/>, on a local port. Running builds
    ///  (status in_progress) get none.
    /// </summary>
    private sealed class FakeGitHubApi : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly string _headSha;
        private readonly List<string> _requests = [];

        public FakeGitHubApi(string headSha)
        {
            _headSha = headSha;
            _listener.Start();
            _ = Task.Run(ServeAsync);
        }

        public string Url => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";

        public IReadOnlyList<string> Requests
        {
            get
            {
                lock (_requests)
                {
                    return [.. _requests];
                }
            }
        }

        public void Dispose() => _listener.Stop();

        private async Task ServeAsync()
        {
            while (true)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync();
                }
                catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
                {
                    return;
                }

                using (client)
                {
                    NetworkStream stream = client.GetStream();
                    string request = await ReadHeadersAsync(stream);
                    string requestLine = request.Split("\r\n")[0];
                    lock (_requests)
                    {
                        _requests.Add(requestLine);
                    }

                    string runs = requestLine.Contains("status=in_progress", StringComparison.Ordinal) ? "" : Run();
                    byte[] body =
                        Encoding.UTF8.GetBytes(
                            $$"""{"total_count":{{(runs.Length > 0 ? 1 : 0)}},"workflow_runs":[{{runs}}]}""");
                    byte[] head = Encoding.ASCII.GetBytes(
                        $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(head);
                    await stream.WriteAsync(body);
                }
            }
        }

        private string Run()
        {
            string time = DateTime.UtcNow.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            return $$"""
                     {"id":7,"name":"CI","head_sha":"{{_headSha}}","head_branch":"main","status":"completed","conclusion":"success",
                     "html_url":"https://github.com/owner/repo/actions/runs/7","created_at":"{{time}}","updated_at":"{{time}}",
                     "run_started_at":"{{time}}","display_title":"second","event":"push","run_number":7,"run_attempt":1}
                     """;
        }

        private static async Task<string> ReadHeadersAsync(NetworkStream stream)
        {
            StringBuilder headers = new();
            byte[] buffer = new byte[1];
            while (!headers.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal)
                   && await stream.ReadAsync(buffer) > 0)
            {
                headers.Append((char)buffer[0]);
            }

            return headers.ToString();
        }
    }
}
