using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;

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

    /// <summary>
    ///  Upstream's <c>RemoteRepoFolderNode</c>: the folder of the inactive remotes below the remotes.
    /// </summary>
    InactiveRemotes,
}

/// <summary>
///  A node of the left panel: upstream's <c>Tree</c> and <c>Node</c> classes in one, with the image and font upstream gives
///  each kind.
/// </summary>
public sealed class RepoObjectNode : ObservableObject
{
    private readonly string _image;
    private readonly string? _toolTip;
    private bool _isExpanded;
    private string? _mergedToolTip;
    private bool _isHidden;
    private string? _hiddenToolTip;
    private bool _isSearchMatch;

    public RepoObjectNode(RepoObjectKind kind, string name, string image, object? item = null, bool isExpanded = false)
    {
        Kind = kind;
        Name = name;
        _image = image;
        Item = item;
        _isExpanded = isExpanded;
    }

    public RepoObjectKind Kind { get; }

    public string Name { get; }

    /// <summary>
    ///  Upstream's image name (<c>GitUI.Properties.Images</c>); a hidden node has <c>EyeClosed</c>, else a merged branch
    ///  upstream's merged image (<c>BranchLocalMerged</c>, <c>BranchRemoteMerged</c>).
    /// </summary>
    public string Image => IsHidden ? HiddenImage : IsMerged ? $"{_image}{MergedImageSuffix}" : _image;

    /// <summary>
    ///  Upstream's <c>BaseBranchLeafNode.IsMerged</c>: the branch is contained in the commit selected in the grid, as
    ///  <see cref="RepoObjectsTree.ShowMerged"/> sets it.
    /// </summary>
    public bool IsMerged => _mergedToolTip is not null;

    /// <summary>
    ///  Upstream's <c>BaseRevisionNode.Visible</c> false: the grid has not loaded the commit of this branch, tag or stash
    ///  (upstream's <c>UpdateVisibility</c>), so the node has upstream's <c>EyeClosed</c> image and gray text, as
    ///  <see cref="RepoObjectsTree.ShowHidden"/> sets it.
    /// </summary>
    public bool IsHidden => _isHidden;

    /// <summary>
    ///  The node matches the left panel's search (<see cref="RepoObjectSearch"/>), which upstream highlights in the Info
    ///  colors.
    /// </summary>
    public bool IsSearchMatch => _isSearchMatch;

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

    /// <summary>
    ///  The node's tooltip; for a branch upstream's "not currently visible" text while it is hidden, else its "contained in
    ///  the currently selected commit" text while it is merged.
    /// </summary>
    public string? ToolTip
    {
        get => _hiddenToolTip ?? _mergedToolTip ?? _toolTip;
        init => _toolTip = value;
    }

    /// <summary>
    ///  An inactive remote (upstream's <c>RemoteRepoNode.Enabled</c> false), which can be activated but not fetched.
    /// </summary>
    public bool IsInactive { get; init; }

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

    // Upstream's merged images are the branch images' names with this suffix.
    private const string MergedImageSuffix = "Merged";

    // Upstream's image of a node whose commit the grid does not show.
    private const string HiddenImage = "EyeClosed";

    // A hidden branch has upstream's InvisibleCommit tooltip; tags and stashes have none.
    internal void SetHidden(bool isHidden, string? hiddenToolTip)
    {
        string? toolTip = isHidden ? hiddenToolTip : null;
        if (_isHidden != isHidden)
        {
            _isHidden = isHidden;
            RaisePropertyChanged(nameof(IsHidden));
            RaisePropertyChanged(nameof(Image));
        }

        if (_hiddenToolTip != toolTip)
        {
            _hiddenToolTip = toolTip;
            RaisePropertyChanged(nameof(ToolTip));
        }
    }

    internal void SetSearchMatch(bool isSearchMatch)
        => SetProperty(ref _isSearchMatch, isSearchMatch, nameof(IsSearchMatch));

    // A null tooltip clears the merged state.
    internal void SetMerged(string? mergedToolTip)
    {
        if (_mergedToolTip == mergedToolTip)
        {
            return;
        }

        bool wasMerged = IsMerged;
        _mergedToolTip = mergedToolTip;
        if (wasMerged != IsMerged)
        {
            RaisePropertyChanged(nameof(IsMerged));
            RaisePropertyChanged(nameof(Image));
        }

        RaisePropertyChanged(nameof(ToolTip));
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
    string RepositoryPath,
    IReadOnlyList<string>? InactiveRemotes = null);

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
    public const string InactiveText = "[ Inactive ]";
    public const string ContainedInCurrentCommitText = "'{0}' is contained in the currently selected commit";
    public const string InvisibleCommitText = "'{0}' is not currently visible";

