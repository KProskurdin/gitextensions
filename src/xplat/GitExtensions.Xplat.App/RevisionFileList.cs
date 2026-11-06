using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FileStatusList</c> for the files of a commit or of a diff between two revisions (the browse window's Diff
///  tab, FormDiff): the group-by buttons (upstream's <c>DiffListSortType</c>, shared with the commit window's lists), the
///  regular expression filter (an invalid one shows its error as a tooltip and filters nothing) and the NoFiles text while
///  a loaded list shows no file. The files are upstream's tree (<see cref="FileTreeRows{T}"/>) below the diff groups'
///  nodes (<see cref="DiffGroupHeader"/>); upstream's tree button switches to the flat variant. The selected files stay
///  selected when the list is grouped or filtered again.
/// </summary>
internal sealed class RevisionFileList<T>
    where T : class
{
    // Upstream's texts.
    public const string NoChanges = "No changes";


    // Upstream's icons of the BASE groups (FileStatusDiffCalculator).
    private const string DiffAIcon = "DiffA";
    private const string DiffBIcon = "DiffB";

    private readonly ListBox _list;
    private readonly FileFilterBox _filter;
    private readonly Func<T, string?>? _oldPath;
    private readonly ToolToggleButton _byPath;
    private readonly ToolToggleButton _byExtension;
    private readonly ToolToggleButton _byStatus;
    private readonly TextBlock _noFiles;
    private readonly IAppPreferences _preferences;
    private readonly Func<T, string> _path;
    private readonly Func<T, DiffGroup?>? _groupOf;
    private readonly FileListTreeButtons? _treeButtons;
    private readonly DiffStatusButtons? _statusButtons;
    private readonly Func<T, DiffBranchStatus> _diffStatus;
    private readonly FileTreeRows<T> _tree;
    private readonly Dictionary<DiffGroup, bool> _expanded = new(ReferenceEqualityComparer.Instance);
    private IReadOnlyList<T>? _files;

    /// <param name="groupOf">The diff group a file is listed in; with files of several groups the list shows upstream's
    ///  group nodes (<see cref="DiffGroupHeader"/>).</param>
    /// <param name="treeButtons">Upstream's collapse and tree buttons, for the lists whose toolbar has them.</param>
    /// <param name="diffStatus">Upstream's <c>DiffStatus</c> of a file in a BASE group, for its image and the
    ///  <paramref name="statusButtons"/>' filter.</param>
    /// <param name="oldPath">A file's path before a rename, which the filter also matches.</param>
    public RevisionFileList(ListBox list, FileFilterBox filter, ToolToggleButton byPath, ToolToggleButton byExtension,
        ToolToggleButton byStatus, TextBlock noFiles, IAppPreferences preferences, Func<T, string> path,
        Func<T, string> status, Func<T, DiffGroup?>? groupOf = null, FileListTreeButtons? treeButtons = null,
        Func<T, DiffBranchStatus>? diffStatus = null, DiffStatusButtons? statusButtons = null,
        Func<T, string?>? oldPath = null)
    {
        _groupOf = groupOf;
        _treeButtons = treeButtons;
        _statusButtons = statusButtons;
        _diffStatus = diffStatus ?? (_ => DiffBranchStatus.Unknown);
        _list = list;
        _filter = filter;
        _oldPath = oldPath;
        _byPath = byPath;
        _byExtension = byExtension;
        _byStatus = byStatus;
        _noFiles = noFiles;
        _preferences = preferences;
        _path = path;
        _tree = new FileTreeRows<T>(list, preferences, path,
            file => FileStatusList.ImageForStatus(status(file), _diffStatus(file)),
            file => FileStatusList.KindOf(status(file)), Show);
        statusButtons?.Wire(Show);
        _noFiles.Text = UpstreamTranslation.ByText(NoChanges);
        _noFiles.IsVisible = false;
        _filter.FilterChanged += (_, _) => Show();
        _byPath.Click += (_, _) => SetGroupBy(DiffListSortType.FilePath);
        _byExtension.Click += (_, _) => SetGroupBy(DiffListSortType.FileExtension);
        _byStatus.Click += (_, _) => SetGroupBy(DiffListSortType.FileStatus);
        if (treeButtons is not null)
        {
            treeButtons.Wire(preferences, () =>
            {
                ShowGroupBy();
                Show();
            });
            treeButtons.CollapseGroups?.Click += (_, _) => CollapseGroups();
        }

        ShowGroupBy();

        // Upstream's diff group nodes expand and collapse on a click on their glyph or on a double-click.
        _list.AddHandler(InputElement.TappedEvent, (_, e) =>
        {
            if (IsExpander(e.Source) && HeaderOf(e.Source) is { } header)
            {
                Toggle(header);
            }
        }, handledEventsToo: true);
        _list.AddHandler(InputElement.DoubleTappedEvent, (_, e) =>
        {
            if (!IsExpander(e.Source) && HeaderOf(e.Source) is { } header)
            {
                Toggle(header);
            }
        }, handledEventsToo: true);
        MenuTree = _tree.MenuTree((node, expanded) =>
        {
            if (node is DiffGroupHeader header)
            {
                _expanded[header.Group] = expanded;
            }
        });
        return;

        static DiffGroupHeader? HeaderOf(object? source)
            => (source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as DiffGroupHeader;

        static bool IsExpander(object? source)
            => (source as Visual)?.FindAncestorOfType<TextBlock>(includeSelf: true)?.Classes
                .Contains(FileTreeRows<T>.ExpanderClass) == true;

        void Toggle(DiffGroupHeader header)
        {
            _expanded[header.Group] = !header.IsExpanded;
            Show();
        }
    }

    /// <summary>
    ///  Upstream's tree items of the list's menu, for its folder, group and diff group nodes.
    /// </summary>
    public FileListTree MenuTree { get; }

    /// <summary>
    ///  The files to list; null while they are being read (the list is empty, without the NoFiles text).
    /// </summary>
    public IReadOnlyList<T>? Files
    {
        get => _files;
        set
        {
            _files = value;
            _expanded.Clear();
            _tree.Reset();
            Show();
        }
    }

    // Upstream's filtered and grouped list, keeping the selection.
    private void Show()
    {
        HashSet<string> selected = [.. (_list.SelectedItems ?? Array.Empty<object>()).OfType<T>().Select(Key)];

        if (_files is null)
        {
            _list.ItemsSource = null;
            _noFiles.IsVisible = false;
            return;
        }

        List<DiffGroup> groups = _groupOf is null
            ? []
            :
            [
                .. _files.Select(_groupOf).OfType<DiffGroup>().Distinct(ReferenceEqualityComparer.Instance)
                    .Cast<DiffGroup>()
            ];
        List<object> items = [];
        _tree.BeginFill();
        int fileCount = 0;
        if (groups.Count < 2)
        {
            IReadOnlyList<T> shown = Matching(_files);
            _tree.Add(items, shown, depth: 0, keyPrefix: "");
            fileCount = shown.Count;
        }
        else
        {
            // Upstream's GetNodes: a node per group, its count and summary, the files below it while it is expanded.
            for (int index = 0; index < groups.Count; index++)
            {
                DiffGroup group = groups[index];
                IReadOnlyList<T> shown = Matching(_files.Where(file => ReferenceEquals(_groupOf?.Invoke(file), group)));
                bool expanded = _expanded.TryGetValue(group, out bool state)
                    ? state
                    : group.ExpandedAt(index, groups.Count);
                string keyPrefix = $"{group.Summary}\n";
                items.Add(new DiffGroupHeader(group, group.Header(shown.Count), expanded)
                {
                    Files = [.. shown],
                    NodeKeys = _tree.NodeKeys(shown, keyPrefix)
                });
                if (expanded)
                {
                    _tree.Add(items, shown, depth: 1, keyPrefix);
                }

                fileCount += shown.Count;
            }
        }

        _list.ItemsSource = items;
        _noFiles.IsVisible = fileCount == 0 && groups.Count < 2;
        _filter.Shown(fileCount);

        // Upstream's UpdateToolbar shows the collapse button while the list has diff group nodes, and the diff status
        // filter while it has a BASE group (HasDiffABGroups).
        _treeButtons?.CollapseGroups?.IsVisible = groups.Count >= 2;
        _statusButtons?.SetVisible(groups.Any(group => group.Icon is DiffAIcon or DiffBIcon));
        // Avalonia keeps a selected item that the new list has an equal one of (the files are records); adding that one
        // too would list the file twice in SelectedItems.
        foreach (T file in items.OfType<T>().Where(file => selected.Contains(Key(file))))
        {
            if (_list.SelectedItems is { } selectedItems && !selectedItems.Contains(file))
            {
                selectedItems.Add(file);
            }
        }

        _tree.EndFill();

        // Upstream's IsFilterMatch: the diff status filter, then the regular expression.
        IReadOnlyList<T> Matching(IEnumerable<T> files)
            =>
            [
                .. files.Where(file => (_statusButtons?.Shows(_diffStatus(file)) ?? true)
                                       && _filter.IsMatch(_path(file), _oldPath?.Invoke(file)))
            ];
    }

    // Upstream's CollapseGroups_Click: the diff groups and the extension or status groups collapse; when none was expanded,
    // the selected file's diff group expands.
    private void CollapseGroups()
    {
        List<object> rows = [.. _list.ItemsSource?.Cast<object>() ?? []];
        bool collapsed = false;
        foreach (DiffGroupHeader header in rows.OfType<DiffGroupHeader>().Where(header => header.IsExpanded))
        {
            _expanded[header.Group] = false;
            collapsed = true;
        }

        collapsed |= _tree.CollapseGroups(rows);
        if (!collapsed && _list.SelectedItem is T file && _groupOf?.Invoke(file) is { } group)
        {
            _expanded[group] = true;
        }

        Show();
    }

    // A file is the same across a reload by its group's summary and its path.
    private string Key(T file) => $"{_groupOf?.Invoke(file)?.Summary}\n{_path(file)}";

    /// <summary>
    ///  Upstream's <c>SelectedGitItem</c> setter, as "Find file" uses it: the first shown file with the path is the only one
    ///  selected and scrolled into view.
    /// </summary>
    public void SelectPath(string path)
    {
        if (_list.Items.OfType<T>().FirstOrDefault(file => _path(file) == path) is { } file)
        {
            _list.SelectedItem = file;
            _list.ScrollIntoView(file);
        }
    }

    private void SetGroupBy(DiffListSortType grouping)
    {
        _preferences.DiffListSorting = FileListTreeButtons.GroupBy(_preferences.DiffListSorting, grouping);
        _preferences.Save();
        ShowGroupBy();
        Show();
    }

    private void ShowGroupBy()
    {
        DiffListSortType sorting = _preferences.DiffListSorting;
        _byPath.IsChecked = sorting is DiffListSortType.FilePath or DiffListSortType.FilePathFlat;
        _byExtension.IsChecked = sorting is DiffListSortType.FileExtension or DiffListSortType.FileExtensionFlat;
        _byStatus.IsChecked = sorting is DiffListSortType.FileStatus or DiffListSortType.FileStatusFlat;
        _treeButtons?.Show(_preferences);
    }
}

/// <summary>
///  Upstream FileStatusList's diff status filter (btnUnequalChange, btnOnlyB, btnOnlyA, btnSameChange): each shows the files
///  of the BASE groups changed on its side, all checked at first; shown with their separator while the list has a BASE
///  group.
/// </summary>
internal sealed record DiffStatusButtons(ToolToggleButton Unequal, ToolToggleButton OnlyB, ToolToggleButton OnlyA,
    ToolToggleButton Same, Control Separator)
{
    /// <summary>
    ///  Fills the list again after a button was clicked.
    /// </summary>
    public void Wire(Action changed)
    {
        foreach (ToolToggleButton button in new[] { Unequal, OnlyB, OnlyA, Same })
        {
            button.Click += (_, _) => changed();
        }
    }

    /// <summary>
    ///  Upstream's <c>IsDiffStatusMatch</c>.
    /// </summary>
    public bool Shows(DiffBranchStatus status) => status switch
    {
        DiffBranchStatus.UnequalChange => Unequal.IsChecked == true,
        DiffBranchStatus.OnlyBChange => OnlyB.IsChecked == true,
        DiffBranchStatus.OnlyAChange => OnlyA.IsChecked == true,
        DiffBranchStatus.SameChange => Same.IsChecked == true,
        _ => true,
    };

    public void SetVisible(bool visible)
    {
        foreach (Control control in new Control[] { Unequal, OnlyB, OnlyA, Same, Separator })
        {
            control.IsVisible = visible;
        }
    }
}

/// <summary>
///  Upstream's group node of a <c>FileStatusList</c> with several diff groups: the group's icon, its count and summary,
///  and whether its files are shown below it.
/// </summary>
public sealed class DiffGroupHeader(DiffGroup group, string text, bool isExpanded) : IFileListNode
{
    public DiffGroup Group { get; } = group;

    public string Text { get; } = text;

    public bool IsExpanded { get; } = isExpanded;

    // The tree's expander, as a glyph.
    public string Expander => IsExpanded ? "▾" : "▸";

    // Upstream's group node is not tagged with a path: the menu takes it as the root folder.
    string IFileListNode.Folder => "";

    IReadOnlyList<object> IFileListNode.Files => Files;

    internal IReadOnlyList<object> Files { get; init; } = [];

    IReadOnlyList<string> IFileListNode.NodeKeys => NodeKeys;

    internal IReadOnlyList<string> NodeKeys { get; init; } = [];

    string IFileListNode.SelectionKey => "group\n" + Group.Summary;
}
