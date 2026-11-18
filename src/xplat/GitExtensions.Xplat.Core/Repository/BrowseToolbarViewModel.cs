using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The browse window's command state (upstream FormBrowse's toolstrip and menus, PLAN.md section 12.6 step 4): what can
///  run, from whether a read or write is in progress, a repository is open, git was found, the current branch, the remotes
///  and the selected commit; the toolstrip's texts (working directory, branch, push's ahead and behind, the commit count
///  and state image) and the window's title. <see cref="Refresh"/> reads the repository's, the grid's and the actions' view
///  models again.
/// </summary>
public sealed partial class BrowseToolbarViewModel : ObservableObject
{
    // ToolStripPushButton's texts.
    public const string PushText = "Push";
    public const string AheadCommitsToPushText = "{0} new commit(s) will be pushed";
    public const string BehindCommitsText = "{0} commit(s) should be integrated (or will be lost if force pushed)";

    // Upstream's TranslatedStrings.NoBranch, the title's branch until it is known.
    public const string NoBranchText = "no branch";

    // Upstream's Commit button text.
    public const string CommitText = "Commit";

    // Upstream's pull button texts (FormBrowse's _pullMerge, _pullRebase, ... translation strings).
    public const string PullMergeText = "Pull - merge";
    public const string PullRebaseText = "Pull - rebase";
    public const string PullFetchText = "Fetch";
    public const string PullFetchAllText = "Fetch all";
    public const string PullFetchPruneAllText = "Fetch and prune all";
    public const string PullOpenDialogText = "Open pull dialog";

    // The status bar's texts.
    public const string WorkingText = "Working...";
    public const string RebaseInProgressText = " (rebase in progress)";
    public const string MergeInProgressText = " (merge in progress)";

    // Upstream's FormBrowse.Designer tooltip of toolStripButtonLevelUp.
    public const string SubmodulesText = "Submodules";

    private readonly CommitListViewModel _commits;
    private readonly RepositoryViewModel _repository;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly RecentRepositoriesViewModel _recent;
    private readonly IAppPreferences _preferences;
    private readonly bool _gitAvailable;
    private readonly Func<string, string> _translate;
    private string? _repositoryPath;
    private SubmoduleStructure? _submodules;

