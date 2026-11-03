namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Upstream's left panel search (<c>RepoObjectsTree.DoSearch</c>): every node that matches is highlighted
///  (<see cref="RepoObjectNode.IsSearchMatch"/>, upstream's Info colors), and each search selects the next one, wrapping
///  around, until the text changes. Branches, tags, stashes, remotes and folders match by their full path, as upstream's
///  <c>BaseRevisionNode.FullPath</c> (a stash by its "stash@{0}", not its message); the tree roots, worktrees and
///  submodules by their text.
/// </summary>
public sealed class RepoObjectSearch
{
    private List<RepoObjectNode> _results = [];
    private bool _criteriaChanged;

    /// <summary>
    ///  The highlighted nodes, in upstream's breadth-first order from the next one to select.
    /// </summary>
    public IReadOnlyList<RepoObjectNode> Results => _results;

    /// <summary>
    ///  Upstream's <c>OnBranchCriterionChanged</c>: the next search starts over with the new text.
    /// </summary>
    public void CriteriaChanged() => _criteriaChanged = true;

    /// <summary>
    ///  Upstream's <c>DoSearch</c>: the node to select next, or <see langword="null"/> when none matches. With
    ///  <paramref name="focusSearchBox"/> the text was emptied, the highlighting cleared, and upstream puts the focus back on
    ///  the search box.
    /// </summary>
    public RepoObjectNode? Next(IReadOnlyList<RepoObjectNode> roots, string text, out bool focusSearchBox)
    {
        focusSearchBox = false;
        if (_criteriaChanged && _results.Count > 0)
        {
            Clear();
            if (string.IsNullOrWhiteSpace(text))
            {
                focusSearchBox = true;
                return null;
            }
        }

        if (_results.Count == 0 && !string.IsNullOrWhiteSpace(text))
        {
            // Upstream leaves its changed flag set here, so its second search after a change starts over and selects the
            // first match again; the new shell goes on to the second match.
            _criteriaChanged = false;
            _results = Search(roots, text);
        }

        if (_results.Count == 0)
        {
            return null;
        }

        RepoObjectNode first = _results[0];
        _results.RemoveAt(0);
        _results.Add(first);
        return first;
    }

    /// <summary>
    ///  Carries the highlighting over to the nodes of a refilled tree, by their keys; nodes that are gone drop out.
    /// </summary>
    public void Remap(IReadOnlyList<RepoObjectNode> roots)
    {
        if (_results.Count == 0)
        {
            return;
        }

        Dictionary<string, RepoObjectNode> byKey = roots.SelectMany(root => root.Descendants())
            .GroupBy(node => node.Key)
            .ToDictionary(group => group.Key, group => group.First());
        _results = [.. _results.Select(node => byKey.GetValueOrDefault(node.Key)).OfType<RepoObjectNode>()];
        foreach (RepoObjectNode node in _results)
        {
            node.SetSearchMatch(true);
        }
    }

    /// <summary>
    ///  Upstream's <c>CollectFilterCandidates</c> for the search box's drop-down: depth first below the roots, the full path
    ///  of each branch, tag, stash, remote or folder without children, and the text of the other nodes.
    /// </summary>
    public static IReadOnlyList<string> Candidates(IReadOnlyList<RepoObjectNode> roots)
    {
        List<string> candidates = [];
        foreach (RepoObjectNode root in roots)
        {
            Collect(root.Children, remotePrefix: "");
        }

        return candidates;

        void Collect(IEnumerable<RepoObjectNode> nodes, string remotePrefix)
        {
            foreach (RepoObjectNode node in nodes)
            {
                if (FullPathOf(node, remotePrefix) is { } fullPath)
                {
                    if (node.Children.Count == 0)
                    {
                        candidates.Add(fullPath);
                    }
                }
                else
                {
                    candidates.Add(node.Text);
                }

                Collect(node.Children, PrefixBelow(node, remotePrefix));
            }
        }
    }

    // Upstream's SearchTree: breadth first from the roots, case ignored.
    private static List<RepoObjectNode> Search(IReadOnlyList<RepoObjectNode> roots, string text)
    {
        Queue<(RepoObjectNode Node, string RemotePrefix)> queue = new(roots.Select(root => (root, "")));
        List<RepoObjectNode> results = [];
        while (queue.Count != 0)
        {
            (RepoObjectNode node, string remotePrefix) = queue.Dequeue();
            string searched = FullPathOf(node, remotePrefix) ?? node.Text;
            if (searched.Contains(text, StringComparison.InvariantCultureIgnoreCase))
            {
                node.SetSearchMatch(true);
                results.Add(node);
            }

            foreach (RepoObjectNode child in node.Children)
            {
                queue.Enqueue((child, PrefixBelow(node, remotePrefix)));
            }
        }

        return results;
    }

    private void Clear()
    {
        _criteriaChanged = false;
        foreach (RepoObjectNode node in _results)
        {
            node.SetSearchMatch(false);
        }

        _results = [];
    }

    // Upstream's BaseRevisionNode.FullPath, or null for the nodes that are no BaseRevisionNode (roots, worktrees,
    // submodules). A remote branch's folders start with their remote's name, as upstream's RemoteBranchTree builds them.
    private static string? FullPathOf(RepoObjectNode node, string remotePrefix)
        => node switch
        {
            { Kind: RepoObjectKind.LocalBranch or RepoObjectKind.RemoteBranch, Branch: { } branch } => branch.Name,
            { Kind: RepoObjectKind.Tag, Item: string tag } => tag,
            { Kind: RepoObjectKind.Stash, Item: StashInfo stash } => stash.Name,
            { Kind: RepoObjectKind.Folder, Item: string path } => remotePrefix + path,
            { Kind: RepoObjectKind.Remote or RepoObjectKind.InactiveRemotes } => node.Name,
            _ => null,
        };

    private static string PrefixBelow(RepoObjectNode node, string remotePrefix)
        => node.Kind == RepoObjectKind.Remote ? $"{node.Name}/" : remotePrefix;
}
