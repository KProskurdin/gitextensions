using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Diff;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  A check item of upstream's settings menu of the commit window's working directory list.
/// </summary>
public enum CommitListSetting
{
    ShowIgnored,
    ShowSkipWorktree,
    ShowAssumeUnchanged,
    ShowUntracked,
}

// Upstream FormCommit's two file lists: what they list (the settings menu of the working directory's list), their grouping,
// the toolbar between them and the buttons over them. The view shows the files in its trees, filters them with its filter
// boxes and passes back what it shows (SetShown).
public sealed partial class CommitViewModel
{
    // Upstream FormCommit's tooltips of the stage and unstage all buttons.
    public const string StageAllText = "Stage all";
    public const string StageFilteredText = "Stage filtered";
    public const string UnstageAllText = "Unstage all";
    public const string UnstageFilteredText = "Unstage filtered";

    // Git's setting of upstream's initial "Show untracked files", and its value that turns them off.
    private const string ShowUntrackedFilesSetting = "status.showuntrackedfiles";
    private const string NoUntrackedFiles = "no";

    // The changes read with the working directory list's settings while they ask for more than the repository's, and the
    // version of the read that may set them.
    private IReadOnlyList<FileChange>? _flaggedChanges;
    private int _flaggedChangesVersion;

    // The files a filtered list shows; null while the list is not filtered.
    private IReadOnlyList<FileChange>? _unstagedShown;
    private IReadOnlyList<FileChange>? _stagedShown;

    /// <summary>
    ///  Raised when stage all or unstage all took the files a filtered list shows: the list (true for the staged one)
    ///  loses its filter, as upstream's.
    /// </summary>
    public event EventHandler<bool>? FilterClearRequested;

    /// <summary>
    ///  The grouping of both lists, one setting as upstream's.
    /// </summary>
    public FileListGroupingViewModel Grouping { get; }

    /// <summary>
    ///  The changes the lists show (before "Show untracked files" and the filters): the repository's, or the working
    ///  directory's read with git's flags while the settings menu asks for ignored, skip-worktree or assumed-unchanged
    ///  files.
    /// </summary>
    public IReadOnlyList<FileChange> ListedChanges => _flaggedChanges ?? _repository.Changes;

    [ObservableProperty]
    public partial bool ShowIgnored { get; private set; }

    [ObservableProperty]
    public partial bool ShowSkipWorktree { get; private set; }

    [ObservableProperty]
    public partial bool ShowAssumeUnchanged { get; private set; }

    /// <summary>
    ///  Upstream's "Show untracked files" of the working directory's list; at first as git's
    ///  <c>status.showuntrackedfiles</c> says.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowUntracked { get; private set; } = true;

    /// <summary>
    ///  Upstream's Unstaged_FilterChanged: the stage all button's image, its filtered one while the list is filtered.
    /// </summary>
    public string StageAllImage => _unstagedShown is null ? "StageAll" : "StageAllFiltered";

    public string StageAllTip => Host.Translate(_unstagedShown is null ? StageAllText : StageFilteredText);

    public string UnstageAllImage => _stagedShown is null ? "UnstageAll" : "UnstageAllFiltered";

    public string UnstageAllTip => Host.Translate(_stagedShown is null ? UnstageAllText : UnstageFilteredText);

    /// <summary>
    ///  What a list shows after the view filtered it: <paramref name="shown"/> while <paramref name="filtered"/>, so stage
    ///  all and unstage all take only those (upstream's StageAllAccordingToFilter).
    /// </summary>
    public void SetShown(bool staged, IReadOnlyList<FileChange> shown, bool filtered)
    {
        if (staged)
        {
            _stagedShown = filtered ? shown : null;
            OnPropertyChanged(nameof(UnstageAllImage));
            OnPropertyChanged(nameof(UnstageAllTip));
        }
        else
        {
            _unstagedShown = filtered ? shown : null;
            OnPropertyChanged(nameof(StageAllImage));
            OnPropertyChanged(nameof(StageAllTip));
        }
    }

    // Upstream's BindContextMenu: "Show untracked files" starts as git says.
    private async Task ReadShowUntrackedAsync()
    {
        if (_services.ReadGitSetting is { } read)
        {
            ShowUntracked = await read(ShowUntrackedFilesSetting) != NoUntrackedFiles;
        }
    }

    // Upstream's StageAllAccordingToFilter and UnstageAllFiles: a filtered list stages or unstages the files it shows and
    // loses its filter.
    [RelayCommand]
    private Task StageAllFilesAsync(bool staged)
    {
        IReadOnlyList<FileChange>? shown = staged ? _stagedShown : _unstagedShown;
        if (shown is not null)
        {
            FilterClearRequested?.Invoke(this, staged);
        }

        return StageAllAsync(staged, shown);
    }

    // Upstream's RescanChanges (the refresh button and its hotkey).
    [RelayCommand]
    private Task RefreshAsync() => GuardAsync(_refresh);

    // Upstream's SolveMergeconflicts button.
    [RelayCommand]
    private Task SolveConflictsAsync() => _services.ShowConflicts is { } show ? GuardAsync(show) : Task.CompletedTask;

    // Upstream's editIgnoredFiles and editLocallyIgnoredFiles items: the lists are read again after a save.
    [RelayCommand]
    private Task EditIgnoredFilesAsync(bool localExclude) => GuardAsync(async () =>
    {
        if (_services.EditIgnoredFiles is { } edit && await edit(localExclude))
        {
            await _refresh();
        }
    });

    // The working directory list's settings menu; the files are read again for the flags.
    [RelayCommand]
    private void ToggleListSetting(CommitListSetting setting)
    {
        switch (setting)
        {
            case CommitListSetting.ShowIgnored:
                ShowIgnored = !ShowIgnored;
                break;
            case CommitListSetting.ShowSkipWorktree:
                ShowSkipWorktree = !ShowSkipWorktree;
                break;
            case CommitListSetting.ShowAssumeUnchanged:
                ShowAssumeUnchanged = !ShowAssumeUnchanged;
                break;
            case CommitListSetting.ShowUntracked:
                ShowUntracked = !ShowUntracked;
                return;
        }

        ReadListedChanges();
    }

    // Upstream's RefreshArtificialCommitOnApplicationActivated item, the same setting as the Options menu's.
    [RelayCommand]
    private void ToggleRefreshOnFocus()
    {
        RefreshOnFocus = !RefreshOnFocus;
        OnPropertyChanged(nameof(RefreshOnFocus));
    }

    // The repository's changes, or the working directory read again with the list's settings while they ask for more.
    private void ReadListedChanges()
    {
        int version = ++_flaggedChangesVersion;
        if ((!ShowIgnored && !ShowSkipWorktree && !ShowAssumeUnchanged) || _services.ReadWorkTreeChanges is not { } read)
        {
            _flaggedChanges = null;
            OnPropertyChanged(nameof(ListedChanges));
            return;
        }

        bool ignored = ShowIgnored;
        bool skipWorktree = ShowSkipWorktree;
        bool assumeUnchanged = ShowAssumeUnchanged;
        Run(async () =>
        {
            IReadOnlyList<FileChange> changes = await read(ignored, assumeUnchanged, skipWorktree);
            if (version == _flaggedChangesVersion)
            {
                _flaggedChanges = changes;
                OnPropertyChanged(nameof(ListedChanges));
            }
        });
    }
}
