using GitCommands;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The kinds of node of upstream's <c>FileStatusList</c> tree.
/// </summary>
public enum FileStatusTreeNodeKind
{
    /// <summary>
    ///  A folder of the files' paths (upstream's node tagged with a <c>RelativePath</c>).
    /// </summary>
    Folder,

    /// <summary>
    ///  A group of the extension or status grouping (upstream's node tagged with a <c>GroupKey</c>).
    /// </summary>
    Group,

    File,
}

/// <summary>
///  A node of upstream's <c>FileStatusList</c> tree: its text (a folder's or file's path below its parent folder), image,
///  a key that names it across a refresh, the file of a file node and the nodes below it.
/// </summary>
public sealed record FileStatusTreeNode<T>(FileStatusTreeNodeKind Kind, string Text, string Image, string Key, T? File,
    IReadOnlyList<FileStatusTreeNode<T>> Children)
    where T : class;

/// <summary>
///  Upstream's <c>FileStatusList</c> grouping (<c>GetNodes</c>' <c>CreateGroup</c>, <c>StatusSorter</c>): by path a
///  folder tree, by extension or status a node per group with the group's folder tree below it; the flat variants list the
///  paths in the same order without folders.
/// </summary>
public static class FileStatusTree
{
    // Upstream's image of a folder node and of an extension without its own icon.
    private const string FolderImage = "FolderClosed";
    private const string DefaultFileImage = "File";

    /// <summary>
    ///  The nodes of <paramref name="files"/> as upstream's list shows one diff group of them.
    /// </summary>
    /// <param name="denseTree">Upstream's <c>FileStatusMergeSingleItemWithFolder</c>: a folder with a single file shows the
    ///  file in its place.</param>
    /// <param name="showGroupNodesInFlatList">Upstream's <c>FileStatusShowGroupNodesInFlatList</c>: the flat variants
    ///  keep the extension and status group nodes.</param>
    public static IReadOnlyList<FileStatusTreeNode<T>> Build<T>(IEnumerable<T> files, DiffListSortType sorting, bool denseTree,
        bool showGroupNodesInFlatList, Func<T, string> path, Func<T, string> image, Func<T, ChangeKind> kind)
        where T : class
    {
        bool flat = sorting.ToString().EndsWith("Flat", StringComparison.Ordinal);
        bool showGroupNodes = !flat || showGroupNodesInFlatList;
        Func<T, string>? groupKey = sorting switch
        {
            DiffListSortType.FileExtension or DiffListSortType.FileExtensionFlat => file => Path.GetExtension(path(file)),
            DiffListSortType.FileStatus or DiffListSortType.FileStatusFlat => file => $"{StatusRank(kind(file))}{image(file)}",
            _ => null,
        };

        if (groupKey is null)
        {
            return PathTree(files, flat, denseTree, path, image, keyPrefix: "");
        }

        List<FileStatusTreeNode<T>> nodes = [];
        foreach (IGrouping<string, T> group in files.GroupBy(groupKey).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            IReadOnlyList<FileStatusTreeNode<T>> tree = PathTree(group, flat, denseTree, path, image, keyPrefix: $"{group.Key}|");

            // A group of one file shows the file alone.
            if (showGroupNodes && tree is [{ Kind: FileStatusTreeNodeKind.File } single])
            {
                nodes.Add(single);
            }
            else if (showGroupNodes)
            {
                bool byExtension = sorting is DiffListSortType.FileExtension or DiffListSortType.FileExtensionFlat;
                nodes.Add(new FileStatusTreeNode<T>(FileStatusTreeNodeKind.Group, byExtension ? group.Key : "",
                    byExtension ? DefaultFileImage : image(group.First()), $"group|{group.Key}", null, tree));
            }
            else
            {
                nodes.AddRange(tree);
            }
        }

        // A single group with files is the list itself.
        return nodes is [{ Kind: FileStatusTreeNodeKind.Group } only] ? only.Children : nodes;
    }

    /// <summary>
    ///  The rank of a status group, as the new shell orders the statuses (see <see cref="FileStatusList.Sort{T}"/>).
    /// </summary>
    private static int StatusRank(ChangeKind kind) => kind switch
    {
        ChangeKind.Conflict => 0,
        ChangeKind.Modified => 1,
        ChangeKind.Renamed => 2,
        ChangeKind.Added or ChangeKind.Untracked => 3,
        _ => 4,
    };

