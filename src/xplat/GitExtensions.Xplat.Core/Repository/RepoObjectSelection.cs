namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Upstream's multi-selection of the left panel (<c>NodeBase.IsSelected</c>, <c>RepoObjectsTree.OnNodeClick</c> and
///  <c>SelectNode</c>): beside the tree's own selection, a click marks the clicked node alone, Ctrl+click adds it to the
///  marked nodes or takes it from them, with Shift its descendants too, and a right click on a marked node keeps the
///  marks for the menu. The marked nodes are underlined (<see cref="RepoObjectNode.IsMultiSelected"/>); "Filter for
///  selected" filters the grid for all their refs, and Expand and Collapse act on all of them.
/// </summary>
public static class RepoObjectSelection
{
    /// <summary>
    ///  Upstream's <c>GetSelectedNodes</c>: the marked nodes, tree by tree, each depth first from its root.
    /// </summary>
    public static IReadOnlyList<RepoObjectNode> Selected(IEnumerable<RepoObjectNode> roots)
        => [.. roots.SelectMany(root => root.Descendants()).Where(node => node.IsMultiSelected)];

    /// <summary>
    ///  Upstream's <c>OnNodeClick</c>: a right click on a marked node keeps the marks, so that the menu acts on all of them;
    ///  any other click marks the node as <see cref="Select"/> does.
    /// </summary>
    public static void Click(IEnumerable<RepoObjectNode> roots, RepoObjectNode node, bool rightButton, bool multiple,
        bool includingDescendants)
    {
        if (rightButton && node.IsMultiSelected)
        {
            return;
        }

        Select(roots, node, multiple, includingDescendants);
    }

    /// <summary>
    ///  Upstream's <c>SelectNode</c>: with <paramref name="multiple"/> (Ctrl) the node's mark is toggled, with
    ///  <paramref name="includingDescendants"/> (Shift) its descendants get the same; else only the node is marked.
    /// </summary>
    public static void Select(IEnumerable<RepoObjectNode> roots, RepoObjectNode node, bool multiple,
        bool includingDescendants)
    {
        if (multiple)
        {
            Mark(node, !node.IsMultiSelected, includingDescendants);
            return;
        }

        foreach (RepoObjectNode selected in Selected(roots))
        {
            selected.SetMultiSelected(false);
        }

        node.SetMultiSelected(true);
    }

    /// <summary>
    ///  Upstream's <c>Tree.ReloadNodesAsync</c> remembers the marked nodes across a refill: the nodes of
    ///  <paramref name="next"/> with the key of a marked node of <paramref name="previous"/> are marked.
    /// </summary>
    public static void Keep(IEnumerable<RepoObjectNode> previous, IEnumerable<RepoObjectNode> next)
    {
        HashSet<string> keys = [.. Selected(previous).Select(node => node.Key)];
        if (keys.Count == 0)
        {
            return;
        }

        foreach (RepoObjectNode node in next.SelectMany(root => root.Descendants()).Where(node => keys.Contains(node.Key)))
        {
            node.SetMultiSelected(true);
        }
    }

    /// <summary>
    ///  Upstream's "Filter for selected": the full names of the branches and tags among <paramref name="nodes"/>
    ///  (upstream's <c>IGitRefActions.FullPath</c>), space separated, or <see langword="null"/> when there is none.
    /// </summary>
    public static string? RefsFilter(IEnumerable<RepoObjectNode> nodes)
    {
        string[] names =
        [
            .. nodes.Select(node => node switch
            {
                { Kind: RepoObjectKind.LocalBranch or RepoObjectKind.RemoteBranch, Branch: { } branch } => branch.Name,
                { Kind: RepoObjectKind.Tag, Item: string tag } => tag,
                _ => null,
            }).OfType<string>(),
        ];
        return names.Length == 0 ? null : string.Join(' ', names);
    }

    // Upstream's NodeBase.Select.
    private static void Mark(RepoObjectNode node, bool select, bool includingDescendants)
    {
        node.SetMultiSelected(select);
        if (!includingDescendants)
        {
            return;
        }

        foreach (RepoObjectNode child in node.Children)
        {
            Mark(child, select, includingDescendants);
        }
    }
}
