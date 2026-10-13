namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The kinds of node in upstream's left panel (<c>RepoObjectsTree</c>): its six trees, then the nodes they hold.
/// </summary>
public enum RepoObjectKind
{
    BranchesRoot,
    RemotesRoot,
    WorktreesRoot,
    TagsRoot,
    SubmodulesRoot,
    StashesRoot,
    Folder,
    LocalBranch,
    Remote,
    RemoteBranch,
    Tag,
    Submodule,
    Stash,
    Worktree,
}

/// <summary>
///  A node of the left panel: upstream's <c>Tree</c> and <c>Node</c> classes in one, with the image and font upstream gives
///  each kind.
/// </summary>
public sealed class RepoObjectNode : ObservableObject
{
    private bool _isExpanded;

    public RepoObjectNode(RepoObjectKind kind, string name, string image, object? item = null, bool isExpanded = false)
    {
        Kind = kind;
        Name = name;
        Image = image;
        Item = item;
        _isExpanded = isExpanded;
    }

    public RepoObjectKind Kind { get; }

    public string Name { get; }

    /// <summary>
    ///  Upstream's image name (<c>GitUI.Properties.Images</c>).
    /// </summary>
    public string Image { get; }

    /// <summary>
    ///  What the node stands for: a <see cref="BranchInfo"/>, the tag or remote name, a <see cref="StashInfo"/>,
    ///  <see cref="SubmoduleInfo"/> or <see cref="WorktreeInfo"/>, or a folder's path below its tree.
    /// </summary>
    public object? Item { get; }

    public BranchInfo? Branch => Item as BranchInfo;

    public List<RepoObjectNode> Children { get; } = [];

    /// <summary>
    ///  Upstream shows the checked-out branch and the current worktree in bold.
    /// </summary>
    public bool IsBold { get; init; }

    /// <summary>
    ///  Upstream's <c>AheadBehindData.ToDisplay</c> text after a branch's name.
    /// </summary>
    public string? AheadBehind { get; init; }

    public string? ToolTip { get; init; }

    /// <summary>
    ///  Upstream's <c>DisplayText</c>: the name, and the ahead and behind counts of a branch in parentheses.
    /// </summary>
    public string Text => string.IsNullOrEmpty(AheadBehind) ? Name : $"{Name} ({AheadBehind})";

    /// <summary>
    ///  Identifies the node across refreshes, so its expanded state can be kept.
    /// </summary>
    public string Key { get; init; } = "";

    public bool IsRoot => Kind <= RepoObjectKind.StashesRoot;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>
    ///  This node and every node below it, depth first.
    /// </summary>
    public IEnumerable<RepoObjectNode> Descendants()
    {
        yield return this;
        foreach (RepoObjectNode descendant in Children.SelectMany(child => child.Descendants()))
        {
            yield return descendant;
        }
    }
}

/// <summary>
///  What the left panel shows, read from the repository.
/// </summary>
public sealed record RepoObjects(
    IReadOnlyList<BranchInfo> Branches,
    IReadOnlyList<string> Remotes,
    IReadOnlyList<string> Tags,
    IReadOnlyList<StashInfo> Stashes,
    IReadOnlyList<SubmoduleInfo> Submodules,
    IReadOnlyList<WorktreeInfo> Worktrees,
    IReadOnlyDictionary<string, string> AheadBehind,
    IReadOnlyDictionary<string, string> RemoteUrls,
    string RepositoryPath);

/// <summary>
///  Builds upstream's left panel (<c>RepoObjectsTree</c> and its trees): the shown trees in their order, each with
///  upstream's root image, and below them the branches and tags in folders, remotes, worktrees, submodules and stashes.
/// </summary>
public static class RepoObjectsTree
{
    // Upstream's TranslatedStrings for the trees.
    public const string BranchesText = "Branches";
    public const string RemotesText = "Remotes";
    public const string WorktreesText = "Worktrees";
    public const string TagsText = "Tags";
    public const string SubmodulesText = "Submodules";
    public const string StashesText = "Stashes";

    /// <summary>
    ///  The six trees in upstream's default order (its <c>RepoObjectsTree.*Index</c> defaults).
    /// </summary>
    public static IReadOnlyList<RepoObjectKind> Roots { get; } =
    [
        RepoObjectKind.BranchesRoot, RepoObjectKind.RemotesRoot, RepoObjectKind.WorktreesRoot, RepoObjectKind.TagsRoot,
        RepoObjectKind.SubmodulesRoot, RepoObjectKind.StashesRoot,
    ];

    /// <summary>
    ///  Upstream's text and image of a tree's root node.
    /// </summary>
    public static (string Text, string Image) RootOf(RepoObjectKind root) => root switch
    {
        RepoObjectKind.BranchesRoot => (BranchesText, "BranchLocalRoot"),
        RepoObjectKind.RemotesRoot => (RemotesText, "BranchRemoteRoot"),
        RepoObjectKind.WorktreesRoot => (WorktreesText, "WorkTree"),
        RepoObjectKind.TagsRoot => (TagsText, "TagHorizontal"),
        RepoObjectKind.SubmodulesRoot => (SubmodulesText, "FolderSubmodule"),
        RepoObjectKind.StashesRoot => (StashesText, "Stash"),
        _ => throw new ArgumentOutOfRangeException(nameof(root)),
    };

