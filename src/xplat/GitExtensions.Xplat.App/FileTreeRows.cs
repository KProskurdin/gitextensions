using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FileStatusList</c> tree in a list box (core <see cref="FileStatusTree"/>): the folder and the extension
///  or status group nodes as rows with an expander that a click on it or a double-click on the row collapses or expands
///  (all expanded at first, as upstream's <c>ExpandAll</c>), the files below them indented and named below their folder
///  (set on each file's container, <see cref="FileRows"/>). A selected node stays selected when the list is filled again.
///  Shared by the browse window's Diff tab, FormDiff, FormStash and the commit window's lists.
/// </summary>
internal sealed class FileTreeRows<T>
    where T : class
{
    /// <summary>
    ///  The class of the expander in a node's template: a click on it collapses or expands the node.
    /// </summary>
    public const string ExpanderClass = "expander";

    private readonly ListBox _list;
    private readonly IAppPreferences _preferences;
    private readonly Func<T, string> _path;
    private readonly Func<T, string> _image;
    private readonly Func<T, ChangeKind> _kind;
    private readonly Action _refresh;

    // The folder and group nodes the user collapsed.
    private readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);

    // The text and depth of each shown file.
    private readonly Dictionary<object, (string Text, int Depth)> _fileRows = new(ReferenceEqualityComparer.Instance);

    // The nodes selected when the fill started.
    private HashSet<string> _selectedNodes = new(StringComparer.Ordinal);

    /// <param name="refresh">Fills the list again after a node was collapsed or expanded.</param>
    public FileTreeRows(ListBox list, IAppPreferences preferences, Func<T, string> path, Func<T, string> image,
        Func<T, ChangeKind> kind, Action refresh)
    {
        _list = list;
        _preferences = preferences;
        _path = path;
        _image = image;
        _kind = kind;
        _refresh = refresh;

        // Upstream's tree selects a node on a click and expands or collapses it on its glyph or on a double-click.
        list.AddHandler(InputElement.TappedEvent, (_, e) =>
        {
            if (IsExpander(e.Source) && RowOf(e.Source) is { } row)
            {
                Toggle(row);
            }
        }, handledEventsToo: true);
        list.AddHandler(InputElement.DoubleTappedEvent, (_, e) =>
        {
            if (!IsExpander(e.Source) && RowOf(e.Source) is { } row)
            {
                Toggle(row);
            }
        }, handledEventsToo: true);
        list.ContainerPrepared += (_, e) =>
        {
            if (e.Container.DataContext is { } item && _fileRows.TryGetValue(item, out (string Text, int Depth) row))
            {
                FileRows.SetText(e.Container, row.Text);
                FileRows.SetIndent(e.Container, FileRows.IndentOf(row.Depth));
            }
            else
            {
                // A container the list reuses for another kind of row.
                e.Container.ClearValue(FileRows.TextProperty);
                e.Container.ClearValue(FileRows.IndentProperty);
            }
        };
    }

    /// <summary>
    ///  Forgets the collapsed nodes, for a new set of files.
    /// </summary>
    public void Reset() => _collapsed.Clear();

    /// <summary>
    ///  Expands the nodes with <paramref name="keys"/> at the next fill.
    /// </summary>
    public void Expand(IEnumerable<string> keys) => _collapsed.ExceptWith(keys);

    /// <summary>
    ///  Makes the shown rows of <paramref name="files"/> the list's selection.
    /// </summary>
    public void Select(IEnumerable<object> files)
    {
        if (_list.SelectedItems is not { } selected)
        {
            return;
        }

        HashSet<object> wanted = new(files, ReferenceEqualityComparer.Instance);
        selected.Clear();
        foreach (object file in _list.Items.OfType<T>().Where(wanted.Contains))
        {
            selected.Add(file);
        }
    }

    /// <summary>
    ///  Starts a new fill of the list: the selected nodes are noted, to be selected again by <see cref="EndFill"/>.
    /// </summary>
    public void BeginFill()
    {
        _fileRows.Clear();
        _selectedNodes = new HashSet<string>(
            _list.SelectedItems?.OfType<IFileListNode>().Select(node => node.SelectionKey) ?? [], StringComparer.Ordinal);
    }

    /// <summary>
    ///  Ends a fill after the list shows its new rows: the nodes selected before are selected again.
    /// </summary>
    public void EndFill()
    {
        if (_selectedNodes.Count == 0 || _list.SelectedItems is not { } selected)
        {
            return;
        }

        foreach (IFileListNode node in _list.Items.OfType<IFileListNode>()
                     .Where(node => _selectedNodes.Contains(node.SelectionKey)))
        {
            selected.Add(node);
        }
    }


    /// <summary>
    ///  Upstream's tree items for the list (<c>FileStatusList.TreeContextMenu.cs</c>) outside file tree mode: select all,
    ///  collapse all and expand all for the selected nodes; no "Collapse root folders".
    /// </summary>
    /// <param name="setExpanded">Collapses or expands a selected node that is not a <see cref="FileTreeRow"/> (a diff
    /// group's).</param>
    public FileListTree MenuTree(Action<IFileListNode, bool>? setExpanded = null)
    {
        return new FileListTree(
            HasSubnodes: () => SelectedNodes().Count > 0,
            SelectAll: SelectAll,
            CollapseAll: () => SetExpanded(SelectedNodes(), false),
            ExpandAll: () => SetExpanded(SelectedNodes(), true),
            AnyRootExpanded: () => false,
            CollapseRootFolders: () => { });

        // Upstream's SelectAll_Click: the selected nodes expanded all the way down, and only the files below them selected.
        void SelectAll()
        {
            List<IFileListNode> nodes = SelectedNodes();
            List<object> files = [.. nodes.SelectMany(node => node.Files)];
            SetExpanded(nodes, true);
            Select(files);
        }

        void SetExpanded(IReadOnlyList<IFileListNode> nodes, bool expanded)
        {
            foreach (IFileListNode node in nodes)
            {
                foreach (string key in node.NodeKeys)
                {
                    if (expanded)
                    {
                        _collapsed.Remove(key);
                    }
                    else
                    {
                        _collapsed.Add(key);
                    }
                }

                if (node is not FileTreeRow)
                {
                    setExpanded?.Invoke(node, expanded);
                }
            }

            _refresh();
        }
    }

    /// <summary>
    ///  The keys of the folder and group nodes <paramref name="files"/> would show below <paramref name="keyPrefix"/>.
    /// </summary>
    public IReadOnlyList<string> NodeKeys(IReadOnlyList<T> files, string keyPrefix)
        => [.. Tree(files).SelectMany(node => node.NodeKeys()).Select(key => keyPrefix + key)];

    private List<IFileListNode> SelectedNodes() => [.. _list.SelectedItems?.OfType<IFileListNode>() ?? []];

    private void Toggle(FileTreeRow row)
    {
        if (!_collapsed.Remove(row.Key))
        {
            _collapsed.Add(row.Key);
        }

        _refresh();
    }

    private static FileTreeRow? RowOf(object? source)
        => (source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as FileTreeRow;

    private static bool IsExpander(object? source)
        => (source as Visual)?.FindAncestorOfType<TextBlock>(includeSelf: true)?.Classes.Contains(ExpanderClass) == true;

    private IReadOnlyList<FileStatusTreeNode<T>> Tree(IReadOnlyList<T> files)
        => FileStatusTree.Build(files, _preferences.DiffListSorting, _preferences.FileStatusMergeSingleItemWithFolder,
            _preferences.FileStatusShowGroupNodesInFlatList, _path, _image, _kind);

    /// <summary>
    ///  Adds the rows of <paramref name="files"/> at <paramref name="depth"/>, their nodes' keys after
    ///  <paramref name="keyPrefix"/> (a diff group's).
    /// </summary>
    public void Add(List<object> items, IReadOnlyList<T> files, int depth, string keyPrefix)
        => AddNodes(items, Tree(files), depth, keyPrefix);

    /// <summary>
    ///  Upstream's <c>CollapseGroups_Click</c> for the extension and status groups among <paramref name="rows"/>; false
    ///  when none was expanded.
    /// </summary>
    public bool CollapseGroups(IEnumerable<object> rows)
    {
        bool collapsed = false;
        foreach (FileTreeRow row in rows.OfType<FileTreeRow>().Where(row => row.IsGroup && row.IsExpanded))
        {
            _collapsed.Add(row.Key);
            collapsed = true;
        }

        return collapsed;
    }

    private void AddNodes(List<object> items, IReadOnlyList<FileStatusTreeNode<T>> nodes, int depth, string keyPrefix)
    {
        foreach (FileStatusTreeNode<T> node in nodes)
        {
            if (node.File is { } file)
            {
                items.Add(file);
                _fileRows[file] = (node.Text, depth);
                continue;
            }

            string key = keyPrefix + node.Key;
            bool expanded = !_collapsed.Contains(key);
            items.Add(new FileTreeRow(node.Text, node.Image, key, expanded, FileRows.IndentOf(depth),
                node.Kind == FileStatusTreeNodeKind.Group)
            {
                Folder = node.FolderPath,
                Files = [.. node.Files()],
                NodeKeys = [.. node.NodeKeys().Select(nodeKey => keyPrefix + nodeKey)]
            });
            if (expanded)
            {
                AddNodes(items, node.Children, depth + 1, keyPrefix);
            }
        }
    }
}

