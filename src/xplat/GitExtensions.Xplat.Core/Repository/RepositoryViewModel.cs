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
    private bool _isApplyingPatch;
    private bool _isBisecting;
    private IReadOnlyList<string> _remotes = [];
    private IReadOnlyList<string> _inactiveRemotes = [];
    private string? _trackingRemote;
    private SyncStatus? _sync;
    private IReadOnlyList<StashInfo> _stashes = [];
    private IReadOnlyList<string> _tags = [];
    private IReadOnlyList<SubmoduleInfo> _submodules = [];
    private IReadOnlyDictionary<string, string> _aheadBehind = new Dictionary<string, string>();
    private IReadOnlyList<WorktreeInfo> _worktrees = [];
    private IReadOnlyDictionary<string, string> _remoteUrls = new Dictionary<string, string>();
    private IReadOnlyDictionary<string, System.Drawing.Color> _remoteColors = new Dictionary<string, System.Drawing.Color>();
    private string _userEmail = "";
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

    /// <summary>
    ///  The remotes upstream's <c>FormRemotes</c> deactivated (see <see cref="RepositorySnapshot.InactiveRemotes"/>).
    /// </summary>
    public IReadOnlyList<string> InactiveRemotes
    {
        get => _inactiveRemotes;
        private set => SetProperty(ref _inactiveRemotes, value);
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
    ///  Upstream's ahead and behind texts of the left panel's branches, by branch name (see
    ///  <see cref="RepositorySnapshot.AheadBehind"/>).
    /// </summary>
    public IReadOnlyDictionary<string, string> AheadBehind
    {
        get => _aheadBehind;
        private set => SetProperty(ref _aheadBehind, value);
    }

    public IReadOnlyList<WorktreeInfo> Worktrees
    {
        get => _worktrees;
        private set => SetProperty(ref _worktrees, value);
    }

    /// <summary>
    ///  The fetch URL of each remote, by its name.
    /// </summary>
    public IReadOnlyDictionary<string, string> RemoteUrls
    {
        get => _remoteUrls;
        private set => SetProperty(ref _remoteUrls, value);
    }

    /// <summary>
    ///  The colors set on remotes, by remote name (see <see cref="RepositorySnapshot.RemoteColors"/>).
    /// </summary>
    public IReadOnlyDictionary<string, System.Drawing.Color> RemoteColors
    {
        get => _remoteColors;
        private set => SetProperty(ref _remoteColors, value);
    }

    /// <summary>
    ///  The effective user.email, whose commits the grid highlights until a commit is selected (upstream's
    ///  <c>AuthorRevisionHighlighting</c>).
    /// </summary>
    public string UserEmail
    {
        get => _userEmail;
        private set => SetProperty(ref _userEmail, value);
    }

    /// <summary>
    ///  True while a rebase is stopped for conflicts or for a commit (upstream's <c>InTheMiddleOfRebase</c>).
    /// </summary>
    public bool IsRebasing
    {
        get => _isRebasing;
        private set => SetProperty(ref _isRebasing, value);
    }

    /// <summary>
    ///  True while a patch series applied with <c>git am</c> is stopped (rebase-apply without a rebase).
    /// </summary>
    public bool IsApplyingPatch
    {
        get => _isApplyingPatch;
        private set => SetProperty(ref _isApplyingPatch, value);
    }

    /// <summary>
    ///  True between <c>git bisect start</c> and <c>git bisect reset</c> (BISECT_START exists).
    /// </summary>
    public bool IsBisecting
    {
        get => _isBisecting;
        private set => SetProperty(ref _isBisecting, value);
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
        IsApplyingPatch = false;
        IsBisecting = false;
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
        InactiveRemotes = [];
        Sync = null;
        TrackingRemote = null;
        Submodules = [];
        AheadBehind = new Dictionary<string, string>();
        Worktrees = [];
        RemoteUrls = new Dictionary<string, string>();
        RemoteColors = new Dictionary<string, System.Drawing.Color>();
        UserEmail = "";
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
            IsApplyingPatch = snapshot.IsApplyingPatch;
            IsBisecting = snapshot.IsBisecting;
            Stashes = snapshot.Stashes;
            Tags = snapshot.Tags;
            Remotes = snapshot.Remotes ?? [];
            InactiveRemotes = snapshot.InactiveRemotes ?? [];
            Sync = snapshot.Sync;
            TrackingRemote = snapshot.TrackingRemote;
            Submodules = snapshot.Submodules ?? [];
            AheadBehind = snapshot.AheadBehind ?? new Dictionary<string, string>();
            Worktrees = snapshot.Worktrees ?? [];
            RemoteUrls = snapshot.RemoteUrls ?? new Dictionary<string, string>();
            RemoteColors = snapshot.RemoteColors ?? new Dictionary<string, System.Drawing.Color>();
            UserEmail = snapshot.UserEmail ?? "";
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
