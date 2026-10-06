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
    private bool _isMerging;
    private bool _isRebasing;
    private IReadOnlyList<string> _remotes = [];
    private string? _trackingRemote;
    private SyncStatus? _sync;
    private IReadOnlyList<StashInfo> _stashes = [];
    private IReadOnlyList<string> _tags = [];
    private IReadOnlyList<SubmoduleInfo> _submodules = [];
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

    /// <summary>
    ///  True while a merge is stopped for conflicts or for a commit (MERGE_HEAD exists).
    /// </summary>
    public bool IsMerging
    {
        get => _isMerging;
        private set => SetProperty(ref _isMerging, value);
    }

    public IReadOnlyList<StashInfo> Stashes
    {
        get => _stashes;
        private set => SetProperty(ref _stashes, value);
    }

    /// <summary>
    ///  Ahead and behind counts against the upstream branch, or null when the branch has no upstream.
    /// </summary>
    public SyncStatus? Sync
    {
        get => _sync;
        private set => SetProperty(ref _sync, value);
    }

    /// <summary>
    ///  The remote the checked-out branch is configured to track, or null when it has none.
    /// </summary>
    public string? TrackingRemote
    {
        get => _trackingRemote;
        private set => SetProperty(ref _trackingRemote, value);
    }

    public IReadOnlyList<string> Remotes
    {
        get => _remotes;
        private set => SetProperty(ref _remotes, value);
    }

    public IReadOnlyList<SubmoduleInfo> Submodules
    {
        get => _submodules;
        private set => SetProperty(ref _submodules, value);
    }

    public IReadOnlyList<string> Tags
    {
        get => _tags;
        private set => SetProperty(ref _tags, value);
    }

    /// <summary>
    ///  True while a rebase is stopped for conflicts or for a commit (rebase-merge or rebase-apply exists).
    /// </summary>
    public bool IsRebasing
    {
        get => _isRebasing;
        private set => SetProperty(ref _isRebasing, value);
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

    /// <summary>
    ///  Forgets the repository's state, e.g. when it is closed. A refresh still in flight is ignored when it completes.
    /// </summary>
    public void Clear()
    {
        _refreshVersion++;
        ClearState();
        IsMerging = false;
        IsRebasing = false;
        IsLoading = false;
    }

    private void ClearState()
    {
        CurrentBranch = "";
        Branches = [];
        Changes = [];
        Stashes = [];
        Tags = [];
        Remotes = [];
        Sync = null;
        TrackingRemote = null;
        Submodules = [];
    }

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
            IsMerging = snapshot.IsMerging;
            IsRebasing = snapshot.IsRebasing;
            Stashes = snapshot.Stashes;
            Tags = snapshot.Tags;
            Remotes = snapshot.Remotes ?? [];
            Sync = snapshot.Sync;
            TrackingRemote = snapshot.TrackingRemote;
            Submodules = snapshot.Submodules ?? [];
        }
        catch (Exception ex)
        {
            if (version == _refreshVersion)
            {
                ClearState();
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