/// <summary>
///  Upstream <c>FileStatusList</c>'s toolbar buttons for the tree: collapse groups (only for the lists that can show diff
///  groups, as upstream shows it), the tree split button (flat list or tree) and its menu's six groupings, dense tree and
///  group nodes in a flat list.
/// </summary>
internal sealed record FileListTreeButtons(Button? CollapseGroups, ToolSplitButton AsTree,
    IReadOnlyList<(MenuItem Item, DiffListSortType Sorting)> Sortings, MenuItem DenseTree, MenuItem ShowGroupNodesInFlatList)
{
    // Upstream's DiffListSortType names its flat variants with this suffix.
    private const string FlatSuffix = "Flat";

    /// <summary>
    ///  Upstream's <c>AsTree_ButtonClick</c>, the menu's groupings and options: each sets upstream's setting, then
    ///  <paramref name="changed"/> shows the lists again.
    /// </summary>
    public void Wire(IAppPreferences preferences, Action changed)
    {
        AsTree.Click += (_, _) =>
        {
            string sorting = preferences.DiffListSorting.ToString();
            Set(Enum.Parse<DiffListSortType>(sorting.EndsWith(FlatSuffix, StringComparison.Ordinal)
                ? sorting[..^FlatSuffix.Length]
                : sorting + FlatSuffix));
        };
        foreach ((MenuItem item, DiffListSortType sorting) in Sortings)
        {
            item.Click += (_, _) => Set(sorting);
        }

        DenseTree.Click += (_, _) =>
        {
            preferences.FileStatusMergeSingleItemWithFolder = !preferences.FileStatusMergeSingleItemWithFolder;
            Set(preferences.DiffListSorting);
        };
        ShowGroupNodesInFlatList.Click += (_, _) =>
        {
            preferences.FileStatusShowGroupNodesInFlatList = !preferences.FileStatusShowGroupNodesInFlatList;
            Set(preferences.DiffListSorting);
        };
        return;

        void Set(DiffListSortType sorting)
        {
            preferences.DiffListSorting = sorting;
            preferences.Save();
            changed();
        }
    }

    /// <summary>
    ///  Upstream's tree image (the flat list's while a flat variant is chosen) and the menu's checks.
    /// </summary>
    public void Show(IAppPreferences preferences)
    {
        DiffListSortType sorting = preferences.DiffListSorting;
        AsTree.Image = IsFlat(sorting) ? "DocumentTree" : "FileTree";
        foreach ((MenuItem item, DiffListSortType itemSorting) in Sortings)
        {
            item.IsChecked = itemSorting == sorting;
        }

        // Upstream's UpdateToolbar: a dense tree only for a tree, group nodes only for a flat list grouped by extension or
        // status.
        DenseTree.IsChecked = preferences.FileStatusMergeSingleItemWithFolder;
        DenseTree.IsEnabled = !IsFlat(sorting);
        ShowGroupNodesInFlatList.IsChecked = preferences.FileStatusShowGroupNodesInFlatList;
        ShowGroupNodesInFlatList.IsEnabled = IsFlat(sorting) && sorting is not DiffListSortType.FilePathFlat;
    }

    /// <summary>
    ///  Upstream's group-by buttons: the grouping chosen keeps the tree or flat variant.
    /// </summary>
    public static DiffListSortType GroupBy(DiffListSortType current, DiffListSortType grouping)
        => IsFlat(current) ? Enum.Parse<DiffListSortType>(grouping + FlatSuffix) : grouping;

    private static bool IsFlat(DiffListSortType sorting)
        => sorting.ToString().EndsWith(FlatSuffix, StringComparison.Ordinal);
}

