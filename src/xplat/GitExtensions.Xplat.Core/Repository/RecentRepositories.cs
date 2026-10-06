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
        return stored is null ? ToPaths(history) : ToPaths(await RepositoryHistoryManager.Locals.RemoveRecentAsync(stored));
    }

    private static IReadOnlyList<string> ToPaths(IEnumerable<HistoryEntry> history) => [.. history.Select(repository => repository.Path)];
}

/// <summary>
///  Keeps the list for the life of the process only. Tests use it so they never write the user's settings file.
/// </summary>
public sealed class InMemoryRecentRepositoryStore : IRecentRepositoryStore
{
    private readonly List<string> _paths = [];

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
        => string.Equals(Trim(left), Trim(right), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    ///  The folder name shown for a recent entry, e.g. "gitextensions" for "/home/me/src/gitextensions/".
    /// </summary>
    public static string DisplayName(string path)
    {
        string trimmed = Trim(path);
        string name = Path.GetFileName(trimmed);
        return name.Length == 0 ? trimmed : name;
    }

    private static string Trim(string path) => path.Length > 1 ? path.TrimEnd('/', '\\') : path;
}
