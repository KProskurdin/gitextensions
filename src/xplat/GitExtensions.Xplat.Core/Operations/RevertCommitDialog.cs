using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's <c>FormRevertCommit</c> without its window: the command, and keeping a commit message that was already
///  prepared before the revert.
/// </summary>
public static class RevertCommitDialog
{
    /// <summary>
    ///  Upstream's <c>Commands.Revert</c>: <paramref name="parentIndex"/> is the 1-based parent of a merge, or 0.
    /// </summary>
    public static ArgumentString Arguments(ObjectId commit, bool autoCommit, int parentIndex)
        => Commands.Revert(commit, autoCommit, parentIndex);

    /// <summary>
    ///  The message prepared before the revert (.git/MERGE_MSG, else .git/COMMITMESSAGE), as upstream reads it first.
    /// </summary>
    public static Task<string> ReadPreparedMessageAsync(GitModule module)
        => Manager(module).GetMergeOrCommitMessageAsync();

    /// <summary>
    ///  Upstream's step after the revert: a message that was prepared before is put above git's revert message, so it is
    ///  not lost; nothing happens without one. Errors are ignored, as upstream's.
    /// </summary>
    public static async Task KeepPreparedMessageAsync(GitModule module, string preparedMessage)
    {
        if (string.IsNullOrWhiteSpace(preparedMessage))
        {
            return;
        }

        try
        {
            ICommitMessageManager manager = Manager(module);
            string message = await manager.GetMergeOrCommitMessageAsync();
            await manager.WriteCommitMessageToFileAsync($"{preparedMessage}\n\n{message}", CommitMessageType.Merge,
                usingCommitTemplate: false, ensureCommitMessageSecondLineEmpty: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // As upstream's, through its CommitMessageManager (the owner control is only used for its dialogs).
    private static ICommitMessageManager Manager(GitModule module)
        => new CommitMessageManager(new System.Windows.Forms.Control(), module.WorkingDirGitDir, module.CommitEncoding);
}
