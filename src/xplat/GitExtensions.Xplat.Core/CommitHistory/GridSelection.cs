using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  The revision grid's selected commits as upstream's <c>RevisionGridControl</c> sees them: in the order of its
///  <c>GetSelectedRevisions</c> (the latest selected first), the latest selected commit the menu and the commit info act
///  on (<c>LatestSelectedRevision</c>), and the pair a diff of the selection compares (<c>GetFirstAndSelected</c>).
/// </summary>
public sealed class GridSelection
{
    // In the order the rows were selected, the earliest first.
    private readonly List<CommitRow> _rows = [];

    /// <summary>
    ///  The selected commits, the latest selected first.
    /// </summary>
    public IReadOnlyList<CommitRow> Revisions => [.. Enumerable.Reverse(_rows)];

    /// <summary>
    ///  Upstream's <c>LatestSelectedRevision</c>: the commit selected last, or null.
    /// </summary>
    public CommitRow? Latest => _rows.Count > 0 ? _rows[^1] : null;

    /// <summary>
    ///  Follows the grid's selection: <paramref name="current"/> is what the grid has selected now and
    ///  <paramref name="added"/> what was just added to it, in the grid's order. Rows that are no longer selected leave,
    ///  added ones come last, and any other selected row not yet known (a selection the grid replaced at once) goes before
    ///  them.
    /// </summary>
    public void Update(IReadOnlyList<CommitRow> current, IReadOnlyList<CommitRow> added)
    {
        HashSet<string> selected = [.. current.Select(row => row.Hash)];
        HashSet<string> addedHashes = [.. added.Select(row => row.Hash)];
        _rows.RemoveAll(row => !selected.Contains(row.Hash) || addedHashes.Contains(row.Hash));
        HashSet<string> known = [.. _rows.Select(row => row.Hash)];
        _rows.AddRange(current.Where(row => !known.Contains(row.Hash) && !addedHashes.Contains(row.Hash)));
        _rows.AddRange(added.Where(row => selected.Contains(row.Hash)).DistinctBy(row => row.Hash));
    }

    /// <summary>
    ///  Upstream's <c>GetFirstAndSelected</c>: for one commit its first parent (null for a root commit) and the commit; for
    ///  several, the earliest selected and the latest selected.
    /// </summary>
    public static (string? First, CommitRow? Selected) FirstAndSelected(IReadOnlyList<CommitRow> revisions)
        => revisions.Count switch
        {
            0 => (null, null),
            1 => (revisions[0].ParentHashes is [{ } parent, ..] ? parent : null, revisions[0]),
            _ => (revisions[^1].Hash, revisions[0]),
        };

    /// <summary>
    ///  How many of a commit's ancestors upstream looks through for one the grid shows (<c>TryGetParents</c>).
    /// </summary>
    public const int AncestorSearchLimit = 50;

    /// <summary>
    ///  What the grid selects after its rows were read again, as upstream's <c>GetToBeSelectedRevisions</c> and
    ///  <c>SetToBeSelectedFromParents</c>: the commits selected before that it still shows; with none selected before, the
    ///  row labeled HEAD (upstream's current checkout); when it shows none of them, the first shown of the latest selected
    ///  commit's ancestors (the commit itself first), nothing for a working directory or index row.
    /// </summary>
    /// <param name="previous">The commits selected before, the earliest selected first.</param>
    /// <param name="shown">The rows the grid shows now.</param>
    /// <param name="ancestors">Reads a commit's first ancestors (<see cref="Ancestors"/>).</param>
    /// <returns>The commits to select, the earliest selected first.</returns>
    public static async Task<IReadOnlyList<string>> ToBeSelectedAsync(IReadOnlyList<string> previous,
        IReadOnlyList<CommitRow> shown, Func<string, Task<IReadOnlyList<string>>> ancestors)
    {
        HashSet<string> shownHashes = new(shown.Select(row => row.Hash), StringComparer.OrdinalIgnoreCase);
        if (previous.Count == 0)
        {
            return shown.FirstOrDefault(row => row.Labels?.Any(label => label.Kind == RefKind.Head) == true) is { } head
                ? [head.Hash]
                : [];
        }

        List<string> kept = [.. previous.Where(shownHashes.Contains)];
        if (kept.Count > 0 || ArtificialCommits.IsArtificial(previous[^1]))
        {
            return kept;
        }

        return (await ancestors(previous[^1])).FirstOrDefault(shownHashes.Contains) is { } ancestor ? [ancestor] : [];
    }

    /// <summary>
    ///  Upstream's <c>TryGetParents</c>: the commit and its first ancestors, newest first; none when git fails.
    /// </summary>
    public static IReadOnlyList<string> Ancestors(IGitModule module, string hash)
    {
        ExecutionResult result = module.GitExecutable.Execute(
            new GitArgumentBuilder("rev-list") { $"--max-count={AncestorSearchLimit}", hash },
            throwOnErrorExit: false);
        return
        [
            .. result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => ObjectId.TryParse(line, out _))
        ];
    }
}
