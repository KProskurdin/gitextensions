using GitCommands.UserRepositoryHistory;
using UserRepository = GitCommands.UserRepositoryHistory.Repository;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The URLs of remotes used before, which upstream's remotes, clone and pull dialogs offer (upstream's
///  <c>RepositoryHistoryManager.Remotes</c>).
/// </summary>
public interface IRemoteUrlHistory
{
    Task<IList<UserRepository>> LoadAsync();

    Task SaveAsync(IList<UserRepository> history);
}

/// <summary>
///  Upstream's remote history, in its settings, shared with the WinForms app.
/// </summary>
public sealed class UpstreamRemoteUrlHistory : IRemoteUrlHistory
{
    public Task<IList<UserRepository>> LoadAsync() => RepositoryHistoryManager.Remotes.LoadRecentHistoryAsync();

    public Task SaveAsync(IList<UserRepository> history) => RepositoryHistoryManager.Remotes.SaveRecentHistoryAsync(history);
}

/// <summary>
///  A remote history kept in memory, for tests.
/// </summary>
public sealed class InMemoryRemoteUrlHistory : IRemoteUrlHistory
{
    public List<UserRepository> Entries { get; } = [];

    public Task<IList<UserRepository>> LoadAsync() => Task.FromResult<IList<UserRepository>>([.. Entries]);

    public Task SaveAsync(IList<UserRepository> history)
    {
        Entries.Clear();
        Entries.AddRange(history);
        return Task.CompletedTask;
    }
}
