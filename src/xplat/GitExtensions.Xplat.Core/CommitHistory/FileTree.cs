namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  A folder or file in the tree of a commit. <see cref="Path"/> is repository-relative with '/' separators, as git writes it.
/// </summary>
public sealed class FileTreeNode : ObservableObject
{
    private bool _isExpanded;

    public FileTreeNode(string name, string path, bool isFolder)
    {
        Name = name;
        Path = path;
        IsFolder = isFolder;
    }

    public string Name { get; }

    public string Path { get; }

    public bool IsFolder { get; }

    /// <summary>
    ///  Upstream's image of the node in its file tree mode: the folder image, or for a file upstream's default file image
    ///  (upstream shows the system's icon for the file's type, which the new shell does not have).
    /// </summary>
    public string Image => IsFolder ? "FolderClosed" : "File";

    public List<FileTreeNode> Children { get; } = [];

    /// <summary>
    ///  Whether the tree shows the folder's children.
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>
    ///  This node and every node below it, depth first.
    /// </summary>
    public IEnumerable<FileTreeNode> Descendants()
    {
        yield return this;
        foreach (FileTreeNode node in Children.SelectMany(child => child.Descendants()))
        {
            yield return node;
        }
    }

    /// <summary>
    ///  The files at or below this node, as upstream's <c>ItemTags</c> of a tree node.
    /// </summary>
    public IEnumerable<FileTreeNode> Files() => Descendants().Where(node => !node.IsFolder);
}

/// <summary>
///  Builds a folder tree from the flat file list git gives (<c>ls-tree -r</c>), as upstream's File tree tab shows it: folders
///  before files, each in name order.
/// </summary>
public static class FileTree
{
    public static IReadOnlyList<FileTreeNode> Build(IEnumerable<string> paths)
    {
        FileTreeNode root = new("", "", isFolder: true);
        Dictionary<string, FileTreeNode> folders = new(StringComparer.Ordinal) { [""] = root };

        foreach (string path in paths)
        {
            string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            FileTreeNode parent = root;
            string folderPath = "";
            for (int i = 0; i < parts.Length - 1; i++)
            {
                folderPath = folderPath.Length == 0 ? parts[i] : $"{folderPath}/{parts[i]}";
                if (!folders.TryGetValue(folderPath, out FileTreeNode? folder))
                {
                    folder = new FileTreeNode(parts[i], folderPath, isFolder: true);
                    folders[folderPath] = folder;
                    parent.Children.Add(folder);
                }

                parent = folder;
            }

            if (parts.Length > 0)
            {
                parent.Children.Add(new FileTreeNode(parts[^1], path, isFolder: false));
            }
        }

        Sort(root);
        return root.Children;
    }

    private static void Sort(FileTreeNode node)
    {
        node.Children.Sort((left, right) => left.IsFolder != right.IsFolder
            ? left.IsFolder ? -1 : 1
            : StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));
        foreach (FileTreeNode child in node.Children.Where(child => child.IsFolder))
        {
            Sort(child);
        }
    }
}
