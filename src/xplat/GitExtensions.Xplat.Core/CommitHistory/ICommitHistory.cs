namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Reads commits of a repository. Implementations run the work off the calling thread.
/// </summary>
public interface ICommitHistory
{
    /// <summary>
    ///  Reads up to <paramref name="limit"/> commits reachable from HEAD. <see cref="CommitPage.HasMore"/> tells whether more exist.
    /// </summary>
    Task<CommitPage> LoadPageAsync(string repositoryPath, int limit);

    Task<CommitDetails> LoadDetailsAsync(string repositoryPath, string hash);
}

public sealed record CommitPage(IReadOnlyList<CommitRow> Rows, bool HasMore);

public sealed record CommitRow(string Hash, string ShortHash, string Subject, string Author, string Date);

public sealed record CommitDetails(string Hash, string Author, string AuthorDate, string CommitDate, string Parents, string Message);
