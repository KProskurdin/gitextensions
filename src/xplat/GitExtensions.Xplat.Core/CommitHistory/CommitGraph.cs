using System.Security.Cryptography;
using System.Text;
using GitExtensions.Extensibility.Git;
using GitUI.UserControls.RevisionGrid.Graph;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  The revision graph of the loaded commits, built by upstream's <see cref="RevisionGraph"/> (lanes, shared lanes,
///  straightened lanes and diagonals) as upstream's revision grid builds it. Upstream reads its graph settings
///  (<c>MergeGraphLanesHavingCommonParent</c>, <c>StraightenGraphDiagonals</c>) when a graph is made.
/// </summary>
public sealed class CommitGraph
{
    private readonly RevisionGraph _graph;

    private CommitGraph(RevisionGraph graph, IReadOnlyList<CommitRow> orderedRows, int laneCount)
    {
        _graph = graph;
        OrderedRows = orderedRows;
        LaneCount = laneCount;
    }

    /// <summary>
    ///  The commits in the graph's order, which is the order upstream's grid shows: git's order, except that a commit
    ///  git listed before one of its children comes after it.
    /// </summary>
    public IReadOnlyList<CommitRow> OrderedRows { get; }

    /// <summary>
    ///  The most lanes any row uses, at most upstream's limit; every row's cell is this wide.
    /// </summary>
    public int LaneCount { get; }

    internal RevisionGraphConfig Config => _graph.Config;

    public static CommitGraph Build(IReadOnlyList<CommitRow> rows)
    {
        RevisionGraph graph = new();
        if (rows.FirstOrDefault(row => row.Labels?.Any(label => label.Kind == RefKind.Head) is true) is { } head)
        {
            graph.HeadId = IdOf(head.Hash);
        }

        Dictionary<ObjectId, CommitRow> byId = [];
        foreach (CommitRow row in rows)
        {
            ObjectId id = IdOf(row.Hash);
            if (!byId.TryAdd(id, row))
            {
                continue;
            }

            graph.Add(new GitRevision(id) { ParentIds = [.. (row.ParentHashes ?? []).Select(IdOf)] });
        }

        // The page is all that is read. As in upstream's renderer, a lane to a parent beyond it is not drawn.
        graph.LoadingCompleted();
        if (byId.Count > 0)
        {
            graph.CacheTo(byId.Count - 1, byId.Count - 1);
        }

        List<CommitRow> ordered = new(byId.Count);
        int laneCount = 0;
        for (int index = 0; index < byId.Count; index++)
        {
            if (graph.GetNodeForRow(index) is { } node && byId.TryGetValue(node.Objectid, out CommitRow? row))
            {
                ordered.Add(row);
            }

            if (graph.GetSegmentsForRow(index) is { } segments)
            {
                laneCount = Math.Max(laneCount, segments.GetLaneCount());
            }
        }

        return new CommitGraph(graph, ordered, Math.Min(laneCount, RevisionGraph.MaxLanes));
    }

    /// <summary>
    ///  The cell of the row at <paramref name="index"/> in <see cref="OrderedRows"/>.
    /// </summary>
    public GraphRowRef RowAt(int index)
    {
        CommitRow row = OrderedRows[index];
        return new GraphRowRef(this, index,
            HasRefs: row.Labels?.Any(label => label.Kind != RefKind.Head) is true,
            IsHead: row.Labels?.Any(label => label.Kind == RefKind.Head) is true);
    }

    internal IRevisionGraphRow? SegmentsFor(int index) => _graph.GetSegmentsForRow(index);

    // git gives full hashes; other text (a test's short names) gets a stable id of its own, so the graph still links rows.
    private static ObjectId IdOf(string hash)
        => ObjectId.TryParse(hash, out ObjectId id)
            ? id
            : ObjectId.Parse(Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(hash))));
}

/// <summary>
///  A row's place in a <see cref="CommitGraph"/>: what its graph cell draws. Upstream draws a commit with refs as a
///  square and the checked-out commit with an outline.
/// </summary>
public sealed record GraphRowRef(CommitGraph Graph, int Index, bool HasRefs, bool IsHead);
