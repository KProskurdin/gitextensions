using System.Text.RegularExpressions;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  What upstream's <c>FormPush.HandlePushOnExit</c> offers after a push the remote rejected, without its windows: a pull
///  (with the default action, rebase or merge) when the current branch is behind, or a force push with lease, and then the
///  push again.
/// </summary>
public static class PushRejection
{
    // Upstream's texts.
    public const string MergeHeading = "Pull latest changes from remote repository";
    public const string ForceHeading = "Push rejected";

    public const string MergeText =
        "The push was rejected because the tip of your current branch is behind its remote counterpart. " +
        "Merge the remote changes before pushing again.";

    public const string ForceText =
        "The push was rejected because the tip of your current branch is behind its remote counterpart";

    public const string PullDefaultButton = "&Pull with the default pull action ({0})";
    public const string PullRebaseButton = "Pull with &rebase";
    public const string PullMergeButton = "Pull with &merge";
    public const string PushForceButton = "&Force push with lease";
    public const string ActionNone = "none";
    public const string ActionFetch = "fetch";
    public const string ActionRebase = "rebase";
    public const string ActionMerge = "merge";
    public const string Caption = "Push was rejected from \"{0}\"";

    // Upstream shows these two untranslated.
    public const string PullActionNotSupported =
        "Automatical pull can only be performed, when the default pull action is either set to Merge or Rebase.";

    public const string RebasesMergeCommit = "Can not perform automatical pull, when the pull action is set to Rebase " +
                                             "and one of the commits that are about to be rebased is a merge commit.";

    /// <summary>
    ///  Whether upstream handles the failed push: null when it does not (another branch than the current one was pushed, the
    ///  push went to a URL, the repository is bare, or git's output shows no rejected ref), else true when the current
    ///  branch was rejected (all choices are offered) and false for another ref (only the force push).
    /// </summary>
    /// <param name="pushedBranch">The branch chosen in the push dialog (upstream checks it on every tab).</param>
    public static bool? Find(IEnumerable<string> output, string pushedBranch, string currentBranch, bool toUrl,
        bool isBare, PushKind kind = PushKind.Branch)
    {
        // A push of several branches has no single command line upstream's retry could add the force to.
        if (pushedBranch != currentBranch || currentBranch.Length == 0 || toUrl || isBare ||
            kind == PushKind.Multiple)
        {
            return null;
        }

        Regex isRejected = new($"! \\[rejected\\]( .* )?((?<currBranch>{Regex.Escape(currentBranch)})|.*) -> ");
        Match match = isRejected.Match(string.Join("\n", output));
        return match.Success ? match.Groups["currBranch"].Success : null;
    }

    /// <summary>
    ///  The default action's button text, as upstream names the action.
    /// </summary>
    public static string DefaultButtonAction(GitPullAction defaultAction) => defaultAction switch
    {
        GitPullAction.Fetch or GitPullAction.FetchAll or GitPullAction.FetchPruneAll => ActionFetch,
        GitPullAction.Merge => ActionMerge,
        GitPullAction.Rebase => ActionRebase,
        _ => ActionNone,
    };

    /// <summary>
    ///  The push again with force with lease, unless it already forces (upstream adds the option to the command line only
    ///  when it has no force option).
    /// </summary>
    public static PushRequest WithForceWithLease(PushRequest request)
        => request.Force == ForcePushOptions.DoNotForce ? request with { Force = ForcePushOptions.ForceWithLease } : request;

    /// <summary>
    ///  The pull to run for the chosen action (<see cref="GitPullAction.Default"/> is the default pull action), or null
    ///  when nothing is pulled; <paramref name="error"/> is upstream's message when the action cannot be pulled with.
    /// </summary>
    public static PullAction? PullFor(GitPullAction chosen, GitPullAction defaultAction, out string? error)
    {
        error = null;
        GitPullAction action = chosen == GitPullAction.Default ? defaultAction : chosen;
        switch (action)
        {
            case GitPullAction.None:
                return null;
            case GitPullAction.Merge:
                return PullAction.Merge;
            case GitPullAction.Rebase:
                return PullAction.Rebase;
            default:
                error = PullActionNotSupported;
                return null;
        }
    }

    /// <summary>
    ///  True when upstream checks for merge commits a rebase would rewrite before pulling: the default pull action is
    ///  rebase (upstream checks the default, whatever was chosen), a branch was pushed to a remote, and it is the current
    ///  branch pushed to the remote it tracks.
    /// </summary>
    public static bool ChecksRebasedMergeCommits(GitPullAction defaultAction, PushRequest request, bool toUrl,
        string pushedBranch, string currentBranch, string? currentBranchRemote)
        => defaultAction == GitPullAction.Rebase && !toUrl && request.Kind == PushKind.Branch &&
           pushedBranch == currentBranch && request.Remote == currentBranchRemote;
}
