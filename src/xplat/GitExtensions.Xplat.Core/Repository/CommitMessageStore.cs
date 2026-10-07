using GitCommands;
using GitCommands.Git;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The commit message being prepared for a repository, kept between commit windows and app runs.
/// </summary>
public interface ICommitMessageStore
{
    /// <summary>
    ///  The prepared message: git's merge message while a merge is stopped, otherwise the draft saved last; empty when none.
    /// </summary>
    Task<string> LoadAsync(string repositoryPath);

    Task SaveAsync(string repositoryPath, string message);

    /// <summary>
    ///  Forgets the draft (and the amend state) after a commit.
    /// </summary>
    Task ResetAsync(string repositoryPath);

    /// <summary>
    ///  Whether the draft was left with Amend checked.
    /// </summary>
    Task<bool> LoadAmendAsync(string repositoryPath);

    Task SaveAmendAsync(string repositoryPath, bool amend);
}

/// <summary>
///  The commit message as upstream's commit window hands it to git.
/// </summary>
public static class CommitMessageFormat
{
    /// <summary>
    ///  Upstream's <c>CommitMessageManager.FormatCommitMessage</c>, used as is: every line ends with the platform's newline,
    ///  and with <paramref name="ensureSecondLineEmpty"/> a second line of text gets an empty line before it. The text box's
    ///  own line breaks are made plain first, as upstream's text box gives them. Commit templates are not supported yet, so
    ///  lines starting with '#' are kept.
    /// </summary>
    public static string Format(string message, bool ensureSecondLineEmpty)
        => CommitMessageManager.FormatCommitMessage(message.ReplaceLineEndings("\n"), usingCommitTemplate: false,
            ensureSecondLineEmpty);
}

/// <summary>
///  Keeps the message where upstream's <see cref="CommitMessageManager"/> does (it is used as is): <c>.git/COMMITMESSAGE</c>,
///  or <c>.git/MERGE_MSG</c> while a merge is stopped, so the WinForms app and the new shell share the draft and the commit
///  window starts from git's merge message.
/// </summary>
public sealed class UpstreamCommitMessageStore : ICommitMessageStore
{
    public Task<string> LoadAsync(string repositoryPath)
        => Task.Run(() => Manager(repositoryPath).GetMergeOrCommitMessageAsync());

    public Task SaveAsync(string repositoryPath, string message)
        => Task.Run(() => Manager(repositoryPath).SetMergeOrCommitMessageAsync(message));

    public Task ResetAsync(string repositoryPath)
        => Task.Run(() => Manager(repositoryPath).ResetCommitMessageAsync());

    // Upstream's .git/GitExtensions.amend. Upstream also checks its RememberAmendCommitState setting here; the commit window
    // checks the same setting through its preferences.
    public Task<bool> LoadAmendAsync(string repositoryPath)
        => Task.Run(() => Manager(repositoryPath).GetAmendStateAsync());

    public Task SaveAmendAsync(string repositoryPath, bool amend)
        => Task.Run(() => Manager(repositoryPath).SetAmendStateAsync(amend));

    // The owner control only parents upstream's error message box; the shim's Control stands in for it.
    private static CommitMessageManager Manager(string repositoryPath)
    {
        GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath);
        return new CommitMessageManager(new System.Windows.Forms.Control(), module.WorkingDirGitDir,
            module.CommitEncoding);
    }
}
