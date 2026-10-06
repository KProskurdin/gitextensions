namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Reads commits of a repository. Implementations run the work off the calling thread.
/// </summary>
public interface ICommitHistory
{
    /// <summary>
    ///  Reads up to <paramref name="limit"/> commits that <paramref name="filter"/> selects, or reachable from HEAD when there is
    ///  no filter. <see cref="CommitPage.HasMore"/> tells whether more exist.
    /// </summary>
    Task<CommitPage> LoadPageAsync(string repositoryPath, int limit, RevisionFilter? filter = null);

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
    ///  Finds up to <paramref name="limit"/> commits reachable from HEAD whose message contains <paramref name="text"/>, ignoring case.
    /// </summary>
    Task<CommitPage> SearchAsync(string repositoryPath, string text, int limit);

    /// <summary>
    ///  Who last changed each line of <paramref name="filePath"/> as of <paramref name="hash"/>.
    /// </summary>
    Task<IReadOnlyList<BlameLine>> LoadBlameAsync(string repositoryPath, string hash, string filePath);

    /// <summary>
    ///  The content of <paramref name="filePath"/> as of <paramref name="hash"/>, or null when the file is binary.
    /// </summary>
    Task<string?> LoadFileTextAsync(string repositoryPath, string hash, string filePath);
}

public sealed record CommitPage(IReadOnlyList<CommitRow> Rows, bool HasMore);

public sealed record CommitRow(string Hash, string ShortHash, string Subject, string Author, string Date, IReadOnlyList<string>? ParentHashes = null, IReadOnlyList<string>? Refs = null, IReadOnlyList<RefLabel>? Labels = null);

public enum RefKind
{
    Head,
    Branch,
    RemoteBranch,
    Tag,
}

/// <summary>
///  A name that points at a commit, as the revision grid labels it: HEAD, a local or remote branch, or a tag.
/// </summary>
public sealed record RefLabel(string Name, RefKind Kind)
{
    /// <summary>
    ///  Classifies a full reference name such as "refs/heads/main"; the label shows its short name.
    /// </summary>
    public static RefLabel? FromRefName(string refName) => refName switch
    {
        _ when refName.StartsWith("refs/heads/", StringComparison.Ordinal) => new(refName["refs/heads/".Length..], RefKind.Branch),
        _ when refName.StartsWith("refs/remotes/", StringComparison.Ordinal) => new(refName["refs/remotes/".Length..], RefKind.RemoteBranch),
        _ when refName.StartsWith("refs/tags/", StringComparison.Ordinal) => new(refName["refs/tags/".Length..], RefKind.Tag),
        _ => null,
    };
}

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
