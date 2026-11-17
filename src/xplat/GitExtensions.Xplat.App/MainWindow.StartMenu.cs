using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using GitCommands;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

// Upstream's StartToolStripMenuItem and RepositoryHistoryUIService: the Start menu's favourite and recent repositories,
// numbered, pinned ones with upstream's pin image, the checked out branch on the right as upstream's shortcut text, and
// the recent list's "Clear list".
public partial class MainWindow
{
    // Whether the click on a recent repository held Ctrl (Cmd on macOS).
    private bool _openRecentInNewInstance;

    // The history the repository menus show last, for the working directory drop-down, which builds its items when it
    // opens.
    private RepositoryHistory _menuHistory = new([], []);

    // Upstream's reportAnIssueToolStripMenuItem_Click and Dashboard's IssuesItem_Click: the environment information is
    // copied for the issue's text before the issues page opens.
    private async Task ReportIssueAsync()
    {
        await AboutWindow.CopyInformationAsync(this, _git?.Version?.ToString());
        OpenUrl(IssuesUrl);
    }

    private void WireStartMenu()
    {
        // The items' actions are the toolbar view model's commands (bound in XAML).
    }

    // The recent list changed: the menus show it at once, then again with the history's pins and the favourites.
    private void ShowRecentMenu()
    {
        _menuHistory = _menuHistory with { Recent = [.. _recent.Items.Select(item => new RepositoryEntry(item.Path))] };
        ShowRecentRepositories(_menuHistory.Recent);
        ReloadDashboardIfChanged();
        Run(async () =>
        {
            _menuHistory = await AppServices.RecentRepositories.LoadHistoryAsync();
            ShowRecentRepositories(_menuHistory.Recent);
            ShowFavouriteRepositories(_menuHistory.Favourites);
        });
    }

    // The Start menu's recent repositories, then upstream's separator and "Clear list" (the toolbar view model's entries).
    private void ShowRecentRepositories(IReadOnlyList<RepositoryEntry> recent)
    {
        IReadOnlyList<MenuEntry> entries = Toolbar.RecentRepositoriesMenu(recent, withClear: true);
        MenuEntries.Fill(RecentMenu.Items, entries, this, TrackNewInstanceClick);
        RecentMenu.IsEnabled = entries.Count > 0;
    }

    private void ShowFavouriteRepositories(IReadOnlyList<RepositoryEntry> favourites)
    {
        IReadOnlyList<MenuEntry> entries = Toolbar.FavouriteRepositoriesMenu(favourites);
        MenuEntries.Fill(FavouritesMenu.Items, entries, this, TrackNewInstanceClick);
        FavouritesMenu.IsEnabled = entries.Count > 0;
    }

    // Upstream's OpenRepo: with Ctrl (Cmd on macOS) held the repository opens in a new instance. A click gives no modifiers,
    // so the release that makes it is read first.
    private void TrackNewInstanceClick(MenuItem item, MenuEntry entry)
    {
        if (entry.Command == Toolbar.OpenRecentRepositoryCommand)
        {
            item.AddHandler(PointerReleasedEvent,
                (_, e) => _openRecentInNewInstance = e.KeyModifiers.HasFlag(Hotkeys.CommandModifier),
                RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }

    private void OpenRecentRepository(string path)
    {
        if (_openRecentInNewInstance)
        {
            NewInstanceCommand.Start(_launcher, path);
        }
        else
        {
            Run(() => OpenRepositoryAsync(path));
        }

        _openRecentInNewInstance = false;
    }
}