/// <summary>
///  A node of upstream's <c>FileStatusList</c> tree with nodes below it, as the file list menu takes it when it is the only
///  one selected (upstream's <c>SelectedFolder</c> and <c>SelectedItems</c>).
/// </summary>
internal interface IFileListNode
{
    /// <summary>
    ///  A folder node's path; "" for a group node (upstream takes a node not tagged with a path as the root folder).
    /// </summary>
    string Folder { get; }

    /// <summary>
    ///  The files at or below the node, also those of its collapsed nodes.
    /// </summary>
    IReadOnlyList<object> Files { get; }

    /// <summary>
    ///  The collapse keys of the node and of the nodes below it.
    /// </summary>
    IReadOnlyList<string> NodeKeys { get; }

    /// <summary>
    ///  Names the node across a fill of the list.
    /// </summary>
    string SelectionKey { get; }
}

/// <summary>
///  The selected node of a file list shown as a tree.
/// </summary>
internal static class FileListNodes
{
    /// <summary>
    ///  Upstream FileStatusList's <c>SelectNextVisibleItem</c> and <c>SelectPreviousVisibleItem</c>, as continuous scroll
    ///  uses them: the next or previous shown file becomes the only one selected (the same one at the list's end), the
    ///  first file when none is selected.
    /// </summary>
    public static void SelectNeighbourFile(ListBox list, bool next, Func<object, bool> isFile)
    {
        List<object> items = [.. list.Items.OfType<object>()];
        int current = list.SelectedItem is { } selected ? items.IndexOf(selected) : -1;
        object? target = current < 0
            ? items.FirstOrDefault(isFile)
            : next
                ? items.Skip(current + 1).FirstOrDefault(isFile)
                : items.Take(current).LastOrDefault(isFile);
        if (target is null)
        {
            return;
        }

        list.SelectedItem = target;
        list.ScrollIntoView(target);
    }

