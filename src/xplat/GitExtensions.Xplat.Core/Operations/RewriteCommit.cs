using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's grid "Advanced" items "Edit commit" and "Reword commit" (<c>RevisionGridControl.LaunchRebase</c>): an
///  interactive rebase onto the commit's first parent whose todo list is changed without an editor, so the commit is
///  edited or reworded and the commits after it are picked again.
/// </summary>
public static class RewriteCommit
{
    /// <summary>
    ///  The todo command that stops at the commit so it can be changed.
    /// </summary>
    public const string Edit = "e";

    /// <summary>
    ///  The todo command that asks for the commit's new message.
    /// </summary>
    public const string Reword = "r";

    /// <summary>
    ///  Upstream's user manual page for these items (<c>getHelpOnHowToUseTheseFeaturesToolStripMenuItem_Click</c>).
    /// </summary>
    public const string HelpSubfolder = "modify_history";

    public const string HelpAnchor = "using-autosquash-rebase-feature";

    /// <summary>
    ///  Upstream's <c>LaunchRebase</c> command: interactive, with autostash, onto <paramref name="firstParent"/> (none for a
    ///  root commit, as upstream's, which git refuses).
    /// </summary>
    public static ArgumentString Arguments(ObjectId? firstParent, bool supportRebaseMerges)
        => Commands.Rebase(new Commands.RebaseOptions
        {
            BranchName = firstParent is { IsZero: false } parent ? parent.ToString() : null,
            Interactive = true,
            AutoStash = true,
            SupportRebaseMerges = supportRebaseMerges,
        });

    /// <summary>
    ///  Upstream's <c>GIT_SEQUENCE_EDITOR</c>: turns the todo list's first <c>pick</c> into <paramref name="command"/>. It
    ///  uses GNU sed's <c>0,/re/</c> address; macOS's sed has none, so there the same change is made by perl, which macOS
    ///  ships.
    /// </summary>
    public static string SequenceEditor(string command, bool onMac)
        => onMac
            ? $"perl -i -pe '$d = s/^pick/{command}/ unless $d'"
            : $"sed -i -re '0,/pick/s//{command}/'";
}
