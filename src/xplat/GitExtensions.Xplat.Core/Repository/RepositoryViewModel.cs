namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Branches and changed files of the open repository. Holds no UI types. Create and use it on the UI thread.
/// </summary>
public sealed class RepositoryViewModel : ObservableObject
{
    private readonly IRepositoryService _service;
    private int _refreshVersion;
    private string _currentBranch = "";
    private IReadOnlyList<BranchInfo> _branches = [];
    private IReadOnlyList<FileChange> _changes = [];
    private bool _isLoading;
    private string? _errorMessage;

    public RepositoryViewModel(IRepositoryService service)
    {
        _service = service;
    }

    /// <summary>
    ///  The checked-out branch, or empty when HEAD is detached.
    /// </summary>
    public string CurrentBranch
    {
        get => _currentBranch;
        private set => SetProperty(ref _currentBranch, value);
    }

    public IReadOnlyList<BranchInfo> Branches
    {
        get => _branches;
        private set => SetProperty(ref _branches, value);
    }

    public IReadOnlyList<FileChange> Changes
    {
        get => _changes;
        private set => SetProperty(ref _changes, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    ///  Set when the repository cannot be read. A view shows it and then calls <see cref="ClearError"/>.
    /// </summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public void ClearError() => ErrorMessage = null;

    public async Task RefreshAsync(string repositoryPath)
    {
        int version = ++_refreshVersion;
        IsLoading = true;
        try
        {
            RepositorySnapshot snapshot = await _service.GetSnapshotAsync(repositoryPath);
            if (version != _refreshVersion)
            {
                return;
            }

            CurrentBranch = snapshot.CurrentBranch ?? "";
            Branches = snapshot.Branches;
            Changes = snapshot.Changes;
        }
        catch (Exception ex)
        {
            if (version == _refreshVersion)
            {
                CurrentBranch = "";
                Branches = [];
                Changes = [];
                ErrorMessage = ex.Message;
            }
        }
        finally
        {
            if (version == _refreshVersion)
            {
                IsLoading = false;
            }
        }
    }
}
