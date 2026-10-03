namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Changes a repository or talks to its remotes. Implementations run each operation off the calling thread and throw
///  <see cref="GitOperationException"/> with git's own message when git exits with an error.
/// </summary>
public interface IGitOperations
{
    Task StageAsync(string repositoryPath, IReadOnlyList<string> paths);

    Task UnstageAsync(string repositoryPath, IReadOnlyList<string> paths);

    Task CommitAsync(string repositoryPath, string message, bool amend);

    Task CreateBranchAsync(string repositoryPath, string name, bool checkout);

    Task CheckoutAsync(string repositoryPath, string branch);

    /// <summary>
    ///  Creates a local branch that tracks <paramref name="remoteBranch"/> (e.g. "origin/feature") and checks it out.
    /// </summary>
    Task CheckoutRemoteAsync(string repositoryPath, string remoteBranch);

    Task DeleteBranchAsync(string repositoryPath, string branch, bool force);

    Task FetchAsync(string repositoryPath, string remote);

    Task PullAsync(string repositoryPath, string remote, string branch, bool rebase);

    Task PushAsync(string repositoryPath, string remote, string branch);

    Task CloneAsync(string sourceUrl, string targetPath);
}

public sealed class GitOperationException(string message) : Exception(message);
