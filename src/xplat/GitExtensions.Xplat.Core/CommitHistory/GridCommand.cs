namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  The revision grid's commands that have a hotkey (upstream <c>RevisionGridControl.Command</c>); they act while the grid
///  has the focus; the grid menu's compare, commit and Navigate items run them too.
/// </summary>
public enum GridCommand
{
    GoToParent,
    GoToChild,
    SelectCurrentRevision,
    RevisionFilter,
    ResetRevisionFilter,
    ResetRevisionPathFilter,
    ShowAllBranches,
    ShowCurrentBranchOnly,
    ToggleHideMergeCommits,
    ShowFirstParent,
    ToggleHighlightSelectedBranch,
    NextQuickSearch,
    PrevQuickSearch,
    GoToFirstParent,
    GoToLastParent,
    ToggleBetweenArtificialAndHeadCommits,
    NavigateBackward,
    NavigateForward,
    GoToCommit,
    GoToMergeBase,
    CreateFixupCommit,
    CreateSquashCommit,
    CreateAmendCommit,
    SelectAsBaseToCompare,
    CompareToBase,
    CompareToWorkingDirectory,
    CompareToCurrentBranch,
    CompareToBranch,
    CompareSelectedCommits,
    OpenCommitsWithDifftool,
}
