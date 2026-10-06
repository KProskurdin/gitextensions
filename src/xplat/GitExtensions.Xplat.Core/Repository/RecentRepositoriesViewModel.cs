namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  A recently opened repository as the Start menu and the dashboard show it.
/// </summary>
public sealed record RecentRepository(string Path)
{
    public string Name => RecentRepositoryPaths.DisplayName(Path);

    public string Display => $"{Name}  ({Path})";
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
    private IReadOnlyList<RecentRepository> _items = [];
    private string? _errorMessage;

    public RecentRepositoriesViewModel(IRecentRepositoryStore store)
    {
        _store = store;
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

    private async Task UpdateAsync(Func<Task<IReadOnlyList<string>>> update)
    {
        try
        {
            IReadOnlyList<string> paths = await update();
            Items = [.. paths.Select(path => new RecentRepository(path))];
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
