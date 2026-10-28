using GitCommands;
using GitCommands.UserRepositoryHistory;
using HistoryEntry = GitCommands.UserRepositoryHistory.Repository;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The list of recently opened local repositories, newest first. Implementations run the work off the calling thread.
/// </summary>
public interface IRecentRepositoryStore
{
    Task<IReadOnlyList<string>> LoadAsync();

    /// <summary>
    ///  Moves <paramref name="repositoryPath"/> to the top of the list, adding it when it is not there.
    /// </summary>
    /// <returns>The list after the change.</returns>
    Task<IReadOnlyList<string>> AddAsync(string repositoryPath);

    /// <returns>The list after the change.</returns>
    Task<IReadOnlyList<string>> RemoveAsync(string repositoryPath);

    /// <summary>
    ///  Upstream's recent and favourite repositories with their categories: a favourite is a repository the user put in a
    ///  category.
    /// </summary>
    Task<RepositoryHistory> LoadHistoryAsync();

    /// <summary>
    ///  Upstream's <c>AssignCategoryAsync</c>: puts the repository in <paramref name="category"/>, making it a favourite, or
    ///  takes it out of the favourites when <paramref name="category"/> is empty.
    /// </summary>
    Task AssignCategoryAsync(string repositoryPath, string? category);

    Task RemoveFavouriteAsync(string repositoryPath);

    /// <summary>
    ///  Upstream's <c>RemoveInvalidRepositoriesAsync</c>: drops the recent and favourite entries that
    ///  <paramref name="isValid"/> rejects.
    /// </summary>
    Task RemoveInvalidAsync(Func<string, bool> isValid);
}

/// <summary>
///  Keeps the list in the upstream settings file, under the key and in the format the WinForms app uses
///  (<see cref="RepositoryHistoryManager.Locals"/>), so both apps read the same history and the fork adds no format.
/// </summary>
public sealed class SettingsRecentRepositoryStore : IRecentRepositoryStore
{
    public async Task<IReadOnlyList<string>> LoadAsync()
        => ToPaths(await RepositoryHistoryManager.Locals.LoadRecentHistoryAsync());

    public async Task<IReadOnlyList<string>> AddAsync(string repositoryPath)
        => ToPaths(await RepositoryHistoryManager.Locals.AddAsMostRecentAsync(repositoryPath));

    public async Task<IReadOnlyList<string>> RemoveAsync(string repositoryPath)
    {
        // Upstream stores paths with a trailing separator; the caller may pass either form.
        IList<HistoryEntry> history = await RepositoryHistoryManager.Locals.LoadRecentHistoryAsync();
        string? stored = history.Select(repository => repository.Path)
            .FirstOrDefault(path => RecentRepositoryPaths.Same(path, repositoryPath));
        return stored is null
            ? ToPaths(history)
            : ToPaths(await RepositoryHistoryManager.Locals.RemoveRecentAsync(stored));
    }

    public async Task<RepositoryHistory> LoadHistoryAsync()
        => new(ToEntries(await RepositoryHistoryManager.Locals.LoadRecentHistoryAsync()),
            ToEntries(await RepositoryHistoryManager.Locals.LoadFavouriteHistoryAsync()));

    public async Task AssignCategoryAsync(string repositoryPath, string? category)
    {
        // Upstream keeps the favourite's entry as the dashboard passed it, so an existing entry keeps its stored path.
        IList<HistoryEntry> favourites = await RepositoryHistoryManager.Locals.LoadFavouriteHistoryAsync();
        HistoryEntry entry = favourites.FirstOrDefault(favourite => RecentRepositoryPaths.Same(favourite.Path, repositoryPath))
                             ?? new HistoryEntry(repositoryPath);
        await RepositoryHistoryManager.Locals.AssignCategoryAsync(entry, category);
    }

    public async Task RemoveFavouriteAsync(string repositoryPath)
    {
        IList<HistoryEntry> favourites = await RepositoryHistoryManager.Locals.LoadFavouriteHistoryAsync();
        if (favourites.FirstOrDefault(favourite => RecentRepositoryPaths.Same(favourite.Path, repositoryPath)) is { } stored)
        {
            await RepositoryHistoryManager.Locals.RemoveFavouriteAsync(stored.Path);
        }
    }

    public Task RemoveInvalidAsync(Func<string, bool> isValid)
        => RepositoryHistoryManager.Locals.RemoveInvalidRepositoriesAsync(isValid);

    private static IReadOnlyList<RepositoryEntry> ToEntries(IEnumerable<HistoryEntry> history)
        => [.. history.Select(repository => new RepositoryEntry(repository.Path, repository.Category, repository.Anchor))];

    private static IReadOnlyList<string> ToPaths(IEnumerable<HistoryEntry> history) =>
        [.. history.Select(repository => repository.Path)];
}

/// <summary>
///  Keeps the list for the life of the process only. Tests use it so they never write the user's settings file.
/// </summary>
public sealed class InMemoryRecentRepositoryStore : IRecentRepositoryStore
{
    private readonly List<string> _paths = [];
    private readonly List<RepositoryEntry> _favourites = [];

    public Task<IReadOnlyList<string>> LoadAsync() => Task.FromResult(Snapshot());

    public Task<IReadOnlyList<string>> AddAsync(string repositoryPath)
    {
        _paths.RemoveAll(path => RecentRepositoryPaths.Same(path, repositoryPath));
        _paths.Insert(0, repositoryPath);
        return Task.FromResult(Snapshot());
    }

