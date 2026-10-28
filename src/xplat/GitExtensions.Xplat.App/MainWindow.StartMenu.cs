using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

// Upstream's StartToolStripMenuItem and RepositoryHistoryUIService: the Start menu's favourite and recent repositories,
// numbered, pinned ones with upstream's pin image, the checked out branch on the right as upstream's shortcut text, and
// the recent list's "Clear list".
public partial class MainWindow
{
    private const string ClearListText = "Clear list";

    // The history the repository menus show last, for the working directory drop-down, which builds its items when it
    // opens.
    private RepositoryHistory _menuHistory = new([], []);

    private void WireStartMenu()
    {
        UserManualMenuItem.Click += (_, _) => OpenUrl(AppSettings.DocumentationBaseUrl);
        TranslateMenuItem.Click += (_, _) => OpenUrl(TranslateUrl);
        ReportIssueMenuItem.Click += (_, _) => OpenUrl(IssuesUrl);
        DashboardRefreshMenuItem.Click += (_, _) => Run(Dashboard.LoadAsync);
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

    // The Start menu's recent repositories, then upstream's separator and "Clear list".
    private void ShowRecentRepositories(IReadOnlyList<RepositoryEntry> recent)
    {
        List<Control> items = RecentRepositoryItems(recent);
        if (items.Count > 0)
        {
            MenuItem clear = new() { Header = UpstreamTranslation.ByText(ClearListText) };
            clear.Click += (_, _) => Run(ClearRecentRepositoriesAsync);
            items.Add(new Separator());
            items.Add(clear);
        }

        RecentMenu.ItemsSource = items;
        RecentMenu.IsEnabled = items.Count > 0;
    }

    // Upstream's PopulateRecentRepositoriesMenu: the top repositories, a separator, the others.
    private List<Control> RecentRepositoryItems(IReadOnlyList<RepositoryEntry> recent)
    {
        (IReadOnlyList<RepositoryMenuEntry> top, IReadOnlyList<RepositoryMenuEntry> rest) =
            RepositoryMenus.Recent(recent, _preferences.RecentRepositoryMenu.Splitter());
        List<Control> items = [.. top.Select(RepositoryMenuItem)];
        if (rest.Count > 0 && top.Count > 0)
        {
            items.Add(new Separator());
        }

        items.AddRange(rest.Select(RepositoryMenuItem));
        return items;
    }

    private void ShowFavouriteRepositories(IReadOnlyList<RepositoryEntry> favourites)
    {
        List<MenuItem> groups = FavouriteRepositoryItems(favourites);
        FavouritesMenu.ItemsSource = groups;
        FavouritesMenu.IsEnabled = groups.Count > 0;
    }

    // Upstream's PopulateFavouriteRepositoriesMenu: a submenu per category.
    private List<MenuItem> FavouriteRepositoryItems(IReadOnlyList<RepositoryEntry> favourites)
        => [.. RepositoryMenus.Favourites(favourites, _preferences.RecentRepositoryMenu.Splitter())
            .Select(group => new MenuItem
            {
                Header = group.Category, ItemsSource = group.Entries.Select(RepositoryMenuItem).ToList()
            })];

    // Upstream's AddRecentRepositories: the number as the access key of the first ten. The item's text is its tag, for
    // the working directory drop-down's search box.
    private MenuItem RepositoryMenuItem(RepositoryMenuEntry entry)
    {
        string number = entry.Number switch
        {
            < 10 => $"_{entry.Number}",
            10 => "1_0",
            _ => $"{entry.Number}",
        };
        string? branch = _recent.Items.FirstOrDefault(item => RecentRepositoryPaths.Same(item.Path, entry.Path))
            ?.Branch;
        DockPanel header = new() { LastChildFill = true };
        if (branch is not null)
        {
            TextBlock branchText = new() { Text = branch, Margin = new Avalonia.Thickness(24, 0, 0, 0), Opacity = 0.7 };
            DockPanel.SetDock(branchText, Dock.Right);
            header.Children.Add(branchText);
        }

        header.Children.Add(new AccessText
        {
            Text = $"{number}: {entry.Caption.Replace("_", "__", StringComparison.Ordinal)}",
            VerticalAlignment = VerticalAlignment.Center,
        });
        MenuItem item = new()
        {
            Header = header,
            Icon = entry.Anchored ? UpstreamIcon("Pin") : null,
            Tag = $"{entry.Number}: {entry.Caption}",
        };
        ToolTip.SetTip(item, entry.Tooltip);

        // Upstream's OpenRepo: with Ctrl held the repository opens in a new instance. A click gives no modifiers, so
        // the release that makes it is read first.
        bool newInstance = false;
        item.AddHandler(PointerReleasedEvent, (_, e) => newInstance = e.KeyModifiers.HasFlag(KeyModifiers.Control),
            RoutingStrategies.Tunnel, handledEventsToo: true);
        item.Click += (_, _) =>
        {
            if (newInstance && Environment.ProcessPath is { } process)
            {
                _launcher.Start(process, [entry.Path], entry.Path);
            }
            else
            {
                Run(() => OpenRepositoryAsync(entry.Path));
            }

            newInstance = false;
        };
        return item;
    }

    // Upstream's tsmiRecentRepositoriesClear_Click empties the recent list without a question.
    private async Task ClearRecentRepositoriesAsync()
    {
        foreach (RecentRepository recent in _recent.Items.ToList())
        {
            await _recent.RemoveAsync(recent.Path);
        }
    }
}