    /// <summary>
    ///  The trees in <paramref name="order"/> (the shown ones only), their texts passed through
    ///  <paramref name="translate"/>. Branches, remotes, worktrees and submodules start expanded, as upstream's trees.
    /// </summary>
    public static IReadOnlyList<RepoObjectNode> Build(RepoObjects objects, IReadOnlyList<RepoObjectKind> order,
        Func<string, string> translate, RefSorting? sorting = null)
    {
        List<RepoObjectNode> roots = [];
        foreach (RepoObjectKind kind in order)
        {
            (string text, string image) = RootOf(kind);
            RepoObjectNode root = new(kind, translate(text), image, isExpanded: kind is not (RepoObjectKind.TagsRoot or RepoObjectKind.StashesRoot))
            {
                Key = kind.ToString(),
            };
            switch (kind)
            {
                case RepoObjectKind.BranchesRoot:
                case RepoObjectKind.RemotesRoot:
                    AddBranches(root, objects, sorting);
                    break;
                case RepoObjectKind.WorktreesRoot:
                    foreach (WorktreeInfo worktree in objects.Worktrees.Where(worktree => !worktree.IsDeleted))
                    {
                        root.Children.Add(new RepoObjectNode(RepoObjectKind.Worktree, worktree.Display, "WorkTree", worktree)
                        {
                            Key = $"{root.Key}/{worktree.Path}",
                            IsBold = RecentRepositoryPaths.Same(worktree.Path, objects.RepositoryPath),
                            ToolTip = worktree.Path,
                        });
                    }

                    break;
                case RepoObjectKind.TagsRoot:
                    foreach (string tag in objects.Tags)
                    {
                        AddInFolders(root, tag, tag, RepoObjectKind.Tag, "TagHorizontal");
                    }

                    break;
                case RepoObjectKind.SubmodulesRoot:
                    foreach (SubmoduleInfo submodule in objects.Submodules)
                    {
                        root.Children.Add(new RepoObjectNode(RepoObjectKind.Submodule, submodule.Display, SubmoduleImage(submodule), submodule)
                        {
                            Key = $"{root.Key}/{submodule.Path}",
                            ToolTip = submodule.Display,
                        });
                    }

                    break;
                case RepoObjectKind.StashesRoot:
                    foreach (StashInfo stash in objects.Stashes)
                    {
                        root.Children.Add(new RepoObjectNode(RepoObjectKind.Stash, stash.Display, "Stash", stash)
                        {
                            Key = $"{root.Key}/{stash.Name}",
                        });
                    }

                    break;
            }

            roots.Add(root);
        }

        return roots;
    }

    /// <summary>
    ///  Keeps the expanded state of the nodes that were in <paramref name="previous"/>, as upstream's tree keeps it across
    ///  a refresh; new nodes keep their default.
    /// </summary>
    public static void KeepExpansion(IEnumerable<RepoObjectNode> previous, IEnumerable<RepoObjectNode> next)
    {
        Dictionary<string, bool> expanded = previous.SelectMany(root => root.Descendants())
            .Where(node => node.Children.Count > 0)
            .GroupBy(node => node.Key)
            .ToDictionary(group => group.Key, group => group.First().IsExpanded);
        foreach (RepoObjectNode node in next.SelectMany(root => root.Descendants()))
        {
            if (expanded.TryGetValue(node.Key, out bool isExpanded))
            {
                node.IsExpanded = isExpanded;
            }
        }
    }

