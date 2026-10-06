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
    ///  Forgets the draft after a commit.
    /// </summary>
    Task ResetAsync(string repositoryPath);
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

    // The owner control only parents upstream's error message box; the shim's Control stands in for it.
    private static CommitMessageManager Manager(string repositoryPath)
    {
        GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath);
        return new CommitMessageManager(new System.Windows.Forms.Control(), module.WorkingDirGitDir, module.CommitEncoding);
    }
}