    /// <summary>
    ///  Upstream FileStatusList's <c>SelectNextItem</c>: the shown file after (or before) the selected one becomes the only
    ///  one selected, around at the ends with <paramref name="loop"/>; the first or last file without a selection. The
    ///  selected file, or null when there is none to select.
    /// </summary>
    public static object? SelectNextFile(ListBox list, bool backward, bool loop, Func<object, bool> isFile)
    {
        List<object> files = [.. list.Items.OfType<object>().Where(isFile)];
        if (files.Count == 0)
        {
            return null;
        }

        int current = list.SelectedItem is { } selected ? files.IndexOf(selected) : -1;
        int next = current < 0
            ? backward ? files.Count - 1 : 0
            : current + (backward ? -1 : 1);
        if (next < 0 || next >= files.Count)
        {
            if (!loop)
            {
                return list.SelectedItem;
            }

            next = backward ? files.Count - 1 : 0;
        }

        list.SelectedItem = files[next];
        list.ScrollIntoView(files[next]);
        return files[next];
    }

    /// <summary>
    ///  The selection when it is a single folder or group node, as upstream's <c>SelectedFolder</c>; else null.
    /// </summary>
    public static IFileListNode? SelectedNode(ListBox list)
        => list.SelectedItems is { Count: 1 } selected && selected[0] is IFileListNode node ? node : null;
}

/// <summary>
///  A folder node, or a group node of the extension or status grouping, of upstream's <c>FileStatusList</c> tree.
/// </summary>
public sealed class FileTreeRow(string text, string image, string key, bool isExpanded, Thickness indent, bool isGroup)
    : IFileListNode
{
    public string Text { get; } = text;

    public string Image { get; } = image;

    public string Key { get; } = key;

    public bool IsExpanded { get; } = isExpanded;

    public Thickness Indent { get; } = indent;

    /// <summary>
    ///  An extension or status group (collapsed by upstream's collapse button), not a folder.
    /// </summary>
    public bool IsGroup { get; } = isGroup;

    // The tree's expander, as a glyph.
    public string Expander => IsExpanded ? "▾" : "▸";

    /// <summary>
    ///  A folder's path; "" for a group.
    /// </summary>
    public string Folder { get; init; } = "";

    IReadOnlyList<object> IFileListNode.Files => Files;

    internal IReadOnlyList<object> Files { get; init; } = [];

    IReadOnlyList<string> IFileListNode.NodeKeys => NodeKeys;

    internal IReadOnlyList<string> NodeKeys { get; init; } = [];

    string IFileListNode.SelectionKey => "node\n" + Key;
}

/// <summary>
///  The text and indent of a file's row in a tree of files, set on its container for its template to read.
/// </summary>
public static class FileRows
{
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Text", typeof(FileRows));

    public static readonly AttachedProperty<Thickness> IndentProperty =
        AvaloniaProperty.RegisterAttached<Control, Thickness>("Indent", typeof(FileRows));

    // Upstream's tree indents each level by about a node's image.
    private const double IndentPerLevel = 16;

    public static string? GetText(Control control) => control.GetValue(TextProperty);

    public static void SetText(Control control, string? value) => control.SetValue(TextProperty, value);

    public static Thickness GetIndent(Control control) => control.GetValue(IndentProperty);

    public static void SetIndent(Control control, Thickness value) => control.SetValue(IndentProperty, value);

    /// <summary>
    ///  The indent of a row at <paramref name="depth"/> in the tree.
    /// </summary>
    public static Thickness IndentOf(int depth) => new(depth * IndentPerLevel, 0, 0, 0);
}
