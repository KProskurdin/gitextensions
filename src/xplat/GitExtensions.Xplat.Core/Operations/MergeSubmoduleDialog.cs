using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's <c>FormMergeSubmodule</c> without its window: the base, local and remote commits of a conflicted
///  submodule, and whether a branch containing the local and remote commits can be checked out in it.
/// </summary>
public static class MergeSubmoduleDialog
{
    // Upstream's texts.
    public const string StageFilename = "Stage {0}";
    public const string Deleted = "deleted";

    /// <summary>
    ///  Upstream's text boxes: each side's commit, or "deleted" when the side has none.
    /// </summary>
    public static (string Base, string Local, string Remote) Commits(ConflictData item,
        Func<string, string>? translate = null)
    {
        translate ??= text => text;
        return (Text(item.Base), Text(item.Local), Text(item.Remote));

        string Text(ConflictedFileData side) => side.ObjectId.IsZero ? translate(Deleted) : side.ObjectId.ToString();
    }

    /// <summary>
    ///  Upstream enables "Checkout Branch" when the base and the remote side have commits.
    /// </summary>
    public static bool CanCheckoutBranch(ConflictData item) => !item.Base.ObjectId.IsZero && !item.Remote.ObjectId.IsZero;

    /// <summary>
    ///  The commits a branch must contain for upstream's "Checkout Branch": the local and the remote one.
    /// </summary>
    public static IReadOnlyList<ObjectId> BranchCommits(ConflictData item) => [item.Local.ObjectId, item.Remote.ObjectId];
}
