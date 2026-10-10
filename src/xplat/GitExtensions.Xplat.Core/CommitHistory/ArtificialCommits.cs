using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Upstream's artificial commits of the revision grid: the working directory (its changes not yet staged) and the commit
///  index (the staged ones), shown just above HEAD with HEAD as the index's parent (<c>RevisionGridControl
///  .AddArtificialRevisions</c>, upstream setting <c>revisiongraphshowworkingdirchanges</c>).
/// </summary>
public static class ArtificialCommits
{
    /// <summary>
    ///  Upstream's <c>TranslatedStrings.Workspace</c>.
    /// </summary>
    public const string WorkTreeSubject = "Working directory";

    /// <summary>
    ///  Upstream's <c>TranslatedStrings.Index</c>.
    /// </summary>
    public const string IndexSubject = "Commit index";

    public static string WorkTreeHash { get; } = ObjectId.WorkTreeId.ToString();

    public static string IndexHash { get; } = ObjectId.IndexId.ToString();

    public static bool IsArtificial(string hash) => hash == WorkTreeHash || hash == IndexHash;

    /// <summary>
    ///  The two rows, by the user's name; the index's parent is <paramref name="head"/> (none before the first commit).
    /// </summary>
    public static IReadOnlyList<CommitRow> Rows(string author, string? head)
        =>
        [
            new CommitRow(WorkTreeHash, "", WorkTreeSubject, author, "", [IndexHash], [], []),
            new CommitRow(IndexHash, "", IndexSubject, author, "", head is null ? [] : [head], [], []),
        ];

    /// <summary>
    ///  <paramref name="rows"/> with the artificial rows just before HEAD's row, as upstream inserts them; at the top of an
    ///  empty history. When HEAD is not among the rows (a filter hides it), the rows are returned as they are.
    /// </summary>
    public static IReadOnlyList<CommitRow> Insert(IReadOnlyList<CommitRow> rows, string author, string? head)
    {
        if (head is null)
        {
            return rows.Count == 0 ? Rows(author, null) : rows;
        }

        int index = rows.ToList().FindIndex(row => row.Hash == head);
        return index < 0 ? rows : [.. rows.Take(index), .. Rows(author, head), .. rows.Skip(index)];
    }
}