    // Upstream's StatusSorter.CreateTreeSortedByPath: the folders before the files at each level (PathFirstComparer), a
    // folder holding only one folder shown as one node ("a/b"), and with a dense tree a folder holding one file shown as
    // that file; each text without its parent folder's path.
    private static IReadOnlyList<FileStatusTreeNode<T>> PathTree<T>(IEnumerable<T> files, bool flat, bool denseTree,
        Func<T, string> path, Func<T, string> image, string keyPrefix)
        where T : class
    {
        List<T> list = [.. files];
        if (flat)
        {
            return
            [
                .. list.OrderBy(file => file, new PathFirstComparer<T>(path))
                    .Select(file => FileNode(file, path(file), path, image, keyPrefix))
            ];
        }

        Folder<T> root = new("");
        foreach (T file in list)
        {
            Folder<T> folder = root;
            string[] parts = path(file).Split('/');
            for (int i = 0; i < parts.Length - 1; i++)
            {
                string folderPath = string.Join('/', parts.Take(i + 1));
                if (!folder.Folders.TryGetValue(parts[i], out Folder<T>? child))
                {
                    child = new Folder<T>(folderPath);
                    folder.Folders.Add(parts[i], child);
                }

                folder = child;
            }

            folder.Files.Add(file);
        }

        return Children(root, parentPath: "");

        IReadOnlyList<FileStatusTreeNode<T>> Children(Folder<T> folder, string parentPath)
        {
            List<FileStatusTreeNode<T>> nodes = [];
            foreach (Folder<T> child in folder.Folders.Values.OrderBy(child => child.Path, StringComparer.InvariantCulture))
            {
                // Upstream creates a folder for a file's whole folder and splits it only where paths part.
                Folder<T> shown = child;
                while (shown.Files.Count == 0 && shown.Folders.Count == 1)
                {
                    shown = shown.Folders.Values.Single();
                }

                if (denseTree && shown.Folders.Count == 0 && shown.Files is [{ } single])
                {
                    nodes.Add(FileNode(single, Relative(path(single), parentPath), path, image, keyPrefix));
                    continue;
                }

                nodes.Add(new FileStatusTreeNode<T>(FileStatusTreeNodeKind.Folder, Relative(shown.Path, parentPath), FolderImage,
                    $"{keyPrefix}{shown.Path}/", null, Children(shown, shown.Path)));
            }

            nodes.AddRange(folder.Files.OrderBy(path, StringComparer.InvariantCulture)
                .Select(file => FileNode(file, Relative(path(file), parentPath), path, image, keyPrefix)));
            return nodes;
        }

        static string Relative(string fullPath, string parentPath)
            => parentPath.Length == 0 ? fullPath : fullPath[(parentPath.Length + 1)..];
    }

    private static FileStatusTreeNode<T> FileNode<T>(T file, string text, Func<T, string> path, Func<T, string> image,
        string keyPrefix)
        where T : class
        => new(FileStatusTreeNodeKind.File, text, image(file), $"{keyPrefix}{path(file)}", file, []);

    private sealed class Folder<T>(string path)
    {
        public string Path { get; } = path;

        public Dictionary<string, Folder<T>> Folders { get; } = new(StringComparer.Ordinal);

        public List<T> Files { get; } = [];
    }

    // Upstream's PathFirstComparer: by folder, a folder's subfolders before its own files, then by name.
    private sealed class PathFirstComparer<T>(Func<T, string> path) : IComparer<T>
    {
        public int Compare(T? left, T? right)
        {
            if (left is null || right is null)
            {
                return left is null ? right is null ? 0 : 1 : -1;
            }

            string l = path(left);
            string r = path(right);
            string leftFolder = FolderOf(l);
            string rightFolder = FolderOf(r);
            int comparison = (leftFolder, rightFolder) switch
            {
                ("", "") => 0,
                (_, "") => -1,
                ("", _) => 1,
                _ => ComparePath(leftFolder, rightFolder),
            };

            return comparison switch
            {
                < 0 => StartsWith(rightFolder, leftFolder) ? 1 : -1,
                > 0 => StartsWith(leftFolder, rightFolder) ? -1 : 1,
                _ => StringComparer.InvariantCulture.Compare(l, r),
            };
        }

        private static string FolderOf(string path) => path.LastIndexOf('/') is var slash and >= 0 ? path[..slash] : "";

        private static int ComparePath(string left, string right)
        {
            string[] l = left.Split('/');
            string[] r = right.Split('/');
            for (int i = 0; i < Math.Min(l.Length, r.Length); i++)
            {
                int part = string.Compare(l[i], r[i], StringComparison.InvariantCulture);
                if (part != 0)
                {
                    return Math.Sign(part);
                }
            }

            return l.Length.CompareTo(r.Length);
        }

        private static bool StartsWith(string longPath, string shortPath)
            => longPath.StartsWith(shortPath, StringComparison.InvariantCulture) && longPath.Length > shortPath.Length
               && longPath[shortPath.Length] == '/';
    }
}
