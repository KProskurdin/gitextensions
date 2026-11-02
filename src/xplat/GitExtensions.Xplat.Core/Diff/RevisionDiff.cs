using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  A file changed between two revisions: its status letter as git's <c>--name-status</c> shows it, its path, the path it
///  had for a rename or copy, and whether git tracks it (a work tree file that is not tracked is diffed against nothing).
/// </summary>
public sealed record RevisionDiffFile(string Status, string Path, string? OldPath, bool IsTracked)
{
    public string Display => OldPath is null ? $"{Status} {Path}" : $"{Status} {OldPath} -> {Path}";

    /// <summary>
    ///  Upstream's status image for the file.
    /// </summary>
    public string Image => Repository.FileStatusList.ImageForStatus(Status);
}

/// <summary>
///  Upstream's <c>FormDiff</c> without its window: the files changed from a BASE revision to a compared one (either may be
///  the work tree or the index), a file's diff between them, and their merge base.
/// </summary>
public static class RevisionDiff
{
    // Upstream's texts (FormDiff and the grid's compare items).
    public const string WorkingDirectory = "Working directory";
    public const string CompareToMergeBase = "Compare to merge _base";
    public const string NoRevisionFound = "No revision found.";
    public const string BaseNotSelected = "Base commit for compare is not selected.";
    public const string NoBranchSelected = "No branch is currently selected";
    public const string WorkTreeToItself = "Cannot diff working directory to itself";
    public const string TwoCommitsToCompare = "You must have two commits selected to compare";

    /// <summary>
    ///  Upstream's <c>FormDiff</c> merge base: of the two revisions (an artificial one counts as HEAD), none when either is
    ///  missing or both are the same commit.
    /// </summary>
    public static ObjectId? MergeBase(GitModule module, ObjectId first, ObjectId second)
    {
        ObjectId head = first.IsArtificial || second.IsArtificial ? module.GetCurrentCheckout() : default;
        ObjectId firstMergeId = first.IsArtificial ? head : first;
        ObjectId secondMergeId = second.IsArtificial ? head : second;
        if (firstMergeId.IsZero || secondMergeId.IsZero || firstMergeId == secondMergeId)
        {
            return null;
        }

        ObjectId mergeBase = module.GetMergeBase(firstMergeId, secondMergeId);
        return mergeBase.IsZero ? null : mergeBase;
    }

    /// <summary>
    ///  Upstream's <c>FileStatusList.SetDiffsAsync</c> for two revisions (<c>GetDiffFilesWithUntracked</c>): the changed
    ///  files, with the work tree's untracked files when either side is the work tree. A git error is thrown.
    /// </summary>
    public static IReadOnlyList<RevisionDiffFile> Files(GitModule module, ObjectId first, ObjectId second)
    {
        IReadOnlyList<GitItemStatus> items = module.GetDiffFilesWithUntracked(first.ToString(), second.ToString(),
            StagedStatus.None);
        if (items.FirstOrDefault(item => item.IsStatusOnly && item.ErrorMessage is not null) is { } error)
        {
            throw new GitOperationException(error.ErrorMessage!.Trim());
        }

        return
        [
            .. items.Where(item => !item.IsStatusOnly)
                .Select(item => new RevisionDiffFile(StatusOf(item), item.Name,
                    item.IsRenamed || item.IsCopied ? item.OldName : null,
                    item.IsTracked || (!item.TreeId.IsZero && !second.IsZero)))
        ];
    }

    /// <summary>
    ///  Upstream's <c>GetItemPatchAsync</c>: <paramref name="file"/>'s diff from <paramref name="first"/> to
    ///  <paramref name="second"/>, with the diff options' arguments, as lines.
    /// </summary>
    public static async Task<IReadOnlyList<DiffLine>> DiffAsync(GitModule module, ObjectId first, ObjectId second,
        RevisionDiffFile file, DiffOptions options, CancellationToken cancellationToken = default)
    {
        (Patch? patch, string? error) = await module.GetSingleDiffAsync(first, second, file.Path, file.OldPath,
            string.Join(" ", options.Arguments), module.FilesEncoding, cacheResult: false, file.IsTracked,
            useGitColoring: false, GitCommandConfiguration.Default, cancellationToken);
        if (patch is null && error is { Length: > 0 })
        {
            throw new GitOperationException(error.Trim());
        }

        return DiffParser.Parse(patch?.Text ?? "");
    }

    /// <summary>
    ///  Upstream's <c>PopulateDiffFiles</c>: the directory diff tool cannot start from the work tree (git for Windows fails
    ///  with <c>-R</c>), so it is off then.
    /// </summary>
    public static bool CanOpenDirectoryDiff(ObjectId first) => first != ObjectId.WorkTreeId;

    private static string StatusOf(GitItemStatus item)
        => item.IsNew ? "A"
            : item.IsDeleted ? "D"
            : item.IsRenamed ? "R"
            : item.IsCopied ? "C"
            : "M";
}