    /// <param name="gitAvailable">Whether git was found; without it only the settings and help work.</param>
    /// <param name="translate">Upstream's translation of an English text.</param>
    /// <param name="diffMergeTools">The diff tools offered under the grid's "Open selected commits with difftool".</param>
    public BrowseToolbarViewModel(CommitListViewModel commits, RepositoryViewModel repository,
        RepositoryOperationsViewModel actions, RecentRepositoriesViewModel recent, IAppPreferences preferences,
        bool gitAvailable, Func<string, string>? translate = null, IDiffMergeToolCatalog? diffMergeTools = null)
    {
        _commits = commits;
        _repository = repository;
        _actions = actions;
        _recent = recent;
        _preferences = preferences;
        _gitAvailable = gitAvailable;
        _translate = translate ?? (text => text);
        TreeMenu = new RepoObjectMenuViewModel(_translate);

        // Upstream's diffSelectedCommitsMenuItem and its LoadCustomDifftools: the directory diff tool from the first to the
        // selected commit, in git's tool or a chosen one.
        GridDiffTools = new CustomDiffToolsViewModel(preferences, diffMergeTools ?? new GitDiffMergeToolCatalog());
        GridDiffToolsMenu = GridDiffTools.Add(tool =>
        {
            BrowseWindow.OpenSelectedCommitsWithDifftool(tool);
            return Task.CompletedTask;
        });
        _commits.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(CommitListViewModel.Status):
                    OnPropertyChanged(nameof(GridStatusText));
                    break;
                case nameof(CommitListViewModel.HasMore):
                    OnPropertyChanged(nameof(HasMoreCommits));
                    break;
            }
        };
        _actions.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(RepositoryOperationsViewModel.IsBusy)
                or nameof(RepositoryOperationsViewModel.StatusMessage))
            {
                OnPropertyChanged(nameof(OperationStatusText));
            }
        };
    }

    /// <summary>
    ///  The diff tools under the grid's "Open selected commits with difftool", read when its menu first opens.
    /// </summary>
    public CustomDiffToolsViewModel GridDiffTools { get; }

    /// <summary>
    ///  "Open selected commits with difftool" and its tools.
    /// </summary>
    public CustomDiffToolMenu GridDiffToolsMenu { get; }

    /// <summary>
    ///  The revision grid's context menu, whose items take the window's state too.
    /// </summary>
    public GridMenuViewModel GridMenu { get; } = new(() => GitVersion.Current.SupportAmendCommits);

    /// <summary>
    ///  The left panel's context menu.
    /// </summary>
    public RepoObjectMenuViewModel TreeMenu { get; }

    /// <summary>
    ///  A read or write is in progress; the commands wait for it.
    /// </summary>
    public bool IsBusy => _commits.IsLoading || _repository.IsLoading || _actions.IsBusy;

    /// <summary>
    ///  A repository is shown: the browse panels and the repository's menus show, else the dashboard and its menu.
    /// </summary>
    public bool IsRepositoryShown => _repositoryPath is not null;

    /// <summary>
    ///  The status bar's branch with the action in progress and, when upstream's "Show ahead and behind information on
    ///  status bar" is on, the counts.
    /// </summary>
    public string StatusBranchText
    {
        get
        {
            string branch = _repository.CurrentBranch;
            if (branch.Length == 0)
            {
                return "";
            }

            string state = _repository.IsRebasing ? RebaseInProgressText
                : _repository.IsMerging ? MergeInProgressText
                : "";
            string sync = _preferences.ShowAheadBehindData && _repository.Sync is { } status
                ? $" [ahead {status.Ahead}, behind {status.Behind}]"
                : "";
            return $"Branch: {branch}{state}{sync}";
        }
    }

    /// <summary>
    ///  Upstream's "Show stash count on status bar in browse window", for a shown repository.
    /// </summary>
    public bool IsStashCountVisible => _preferences.ShowStashCount && _repositoryPath is not null;

    public string StashCountText => $"Stashes: {_repository.Stashes.Count}";

    /// <summary>
    ///  The grid's read: the commits shown, or the error.
    /// </summary>
    public string GridStatusText => _commits.Status;

    /// <summary>
    ///  The grid stopped at its page size: "Load more" reads the next page.
    /// </summary>
    public bool HasMoreCommits => _commits.HasMore;

    /// <summary>
    ///  Upstream's notification bar for a stopped rebase, merge or patch or for unresolved conflicts (FormBrowse's
    ///  <c>RefreshGitAction</c>); null when there is nothing to show.
    /// </summary>
    public GitActionBanner? ActionBanner
        => GitActionBanner.ForRepository(_repository.IsRebasing, _repository.IsMerging, _repository.IsApplyingPatch,
            _repository.Changes.Any(change => change.Kind == ChangeKind.Conflict));

    public bool ShowsResolveButton => ShowsActionButton(GitActionButton.Resolve);

    public bool ShowsContinueButton => ShowsActionButton(GitActionButton.Continue);

    public bool ShowsAbortButton => ShowsActionButton(GitActionButton.Abort);

    public bool ShowsMoreButton => ShowsActionButton(GitActionButton.More);

    /// <summary>
    ///  Upstream's bisect bar (<c>RefreshBisect</c>); null while no bisect runs.
    /// </summary>
    public GitActionBanner? BisectBanner => GitActionBanner.ForBisect(_repository.IsBisecting);

    /// <summary>
    ///  A bisect runs: the grid's menu offers its marks and its stop.
    /// </summary>
    public bool IsBisecting => _repository.IsBisecting;

    /// <summary>
    ///  The left panel's toolstrip: which trees show.
    /// </summary>
    public bool ShowsBranchesTree => _preferences.ShowsRepoObjectsTree(RepoObjectKind.BranchesRoot);

    public bool ShowsRemotesTree => _preferences.ShowsRepoObjectsTree(RepoObjectKind.RemotesRoot);

    public bool ShowsWorktreesTree => _preferences.ShowsRepoObjectsTree(RepoObjectKind.WorktreesRoot);

    public bool ShowsTagsTree => _preferences.ShowsRepoObjectsTree(RepoObjectKind.TagsRoot);

    public bool ShowsSubmodulesTree => _preferences.ShowsRepoObjectsTree(RepoObjectKind.SubmodulesRoot);

    public bool ShowsStashesTree => _preferences.ShowsRepoObjectsTree(RepoObjectKind.StashesRoot);

    /// <summary>
    ///  The last action's message, or that one is running.
    /// </summary>
    public string OperationStatusText => _actions.IsBusy ? WorkingText : _actions.StatusMessage;

    /// <summary>
    ///  A repository is open and nothing is in progress: the repository's commands can run.
    /// </summary>
    public bool IsOpen => !IsBusy && _repositoryPath is not null;

    /// <summary>
    ///  Open, init and clone, and the working directory's menu: they need git, not a repository.
    /// </summary>
    public bool CanStartRepository => !IsBusy && _gitAvailable;

    public bool CanLoadMore => !IsBusy;

    /// <summary>
    ///  Upstream's commit actions do not apply to the artificial working directory and index rows.
    /// </summary>
    public bool IsCommitSelected
        => IsOpen && _commits.Selected is { } row && !ArtificialCommits.IsArtificial(row.Hash);

    public bool CanRebaseInteractive => IsCommitSelected && _actions.EditorCommand is not null;

    public bool HasBranch => _repository.CurrentBranch.Length > 0;

    /// <summary>
    ///  Pull, push and the pull button's merge and rebase: on a branch.
    /// </summary>
    public bool CanPullOrPush => IsOpen && HasBranch;

    public bool CanOpenPullDialog => IsOpen && _repository.Remotes.Count > 0;

    public bool CanOpenPushDialog
        => CanOpenPullDialog && _repository.Branches.Any(branch => !branch.IsRemote);

    /// <summary>
    ///  Upstream's <c>UpdateFetchAllVisibility</c>: "Fetch all" only with more than one remote.
    /// </summary>
    public bool IsFetchAllVisible => _repository.Remotes.Count > 1;

    /// <summary>
    ///  Upstream's <c>RefreshContent</c>: the caption among the recent repositories.
    /// </summary>
    public string WorkingDirText
        => _repositoryPath is { } path
            ? RecentRepositoryPaths.Caption(path, [.. _recent.Items.Select(item => item.Path)],
                _preferences.RecentRepositoryMenu.Splitter())
            : "";

    /// <summary>
    ///  Upstream's <c>RecentReposComboMinWidth</c>.
    /// </summary>
    public double WorkingDirMinWidth => _preferences.RecentRepositoryMenu.ComboMinWidth;

    /// <summary>
    ///  The branch as upstream's branch drop-down and title show it: "(no branch)" on a detached head.
    /// </summary>
    public string CurrentBranchName
        => _repository.CurrentBranch.Length > 0 ? _repository.CurrentBranch : DetachedHeadParser.DetachedBranch;

    public string BranchText => _repositoryPath is null ? "" : CurrentBranchName;

    /// <summary>
    ///  Upstream's FormBrowse title (<c>AppTitleGenerator</c>), refreshed with the revision grid.
    /// </summary>
    public string Title
        => BrowseWindowTitle.Of(_repositoryPath, _repositoryPath is null ? null : CurrentBranchName,
            _translate(NoBranchText), _commits.Filter.PathFilter);

    /// <summary>
    ///  Upstream's <c>ToolStripPushButton.DisplayAheadBehindInformation</c>: the counts as the button's text; null without
    ///  them.
    /// </summary>
    public string? PushButtonText
        => ShownSync is { } sync
            ? new AheadBehindData(_repository.CurrentBranch, "", sync.Ahead.ToString(CultureInfo.InvariantCulture),
                sync.Behind > 0 ? sync.Behind.ToString(CultureInfo.InvariantCulture) : "").ToDisplay()
            : null;

    /// <summary>
    ///  The push button's image: upstream's unstage image when the branch is behind.
    /// </summary>
    public string PushButtonImage => ShownSync is { Behind: > 0 } ? "Unstage" : "Push";

    public string PushButtonTip
    {
        get
        {
            if (ShownSync is not { } sync)
            {
                return _translate(PushText);
            }

            List<string> tips = [];
            if (sync.Ahead > 0)
            {
                tips.Add(string.Format(CultureInfo.CurrentCulture, _translate(AheadCommitsToPushText), sync.Ahead));
            }

            if (sync.Behind > 0)
            {
                tips.Add(string.Format(CultureInfo.CurrentCulture, _translate(BehindCommitsText), sync.Behind));
            }

            return tips.Count > 0 ? string.Join(Environment.NewLine, tips) : _translate(PushText);
        }
    }

    /// <summary>
    ///  Upstream's Commit button: its text with the count of changed files while the toolbar shows the status.
    /// </summary>
    public string CommitButtonText
    {
        get
        {
            int count = _repository.Changes.Select(change => change.Path).Distinct(StringComparer.Ordinal).Count();
            string commit = _translate(CommitText);
            return count == 0 || !_preferences.ShowGitStatusInBrowseToolbar ? commit : $"{commit} ({count})";
        }
    }

    /// <summary>
    ///  Upstream's <c>RepoStateVisualiser</c>: the clean image while the toolbar shows no status, the unknown one until the
    ///  status has been read.
    /// </summary>
    public string CommitButtonImage
        => _repositoryPath is null || !_preferences.ShowGitStatusInBrowseToolbar ? nameof(RepoState.RepoStateClean)
            : !_repository.ChangesRead ? nameof(RepoState.RepoStateUnknown)
            : RepoStates.Of(_repository.Changes).ToString();

    /// <summary>
    ///  Upstream's <c>DefaultPullAction</c>: what the pull button does.
    /// </summary>
    public GitPullAction DefaultPullAction => _preferences.DefaultPullAction;

    /// <summary>
    ///  Upstream's <c>RefreshDefaultPullAction</c>: the pull button shows the default action's image and text.
    /// </summary>
    public string PullButtonImage => PullButton.Image;

    public string PullButtonTip => _translate(PullButton.Text);

    private (string Image, string Text) PullButton
        => DefaultPullAction switch
        {
            GitPullAction.Fetch => ("PullFetch", PullFetchText),
            GitPullAction.FetchAll => ("PullFetchAll", PullFetchAllText),
            GitPullAction.FetchPruneAll => ("PullFetchPruneAll", PullFetchPruneAllText),
            GitPullAction.Merge => ("PullMerge", PullMergeText),
            GitPullAction.Rebase => ("PullRebase", PullRebaseText),
            _ => ("Pull", PullOpenDialogText),
        };

    /// <summary>
    ///  The open repository is a submodule: upstream's toolStripButtonLevelUp then goes up to the superproject.
    /// </summary>
    public bool IsInSubmodule => _submodules?.SuperProject is not null;

    public string SubmodulesButtonImage => IsInSubmodule ? "NavigateUp" : "SubmodulesManage";

    public string SubmodulesButtonTip
        => _translate(IsInSubmodule ? SubmoduleStructure.GoToSuperprojectText : SubmodulesText);

    /// <summary>
    ///  Upstream disables the submodules button in a bare repository.
    /// </summary>
    public bool CanUseSubmodules => IsOpen && _submodules?.IsBareRepository is not true;

    private bool ShowsActionButton(GitActionButton button) => ActionBanner?.Buttons.Contains(button) == true;

    // The ahead and behind counts the push button shows (upstream's ShowAheadBehindData, on a branch).
    private SyncStatus? ShownSync
        => _preferences.ShowAheadBehindData && _repository.CurrentBranch.Length > 0 ? _repository.Sync : null;

    /// <summary>
    ///  Upstream's "Set default Pull button action" items.
    /// </summary>
    [RelayCommand]
    private void SetDefaultPullAction(GitPullAction action)
    {
        _preferences.DefaultPullAction = action;
        OnPropertyChanged(nameof(DefaultPullAction));
        OnPropertyChanged(nameof(PullButtonImage));
        OnPropertyChanged(nameof(PullButtonTip));
    }

    /// <summary>
    ///  The open repository's submodules, as the left panel read them; null until they are read.
    /// </summary>
    public void ShowSubmodules(SubmoduleStructure? structure)
    {
        _submodules = structure;
        OnPropertyChanged(nameof(IsInSubmodule));
        OnPropertyChanged(nameof(SubmodulesButtonImage));
        OnPropertyChanged(nameof(SubmodulesButtonTip));
        OnPropertyChanged(nameof(CanUseSubmodules));
    }

    /// <summary>
    ///  Reads the state again for the open repository (null for none).
    /// </summary>
    public void Refresh(string? repositoryPath)
    {
        _repositoryPath = repositoryPath;

        // Every property above follows these.
        OnPropertyChanged(string.Empty);
    }
}
