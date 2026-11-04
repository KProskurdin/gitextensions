using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  The filter options upstream's <c>FilterInfo</c> keeps in settings, which every revision grid (the browse window's, the
///  file history's) starts from: the current branch choice (<c>ShowCurrentBranchOnly</c>), hide merge commits, first
///  parent, simplify by decoration, and the full history and merge simplification the file history shares.
/// </summary>
public static class StoredRevisionFilter
{
    /// <summary>
    ///  The filter a grid starts with.
    /// </summary>
    public static RevisionFilter Initial(IAppPreferences preferences)
        => WithStoredHistory(new RevisionFilter(
            CurrentBranchOnly: preferences.ShowCurrentBranchOnly,
            NoMerges: preferences.HideMergeCommits,
            FirstParent: preferences.ShowOnlyFirstParent,
            SimplifyByDecoration: preferences.ShowSimplifyByDecoration), preferences);

    /// <summary>
    ///  The filter with the stored full history and merge simplification, which only the filter window and the file
    ///  history's toolstrip change.
    /// </summary>
    public static RevisionFilter WithStoredHistory(RevisionFilter filter, IAppPreferences preferences)
        => filter with
        {
            FullHistory = preferences.FileHistory.FullHistory,
            SimplifyMerges = preferences.FileHistory.SimplifyMerges,
        };

    /// <summary>
    ///  Upstream's <c>FilterInfo</c> setters on each change: hide merge commits, first parent and simplify by decoration.
    /// </summary>
    public static void Store(RevisionFilter filter, IAppPreferences preferences)
    {
        preferences.HideMergeCommits = filter.NoMerges;
        preferences.ShowOnlyFirstParent = filter.FirstParent;
        preferences.ShowSimplifyByDecoration = filter.SimplifyByDecoration;
        preferences.Save();
    }

    /// <summary>
    ///  Upstream's <c>FormRevisionFilter.OkClick</c>: the current branch choice, the full history and merge simplification.
    /// </summary>
    public static void StoreDialogChoice(RevisionFilter filter, IAppPreferences preferences)
    {
        preferences.ShowCurrentBranchOnly = filter.CurrentBranchOnly;
        preferences.FileHistory = preferences.FileHistory with
        {
            FullHistory = filter.FullHistory, SimplifyMerges = filter.SimplifyMerges,
        };
    }
}
