using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GitExtensions.Xplat.Core.BuildServer;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using GitUIPluginInterfaces.BuildServerIntegration;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The grid's build server integration, as upstream's <c>RevisionGridControl</c> has it: the "Build Status" column, the
///  View menu's toggles for it, the grid menu's links to the build report and pull request, and the
///  <see cref="BuildServerWatcher"/> that fills the column.
/// </summary>
public partial class MainWindow
{
    private const double BuildStatusIconWidth = 24;
    private const double BuildStatusTextWidth = 150;

    private BuildServerWatcher? _buildWatcher;
    private string? _buildServerType;
    private string? _buildStatusRepository;

    /// <summary>
    ///  The build server whose results the grid shows, or null.
    /// </summary>
    public string? BuildServerType => _buildServerType;

    /// <summary>
    ///  The watcher of the open repository, while it shows a build server's results.
    /// </summary>
    internal BuildServerWatcher? BuildWatcher => _buildWatcher;

    private void WireBuildStatus()
    {
        ShowBuildStatusColumn();
        ShowBuildStatusIconMenuItem.Click += (_, _) =>
        {
            _preferences.ShowBuildStatusIconColumn = !_preferences.ShowBuildStatusIconColumn;
            ShowBuildStatusColumn();
        };
        ShowBuildStatusTextMenuItem.Click += (_, _) =>
        {
            _preferences.ShowBuildStatusTextColumn = !_preferences.ShowBuildStatusTextColumn;
            ShowBuildStatusColumn();
        };
        OpenBuildReportMenuItem.Click += (_, _) => OpenBuildLink(SelectedBuildStatus()?.Url);
        OpenPullRequestPageMenuItem.Click += (_, _) => OpenBuildLink(SelectedBuildStatus()?.PullRequestUrl);
        CommitContextMenu.Opening += (_, _) =>
        {
            OpenBuildReportMenuItem.IsEnabled = SelectedBuildStatus()?.Url is not null;
            OpenPullRequestPageMenuItem.IsEnabled = SelectedBuildStatus()?.PullRequestUrl is not null;
        };

        // As upstream, a click on a build status opens the build report.
        CommitList.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left
                && (e.Source as Control)?.FindAncestorOfType<StackPanel>(includeSelf: true) is { } cell
                && cell.Classes.Contains("buildStatus")
                && cell.DataContext is CommitListItem item)
            {
                OpenBuildLink(item.Build.Url);
            }
        }, handledEventsToo: true);
    }

    /// <summary>
    ///  Upstream's <c>BuildStatusColumnProvider.ApplySettings</c>: the column shows while a build server gives results and the
    ///  symbol or the text is chosen; the symbol alone makes it narrow.
    /// </summary>
    private void ShowBuildStatusColumn()
    {
        bool showIcon = _preferences.ShowBuildStatusIconColumn;
        bool showText = _preferences.ShowBuildStatusTextColumn;
        ShowBuildStatusIconMenuItem.IsChecked = showIcon;
        ShowBuildStatusTextMenuItem.IsChecked = showText;
        Resources["BuildStatusShowIcon"] = showIcon;
        Resources["BuildStatusShowText"] = showText;
        Resources["BuildStatusColumnWidth"] = _buildServerType is null || !(showIcon || showText) ? 0.0
            : showText ? BuildStatusTextWidth
            : BuildStatusIconWidth;
    }

    /// <summary>
    ///  As upstream's grid after each read of the revisions: the build server is chosen again (so a settings change applies)
    ///  and polled. A failure of a plugin is traced, not shown, as upstream's fire-and-forget launch.
    /// </summary>
    private async Task LaunchBuildServerAsync()
    {
        StopBuildServer();
        if (RepositoryPath is not { } path)
        {
            return;
        }

        if (path != _buildStatusRepository)
        {
            _commits.ClearBuildStatuses();
            _buildStatusRepository = path;
        }

        BuildServerWatcher watcher = new(
            GitModules.Open(path),
            () => AppServices.RevisionLinks.Open(path),
            AppServices.BuildServers,
            AppServices.BuildServerCredentials,
            new BuildServerWatcherHost(
                _commits.IsLoaded,
                info => Dispatcher.UIThread.Post(() => _commits.ApplyBuildInfo(info)),
                () => Dispatcher.UIThread.Post(() => Run(() => ShowSettingsAsync())),
                AskBuildServerCredentials));
        _buildWatcher = watcher;
        try
        {
            string? type = await watcher.LaunchAsync();
            if (_buildWatcher == watcher)
            {
                _buildServerType = type;
                ShowBuildStatusColumn();
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
        _buildServerType = null;
        ShowBuildStatusColumn();
    }

    // Asked on the adapter's thread; the window shows on the UI thread and the answer comes back when it closes.
    private IBuildServerCredentials? AskBuildServerCredentials(string uniqueKey, IBuildServerCredentials credentials)
        => ModalWindow.Show(() => new BuildServerCredentialsWindow(uniqueKey, credentials), new WindowOwner(this)).Credentials;

    private BuildStatusCell? SelectedBuildStatus()
        => (CommitList.SelectedItem as CommitListItem)?.Build;

    private void OpenBuildLink(string? url)
    {
        if (url is not null && Uri.TryCreate(url, UriKind.Absolute, out Uri? address))
        {
            _ = Launcher.LaunchUriAsync(address);
        }
    }
}
