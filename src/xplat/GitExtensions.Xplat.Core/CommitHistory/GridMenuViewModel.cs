using CommunityToolkit.Mvvm.ComponentModel;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  The revision grid's context menu state (upstream's <c>RevisionGridControl.ContextMenuOpening</c> and its
///  <c>SetEnabled</c> calls, PLAN.md principle 6): which items apply to the selected commit, from the commit (an artificial
///  row, a stash), its ref submenus (<see cref="GridMenus"/>), the editor and the build server's links. An item that does
///  not apply is hidden. The view fills the submenus, orders the items and hides the separators left alone.
/// </summary>
public sealed class GridMenuViewModel : ObservableObject
{
    private readonly Func<bool> _supportsAmend;
    private CommitRow? _row;
    private GridRefMenus? _menus;
    private bool _editorAvailable;
    private bool _hasBuildReport;
    private bool _hasPullRequest;

    /// <param name="supportsAmend">Whether git can make an amend commit (upstream's <c>SupportAmendCommits</c>).</param>
    public GridMenuViewModel(Func<bool>? supportsAmend = null)
    {
        _supportsAmend = supportsAmend ?? (() => true);
    }

    /// <summary>
    ///  The working directory's or the index's row.
    /// </summary>
    public bool IsArtificial => _row is { } row && ArtificialCommits.IsArtificial(row.Hash);

    /// <summary>
    ///  The items for a commit: copy, rebase on, the commit's actions, its advanced items, create a tag here.
    /// </summary>
    public bool IsCommitItemVisible => _row is not null && !IsArtificial;

    /// <summary>
    ///  Upstream's reset changes and commit items, for an artificial row.
    /// </summary>
    public bool IsArtificialItemVisible => IsArtificial;

    /// <summary>
    ///  Upstream's showStash: the stash items for a stash row.
    /// </summary>
    public bool IsStashItemVisible => IsCommitItemVisible && _row is { IsStash: true };

    public bool IsCheckoutBranchVisible => IsCommitItemVisible && HasRefs(_menus?.Checkout);

    public bool IsMergeBranchVisible => IsCommitItemVisible && HasRefs(_menus?.Merge);

    public bool IsPushBranchVisible => IsCommitItemVisible && HasRefs(_menus?.Push);

    /// <summary>
    ///  Upstream's rebase items: on a commit that another branch can be rebased on.
    /// </summary>
    public bool CanRebaseOn => _menus?.RebaseOnTopOf is not null;

    public bool CanRebaseInteractive => CanRebaseOn && _editorAvailable;

    public bool IsRenameBranchVisible => HasRefs(_menus?.Rename);

    /// <summary>
    ///  Upstream shows the delete item disabled on the current branch's head, which cannot be deleted.
    /// </summary>
    public bool IsDeleteBranchVisible => CanDeleteBranch || _menus is { IsHeadOfCurrentBranch: true };

    public bool CanDeleteBranch => HasRefs(_menus?.Delete);

    public bool IsDeleteTagVisible => HasRefs(_menus?.DeleteTags);

    public bool IsAmendVisible => IsCommitItemVisible && _supportsAmend();

    public bool IsSelectInLeftPanelVisible => _menus is { SelectInLeftPanel.Count: > 0 };

    public bool IsBuildReportVisible => _hasBuildReport;

    public bool IsPullRequestVisible => _hasPullRequest;

    /// <summary>
    ///  The state for the selected commit as the menu opens.
    /// </summary>
    /// <param name="hasBuildReport">The commit's build has a report page.</param>
    /// <param name="hasPullRequest">The commit's build has a pull request page.</param>
    public void Refresh(CommitRow row, GridRefMenus menus, bool editorAvailable, bool hasBuildReport, bool hasPullRequest)
    {
        _row = row;
        _menus = menus;
        _editorAvailable = editorAvailable;
        _hasBuildReport = hasBuildReport;
        _hasPullRequest = hasPullRequest;

        // Every property above follows these.
        OnPropertyChanged(string.Empty);
    }

    // Upstream's submenu items for refs; a null entry is a separator.
    private static bool HasRefs<T>(IReadOnlyList<T?>? refs) where T : class => refs?.Any(item => item is not null) == true;
}
