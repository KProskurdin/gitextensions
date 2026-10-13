using GitCommands;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Reads commits of a repository. Implementations run the work off the calling thread.
/// </summary>
public interface ICommitHistory
{
    /// <summary>
    ///  Reads up to <paramref name="limit"/> commits that <paramref name="filter"/> selects, or reachable from HEAD when there is
    ///  no filter, after the first <paramref name="skip"/> of them. <see cref="CommitPage.HasMore"/> tells whether more exist.
    /// </summary>
    Task<CommitPage> LoadPageAsync(string repositoryPath, int limit, RevisionFilter? filter = null, int skip = 0);

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
    ///  Renames are followed as the history options say.
    /// </summary>
    Task<CommitPage> LoadFileHistoryAsync(string repositoryPath, string hash, string filePath, int limit);

    /// <summary>
    ///  Finds up to <paramref name="limit"/> commits reachable from HEAD whose message contains <paramref name="text"/>, ignoring case.
    /// </summary>
    Task<CommitPage> SearchAsync(string repositoryPath, string text, int limit);

    /// <summary>
    ///  Who last changed each line of <paramref name="filePath"/> as of <paramref name="hash"/>, blamed with the git flags
    ///  of <paramref name="options"/> (upstream's defaults when none are given).
    /// </summary>
    Task<IReadOnlyList<BlameLine>> LoadBlameAsync(string repositoryPath, string hash, string filePath,
        BlameOptions? options = null);

    /// <summary>
    ///  The content of <paramref name="filePath"/> as of <paramref name="hash"/>, or null when the file is binary.
    /// </summary>
    Task<string?> LoadFileTextAsync(string repositoryPath, string hash, string filePath);
}

/// <param name="FilePaths">For a file history that follows renames: the path the file had in each commit, by commit hash.</param>
public sealed record CommitPage(
    IReadOnlyList<CommitRow> Rows,
    bool HasMore,
    IReadOnlyDictionary<string, string>? FilePaths = null);

/// <summary>
///  How a file history is read: upstream's <c>FollowRenamesInFileHistory</c> (on by default) and
///  <c>FollowRenamesInFileHistoryExactOnly</c>.
/// </summary>
public sealed record FileHistoryOptions(bool FollowRenames = true, bool ExactRenamesOnly = false);

public sealed record CommitRow(
    string Hash,
    string ShortHash,
    string Subject,
    string Author,
    string Date,
    IReadOnlyList<string>? ParentHashes = null,
    IReadOnlyList<string>? Refs = null,
    IReadOnlyList<RefLabel>? Labels = null,
    RevisionTooltip? Tooltip = null,
    string? Body = null,
    bool HasMultiLineMessage = false,
    string? AuthorEmail = null);

public enum RefKind
{
    Head,
    Branch,
    RemoteBranch,
    Tag,

    /// <summary>
    ///  A commit marked good during a bisect (refs/bisect/good-*). Upstream draws an icon for it.
    /// </summary>
    BisectGood,

    /// <summary>
    ///  The commit marked bad during a bisect (refs/bisect/bad).
    /// </summary>
    BisectBad,
}

/// <summary>
///  A name that points at a commit, as the revision grid labels it: HEAD, a local or remote branch, a tag, or a bisect
///  mark.
/// </summary>
public sealed record RefLabel(string Name, RefKind Kind)
{
    public const string BisectGoodName = "good";
    public const string BisectBadName = "bad";

    /// <summary>
    ///  True for a bisect mark, which is not a name git can check out or delete.
    /// </summary>
    public bool IsBisect => Kind is RefKind.BisectGood or RefKind.BisectBad;

    /// <summary>
    ///  The checked-out branch (upstream's <c>IGitRef.IsSelected</c>), which the grid draws bold with a filled arrow.
    /// </summary>
    public bool IsCurrent { get; init; }

    /// <summary>
    ///  For a local branch, the remote branch it tracks ("origin/main"); null when it tracks none.
    /// </summary>
    public string? Upstream { get; init; }

    /// <summary>
    ///  For a remote branch, the name of its remote.
    /// </summary>
    public string? Remote { get; init; }

    /// <summary>
    ///  Upstream's ahead and behind arrows for the label next to this one (<c>AheadBehindData.ToDisplay</c> without
    ///  counts): for a local branch from its tracked remote branch's side, for a remote branch from the side of the local
    ///  branch tracking it; null when there is none.
    /// </summary>
    public string? AheadBehind { get; init; }

    /// <summary>
    ///  Classifies a full reference name such as "refs/heads/main"; the label shows its short name.
    /// </summary>
    public static RefLabel? FromRefName(string refName) => refName switch
    {
        _ when refName.StartsWith("refs/heads/", StringComparison.Ordinal) => new(refName["refs/heads/".Length..],
            RefKind.Branch),
        _ when refName.StartsWith("refs/remotes/", StringComparison.Ordinal) => new(refName["refs/remotes/".Length..],
            RefKind.RemoteBranch),
        _ when refName.StartsWith("refs/tags/", StringComparison.Ordinal) => new(refName["refs/tags/".Length..],
            RefKind.Tag),
        _ when refName.StartsWith(GitRefName.RefsBisectGoodPrefix, StringComparison.Ordinal) => new(BisectGoodName,
            RefKind.BisectGood),
        _ when refName.StartsWith(GitRefName.RefsBisectBadPrefix, StringComparison.Ordinal) => new(BisectBadName,
            RefKind.BisectBad),
        _ => null,
    };
}

/// <summary>
///  A commit's details. <see cref="Data"/> is upstream's model of it, from which the commit info header is drawn
///  (<see cref="CommitInfoText.Header"/>), and <see cref="MessageLines"/> its message with the hashes in it as links, as
///  upstream's <c>CommitDataBodyRenderer</c> renders it.
/// </summary>
public sealed record CommitDetails(
    string Hash,
    string Author,
    string AuthorDate,
    string CommitDate,
    string Parents,
    string Message,
    IReadOnlyList<RevisionLink>? Links = null)
{
    public GitUIPluginInterfaces.CommitData? Data { get; init; }

    public IReadOnlyList<IReadOnlyList<InfoSpan>>? MessageLines { get; init; }
}

/// <summary>
///  A link upstream's commit info lists under "Related links": the caption and the address.
/// </summary>
public sealed record RevisionLink(string Caption, string Uri);

public sealed record CommitFile(string Status, string Path)
{
    public string Display => $"{Status} {Path}";
}
