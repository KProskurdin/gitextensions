namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  A recently opened repository as the Start menu and the dashboard show it.
/// </summary>
public sealed record RecentRepository(string Path, string? Branch = null)
{
    public string Name => RecentRepositoryPaths.DisplayName(Path);

    public string Display => Branch is null ? $"{Name}  ({Path})" : $"{Name} [{Branch}]  ({Path})";

    /// <summary>
    ///  Whether <see cref="Branch"/> is known, for the dashboard to show it.
    /// </summary>
    public bool HasBranch => Branch is not null;
}

/// <summary>
///  The recent repositories list for a view. Holds no UI types. Create and use it on the UI thread.
/// </summary>
/// <remarks>
///  The list is a convenience: a failure to read or write it never stops a repository from opening, so errors are kept in
///  <see cref="ErrorMessage"/> and not thrown.
/// </remarks>
public sealed class RecentRepositoriesViewModel : ObservableObject
{
    private readonly IRecentRepositoryStore _store;
    private readonly Func<string, string?>? _currentBranch;
    private IReadOnlyList<RecentRepository> _items = [];
    private string? _errorMessage;

    /// <param name="currentBranch">The checked out branch of a repository (upstream's <c>ShowRepoCurrentBranch</c>), read
    ///  off the UI thread after the list shows; null, or a null result, shows no branch.</param>
    public RecentRepositoriesViewModel(IRecentRepositoryStore store, Func<string, string?>? currentBranch = null)
    {
        _store = store;
        _currentBranch = currentBranch;
    }

    public IReadOnlyList<RecentRepository> Items
    {
        get => _items;
        private set => SetProperty(ref _items, value);
    }

    /// <summary>
    ///  The last failure to read or write the list, or null.
    /// </summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public Task LoadAsync() => UpdateAsync(_store.LoadAsync);

    /// <summary>
    ///  Records that <paramref name="repositoryPath"/> was opened, moving it to the top.
    /// </summary>
    public Task AddAsync(string repositoryPath) => UpdateAsync(() => _store.AddAsync(repositoryPath));

    /// <summary>
    ///  Drops an entry, e.g. one whose folder no longer exists.
    /// </summary>
    public Task RemoveAsync(string repositoryPath) => UpdateAsync(() => _store.RemoveAsync(repositoryPath));

    /// <summary>
    ///  The read of the branches of the list shown last; the list shows first, so opening a repository does not wait for it.
    /// </summary>
    public Task BranchesRead { get; private set; } = Task.CompletedTask;

    private async Task ReadBranchesAsync(IReadOnlyList<RecentRepository> items)
    {
        if (_currentBranch is not { } currentBranch || items.Count == 0)
        {
            return;
        }

        IReadOnlyList<RecentRepository> withBranches = await Task.Run(() =>
            (IReadOnlyList<RecentRepository>)[.. items.Select(item => item with { Branch = currentBranch(item.Path) })]);

        // A list read again meanwhile is newer.
        if (ReferenceEquals(Items, items))
        {
            Items = withBranches;
        }
    }

    private async Task UpdateAsync(Func<Task<IReadOnlyList<string>>> update)
    {
        try
        {
            IReadOnlyList<string> paths = await update();
            IReadOnlyList<RecentRepository> items = [.. paths.Select(path => new RecentRepository(path))];
            Items = items;
            ErrorMessage = null;
            BranchesRead = ReadBranchesAsync(items);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