    public Task<IReadOnlyList<string>> RemoveAsync(string repositoryPath)
    {
        _paths.RemoveAll(path => RecentRepositoryPaths.Same(path, repositoryPath));
        return Task.FromResult(Snapshot());
    }

    public Task<RepositoryHistory> LoadHistoryAsync()
        => Task.FromResult(new RepositoryHistory([.. _paths.Select(path => new RepositoryEntry(path))], [.. _favourites]));

    public Task AssignCategoryAsync(string repositoryPath, string? category)
    {
        int index = _favourites.FindIndex(favourite => RecentRepositoryPaths.Same(favourite.Path, repositoryPath));
        if (string.IsNullOrWhiteSpace(category))
        {
            if (index >= 0)
            {
                _favourites.RemoveAt(index);
            }
        }
        else if (index >= 0)
        {
            _favourites[index] = _favourites[index] with { Category = category };
        }
        else
        {
            _favourites.Add(new RepositoryEntry(repositoryPath, category));
        }

        return Task.CompletedTask;
    }

    public Task RemoveFavouriteAsync(string repositoryPath)
    {
        _favourites.RemoveAll(favourite => RecentRepositoryPaths.Same(favourite.Path, repositoryPath));
        return Task.CompletedTask;
    }

    public Task RemoveInvalidAsync(Func<string, bool> isValid)
    {
        _paths.RemoveAll(path => !isValid(path));
        _favourites.RemoveAll(favourite => !isValid(favourite.Path));
        return Task.CompletedTask;
    }

    private IReadOnlyList<string> Snapshot() => [.. _paths];
}

/// <summary>
///  Path comparison for the recent list, which holds paths as the user's OS writes them.
/// </summary>
public static class RecentRepositoryPaths
{
    /// <summary>
    ///  True when both paths name the same folder, ignoring a trailing separator. Case is ignored only on Windows, where
    ///  the file system ignores it.
    /// </summary>
    public static bool Same(string left, string right)
        => string.Equals(Trim(left), Trim(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    ///  The folder name shown for a recent entry, e.g. "gitextensions" for "/home/me/src/gitextensions/".
    /// </summary>
    public static string DisplayName(string path)
    {
        string trimmed = Trim(path);
        string name = Path.GetFileName(trimmed);
        return name.Length == 0 ? trimmed : name;
    }

    /// <summary>
    ///  The working directory button's caption, as upstream's <c>WorkingDirectoryToolStripSplitButton</c> sets it: the
    ///  caption upstream's <see cref="RecentRepoSplitter"/> gives the path among the recent repositories (shortened as the
    ///  user's <c>ShorteningRecentRepoPathStrategy</c> asks, the full path by default), with the home folder as "~".
    /// </summary>
    /// <param name="path">The open repository.</param>
    /// <param name="recentPaths">The recent repositories, newest first.</param>
    /// <param name="splitter">The splitter with the shortening settings; upstream's settings when null.</param>
    public static string Caption(string path, IReadOnlyList<string> recentPaths, RecentRepoSplitter? splitter = null)
    {
        // Upstream's working directory always ends with a separator, and it adds the open repository to the history first,
        // so it is always among them.
        string workingDir = path.EnsureTrailingPathSeparator() ?? path;
        List<HistoryEntry> history = [new HistoryEntry(workingDir)];
        history.AddRange(recentPaths.Where(recent => !Same(recent, path)).Select(recent => new HistoryEntry(recent)));

        List<RecentRepoInfo> repositories = [];
        (splitter ?? new RecentRepoSplitter()).SplitRecentRepos(history, repositories, repositories);
        RecentRepoInfo? shown = repositories.Find(repository => Same(repository.Repo.Path, path));
        return PathUtil.GetDisplayPath(shown?.Caption ?? workingDir);
    }

    private static string Trim(string path) => path.Length > 1 ? path.TrimEnd('/', '\\') : path;
}

/// <summary>
///  Upstream's <c>RepositoryCurrentBranchNameProvider</c> for the dashboard and the recent repositories menu.
/// </summary>
public static class RecentRepositoryBranches
{
    /// <summary>
    ///  The checked out branch, as upstream's <c>Commands.GetSelectedBranch</c> reads it (mostly from the HEAD file, without
    ///  running git); null for a folder that is gone, not a repository or bare, as upstream shows no branch then.
    /// </summary>
    public static string? CurrentBranch(string path)
    {
        try
        {
            if (!GitCommands.GitModule.IsValidGitWorkingDir(path) || GitCommands.GitModule.IsBareRepository(path))
            {
                return null;
            }

            string branch = GitCommands.Git.Commands.GetSelectedBranch(
                new GitCommands.GitExecutorProvider(new GitCommands.Git.GitDirectoryResolver()).GetExecutor(path));
            return branch.Length == 0 ? null : branch;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }
}

/// <summary>
///  An entry of upstream's repository history: the path as stored, for a favourite its category, and where the user
///  pinned it (upstream's <c>Anchor</c>).
/// </summary>
public sealed record RepositoryEntry(string Path, string? Category = null,
    HistoryEntry.RepositoryAnchor Anchor = HistoryEntry.RepositoryAnchor.None);

/// <summary>
///  Upstream's recent repositories, newest first, and its favourites, in the order stored.
/// </summary>
public sealed record RepositoryHistory(IReadOnlyList<RepositoryEntry> Recent, IReadOnlyList<RepositoryEntry> Favourites);
