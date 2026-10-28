using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
        ShowRecentRepositories([.. _recent.Items.Select(item => new RepositoryEntry(item.Path))]);
        ReloadDashboardIfChanged();
        Run(async () =>
        {
            RepositoryHistory history = await AppServices.RecentRepositories.LoadHistoryAsync();
            ShowRecentRepositories(history.Recent);
            ShowFavouriteRepositories(history.Favourites);
        });
    }

    // Upstream's PopulateRecentRepositoriesMenu, then its separator and "Clear list".
    private void ShowRecentRepositories(IReadOnlyList<RepositoryEntry> recent)
    {
        (IReadOnlyList<RepositoryMenuEntry> top, IReadOnlyList<RepositoryMenuEntry> rest) =
            RepositoryMenus.Recent(recent);
        List<Control> items = [.. top.Select(RepositoryMenuItem)];
        if (rest.Count > 0 && top.Count > 0)
        {
            items.Add(new Separator());
        }

        items.AddRange(rest.Select(RepositoryMenuItem));
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

    // Upstream's PopulateFavouriteRepositoriesMenu: a submenu per category.
    private void ShowFavouriteRepositories(IReadOnlyList<RepositoryEntry> favourites)
    {
        IReadOnlyList<RepositoryMenuGroup> groups = RepositoryMenus.Favourites(favourites);
        FavouritesMenu.ItemsSource = groups
            .Select(group => new MenuItem
            {
                Header = group.Category, ItemsSource = group.Entries.Select(RepositoryMenuItem).ToList()
            })
            .ToList();
        FavouritesMenu.IsEnabled = groups.Count > 0;
    }

    // Upstream's AddRecentRepositories: the number as the access key of the first ten.
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
        MenuItem item = new() { Header = header, Icon = entry.Anchored ? UpstreamIcon("Pin") : null };
        ToolTip.SetTip(item, entry.Tooltip);
        item.Click += (_, _) => Run(() => OpenRepositoryAsync(entry.Path));
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