    /// <summary>
    ///  Upstream's left panel search: the next node after <paramref name="current"/> whose text contains
    ///  <paramref name="text"/>, ignoring case, wrapping around; null when none does.
    /// </summary>
    public static RepoObjectNode? FindNext(IReadOnlyList<RepoObjectNode> roots, string text, RepoObjectNode? current)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        List<RepoObjectNode> nodes = [.. roots.SelectMany(root => root.Descendants()).Where(node => !node.IsRoot)];
        int start = current is null ? 0 : nodes.IndexOf(current) + 1;
        for (int i = 0; i < nodes.Count; i++)
        {
            RepoObjectNode node = nodes[(start + i) % nodes.Count];
            if (node.Text.Contains(text.Trim(), StringComparison.CurrentCultureIgnoreCase))
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>
    ///  The nodes from a root down to <paramref name="node"/>, so a view can expand them.
    /// </summary>
    public static IReadOnlyList<RepoObjectNode> PathTo(IReadOnlyList<RepoObjectNode> roots, RepoObjectNode node)
    {
        foreach (RepoObjectNode root in roots)
        {
            List<RepoObjectNode> path = [];
            if (Find(root, path))
            {
                return path;
            }
        }

        return [];

        bool Find(RepoObjectNode parent, List<RepoObjectNode> path)
        {
            path.Add(parent);
            if (parent == node || parent.Children.Any(child => Find(child, path)))
            {
                return true;
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }
    }

    // Upstream's RemoteRepoNode image: the host's logo for GitHub, Bitbucket and Azure DevOps.
    public static string RemoteImage(string? url)
        => url is null ? "Remote"
            : url.Contains("github.com", StringComparison.Ordinal) ? "GitHub"
            : url.Contains("bitbucket.", StringComparison.Ordinal) ? "BitBucket"
            : url.Contains("visualstudio.com", StringComparison.Ordinal) || url.Contains("dev.azure.com", StringComparison.Ordinal)
                ? "VisualStudioTeamServices"
                : "Remote";

    // Upstream's GetSubmoduleItemImage with the status the new shell reads: changed or not.
    private static string SubmoduleImage(SubmoduleInfo submodule)
        => submodule.IsInitialized && !submodule.IsUpToDate ? "FileStatusModified" : "FolderSubmodule";

    // The branch trees as BranchTree groups them (upstream's prioritized branches and remotes first), with upstream's
    // images, the current branch in bold and the ahead and behind counts; remotes without branches are listed too.
    private static void AddBranches(RepoObjectNode root, RepoObjects objects, RefSorting? sorting)
    {
        IReadOnlyList<BranchTreeNode> groups = BranchTree.Build(objects.Branches, sorting);
        if (root.Kind == RepoObjectKind.BranchesRoot)
        {
            foreach (BranchTreeNode child in groups.First(group => group.Name == BranchTree.LocalGroupName).Children)
            {
                root.Children.Add(Convert(child, root.Key, "", objects));
            }

            return;
        }

        BranchTreeNode? remotes = groups.FirstOrDefault(group => group.Name == BranchTree.RemoteGroupName);
        foreach (BranchTreeNode remote in remotes?.Children ?? [])
        {
            root.Children.Add(RemoteNode(remote.Name, root.Key, objects, remote.Children, remote.IsExpanded));
        }

        foreach (string name in objects.Remotes.Where(name => root.Children.All(remote => remote.Name != name)).Order(StringComparer.Ordinal))
        {
            root.Children.Add(RemoteNode(name, root.Key, objects, [], isExpanded: true));
        }
    }

    private static RepoObjectNode RemoteNode(string name, string parentKey, RepoObjects objects,
        IReadOnlyList<BranchTreeNode> branches, bool isExpanded)
    {
        objects.RemoteUrls.TryGetValue(name, out string? url);
        RepoObjectNode node = new(RepoObjectKind.Remote, name, RemoteImage(url), name, isExpanded)
        {
            Key = $"{parentKey}/{name}",
            ToolTip = url,
        };
        foreach (BranchTreeNode branch in branches)
        {
            node.Children.Add(Convert(branch, node.Key, "", objects));
        }

        return node;
    }

    private static RepoObjectNode Convert(BranchTreeNode node, string parentKey, string folderPath, RepoObjects objects)
    {
        string key = $"{parentKey}/{node.Name}";
        if (node.Branch is { } branch)
        {
            objects.AheadBehind.TryGetValue(branch.Name, out string? aheadBehind);
            return new RepoObjectNode(branch.IsRemote ? RepoObjectKind.RemoteBranch : RepoObjectKind.LocalBranch,
                node.Name, branch.IsRemote ? "BranchRemote" : "BranchLocal", branch)
            {
                Key = key,
                IsBold = branch.IsCurrent,
                AheadBehind = aheadBehind,
            };
        }

        string path = folderPath.Length == 0 ? node.Name : $"{folderPath}/{node.Name}";
        RepoObjectNode folder = new(RepoObjectKind.Folder, node.Name, "BranchFolder", path, node.IsExpanded) { Key = key };
        foreach (BranchTreeNode child in node.Children)
        {
            folder.Children.Add(Convert(child, key, path, objects));
        }

        return folder;
    }

    // Upstream's BaseRefTree: a name with '/' sits in folders.
    private static void AddInFolders(RepoObjectNode root, string name, object item, RepoObjectKind kind, string image)
    {
        string[] parts = name.Split('/');
        RepoObjectNode parent = root;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            string path = string.Join('/', parts[..(i + 1)]);
            RepoObjectNode? folder = parent.Children.FirstOrDefault(child => child.Kind == RepoObjectKind.Folder && child.Name == parts[i]);
            if (folder is null)
            {
                folder = new RepoObjectNode(RepoObjectKind.Folder, parts[i], "BranchFolder", path) { Key = $"{root.Key}/{path}/" };
                parent.Children.Add(folder);
            }

            parent = folder;
        }

        parent.Children.Add(new RepoObjectNode(kind, parts[^1], image, item) { Key = $"{root.Key}/{name}" });
    }
}
