using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's <c>FormCherryPick</c> without its window: the command its options make.
/// </summary>
public static class CherryPickDialog
{
    // Upstream's text.
    public const string NoneParentSelected = "None parent is selected!";

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
