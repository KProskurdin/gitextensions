using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Repository;

// Upstream's StartToolStripMenuItem, RepositoryHistoryUIService and WorkingDirectoryToolStripSplitButton (MVVM.md): the
// Start menu's and the working directory drop-down's repositories as entries, numbered, pinned ones with upstream's pin,
// the checked out branch on the right as upstream's shortcut text, and their commands.
public sealed partial class BrowseToolbarViewModel
{
    public const string ClearListText = "Clear list";
    public const string ConfigureWorkingDirMenuText = "Co&nfigure this menu...";

    /// <summary>
    ///  Upstream's PopulateRecentRepositoriesMenu: the top repositories, a separator, the others; with
    ///  <paramref name="withClear"/> (the Start menu) a separator and "Clear list" after them.
    /// </summary>
    public IReadOnlyList<MenuEntry> RecentRepositoriesMenu(IReadOnlyList<RepositoryEntry> recent, bool withClear)
    {
        (IReadOnlyList<RepositoryMenuEntry> top, IReadOnlyList<RepositoryMenuEntry> rest) =
            RepositoryMenus.Recent(recent, _preferences.RecentRepositoryMenu.Splitter());
        List<MenuEntry> entries = [.. top.Select(RepositoryEntry)];
        if (rest.Count > 0 && top.Count > 0)
        {
            entries.Add(MenuEntry.Separator());
        }

        entries.AddRange(rest.Select(RepositoryEntry));

        // Upstream's tsmiRecentRepositoriesClear.
        if (withClear && entries.Count > 0)
        {
            entries.Add(MenuEntry.Separator());
            entries.Add(new MenuEntry(_translate(ClearListText), ClearRecentRepositoriesCommand) { HasAccessKey = true });
        }

        return entries;
    }

    /// <summary>
    ///  Upstream's PopulateFavouriteRepositoriesMenu: a submenu per category.
    /// </summary>
    public IReadOnlyList<MenuEntry> FavouriteRepositoriesMenu(IReadOnlyList<RepositoryEntry> favourites)
        =>
        [
            .. RepositoryMenus.Favourites(favourites, _preferences.RecentRepositoryMenu.Splitter())
                .Select(group => new MenuEntry(group.Category ?? "")
                {
                    HasAccessKey = true, Children = [.. group.Entries.Select(RepositoryEntry)],
                })
        ];

    /// <summary>
    ///  Upstream's WorkingDirectoryToolStripSplitButton.FillDropDown below its search box: the favourites, the recent
    ///  repositories, Open and Close as in the menus, and "Configure this menu...".
    /// </summary>
    /// <param name="favouritesText">The Start menu's "Favorite repositories" text.</param>
    /// <param name="openText">The Start menu's "Open..." text.</param>
    /// <param name="openGesture">Its hotkey, as the view's key gesture.</param>
    /// <param name="closeText">The Start menu's "Close" text.</param>
    /// <param name="closeGesture">Its hotkey.</param>
    public IReadOnlyList<MenuEntry> WorkingDirMenu(RepositoryHistory history, string favouritesText, string openText,
        object? openGesture, string closeText, object? closeGesture)
    {
        List<MenuEntry> entries = [];
        IReadOnlyList<MenuEntry> favourites = FavouriteRepositoriesMenu(history.Favourites);
        if (favourites.Count > 0)
        {
            entries.Add(new MenuEntry(favouritesText) { HasAccessKey = true, Image = "Star", Children = favourites });
        }

        entries.AddRange(RecentRepositoriesMenu(history.Recent, withClear: false));
        entries.Add(MenuEntry.Separator());
        entries.Add(new MenuEntry(openText, OpenRepositoryCommand)
        {
            HasAccessKey = true, Image = "RepoOpen", Gesture = openGesture,
        });
        entries.Add(new MenuEntry(closeText, CloseRepositoryCommand)
        {
            HasAccessKey = true, Image = "DashboardFolderGit", Gesture = closeGesture, IsEnabled = IsOpen,
        });
        entries.Add(MenuEntry.Separator());
        entries.Add(new MenuEntry(AccessKeyText(_translate(ConfigureWorkingDirMenuText)), ConfigureRecentRepositoriesCommand)
        {
            HasAccessKey = true, Name = "ConfigureWorkingDirMenuItem",
        });
        return entries;
    }

    /// <summary>
    ///  Upstream's search box filter: the repositories whose text has <paramref name="search"/>, case ignored; the other
    ///  entries stay.
    /// </summary>
    public static void FilterRepositories(IEnumerable<MenuEntry> entries, string? search)
    {
        foreach (MenuEntry entry in entries.Where(entry => entry.SearchText is not null))
        {
            entry.IsVisible = string.IsNullOrWhiteSpace(search)
                              || entry.SearchText!.Contains(search, StringComparison.CurrentCultureIgnoreCase);
        }
    }

    // Upstream's AddRecentRepositories: the number as the access key of the first ten. The text the search box finds the
    // entry by is its number and caption.
    private MenuEntry RepositoryEntry(RepositoryMenuEntry entry)
    {
        string number = entry.Number switch
        {
            < 10 => $"_{entry.Number}",
            10 => "1_0",
            _ => $"{entry.Number}",
        };
        string? branch = _recent.Items.FirstOrDefault(item => RecentRepositoryPaths.Same(item.Path, entry.Path))?.Branch;
        return new MenuEntry($"{number}: {entry.Caption.Replace("_", "__", StringComparison.Ordinal)}",
            OpenRecentRepositoryCommand, entry.Path)
        {
            HasAccessKey = true,
            HasPanelHeader = true,
            SecondaryText = branch,
            Image = entry.Anchored ? "Pin" : null,
            Tip = entry.Tooltip,
            SearchText = $"{entry.Number}: {entry.Caption}",
        };
    }

    // Upstream's OpenRepo: with Ctrl (Cmd on macOS) held the repository opens in a new instance, which the window knows.
    [RelayCommand]
    private void OpenRecentRepository(string path) => BrowseWindow.OpenRecentRepository(path);

    // Upstream's tsmiRecentRepositoriesClear_Click empties the recent list without a question.
    [RelayCommand]
    private void ClearRecentRepositories()
        => Run(async () =>
        {
            foreach (RecentRepository recent in _recent.Items.ToList())
            {
                await _recent.RemoveAsync(recent.Path);
            }
        });

    [RelayCommand]
    private void ConfigureRecentRepositories() => Run(BrowseWindow.ConfigureRecentRepositoriesAsync);
}
