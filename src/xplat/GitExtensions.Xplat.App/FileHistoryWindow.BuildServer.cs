using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GitCommands.Settings;
using GitExtensions.Xplat.Core.BuildServer;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using GitUIPluginInterfaces.BuildServerIntegration;

namespace GitExtensions.Xplat.App;

/// <summary>
///  FormFileHistory's <c>RevisionGrid.ShowBuildServerInfo = true</c> and <c>BuildReportTabPageExtension</c>: the file's grid
///  shows the build status column as the browse window's does, and the Build Report tab follows a single selected commit
///  whose build has a report, while the repository's "show build result page" is on (a link to it, as the browse window's
///  tab, since the new shell has no web view).
/// </summary>
public partial class FileHistoryWindow
{
    // Upstream's _buildReportTabCaption.
    public const string BuildReportTabCaption = "Build Report";

    private const double BuildStatusIconWidth = 24;
    private const double BuildStatusTextWidth = 150;

    private BuildServerWatcher? _buildWatcher;
    private string? _buildServerType;
    private bool _showBuildReport;
    private BuildStatusCell? _reportedBuild;

    /// <summary>
    ///  Opens the settings, which upstream's build server watcher asks for when its server is not set up; null to ignore.
    /// </summary>
    public Func<Task>? ShowSettings { get; init; }

    /// <summary>
    ///  The build server whose results the grid shows, or null, for tests.
    /// </summary>
    public string? BuildServerType => _buildServerType;

    private void WireBuildServer()
    {
        HistoryList.SelectionChanged += (_, _) => FollowSelectedBuild();
        OpenBuildReportLink.Click += (_, _) => OpenBuildLink(_reportedBuild?.Url);

        // As upstream's grid, a click on a build status opens the build report.
        HistoryList.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left
                && (e.Source as Control)?.FindAncestorOfType<StackPanel>(includeSelf: true) is { } cell
                && cell.Classes.Contains("buildStatus")
                && cell.DataContext is CommitListItem item)
            {
                OpenBuildLink(item.Build.Url);
            }
        }, handledEventsToo: true);
        Closed += (_, _) => StopBuildServer();
    }

    // Upstream's BuildStatusColumnProvider.ApplySettings, as the browse window's grid.
    private void ShowBuildStatusColumn()
    {
        bool showIcon = _preferences.ShowBuildStatusIconColumn;
        bool showText = _preferences.ShowBuildStatusTextColumn;
        Resources["BuildStatusShowIcon"] = showIcon;
        Resources["BuildStatusShowText"] = showText;
        Resources["BuildStatusColumnWidth"] = _buildServerType is null || !(showIcon || showText) ? 0.0
            : showText ? BuildStatusTextWidth
            : BuildStatusIconWidth;
    }

    // As upstream's grid after each read of the revisions: the build server is chosen again and polled. A failure of a
    // plugin is traced, not shown, as upstream's fire-and-forget launch.
    private async Task LaunchBuildServerAsync()
    {
        _buildWatcher?.Dispose();
        BuildServerWatcher watcher = new(
            _module,
            () => AppServices.RevisionLinks.Open(_repositoryPath),
            AppServices.BuildServers,
            AppServices.BuildServerCredentials,
            new BuildServerWatcherHost(
                _commits.IsLoaded,
                info => Dispatcher.UIThread.Post(() => _commits.ApplyBuildInfo(info)),
                () => Dispatcher.UIThread.Post(() =>
                {
                    if (ShowSettings is { } showSettings)
                    {
                        Run(showSettings);
                    }
                }),
                AskBuildServerCredentials));
        _buildWatcher = watcher;
        try
        {
            string? type = await watcher.LaunchAsync();
            bool showReport = await Task.Run(() =>
                BuildServerSettings.ShowBuildResultPage.ValueOrDefault(AppServices.RevisionLinks.Open(_repositoryPath)));
            if (_buildWatcher == watcher)
            {
                _buildServerType = type;
                _showBuildReport = type is not null && showReport;
                ShowBuildStatusColumn();
                ShowBuildReportTab();
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Build server integration failed: {ex}");
        }
    }

    private void StopBuildServer()
    {
        _buildWatcher?.Dispose();
        _buildWatcher = null;
    }

    // The tab follows the selected commit's build, which a later poll may give or change.
    private void FollowSelectedBuild()
    {
        _reportedBuild?.PropertyChanged -= OnReportedBuildChanged;
        _reportedBuild = _selection.Revisions.Count == 1 ? LatestSelectedItem?.Build : null;
        _reportedBuild?.PropertyChanged += OnReportedBuildChanged;
        ShowBuildReportTab();
    }

    private void OnReportedBuildChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BuildStatusCell.Url))
        {
            ShowBuildReportTab();
        }
    }

    // Upstream's FillBuildReport: the tab shows for one selected commit whose build has a report, while the page is on.
    private void ShowBuildReportTab()
    {
        bool show = _showBuildReport && !string.IsNullOrEmpty(_reportedBuild?.Url);
        if (!show && Tabs.SelectedItem == BuildReportTab)
        {
            Tabs.SelectedItem = TabFor(FileHistoryTab.Diff).IsVisible ? TabFor(FileHistoryTab.Diff) : CommitTab;
        }

        BuildReportTab.IsVisible = show;
        ToolTip.SetTip(OpenBuildReportLink, show ? _reportedBuild!.Url : null);
    }

    // Asked on the adapter's thread; the window shows on the UI thread and the answer comes back when it closes.
    private IBuildServerCredentials? AskBuildServerCredentials(string uniqueKey, IBuildServerCredentials credentials)
        => ModalWindow.Show(() => new BuildServerCredentialsWindow(uniqueKey, credentials), new WindowOwner(this))
            .Credentials;

    private void OpenBuildLink(string? url)
    {
        if (url is not null && Uri.TryCreate(url, UriKind.Absolute, out Uri? address))
        {
            _ = Launcher.LaunchUriAsync(address);
        }
    }
}
