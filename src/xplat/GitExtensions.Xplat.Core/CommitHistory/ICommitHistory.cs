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

    /// <summary>
    ///  The files a commit changed, with their status letter as git reports it (M, A, D, R, ...).
    /// </summary>
    Task<IReadOnlyList<CommitFile>> LoadFilesAsync(string repositoryPath, string hash);

    /// <summary>
    ///  Every file in the tree of <paramref name="hash"/>, as repository-relative paths in order.
    /// </summary>
    Task<IReadOnlyList<string>> LoadTreeAsync(string repositoryPath, string hash);

    /// <summary>
    ///  Up to <paramref name="limit"/> commits reachable from <paramref name="hash"/> that changed <paramref name="filePath"/>.
    ///  Renames are not followed.
    /// </summary>
    Task<CommitPage> LoadFileHistoryAsync(string repositoryPath, string hash, string filePath, int limit);

    /// <summary>
    ///  Who last changed each line of <paramref name="filePath"/> as of <paramref name="hash"/>.
    /// </summary>
    Task<IReadOnlyList<BlameLine>> LoadBlameAsync(string repositoryPath, string hash, string filePath);
}

public sealed record CommitPage(IReadOnlyList<CommitRow> Rows, bool HasMore);

public sealed record CommitRow(string Hash, string ShortHash, string Subject, string Author, string Date);

public sealed record CommitDetails(
    string Hash,
    string Author,
    string AuthorDate,
    string CommitDate,
    string Parents,
    string Message);

public sealed record CommitFile(string Status, string Path)
{
    public string Display => $"{Status} {Path}";
}
