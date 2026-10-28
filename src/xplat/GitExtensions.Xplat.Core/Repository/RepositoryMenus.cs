using GitCommands.UserRepositoryHistory;
using HistoryEntry = GitCommands.UserRepositoryHistory.Repository;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  A repository of upstream's "Recent repositories" or "Favorite repositories" menu: the caption upstream's
///  <see cref="RecentRepoSplitter"/> gives the path, its number in the menu (upstream's access key for the first ten) and
///  whether it is pinned (upstream's pin image).
/// </summary>
public sealed record RepositoryMenuEntry(string Path, string Caption, int Number, bool Anchored)
{
    /// <summary>
    ///  Upstream shows the path as the item's tooltip when the caption is not the path.
    /// </summary>
    public string? Tooltip => Caption == Path ? null : Path;
}

/// <summary>
///  The favourites of one category as upstream's "Favorite repositories" menu lists them, numbered within the category.
/// </summary>
public sealed record RepositoryMenuGroup(string? Category, IReadOnlyList<RepositoryMenuEntry> Entries);

/// <summary>
///  Upstream's <c>RepositoryHistoryUIService.PopulateRecentRepositoriesMenu</c> and <c>PopulateFavouriteRepositoriesMenu</c>
///  as data: the Start menu's and the working directory menu's repositories, split and shortened by upstream's
///  <see cref="RecentRepoSplitter"/> with the user's recent repositories settings.
/// </summary>
public static class RepositoryMenus
{
    /// <summary>
    ///  The recent repositories: the top ones, then, after upstream's separator, the others, numbered across both.
    /// </summary>
    /// <param name="recent">The recent repositories, newest first.</param>
    /// <param name="splitter">The splitter with the shortening settings; upstream's settings when null.</param>
    public static (IReadOnlyList<RepositoryMenuEntry> Top, IReadOnlyList<RepositoryMenuEntry> Others) Recent(
        IReadOnlyList<RepositoryEntry> recent, RecentRepoSplitter? splitter = null)
    {
        List<RecentRepoInfo> top = [];
        List<RecentRepoInfo> rest = [];
        (splitter ?? new RecentRepoSplitter()).SplitRecentRepos(History(recent), top, rest);
        int number = 0;
        return ([.. top.Select(info => Entry(info, ++number))], [.. rest.Select(info => Entry(info, ++number))]);
    }

    /// <summary>
    ///  The favourites grouped by category in the order of the categories' names, each numbered from one.
    /// </summary>
    /// <param name="favourites">The favourite repositories, as stored.</param>
    /// <param name="splitter">The splitter with the shortening settings; upstream's settings when null.</param>
    public static IReadOnlyList<RepositoryMenuGroup> Favourites(IReadOnlyList<RepositoryEntry> favourites,
        RecentRepoSplitter? splitter = null)
    {
        List<RecentRepoInfo> top = [];
        List<RecentRepoInfo> rest = [];
        (splitter ?? new RecentRepoSplitter()).SplitRecentRepos(History(favourites), top, rest);
        return
        [
            .. top.Union(rest).GroupBy(info => info.Repo.Category).OrderBy(group => group.Key).Select(group =>
            {
                int number = 0;
                return new RepositoryMenuGroup(group.Key, [.. group.Select(info => Entry(info, ++number))]);
            })
        ];
    }

    private static List<HistoryEntry> History(IReadOnlyList<RepositoryEntry> entries)
        => [.. entries.Select(entry => new HistoryEntry(entry.Path) { Category = entry.Category, Anchor = entry.Anchor })];

    // Upstream's AddRecentRepositories: the view adds the number as the access key, the pin image and the branch.
    private static RepositoryMenuEntry Entry(RecentRepoInfo info, int number)
        => new(info.Repo.Path, info.Caption ?? info.Repo.Path, number, info.Anchored);
}
