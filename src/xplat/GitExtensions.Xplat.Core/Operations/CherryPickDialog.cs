using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  A parent of the merge to cherry pick or revert, as upstream's list shows it: its number, message, author and date.
/// </summary>
public sealed record CherryPickParent(int Number, string Subject, string Author, string Date);

/// <summary>
///  Upstream's <c>FormCherryPick</c> without its window: the command its options make.
/// </summary>
public static class CherryPickDialog
{
    // Upstream's text.
    public const string NoneParentSelected = "None parent is selected!";

    /// <summary>
    ///  Upstream's <c>OnRevisionChanged</c> (cherry pick) and <c>LoadRevisionInfo</c> (revert): the commit, whether it is a
    ///  merge, and a merge's parents numbered from one; no commit when git does not know it. Reads git, so it runs off the
    ///  UI thread.
    /// </summary>
    public static (GitRevision? Revision, bool IsMerge, IReadOnlyList<CherryPickParent> Parents) ReadCommit(
        IGitModule module, string? revision)
    {
        if (revision is null || module.RevParse(revision) is not { IsZero: false } id)
        {
            return (null, false, []);
        }

        GitRevision commit = module.GetRevision(id, shortFormat: true, loadRefs: true);
        bool merge = module.IsMerge(id);
        IReadOnlyList<GitRevision> parents = merge ? module.GetParentRevisions(id) : [];
        return (commit, merge,
        [
            .. parents.Select((parent, index) => new CherryPickParent(index + 1, parent.Subject, parent.Author ?? "",
                parent.CommitDate.ToShortDateString())),
        ]);
    }

    /// <summary>
    ///  Upstream's <c>btnPick_Click</c>: <c>-m</c> with the chosen parent of a merge (numbered from one), <c>-x</c> for the
    ///  commit reference, and <c>--no-commit</c> unless the commit is made at once.
    /// </summary>
    public static ArgumentString Arguments(ObjectId commit, bool autoCommit, int? parentNumber, bool addReference)
    {
        ArgumentBuilder options = [];
        if (parentNumber is { } parent)
        {
            options.Add("-m " + parent);
        }

        if (addReference)
        {
            options.Add("-x");
        }

        return GitCommands.Git.Commands.CherryPick(commit, autoCommit, options.ToString());
    }
}
