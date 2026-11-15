using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
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

    private readonly CommitListViewModel _commits;
    private readonly RepositoryViewModel _repository;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly RecentRepositoriesViewModel _recent;
    private readonly IAppPreferences _preferences;
    private readonly bool _gitAvailable;
    private readonly Func<string, string> _translate;
    private string? _repositoryPath;

    /// <param name="gitAvailable">Whether git was found; without it only the settings and help work.</param>
    /// <param name="translate">Upstream's translation of an English text.</param>
    public BrowseToolbarViewModel(CommitListViewModel commits, RepositoryViewModel repository,
        RepositoryOperationsViewModel actions, RecentRepositoriesViewModel recent, IAppPreferences preferences,
        bool gitAvailable, Func<string, string>? translate = null)
    {
        _commits = commits;
        _repository = repository;
        _actions = actions;
        _recent = recent;
        _preferences = preferences;
        _gitAvailable = gitAvailable;
        _translate = translate ?? (text => text);
    }

    /// <summary>
    ///  The revision grid's context menu, whose items take the window's state too.
    /// </summary>
    public GridMenuViewModel GridMenu { get; } = new(() => GitVersion.Current.SupportAmendCommits);

    /// <summary>
    ///  A read or write is in progress; the commands wait for it.
    /// </summary>
    public bool IsBusy => _commits.IsLoading || _repository.IsLoading || _actions.IsBusy;

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
    ///  Reads the state again for the open repository (null for none).
    /// </summary>
    public void Refresh(string? repositoryPath)
    {
        _repositoryPath = repositoryPath;

        // Every property above follows these.
        OnPropertyChanged(string.Empty);
    }
}
