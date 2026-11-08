using GitCommands;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  A group of upstream's <c>FileStatusList</c> (<c>FileStatusWithDescription</c>): the files changed from
///  <paramref name="First"/> to <paramref name="Second"/>, with upstream's summary and icon. <paramref name="First"/> is
///  null for a root commit (its files are new), <see cref="ObjectId.CombinedDiffId"/> for a merge's combined diff.
/// </summary>
public sealed record DiffGroup(ObjectId? First, ObjectId Second, string Summary, string Icon,
    IReadOnlyList<RevisionDiffFile> Files)
{
    /// <summary>
    ///  Upstream's "Combined diff" group of a merge: the files that differ from every parent, diffed with
    ///  <c>diff-tree --cc</c>.
    /// </summary>
    public bool IsCombinedDiff => First == ObjectId.CombinedDiffId;

    /// <summary>
    ///  Upstream's "grep" group: the files of <see cref="Second"/> that git-grep matched.
    /// </summary>
    public bool IsGrep => Icon == GrepIcon;

    /// <summary>
    ///  Upstream's image of the "grep" group (its <c>GitGrepIconName</c> stands for <c>Images.ViewFile</c>).
    /// </summary>
    public const string GrepIcon = "ViewFile";

    /// <summary>
    ///  Upstream's image of the range diff item and its group (<c>Images.DiffR</c>).
    /// </summary>
    public const string RangeDiffIcon = "DiffR";

    /// <summary>
    ///  Upstream's range diff group, whose one item the list shows without a group node (<c>GetNodes</c>).
    /// </summary>
    public bool IsRangeDiff => Icon == RangeDiffIcon;

    /// <summary>
    ///  Upstream's <c>GetGroupName</c>: the shown and the total count (only the total when all are shown) and the summary.
    /// </summary>
    public string Header(int shown) => $"({(shown >= Files.Count ? "" : $"{shown}/")}{Files.Count}) {Summary}";

    /// <summary>
    ///  Upstream's <c>GetNodes</c> state of the group at <paramref name="index"/> of <paramref name="count"/> groups:
    ///  expanded when it has files and is the first, one of fewer than three, or a plain diff of at most seven files.
    /// </summary>
    public bool ExpandedAt(int index, int count)
        => Files.Count > 0 && ((Files.Count <= 7 && Icon == PlainDiffIcon) || count < 3 || index == 0);

    // Upstream's icon of a group between two revisions (Images.Diff), as against BASE (DiffA, DiffB) or combined (DiffC).
    private const string PlainDiffIcon = "Diff";
}

/// <summary>
///  Upstream's <c>FileStatusDiffCalculator</c>, linked unchanged, for the browse window's Diff tab: for one commit a group
///  against each parent (only the first unless upstream's <c>ShowDiffForAllParents</c>) and a merge's combined diff; for
///  two to four commits the earliest against the latest and, with <c>ShowDiffForAllParents</c>, the others against the
///  latest or, when they are on separate branches, BASE against each of them.
/// </summary>
public static class DiffGroups
{
    private const string NoPatchArgument = "--no-patch";

    // Upstream's CombinedDiffHighlightService.GetGitCommandConfiguration.
    private const string CombinedDiffCommand = "diff-tree";

    /// <summary>
    ///  The groups of <paramref name="revisions"/> (the latest selected first, as upstream's <c>GetSelectedRevisions</c>),
    ///  each as <paramref name="describe"/> names its revision.
    /// </summary>
    /// <param name="grep">Upstream's git-grep argument (<see cref="GitGrep.SearchArgument"/>); with one, the last group is
    /// upstream's "grep" group of the latest selected revision's matching files.</param>
    /// <param name="showSkipWorktreeFiles">Upstream's "Show skip-worktree files".</param>
    /// <param name="showUntrackedFiles">Upstream's "Show untracked files".</param>
    public static IReadOnlyList<DiffGroup> Calculate(GitModule module, IReadOnlyList<GitRevision> revisions,
        ObjectId headId, Func<ObjectId, string> describe, CancellationToken cancellationToken = default, string grep = "",
        bool showSkipWorktreeFiles = false, bool showUntrackedFiles = true)
    {
        FileStatusDiffCalculator calculator = new(() => module)
        {
            DescribeRevision = describe,

            // Upstream's grid rewrites the parents of a filtered history; the new shell reads them from git.
            GetActualRevision = revision => revision.IsArtificial
                ? revision
                : module.GetRevision(revision.ObjectId, shortFormat: true),
        };
        calculator.SetDiff(revisions, headId, allowMultiDiff: true, showSkipWorktreeFiles, showUntrackedFiles);
        calculator.SetGrep(grep, fileTreeMode: false);
        return
        [
            .. calculator.Calculate([], refreshDiff: true, refreshGrep: grep.Length > 0, cancellationToken)
                .Select(group => new DiffGroup(group.FirstRev?.ObjectId, group.SecondRev.ObjectId, group.Summary,
                    FileStatusDiffCalculator.IsGrepItemStatuses(group) ? DiffGroup.GrepIcon : group.IconName,
                    [.. group.Statuses.Where(item => !item.IsStatusOnly).Select(item => File(item, group))]))
        ];
    }

