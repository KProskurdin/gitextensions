using GitCommands;
using GitCommands.UserRepositoryHistory;
using GitExtensions.Xplat.Core.Settings;
using HistoryEntry = GitCommands.UserRepositoryHistory.Repository;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Upstream's settings of the recent repositories menus, as <c>FormRecentReposSettings</c> edits them and
///  <see cref="RecentRepoSplitter"/> reads them; the defaults are upstream's.
/// </summary>
public sealed record RecentRepositoryMenuSettings(
    int MaxTopRepositories = 0,
    bool HideTopRepositoriesFromRecentList = false,
    ShorteningRecentRepoPathStrategy Shortening = ShorteningRecentRepoPathStrategy.None,
    bool SortTopRepos = false,
    bool SortRecentRepos = false,
    int ComboMinWidth = 0)
{
    /// <summary>
    ///  Upstream's splitter with these settings.
    /// </summary>
    public RecentRepoSplitter Splitter() => new()
    {
        MaxTopRepositories = MaxTopRepositories,
        HideTopRepositoriesFromRecentList = HideTopRepositoriesFromRecentList,
        ShorteningStrategy = Shortening,
        SortTopRepos = SortTopRepos,
        SortRecentRepos = SortRecentRepos,
        RecentReposComboMinWidth = ComboMinWidth,
    };
}

/// <summary>
///  A repository in one of upstream's two lists: its caption, its path (upstream's tooltip), bold when it is anchored to
///  that list, red when its folder is gone.
/// </summary>
public sealed record RecentRepositorySettingsRow(string Path, string Caption, bool Anchored, bool Missing);

/// <summary>
///  Upstream's <c>FormRecentReposSettings</c> without its window: the settings of the recent repositories menus, the top
///  and recent repositories as upstream's <see cref="RecentRepoSplitter"/> splits them with those settings, and the
///  anchors the user sets, saved with OK.
/// </summary>
public sealed class RecentRepositoriesSettingsDialog
{
    /// <summary>
    ///  Upstream's narrowest column: a smaller width is 0 (autosize) going down, or this going up.
    /// </summary>
    public const int MinComboWidthAllowed = 30;

    // Upstream's NumericUpDown limits.
    public const int MinHistorySize = 10;
    public const int MaxHistorySize = 999;
    public const int MaxTopRepositoriesLimit = 1000000;
    public const int MaxComboWidth = 800;

    private readonly IRecentRepositoryStore _store;
    private List<HistoryEntry> _history;

    private RecentRepositoriesSettingsDialog(IRecentRepositoryStore store, IReadOnlyList<RepositoryEntry> history,
        RecentRepositoryMenuSettings settings, int historySize)
    {
        _store = store;
        _history = ToHistory(history);
        Settings = settings with
        {
            MaxTopRepositories = Math.Clamp(settings.MaxTopRepositories, 0, MaxTopRepositoriesLimit),
            ComboMinWidth = Math.Clamp(settings.ComboMinWidth, 0, MaxComboWidth),
        };
        HistorySize = Math.Clamp(historySize, MinHistorySize, MaxHistorySize);
    }

    /// <summary>
    ///  The settings as the window shows them; the lists follow them at once, they are stored with OK.
    /// </summary>
    public RecentRepositoryMenuSettings Settings { get; set; }

    /// <summary>
    ///  Upstream's "Maximum number of recent repositories".
    /// </summary>
    public int HistorySize { get; set; }

    /// <summary>
    ///  Upstream's constructor: the stored recent repositories and the user's settings, kept within upstream's limits.
    /// </summary>
    public static async Task<RecentRepositoriesSettingsDialog> LoadAsync(IRecentRepositoryStore store,
        IAppPreferences preferences)
        => new(store, (await store.LoadHistoryAsync()).Recent, preferences.RecentRepositoryMenu,
            preferences.RecentRepositoriesHistorySize);

