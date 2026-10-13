using GitCommands.UserRepositoryHistory;
using HistoryEntry = GitCommands.UserRepositoryHistory.Repository;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  A repository tile of upstream's dashboard (<c>UserRepositoriesList</c>): the caption upstream's
///  <see cref="RecentRepoSplitter"/> gives the path, the category of a favourite, and, once read, the checked out branch and
///  whether the folder is still a repository.
/// </summary>
public sealed record DashboardRepository(string Path, string Caption, string? Category, bool IsFavourite)
{
    public string? Branch { get; init; }

    public bool IsValid { get; init; } = true;

    /// <summary>
    ///  Upstream draws a star on the tile of a repository in a category.
    /// </summary>
    public bool HasCategory => !string.IsNullOrWhiteSpace(Category);

    public bool HasBranch => !string.IsNullOrEmpty(Branch);
}

/// <summary>
///  A group of upstream's dashboard: the recent repositories (<see cref="Category"/> null), or the favourites of a category.
/// </summary>
public sealed record DashboardGroup(string Header, string? Category, IReadOnlyList<DashboardRepository> Repositories)
{
    public bool IsRecent => Category is null;
}

/// <summary>
///  What the dashboard learns of a repository's folder after the list shows.
/// </summary>
public readonly record struct DashboardRepositoryState(bool IsValid, string? Branch);

/// <summary>
///  Upstream's dashboard repository list (<c>UserRepositoriesList</c> and <c>UserRepositoriesListController</c>): the recent
///  repositories in one group and the favourites grouped by category, filtered by the search text, with upstream's
///  category and removal actions. Holds no UI types; create and use it on the UI thread.
/// </summary>
public sealed class DashboardViewModel : ObservableObject
{
    /// <summary>
    ///  Upstream's <c>_groupRecentRepositories</c>.
    /// </summary>
    public const string RecentGroupHeader = "Recent repositories";

    private readonly IRecentRepositoryStore _store;
    private readonly Func<string, DashboardRepositoryState> _readState;
    private readonly Func<RecentRepoSplitter> _splitter;
    private readonly Dictionary<string, DashboardRepositoryState> _states = new(StringComparer.Ordinal);
    private RepositoryHistory _history = new([], []);
    private IReadOnlyList<DashboardGroup> _groups = [];
    private string _searchText = "";
    private int _loadVersion;

    /// <param name="readState">Whether a folder is a repository and its checked out branch, read off the UI thread after
    ///  the list shows (upstream's <c>IsValidGitWorkingDir</c> and <c>GetCurrentBranchName</c>).</param>
    /// <param name="splitter">Upstream's splitter with the recent repositories settings; upstream's settings when null.</param>
    public DashboardViewModel(IRecentRepositoryStore store, Func<string, DashboardRepositoryState> readState,
        Func<RecentRepoSplitter>? splitter = null)
    {
        _store = store;
        _readState = readState;
        _splitter = splitter ?? (() => new RecentRepoSplitter());
    }

    public IReadOnlyList<DashboardGroup> Groups
    {
        get => _groups;
        private set => SetProperty(ref _groups, value);
    }

