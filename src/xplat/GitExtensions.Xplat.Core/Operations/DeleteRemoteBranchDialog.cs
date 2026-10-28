using System.Text;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's <c>FormDeleteRemoteBranch</c> without its window: the local branches that track the selected remote
///  branches, the text listing them, and the selected branches by remote.
/// </summary>
public static class DeleteRemoteBranchDialog
{
    // Upstream's texts.
    public const string DeleteRemoteBranchesCaption = "Delete remote branches";
    public const string ToDeleteCandidates = "Local tracking branche(s) candidate to deletion:";
    public const string AndMore = "and {0} more...";

    /// <summary>
    ///  Upstream's <c>_confirmDeleteUnmergedRemoteBranchMessage</c>.
    /// </summary>
    public static string ConfirmDeleteUnmergedRemoteBranch { get; } =
        "At least one remote branch is unmerged. Are you sure you want to delete it?" + Environment.NewLine
        + "Deleting a branch can cause commits to be deleted too!";

    // Upstream lists this many candidates and counts the rest.
    private const int MaxDisplayed = 8;

    /// <summary>
    ///  Upstream's <c>GetTrackingReferenceOfRemoteRefs</c>: the local branches tracking one of <paramref name="remotes"/>.
    /// </summary>
    public static IReadOnlyList<string> TrackingBranches(IEnumerable<IGitRef> heads, IReadOnlyList<IGitRef> remotes)
        => [.. heads.Where(head => remotes.Any(head.IsTrackingRemote)).Select(head => head.LocalName)];

    /// <summary>
    ///  Upstream's <c>CheckDeleteTrackingAllowed</c> text: the first candidates and how many more; empty without any.
    /// </summary>
    public static string CandidatesText(IReadOnlyList<string> tracking, Func<string, string>? translate = null)
    {
        if (tracking.Count == 0)
        {
            return "";
        }

        translate ??= text => text;
        StringBuilder text = new();
        text.AppendLine(translate(ToDeleteCandidates));
        foreach (string branch in tracking.Take(MaxDisplayed))
        {
            text.Append(" - ").AppendLine(branch);
        }

        if (tracking.Count > MaxDisplayed)
        {
            text.AppendLine().AppendFormat(translate(AndMore), tracking.Count - MaxDisplayed);
        }

        return text.ToString();
    }

    /// <summary>
    ///  Upstream's <c>Delete_Click</c>: true when one of the branches is not merged (its complete name is not among
    ///  <c>GetMergedRemoteBranches</c>).
    /// </summary>
    public static bool HasUnmerged(IEnumerable<IGitRef> selected, IReadOnlyCollection<string> merged)
        => selected.Any(branch => !merged.Contains(branch.CompleteName));

    /// <summary>
    ///  The selected branches by remote, each pushed once, as upstream's <c>GroupBy(b =&gt; b.Remote)</c>.
    /// </summary>
    public static IReadOnlyList<(string Remote, IReadOnlyList<string> Branches)> ByRemote(IEnumerable<IGitRef> selected)
        => [.. selected.GroupBy(branch => branch.Remote)
            .Select(group => (group.Key, (IReadOnlyList<string>)[.. group.Select(branch => branch.LocalName)]))];
}
