using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  What upstream's <c>GoToMergeBase</c> found: nothing to look for, no common ancestor (upstream's error), or the commit.
/// </summary>
public sealed record MergeBaseResult(bool NoCommonAncestor, string? Hash)
{
    public static MergeBaseResult None { get; } = new(NoCommonAncestor: false, Hash: null);
}

/// <summary>
///  Upstream's Navigate menu commands that look a commit up in git: <c>RevisionGridMenuCommands.GotoCommitExecute</c>
///  with <c>FormGoToCommit</c>, and <c>RevisionGridControl.GoToMergeBase</c>.
/// </summary>
public static class GoToCommit
{
    // Upstream's texts.
    public const string NoMergeBaseCommit = "There is no common ancestor for the selected commits.";
    public const string NoRevisionFound = "No revision found.";
    public const string CannotFindRevisionCaption = "Cannot find revision";
    public const string GitRevParseUrl = "https://git-scm.com/docs/git-rev-parse#_specifying_revisions";

    /// <summary>
    ///  Upstream's limit of the tag and branch drop-downs.
    /// </summary>
    public const int MaxDropDownCount = 1_000;

    /// <summary>
    ///  Upstream's <c>LoadTagsAsync</c> and <c>LoadBranchesAsync</c>: the first refs of the kind, as the drop-downs list them.
    /// </summary>
    public static IReadOnlyList<IGitRef> Refs(IGitModule module, RefsFilter filter)
        => [.. module.GetRefs(filter).Take(MaxDropDownCount)];

    /// <summary>
    ///  Upstream's <c>ValidateAndGetSelectedObjectId</c>: the commit the expression names, zero when git knows none.
    /// </summary>
    public static ObjectId Resolve(IGitModule module, string revision)
        => revision.Trim() is { Length: > 0 } trimmed ? module.RevParse(trimmed) : default;

    /// <summary>
    ///  Upstream's <c>GoToMergeBase</c> arguments: the selected commits, artificial ones as HEAD, filled with HEAD to two;
    ///  <c>--octopus</c> for more than two. Null when there is nothing to look for.
    /// </summary>
    /// <param name="selectedHashes">The selected rows, artificial ones included.</param>
    /// <param name="head">HEAD's commit, zero without one.</param>
    public static GitArgumentBuilder? MergeBaseArguments(IReadOnlyList<string> selectedHashes, ObjectId head)
    {
        List<string> revisions = [.. selectedHashes.Where(hash => !ArtificialCommits.IsArtificial(hash))];
        bool hasArtificial = selectedHashes.Any(ArtificialCommits.IsArtificial);
        if (head.IsZero || (revisions.Count == 0 && !hasArtificial))
        {
            return null;
        }

        return new GitArgumentBuilder("merge-base")
        {
            { revisions.Count > 2 || (revisions.Count == 2 && hasArtificial), "--octopus" },
            { revisions.Count < 1, head.ToString() },
            { revisions.Count < 2, head.ToString() },
            revisions
        };
    }

    /// <summary>
    ///  Upstream's <c>GoToMergeBase</c>: the common ancestor of the selected commits (one commit with HEAD).
    /// </summary>
    public static MergeBaseResult MergeBase(IGitModule module, IReadOnlyList<string> selectedHashes)
    {
        if (MergeBaseArguments(selectedHashes, module.RevParse("HEAD")) is not { } args)
        {
            return MergeBaseResult.None;
        }

        ExecutionResult result = module.GitExecutable.Execute(args, throwOnErrorExit: false);

        // Upstream's exit code for histories without a common ancestor.
        const int noCommonAncestorsExitCode = 1;
        if (result.ExitCode == noCommonAncestorsExitCode)
        {
            return new MergeBaseResult(NoCommonAncestor: true, Hash: null);
        }

        result.ThrowIfErrorExit();
        string hash = result.StandardOutput.TrimEnd();
        return string.IsNullOrWhiteSpace(hash)
            ? new MergeBaseResult(NoCommonAncestor: true, Hash: null)
            : new MergeBaseResult(NoCommonAncestor: false, Hash: ObjectId.Parse(hash).ToString());
    }
}
