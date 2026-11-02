using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's <c>FormResetAnotherBranch</c> without its window: the local branches that can be moved to a commit, the one
///  proposed, the command that moves it, and the check that the move is a fast-forward.
/// </summary>
public static class ResetAnotherBranchDialog
{
    // Upstream's texts.
    public const string LocalRefInvalid = "The entered value '{0}' is not the name of an existing local branch.";
    public const string Warning = "You can only reset a branch safely if there is a direct path from it to selected revision.\r\nForcing a branch to reset if it has not been merged might leave some commits unreachable.";

    /// <summary>
    ///  Upstream's <c>InitLocalBranchesWithoutCurrent</c>: the local branches but the current one (all of them on a
    ///  detached HEAD) and those already at <paramref name="revision"/>; the branches tracking one of the commit's remote
    ///  branches first, then those named as one of them.
    /// </summary>
    /// <param name="heads">The repository's local branches.</param>
    /// <param name="currentBranch">The current branch, or upstream's detached head name.</param>
    /// <param name="revision">The commit to reset to.</param>
    /// <param name="revisionRefs">The refs pointing at the commit.</param>
    public static IReadOnlyList<IGitRef> Candidates(IEnumerable<IGitRef> heads, string currentBranch, ObjectId revision,
        IReadOnlyList<IGitRef> revisionRefs)
    {
        bool isDetachedHead = currentBranch == DetachedHeadParser.DetachedBranch;
        List<IGitRef> remotes = [.. revisionRefs.Where(gitRef => gitRef.IsRemote)];
        return
        [
            .. heads
                .Where(gitRef => gitRef.IsHead)
                .Where(gitRef => isDetachedHead || gitRef.LocalName != currentBranch)
                .Where(gitRef => revision != gitRef.ObjectId)
                .OrderByDescending(gitRef => remotes.Any(gitRef.IsTrackingRemote))
                .ThenByDescending(gitRef => remotes.Any(remote => remote.LocalName == gitRef.LocalName)),
        ];
    }

    /// <summary>
    ///  Upstream's proposed branch: when one remote branch points at the commit, the only candidate tracking it or named
    ///  as it; otherwise none.
    /// </summary>
    public static string? ProposedBranch(IReadOnlyList<IGitRef> candidates, IReadOnlyList<IGitRef> revisionRefs)
    {
        List<IGitRef> remotes = [.. revisionRefs.Where(gitRef => gitRef.IsRemote)];
        if (remotes.Count != 1)
        {
            return null;
        }

        IGitRef remote = remotes[0];
        List<IGitRef> proposed =
            [.. candidates.Where(gitRef => gitRef.IsTrackingRemote(remote) || gitRef.LocalName == remote.LocalName)];
        return proposed.Count == 1 ? proposed[0].Name : null;
    }

    /// <summary>
    ///  Upstream's <c>Commands.UpdateRef</c>: moves <paramref name="branch"/> (its complete name) to the commit.
    /// </summary>
    public static ArgumentString Arguments(IGitRef branch, ObjectId revision)
        => Commands.UpdateRef(branch.CompleteName, revision);

    /// <summary>
    ///  Upstream's check in <c>Validate</c>: <c>git merge-base --is-ancestor</c> succeeds when the branch is an ancestor of
    ///  the commit, so the move is a fast-forward and needs no force.
    /// </summary>
    public static ArgumentString IsAncestorArguments(IGitRef branch, ObjectId revision)
        => new GitArgumentBuilder("merge-base")
        {
            "--is-ancestor",
            branch.CompleteName.QuoteNE(),
            revision,
        };
}
