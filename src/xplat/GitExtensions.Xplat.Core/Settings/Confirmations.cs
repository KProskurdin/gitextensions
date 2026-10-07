namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  The questions upstream asks before an action, which the user can turn off (upstream's <c>ConfirmationsSettingsPage</c>).
///  Only those whose action the new shell has are listed.
/// </summary>
public enum Confirmation
{
    /// <summary>Upstream <c>DontConfirmAmend</c>: committing with Amend.</summary>
    Amend,

    /// <summary>Upstream <c>DontConfirmCommitIfNoBranch</c>: committing while no branch is checked out.</summary>
    CommitWithoutBranch,

    /// <summary>Upstream <c>DontConfirmRebase</c>: rebasing on the selected commit from the grid.</summary>
    Rebase,

    /// <summary>Upstream <c>DontConfirmFetchAndPruneAll</c>: a fetch that prunes remote-tracking branches.</summary>
    FetchAndPrune,

    /// <summary>Upstream <c>DontConfirmPushNewBranch</c>: pushing a branch the remote does not have yet.</summary>
    PushNewBranch,

    /// <summary>Upstream <c>DontConfirmDeleteUnmergedBranch</c>: deleting a branch that is not merged.</summary>
    DeleteUnmergedBranch,

    /// <summary>Upstream <c>ConfirmBranchCheckout</c>: checking out a branch from the left panel. Off by default.</summary>
    BranchCheckout,

    /// <summary>Upstream <c>stashconfirmdropshow</c> (<c>DontConfirmStashDrop</c>): dropping a stash.</summary>
    StashDrop,

    /// <summary>Upstream <c>DontConfirmSwitchWorktree</c>: opening another worktree in the window.</summary>
    SwitchWorktree,
}

/// <summary>
///  A check box of the Confirmations page: its group and upstream's label.
/// </summary>
public sealed record ConfirmationOption(string Group, Confirmation Confirmation, string Label);

/// <summary>
///  Upstream's texts for the confirmations: the page's labels in upstream's order, and the questions.
/// </summary>
public static class Confirmations
{
    /// <summary>
    ///  Upstream's "Don't show me this message again" check box.
    /// </summary>
    public const string DontShowAgain = "Don't show me this message again";

    /// <summary>
    ///  The page's check boxes, grouped and labeled as upstream's.
    /// </summary>
    public static IReadOnlyList<ConfirmationOption> Options { get; } =
    [
        new("Commits", Confirmation.Amend, "Amend last commit"),
        new("Commits", Confirmation.CommitWithoutBranch,
            "Commit when no branch is currently checked out (headless state)"),
        new("Commits", Confirmation.Rebase, "Rebase on top of selected commit"),
        new("Branches", Confirmation.FetchAndPrune, "Fetch and prune branches"),
        new("Branches", Confirmation.PushNewBranch, "Push a new branch for the remote"),
        new("Branches", Confirmation.DeleteUnmergedBranch, "Delete unmerged branches"),
        new("Branches", Confirmation.BranchCheckout, "Checkout branch using left panel"),
        new("Stash", Confirmation.StashDrop, "Drop stash"),
        new("Worktrees", Confirmation.SwitchWorktree, "Switch worktree"),
    ];

    /// <summary>
    ///  Upstream FormCommit's amend question.
    /// </summary>
    public static string AmendQuestion { get; } =
        "You are about to rewrite history." + Environment.NewLine
        + "Only use Amend if the commit has not been published yet!" + Environment.NewLine
        + Environment.NewLine
        + "Do you want to continue?";

    /// <summary>
    ///  Upstream FormCommit's question when no branch is checked out.
    /// </summary>
    public static string NotOnBranchQuestion { get; } =
        "You are not working on a branch" + Environment.NewLine + Environment.NewLine
        + "This commit will be unreferenced when switching to another branch and can be lost."
        + Environment.NewLine + Environment.NewLine + "Do you want to continue?";

    public const string RebaseQuestion = "Are you sure you want to rebase? This action will rewrite commit history.";

    public const string FetchAndPruneQuestion =
        "Warning! The fetch with prune will remove all the remote-tracking references which no longer exist on remotes. " +
        "Do you want to proceed?";

    /// <summary>
    ///  Upstream FormPush's question for a branch the remote does not know.
    /// </summary>
    public static string PushNewBranchQuestion { get; } =
        "The branch you are about to push seems to be a new branch for the remote." + Environment.NewLine
        + "Are you sure you want to push this branch?";

    /// <summary>
    ///  Upstream FormDeleteBranch's question, with its reflog hint.
    /// </summary>
    public static string DeleteUnmergedBranchQuestion { get; } =
        "The selected branch(es) have not been merged into HEAD." + Environment.NewLine + "Proceed?"
        + Environment.NewLine + Environment.NewLine
        + "Did you know you can use reflog to restore deleted branches?";

    public const string BranchCheckoutQuestion = "Are you sure you want to check out branch \"{0}\"?";

    public const string StashDropQuestion = "Are you sure you want to drop the stash? This action cannot be undone.";

    public const string SwitchWorktreeQuestion = "Switch to worktree at {0}?";
}
