namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  A git action that stops and waits for the user, as upstream's <c>InteractiveGitActionControl.GitAction</c> names them.
/// </summary>
public enum GitAction
{
    None,
    Bisect,
    Rebase,
    Merge,
    Patch,
}

/// <summary>
///  A button of an action banner. <see cref="More"/> opens the action's own dialog or menu.
/// </summary>
public enum GitActionButton
{
    Resolve,
    Continue,
    Abort,
    More,
}

/// <summary>
///  What upstream's notification bars above the revision grid show: one bar for a bisect, and one for a stopped rebase,
///  merge or patch or for unresolved conflicts. Port of <c>InteractiveGitActionControl.SetGitAction</c>, with its texts and
///  its buttons in its order.
/// </summary>
public sealed record GitActionBanner(GitAction Action, bool HasConflicts, string Text, IReadOnlyList<GitActionButton> Buttons)
{
    private const string ProgressMessage = "{0} is currently in progress.";
    private const string ConflictsMessage = "There are unresolved merge conflicts.";
    private const string ProgressWithConflictsMessage = "{0} is currently in progress with merge conflicts.";

    /// <summary>
    ///  The bisect bar, or null when no bisect is running. Upstream keeps it apart from the other bar, because a bisect can
    ///  stop at a commit while a merge or rebase is stopped too.
    /// </summary>
    public static GitActionBanner? ForBisect(bool isBisecting)
        => isBisecting ? For(GitAction.Bisect, hasConflicts: false) : null;

    /// <summary>
    ///  The bar for a stopped rebase, merge or patch, checked in upstream's order (<c>RefreshGitAction</c>), or for
    ///  conflicts left by any other command; null when there is nothing to show.
    /// </summary>
    public static GitActionBanner? ForRepository(bool isRebasing, bool isMerging, bool isApplyingPatch, bool hasConflicts)
    {
        GitAction action = isRebasing ? GitAction.Rebase
            : isMerging ? GitAction.Merge
            : isApplyingPatch ? GitAction.Patch
            : GitAction.None;
        return For(action, hasConflicts);
    }

    public static GitActionBanner? For(GitAction action, bool hasConflicts)
    {
        if (action == GitAction.None && !hasConflicts)
        {
            return null;
        }

        GitActionButton resolveOrContinue = hasConflicts ? GitActionButton.Resolve : GitActionButton.Continue;
        IReadOnlyList<GitActionButton> buttons = action switch
        {
            GitAction.Bisect => [GitActionButton.More],
            GitAction.Rebase => [resolveOrContinue, GitActionButton.Abort, GitActionButton.More],
            GitAction.Merge => [resolveOrContinue, GitActionButton.Abort],
            GitAction.Patch => [resolveOrContinue, GitActionButton.Abort, GitActionButton.More],
            _ => [GitActionButton.Resolve],
        };

        string text = action == GitAction.None
            ? ConflictsMessage
            : string.Format(hasConflicts ? ProgressWithConflictsMessage : ProgressMessage, action);
        return new GitActionBanner(action, hasConflicts, text, buttons);
    }
}
