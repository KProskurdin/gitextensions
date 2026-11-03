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
///  or status group nodes as rows with an expander that a click collapses or expands (all expanded at first, as upstream's
///  <c>ExpandAll</c>), the files below them indented and named below their folder (set on each file's container,
///  <see cref="FileRows"/>). Shared by the browse window's Diff tab, FormDiff and the commit window's lists.
/// </summary>
internal sealed class FileTreeRows<T>
    where T : class
{
    private readonly IAppPreferences _preferences;
    private readonly Func<T, string> _path;
    private readonly Func<T, string> _image;
    private readonly Func<T, ChangeKind> _kind;

    // The folder and group nodes the user collapsed.
    private readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);

    // The text and depth of each shown file.
    private readonly Dictionary<object, (string Text, int Depth)> _fileRows = new(ReferenceEqualityComparer.Instance);

    /// <param name="refresh">Fills the list again after a node was collapsed or expanded.</param>
    public FileTreeRows(ListBox list, IAppPreferences preferences, Func<T, string> path, Func<T, string> image,
        Func<T, ChangeKind> kind, Action refresh)
    {
        _preferences = preferences;
        _path = path;
        _image = image;
        _kind = kind;
        list.AddHandler(InputElement.TappedEvent, (_, e) =>
        {
            if ((e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is FileTreeRow row)
            {
                if (!_collapsed.Remove(row.Key))
                {
                    _collapsed.Add(row.Key);
                }

                refresh();
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
    ///  Starts a new fill of the list.
    /// </summary>
    public void BeginFill() => _fileRows.Clear();

    /// <summary>
    ///  Adds the rows of <paramref name="files"/> at <paramref name="depth"/>, their nodes' keys after
    ///  <paramref name="keyPrefix"/> (a diff group's).
    /// </summary>
    public void Add(List<object> items, IReadOnlyList<T> files, int depth, string keyPrefix)
        => AddNodes(items, FileStatusTree.Build(files, _preferences.DiffListSorting,
            _preferences.FileStatusMergeSingleItemWithFolder, _preferences.FileStatusShowGroupNodesInFlatList, _path,
            _image, _kind), depth, keyPrefix);

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
                node.Kind == FileStatusTreeNodeKind.Group));
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
///  A folder node, or a group node of the extension or status grouping, of upstream's <c>FileStatusList</c> tree.
/// </summary>
public sealed class FileTreeRow(string text, string image, string key, bool isExpanded, Thickness indent, bool isGroup)
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