    /// <summary>
    ///  Upstream's search box: shows the repositories whose path contains the text, ignoring case.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                Rebuild();
            }
        }
    }

    /// <summary>
    ///  Upstream's <c>_hasInvalidRepos</c>: some shown folder is no longer a repository, so "Remove missing projects" shows.
    /// </summary>
    public bool HasInvalid => _groups.SelectMany(group => group.Repositories).Any(repository => !repository.IsValid);

    /// <summary>
    ///  Upstream's <c>GetCategories</c>: the categories of the shown repositories, in order.
    /// </summary>
    public IReadOnlyList<string> Categories
        =>
        [
            .. _groups.SelectMany(group => group.Repositories)
                .Select(repository => repository.Category)
                .OfType<string>()
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Order(StringComparer.CurrentCulture)
                .Distinct(StringComparer.CurrentCulture)
        ];

    /// <summary>
    ///  The read of the shown folders' states; the list shows first.
    /// </summary>
    public Task StatesRead { get; private set; } = Task.CompletedTask;

    public async Task LoadAsync()
    {
        int version = ++_loadVersion;
        RepositoryHistory history = await _store.LoadHistoryAsync();
        if (version != _loadVersion)
        {
            return;
        }

        _history = history;
        _states.Clear();
        Rebuild();
        StatesRead = ReadStatesAsync(version);
        await StatesRead;
    }

    public async Task AssignCategoryAsync(DashboardRepository repository, string? category)
    {
        await _store.AssignCategoryAsync(repository.Path, category);
        await LoadAsync();
    }

    /// <summary>
    ///  Upstream's "Remove project from the list": a favourite leaves the favourites, another the recent list.
    /// </summary>
    public async Task RemoveAsync(DashboardRepository repository)
    {
        if (repository.IsFavourite)
        {
            await _store.RemoveFavouriteAsync(repository.Path);
        }
        else
        {
            await _store.RemoveAsync(repository.Path);
        }

        await LoadAsync();
    }

    /// <summary>
    ///  Upstream's "Remove missing projects from the list".
    /// </summary>
    public async Task RemoveMissingAsync()
    {
        await _store.RemoveInvalidAsync(path => _states.TryGetValue(path, out DashboardRepositoryState state)
            ? state.IsValid
            : _readState(path).IsValid);
        await LoadAsync();
    }

    /// <summary>
    ///  Upstream's <c>UpdateCategoryName</c>: the shown repositories of <paramref name="category"/> move to
    ///  <paramref name="newName"/>; a null name deletes the category, and its repositories stop being favourites.
    /// </summary>
    public async Task RenameCategoryAsync(string category, string? newName)
    {
        foreach (DashboardRepository repository in _groups.SelectMany(group => group.Repositories)
                     .Where(repository => repository.Category == category))
        {
            await _store.AssignCategoryAsync(repository.Path, newName);
        }

        await LoadAsync();
    }

    /// <summary>
    ///  Upstream's "Clear all recent repositories": every shown repository leaves the recent list.
    /// </summary>
    public async Task ClearRecentAsync()
    {
        foreach (string path in _groups.SelectMany(group => group.Repositories).Select(repository => repository.Path)
                     .Distinct(StringComparer.Ordinal))
        {
            await _store.RemoveAsync(path);
        }

        await LoadAsync();
    }

    /// <summary>
    ///  The number of shown repositories in <paramref name="category"/>, for upstream's delete question.
    /// </summary>
    public int CountIn(string category)
        => _groups.Where(group => group.Category == category).Sum(group => group.Repositories.Count);

    // Upstream's ShowRecentRepositories: the recent group first, then the categories in order; recent repositories in the
    // recent group, favourites in their category's.
    private void Rebuild()
    {
        IReadOnlyList<DashboardRepository> recent = Split(_history.Recent, isFavourite: false);
        IReadOnlyList<DashboardRepository> favourites = Split(_history.Favourites, isFavourite: true);
        List<DashboardGroup> groups = [];
        if (recent.Count > 0)
        {
            groups.Add(new DashboardGroup(RecentGroupHeader, null, recent));
        }

        IEnumerable<string> categories = recent.Concat(favourites)
            .Select(repository => repository.Category)
            .OfType<string>()
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.CurrentCulture)
            .Order(StringComparer.Ordinal);
        foreach (string category in categories)
        {
            List<DashboardRepository> members =
            [
                .. favourites.Where(repository => StringComparer.CurrentCulture.Equals(repository.Category, category))
            ];
            if (members.Count > 0)
            {
                groups.Add(new DashboardGroup(category, category, members));
            }
        }

        Groups = groups;
        RaisePropertyChanged(nameof(HasInvalid));
    }

    // Upstream's PreRenderRepositories: filtered by path, then the splitter's top and other repositories with its captions.
    private IReadOnlyList<DashboardRepository> Split(IReadOnlyList<RepositoryEntry> entries, bool isFavourite)
    {
        List<HistoryEntry> repositories =
        [
            .. entries.Where(entry => _searchText.Length == 0
                                      || entry.Path.Contains(_searchText, StringComparison.CurrentCultureIgnoreCase))
                .Select(entry => new HistoryEntry(entry.Path) { Category = entry.Category })
        ];
        List<RecentRepoInfo> top = [];
        List<RecentRepoInfo> rest = [];
        _splitter().SplitRecentRepos(repositories, top, rest);
        return
        [
            .. top.Union(rest).Select(info =>
            {
                DashboardRepository repository = new(info.Repo.Path, info.Caption ?? info.Repo.Path, info.Repo.Category,
                    isFavourite);
                return _states.TryGetValue(info.Repo.Path, out DashboardRepositoryState state)
                    ? repository with { IsValid = state.IsValid, Branch = state.Branch }
                    : repository;
            })
        ];
    }

    private async Task ReadStatesAsync(int version)
    {
        List<string> paths =
            [.. _history.Recent.Concat(_history.Favourites).Select(entry => entry.Path).Distinct(StringComparer.Ordinal)];
        if (paths.Count == 0)
        {
            return;
        }

        Dictionary<string, DashboardRepositoryState> states =
            await Task.Run(() => paths.ToDictionary(path => path, _readState, StringComparer.Ordinal));
        if (version != _loadVersion)
        {
            return;
        }

        foreach ((string path, DashboardRepositoryState state) in states)
        {
            _states[path] = state;
        }

        Rebuild();
    }
}