    /// <summary>
    ///  Upstream's <c>RefreshRepos</c>: the top and recent repositories as the menus would show them.
    /// </summary>
    public (IReadOnlyList<RecentRepositorySettingsRow> Top, IReadOnlyList<RecentRepositorySettingsRow> Recent) Rows()
    {
        List<RecentRepoInfo> top = [];
        List<RecentRepoInfo> recent = [];
        Settings.Splitter().SplitRecentRepos(_history, top, recent);
        return ([.. top.Select(info => Row(info, HistoryEntry.RepositoryAnchor.AnchoredInTop))],
            [.. recent.Select(info => Row(info, HistoryEntry.RepositoryAnchor.AnchoredInRecent))]);
    }

    /// <summary>
    ///  Where the repository is anchored, for upstream's context menu.
    /// </summary>
    public HistoryEntry.RepositoryAnchor AnchorOf(string path)
        => _history.FirstOrDefault(entry => entry.Path == path)?.Anchor ?? HistoryEntry.RepositoryAnchor.None;

    /// <summary>
    ///  Upstream's anchor items and "Remove anchor"; the double-clicks anchor to the other list.
    /// </summary>
    public void Anchor(IEnumerable<string> paths, HistoryEntry.RepositoryAnchor anchor)
    {
        HashSet<string> selected = [.. paths];
        foreach (HistoryEntry entry in _history.Where(entry => selected.Contains(entry.Path)))
        {
            entry.Anchor = anchor;
        }
    }

    /// <summary>
    ///  Upstream's "Remove from recent repositories": removed from the stored list at once, which, as upstream, is read
    ///  back without the anchors not yet saved.
    /// </summary>
    public async Task RemoveAsync(IEnumerable<string> paths)
    {
        foreach (string path in paths.ToList())
        {
            await _store.RemoveAsync(path);
        }

        _history = ToHistory((await _store.LoadHistoryAsync()).Recent);
    }

    /// <summary>
    ///  Upstream's OK: the settings and the repositories with their anchors.
    /// </summary>
    public async Task SaveAsync(IAppPreferences preferences)
    {
        preferences.RecentRepositoryMenu = Settings;
        preferences.RecentRepositoriesHistorySize = HistorySize;
        await _store.SaveRecentAsync(
            [.. _history.Select(entry => new RepositoryEntry(entry.Path, entry.Category, entry.Anchor))]);
    }

    /// <summary>
    ///  Upstream's <c>comboMinWidthEdit_ValueChanged</c>: a width below <see cref="MinComboWidthAllowed"/> becomes 0 going
    ///  down and <see cref="MinComboWidthAllowed"/> going up.
    /// </summary>
    public static int ComboMinWidth(int previous, int value)
        => value >= MinComboWidthAllowed || value == previous ? value
            : value < previous ? 0
            : MinComboWidthAllowed;

    /// <summary>
    ///  Upstream's <c>contextMenuStrip1_Opening</c>, for the last selected repository's anchor.
    /// </summary>
    public static (bool AnchorToTop, bool AnchorToRecent, bool RemoveAnchor) MenuState(
        HistoryEntry.RepositoryAnchor anchor)
        => (anchor != HistoryEntry.RepositoryAnchor.AnchoredInTop,
            anchor != HistoryEntry.RepositoryAnchor.AnchoredInRecent,
            anchor != HistoryEntry.RepositoryAnchor.None);

    private static RecentRepositorySettingsRow Row(RecentRepoInfo info, HistoryEntry.RepositoryAnchor listAnchor)
        => new(info.Repo.Path, info.Caption ?? info.Repo.Path, info.Repo.Anchor == listAnchor,
            !Directory.Exists(info.Repo.Path));

    private static List<HistoryEntry> ToHistory(IReadOnlyList<RepositoryEntry> entries)
        => [.. entries.Select(entry =>
            new HistoryEntry(entry.Path) { Category = entry.Category, Anchor = entry.Anchor })];
}