    // What upstream asks about for an artificial commit.
    private const string HeadRevision = "HEAD";

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
                    AddBranches(root, objects, sorting, translate);
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
    ///  The revision upstream's <c>OnSelected</c> goes to in the grid when <paramref name="node"/> is selected
    ///  (<c>GoToRevision</c>): a branch's name (local or remote), a tag's, a stash's commit and a worktree's checked-out commit;
    ///  <see langword="null"/> for the other nodes.
    /// </summary>
    public static string? RevisionOf(RepoObjectNode node)
        => node switch
        {
            { Kind: RepoObjectKind.LocalBranch or RepoObjectKind.RemoteBranch, Branch: { } branch } => branch.Name,
            { Kind: RepoObjectKind.Tag, Item: string tag } => tag,
            { Kind: RepoObjectKind.Stash, Item: StashInfo stash } => stash.Name,
            { Kind: RepoObjectKind.Worktree, Item: WorktreeInfo { Sha1: { Length: > 0 } sha1 } } => sha1,
            _ => null,
        };

    /// <summary>
    ///  Upstream's <c>SelectionChanged</c> returns at once when nothing is selected in the grid or in the tree, or when the
    ///  grid's one selected commit is the one of the branch or tag selected in the tree (the grid followed the tree); the
    ///  merged branches stay as they were.
    /// </summary>
    /// <param name="selected">The grid's selected commits, the latest selected first.</param>
    /// <param name="node">The node selected in the tree.</param>
    public static bool KeepsMerged(IReadOnlyList<CommitRow> selected, RepoObjectNode? node)
        => (selected.Count == 0 && node is null)
           || (selected.Count == 1 && node is not null && CompleteNameOf(node) is { } name
               && (selected[0].Labels ?? []).Any(label => CompleteNameOf(label) == name));

    /// <summary>
    ///  The commit upstream's <c>SelectionChanged</c> asks <c>git branch -a --merged</c> about: the latest selected one, HEAD
    ///  for an artificial commit; <see langword="null"/> when none is selected, which clears every branch's merged state.
    /// </summary>
    /// <param name="selected">The grid's selected commits, the latest selected first.</param>
    public static string? MergedBranchesCommit(IReadOnlyList<CommitRow> selected)
        => selected.Count == 0 ? null
            : ArtificialCommits.IsArtificial(selected[0].Hash) ? HeadRevision
            : selected[0].Hash;

    /// <summary>
    ///  Upstream's end of <c>SelectionChanged</c>: the local and remote branches in <paramref name="merged"/> (full ref
    ///  names, as <c>git branch -a --merged --format=%(refname)</c> lists them) get upstream's merged image and tooltip,
    ///  except the ones at the <paramref name="selected"/> commit itself; the other branches lose them.
    /// </summary>
    public static void ShowMerged(IEnumerable<RepoObjectNode> roots, IEnumerable<string> merged, CommitRow? selected,
        Func<string, string> translate)
    {
        HashSet<string> names = new(merged, StringComparer.Ordinal);
        foreach (RefLabel label in selected?.Labels ?? [])
        {
            if (CompleteNameOf(label) is { } name)
            {
                names.Remove(name);
            }
        }

        string text = translate(ContainedInCurrentCommitText);
        foreach (RepoObjectNode node in roots.SelectMany(root => root.Descendants()))
        {
            if (node.Kind is RepoObjectKind.LocalBranch or RepoObjectKind.RemoteBranch)
            {
                node.SetMerged(CompleteNameOf(node) is { } name && names.Contains(name) ? string.Format(text, node.Name) : null);
            }
        }
    }