    /// <summary>
    ///  Upstream's <c>GetItemPatchAsync</c> for a file of <paramref name="group"/>: the combined diff of the merge for that
    ///  group, else the diff between its two revisions (a root commit's files against nothing).
    /// </summary>
    public static Task<IReadOnlyList<DiffLine>> DiffAsync(GitModule module, DiffGroup group, RevisionDiffFile file,
        DiffOptions options, CancellationToken cancellationToken = default)
    {
        if (!group.IsCombinedDiff)
        {
            return RevisionDiff.DiffAsync(module, group.First ?? default, group.Second, file, options, cancellationToken);
        }

        return Task.Run<IReadOnlyList<DiffLine>>(() =>
        {
            // Upstream's isCombinedDiff: no word diff; CombinedDiffHighlightService's colors.
            DiffOptions combined = options with { Appearance = DiffDisplayAppearance.Patch };
            module.GetCombinedDiffContent(group.Second, file.Path, string.Join(" ", combined.Arguments),
                combined.EncodingFor(module), out string diff, combined.UsesGitColors,
                combined.UsesGitColors ? GitColoring.Configuration(module, CombinedDiffCommand) : GitCommandConfiguration.Default,
                cancellationToken);
            return InlineDifferences.Add(
                combined.UsesGitColors ? GitColoring.ParseDiff(diff, isCombinedDiff: true) : DiffParser.ParseCombined(diff),
                combined);
        }, cancellationToken);
    }

    /// <summary>
    ///  Upstream's <c>ViewChangesAsync</c> for the range diff item: what <c>git range-diff</c> says about the two revisions
    ///  of <paramref name="group"/>, in git's colors, or its error.
    /// </summary>
    /// <param name="pathFilter">Upstream's <c>_pathFilter</c>: the grid's path filter, where git supports it.</param>
    public static async Task<IReadOnlyList<DiffLine>> RangeDiffAsync(GitModule module, DiffGroup group, DiffOptions options,
        string pathFilter, CancellationToken cancellationToken = default)
    {
        // Upstream's GetExtraDiffArguments(isRangeDiff: true): no context lines means only the summary.
        IEnumerable<string> arguments = options.ContextLines == 0
            ? [.. options.Arguments, NoPatchArgument]
            : options.Arguments;
        ExecutionResult result = await module.GetRangeDiffAsync(group.First ?? default, group.Second, firstBase: default,
            secondBase: default, string.Join(" ", arguments), module.GitVersion.SupportRangeDiffPath ? pathFilter : "",
            useGitColoring: true, GitColoring.Configuration(module, GitColoring.RangeDiffCommand), cancellationToken);
        if (!result.ExitedSuccessfully)
        {
            return DiffParser.ParseText($"{result.StandardError}{Environment.NewLine}Git output (exit code: {result.ExitCodeDisplay}): {Environment.NewLine}{result.StandardOutput}");
        }

        return GitColoring.ParseRangeDiff(GitModule.ReEncodeStringFromLossless(result.StandardOutput, options.EncodingFor(module)) ?? "");
    }

    private static RevisionDiffFile File(GitItemStatus item, FileStatusWithDescription group)
        => new(item.IsNew ? "A"
                : item.IsDeleted ? "D"
                : item.IsRenamed ? "R"
                : item.IsCopied ? "C"
                : "M",
            item.Name, item.IsRenamed || item.IsCopied ? item.OldName : null,
            item.IsTracked || (!item.TreeId.IsZero && !group.SecondRev.ObjectId.IsZero))
        {
            DiffStatus = item.DiffStatus,
            GrepString = string.IsNullOrWhiteSpace(item.GrepString) ? null : item.GrepString,
            IsSubmodule = item.IsSubmodule,
            IsRangeDiff = item.IsRangeDiff,
        };
}
