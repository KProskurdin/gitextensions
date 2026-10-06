namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  A node of the branch tree: a group ("Branches", "Remotes"), a remote, a folder of a branch name, or a branch
///  (<see cref="Branch"/> set).
/// </summary>
public sealed class BranchTreeNode : ObservableObject
{
    private bool _isExpanded;

    public BranchTreeNode(string name, BranchInfo? branch = null, bool isExpanded = false)
    {
        Name = name;
        Branch = branch;
        _isExpanded = isExpanded;
    }

    public string Name { get; }

    public BranchInfo? Branch { get; }

    public List<BranchTreeNode> Children { get; } = [];

    /// <summary>
    ///  The text shown: the checked-out branch is marked with "*", groups and folders show how many branches they hold.
    /// </summary>
    public string Display => Branch is { IsCurrent: true } ? $"* {Name}" : Branch is null ? $"{Name} ({CountBranches()})" : Name;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public int CountBranches() => Children.Sum(child => child.Branch is null ? child.CountBranches() : 1);

    /// <summary>
    ///  This node and every node below it, depth first.
    /// </summary>
    public IEnumerable<BranchTreeNode> Descendants()
    {
        yield return this;
        foreach (BranchTreeNode descendant in Children.SelectMany(child => child.Descendants()))
        {
            yield return descendant;
        }
    }
}

/// <summary>
///  Groups branches as upstream's left panel (<c>RepoObjectsTree</c>) does: local branches under "Branches", remote branches
///  under "Remotes" and their remote, and both split into folders at '/'.
/// </summary>
public static class BranchTree
{
    public const string LocalGroupName = "Branches";
    public const string RemoteGroupName = "Remotes";

    public static IReadOnlyList<BranchTreeNode> Build(IReadOnlyList<BranchInfo> branches)
    {
        BranchTreeNode local = new(LocalGroupName, isExpanded: true);
        BranchTreeNode remotes = new(RemoteGroupName, isExpanded: true);

        foreach (BranchInfo branch in branches)
        {
            if (branch.IsRemote)
            {
                // "origin/feature/x": the remote, then the folders of the branch name.
                string[] parts = branch.Name.Split('/');
                BranchTreeNode remote = Child(remotes, parts[0], isExpanded: true);
                Add(remote, parts.Skip(1).ToArray(), branch, expandFolders: false);
            }
            else
            {
                Add(local, branch.Name.Split('/'), branch, expandFolders: true);
            }
        }

        List<BranchTreeNode> roots = [local];
        if (remotes.Children.Count > 0)
        {
            roots.Add(remotes);
        }

        return roots;
    }

    private static void Add(BranchTreeNode parent, string[] parts, BranchInfo branch, bool expandFolders)
    {
        if (parts.Length == 0)
        {
            parent.Children.Add(new BranchTreeNode(branch.Name, branch));
            return;
        }

        BranchTreeNode folder = parent;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            folder = Child(folder, parts[i], expandFolders);
        }

        folder.Children.Add(new BranchTreeNode(parts[^1], branch));

        // The checked-out branch is always visible.
        if (branch.IsCurrent)
        {
            folder.IsExpanded = true;
        }
    }

    private static BranchTreeNode Child(BranchTreeNode parent, string name, bool isExpanded)
    {
        BranchTreeNode? existing = parent.Children.FirstOrDefault(child => child.Branch is null && child.Name == name);
        if (existing is not null)
        {
            return existing;
        }

        BranchTreeNode folder = new(name, isExpanded: isExpanded);
        parent.Children.Add(folder);
        return folder;
    }
}
