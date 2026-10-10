using System.Security.Cryptography;
using System.Text;
using GitCommands;
using GitCommands.Git;
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

    /// <summary>
    ///  How lanes are colored: upstream's <c>RevisionGraphDrawStyle</c>.
    /// </summary>
    public GraphDrawStyle DrawStyle { get; set; } = GraphDrawStyle.DrawNonRelativesGray;

    /// <summary>
    ///  The commits shown in their colors while a ref label is hovered (upstream's hover highlight), or null.
    /// </summary>
    public IReadOnlySet<ObjectId>? HoverHighlighted { get; set; }

    /// <summary>
    ///  Upstream's "Highlight selected branch": the ancestry of <paramref name="hash"/> becomes the relatives, drawn in their
    ///  colors while the rest is gray, until the graph is read again.
    /// </summary>
    public void HighlightBranch(string hash)
    {
        _graph.HighlightBranch(IdOf(hash));
        DrawStyle = GraphDrawStyle.HighlightSelected;
    }

    /// <summary>
    ///  The ancestry of the row at <paramref name="index"/> among the loaded commits, as upstream's hover highlight walks it.
    /// </summary>
    public IReadOnlySet<ObjectId> AncestryOf(int index)
    {
        HashSet<ObjectId> ancestry = [];
        if (_graph.GetNodeForRow(index) is not { } start)
        {
            return ancestry;
        }

        Stack<RevisionGraphRevision> pending = new([start]);
        while (pending.TryPop(out RevisionGraphRevision? revision))
        {
            if (!ancestry.Add(revision.Objectid))
            {
                continue;
            }

            foreach (RevisionGraphRevision parent in revision.Parents)
            {
                pending.Push(parent);
            }
        }

        return ancestry;
    }

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

            // The message and refs are what the lane tooltips show (upstream's LaneInfoProvider and BranchFinder).
            graph.Add(new GitRevision(id)
            {
                ParentIds = [.. (row.ParentHashes ?? []).Select(IdOf)],
                Subject = row.Subject,
                Body = row.Body,
                HasMultiLineMessage = row.HasMultiLineMessage,
                Refs = [.. (row.Labels ?? []).Select(label => RefOf(id, label)).OfType<IGitRef>()],
            });
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

    /// <summary>
    ///  The lanes the row at <paramref name="index"/> uses, at most upstream's limit.
    /// </summary>
    public int LaneCountAt(int index)
        => Math.Min(_graph.GetSegmentsForRow(index)?.GetLaneCount() ?? 0, RevisionGraph.MaxLanes);

    /// <summary>
    ///  How many lanes the graph column is wide: as upstream's column, the most lanes of the rows on screen, which the view
    ///  sets as it scrolls; <see cref="LaneCount"/> (the whole page) until then.
    /// </summary>
    public int ShownLaneCount
    {
        get => _shownLaneCount ?? LaneCount;
        set => _shownLaneCount = value;
    }

    private int? _shownLaneCount;

    /// <summary>
    ///  Upstream's lane tooltip for <paramref name="lane"/> of the row at <paramref name="index"/>: the commit the lane comes
    ///  from, its branch and message; empty where there is no lane.
    /// </summary>
    public string LaneInfo(int index, int lane)
        => new LaneInfoProvider(new LaneNodeLocator(_graph), new GitRevisionSummaryBuilder()).GetLaneInfo(index, lane);

    // The labels as upstream's refs, which BranchFinder reads by kind (local or remote branch); HEAD and bisect marks are not
    // refs there. The module is not used for these.
    private static GitRef? RefOf(ObjectId id, RefLabel label)
        => label.Kind switch
        {
            RefKind.Branch => new GitRef(_refModule.Value, id, GitRefName.RefsHeadsPrefix + label.Name),
            RefKind.RemoteBranch => new GitRef(_refModule.Value, id, GitRefName.RefsRemotesPrefix + label.Name),
            RefKind.Tag => new GitRef(_refModule.Value, id, GitRefName.RefsTagsPrefix + label.Name),
            _ => null,
        };

    private static readonly Lazy<GitModule> _refModule = new(() => Repository.GitModules.Open(""));

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