    /// <summary>
    ///  Upstream's <c>BaseRevisionTree.UpdateVisibility</c> after the grid has loaded: a local or remote branch, a tag or a
    ///  stash whose commit is not among <paramref name="loaded"/> (the grid's rows) is hidden; the others are shown. The
    ///  rows' labels name every ref at their commit, and a stash row its stash, which stands in for upstream's check of
    ///  the node's object id (<c>ICheckRefs.Contains</c>). A hidden branch gets upstream's <c>InvisibleCommit</c> tooltip
    ///  with its full name.
    /// </summary>
    public static void ShowHidden(IEnumerable<RepoObjectNode> roots, IEnumerable<CommitRow> loaded,
        Func<string, string> translate)
    {
        HashSet<string> refs = new(StringComparer.Ordinal);
        HashSet<string> stashes = new(StringComparer.Ordinal);
        foreach (CommitRow row in loaded)
        {
            foreach (RefLabel label in row.Labels ?? [])
            {
                if (CompleteNameOf(label) is { } name)
                {
                    refs.Add(name);
                }
            }

            if (row.StashLabel is { } stash)
            {
                stashes.Add(stash);
            }
        }

        string text = translate(InvisibleCommitText);
        foreach (RepoObjectNode node in roots.SelectMany(root => root.Descendants()))
        {
            bool refHidden = CompleteNameOf(node) is not { } refName || !refs.Contains(refName);
            switch (node)
            {
                case { Kind: RepoObjectKind.LocalBranch or RepoObjectKind.RemoteBranch, Branch: { } branch }:
                    node.SetHidden(refHidden, string.Format(text, branch.Name));
                    break;
                case { Kind: RepoObjectKind.Tag }:
                    node.SetHidden(refHidden, hiddenToolTip: null);
                    break;
                case { Kind: RepoObjectKind.Stash, Item: StashInfo stash }:
                    node.SetHidden(!stashes.Contains(stash.Name), hiddenToolTip: null);
                    break;
            }
        }
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

    // A branch's or tag's full ref name (upstream's IGitRef.CompleteName).
    private static string? CompleteNameOf(RepoObjectNode node)
        => node switch
        {
            { Kind: RepoObjectKind.LocalBranch, Branch: { } branch } => GitRefName.RefsHeadsPrefix + branch.Name,
            { Kind: RepoObjectKind.RemoteBranch, Branch: { } branch } => GitRefName.RefsRemotesPrefix + branch.Name,
            { Kind: RepoObjectKind.Tag, Item: string tag } => GitRefName.RefsTagsPrefix + tag,
            _ => null,
        };

    private static string? CompleteNameOf(RefLabel label)
        => label.Kind switch
        {
            RefKind.Branch => GitRefName.RefsHeadsPrefix + label.Name,
            RefKind.RemoteBranch => GitRefName.RefsRemotesPrefix + label.Name,
            RefKind.Tag => GitRefName.RefsTagsPrefix + label.Name,
            _ => null,
        };

    // Upstream's GetSubmoduleItemImage with the status the new shell reads: changed or not.
    private static string SubmoduleImage(SubmoduleInfo submodule)
        => submodule.IsInitialized && !submodule.IsUpToDate ? "FileStatusModified" : "FolderSubmodule";

    // The branch trees as BranchTree groups them (upstream's prioritized branches and remotes first), with upstream's
    // images, the current branch in bold and the ahead and behind counts; remotes without branches are listed too.
    private static void AddBranches(RepoObjectNode root, RepoObjects objects, RefSorting? sorting,
        Func<string, string> translate)
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

        // Upstream's RemoteBranchTree: the inactive remotes last, in a folder of their own.
        if (objects.InactiveRemotes is { Count: > 0 } inactiveRemotes)
        {
            RepoObjectNode folder = new(RepoObjectKind.InactiveRemotes, translate(InactiveText), "EyeClosed")
            {
                Key = $"{root.Key}/{InactiveText}",
            };
            foreach (string name in inactiveRemotes.Order(StringComparer.Ordinal))
            {
                folder.Children.Add(RemoteNode(name, folder.Key, objects, [], isExpanded: false, isInactive: true));
            }

            root.Children.Add(folder);
        }
    }

    private static RepoObjectNode RemoteNode(string name, string parentKey, RepoObjects objects,
        IReadOnlyList<BranchTreeNode> branches, bool isExpanded, bool isInactive = false)
    {
        objects.RemoteUrls.TryGetValue(name, out string? url);
        RepoObjectNode node = new(RepoObjectKind.Remote, name, RemoteImage(url), name, isExpanded)
        {
            Key = $"{parentKey}/{name}",
            ToolTip = url,
            IsInactive = isInactive,
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
