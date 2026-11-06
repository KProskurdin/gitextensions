using System.Text.RegularExpressions;
using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The File tree tab's file list menu (<see cref="FileStatusListMenu"/>) as upstream's File tree tab has it: a
///  <c>RevisionDiffControl</c> in file tree mode, whose FileStatusList lists the selected commit's files with no first
///  revision, offers the tree items, and toggles Blame in place of the file's content.
/// </summary>
public partial class MainWindow
{
    // Upstream's tsmiBlame.Checked in file tree mode: the selected file shows as blame, and so does every file selected
    // after it until Blame is clicked again.
    private bool _fileTreeBlame;


    // The File tree tab's files (the commit's, or a git-grep search's) before the filter.
    private IReadOnlyList<string> _fileTreeFiles = [];

    // Upstream's file tree mode keeps the list's filter box: the tree shows the files whose paths match the regular
    // expression (an invalid one shows its error as a tooltip and filters nothing), all expanded while they are few when
    // the list is filtered or searched; the selected file stays selected when it is still shown.
    private void ShowFileTreeFiles()
    {
        List<string> shown = [.. _fileTreeFiles.Where(file => FileTreeFilterBox.IsMatch(file))];
        string? selected = (FileTreeView.SelectedItem as FileTreeNode)?.Path;
        IReadOnlyList<FileTreeNode> roots = FileTree.Build(shown);
        if ((FileTreeFilterBox.Filter is not null || _fileTreeGrep.Length > 0) && shown.Count < MaxExpandedFiles)
        {
            SetExpanded(roots.SelectMany(root => root.Descendants()), true);
        }

        FileTreeView.ItemsSource = roots;
        if (selected is not null)
        {
            SelectInFileTree(selected);
        }

        FileTreeFilterBox.Shown(shown.Count);
    }

    private void WireFileTreeMenu()
    {
        FileTreeFilterBox.WorkingDirectory = () => RepositoryPath;
        FileTreeFilterBox.FilterChanged += (_, _) => ShowFileTreeFiles();
        FileTreeContextMenu.Bind(new FileStatusListMenuBinding
        {
            Owner = this,
            RepositoryPath = () => RepositoryPath,
            Selection = FileTreeMenuSelection,
            DescribeRevision = DescribeRevision,
            Services = () => FileListMenuServices,
            Refresh = RefreshRepository,
            FilterFileInGrid = filter => ApplyFilter(_commits.Filter with { PathFilter = filter }),
            RunScript = script =>
                RunOnRepository(path => Scripts.RunAsync(script, path, SelectedHashes, FileScriptOptions())),
            FindFile = new FileListFind(
                () => [.. TreeFiles().Select(file => (file.Path, (string?)null))],
                SelectInFileTree),
            Blame = ToggleFileTreeBlame,
            GitGrep = _fileTreeGitGrep,
            IsFileTreeMode = true,
            BlameChecked = () => _fileTreeBlame,
            CurrentCheckout = () => _commits.VisibleRows.FirstOrDefault(item => item.IsCheckedOut)?.Row.Hash,
            Tree = new FileListTree(
                HasSubnodes: () => SelectedTreeNodes().Any(node => node.Children.Count > 0),
                SelectAll: SelectAllInFileTree,
                CollapseAll: () => SetExpanded(SelectedTreeNodes().SelectMany(node => node.Descendants()), false),
                ExpandAll: () => SetExpanded(SelectedTreeNodes().SelectMany(node => node.Descendants()), true),
                AnyRootExpanded: () => TreeRoots().Any(root => root.IsExpanded),
                CollapseRootFolders: CollapseRootFolders),
        });
        FileTreeBlame.CommitRequested += (_, hash) => Run(() => GoToRevisionAsync(hash, showNoRevisionMessage: true));
        FileTreeBlame.ErrorOccurred += (_, message) => ShowError(message);
        FileTreeBlame.RepositoryHost = RelevantRepositoryHost;
    }

    // Upstream's FileStatusList in file tree mode: the files are the tree's (GetTreeFiles) with no first revision and the
    // commit as the second; one selected node stands for its files (a folder for every file below it, upstream's
    // SelectedFolder), several for the files among them.
    private DiffMenuSelection? FileTreeMenuSelection()
    {
        if (!ObjectId.TryParse(_fileTreeHash, out ObjectId second)
            || SelectedTreeNodes() is not { Count: > 0 } nodes)
        {
            return null;
        }

        IEnumerable<FileTreeNode> files = nodes is [{ } single] ? single.Files() : nodes.Where(node => !node.IsFolder);
        return new DiffMenuSelection(null, second, ParentsOf(second),
            [.. files.Select(file => new DiffMenuFile("", file.Path, null, IsTracked: true))],
            Folder: nodes is [{ IsFolder: true } folder] ? folder.Path : null,
            TreeFiles: true);
    }

    private List<FileTreeNode> SelectedTreeNodes() => [.. FileTreeView.SelectedItems?.OfType<FileTreeNode>() ?? []];

    private IEnumerable<FileTreeNode> TreeRoots() => FileTreeView.ItemsSource as IEnumerable<FileTreeNode> ?? [];

    private IEnumerable<FileTreeNode> TreeFiles() => TreeRoots().SelectMany(root => root.Files());

    private static void SetExpanded(IEnumerable<FileTreeNode> nodes, bool expanded)
    {
        foreach (FileTreeNode node in nodes.Where(node => node.IsFolder).ToList())
        {
            node.IsExpanded = expanded;
        }
    }

    // Upstream's SelectAll_Click: the selected nodes expanded all the way down, and only the files below them selected.
    private void SelectAllInFileTree()
    {
        List<FileTreeNode> nodes = SelectedTreeNodes();
        SetExpanded(nodes.SelectMany(node => node.Descendants()), true);
        List<FileTreeNode> files = [.. nodes.SelectMany(node => node.Files()).Distinct()];
        if (FileTreeView.SelectedItems is not { } selected)
        {
            return;
        }

        selected.Clear();
        foreach (FileTreeNode file in files)
        {
            selected.Add(file);
        }
    }

    // Upstream's CollapseRootFolders_Click: the selected node's root folder becomes the selection, then the root folders
    // collapse (the folders below them keep their state).
    private void CollapseRootFolders()
    {
        if (FileTreeView.SelectedItem is FileTreeNode node
            && TreeRoots().FirstOrDefault(root => root != node && root.Descendants().Contains(node)) is { } root)
        {
            FileTreeView.SelectedItem = root;
        }

        foreach (FileTreeNode folder in TreeRoots().Where(folder => folder.IsFolder))
        {
            folder.IsExpanded = false;
        }
    }

    // Upstream RevisionDiffControl's BlameFile in file tree mode: Blame is checked or unchecked and the selected file shown
    // again.
    private void ToggleFileTreeBlame()
    {
        _fileTreeBlame = !_fileTreeBlame;
        if (!_fileTreeBlame)
        {
            ShowFileTreeBlame(false);
        }

        Run(ShowSelectedTreeFileAsync);
    }

    private void ShowFileTreeBlame(bool show)
    {
        FileTreeBlame.IsVisible = show;
        FileContentView.IsVisible = !show;
    }

    /// <summary>
    ///  Upstream's <c>SelectFileOrFolder</c> of the File tree tab (its <c>OpenInFileTreeTab</c>): the tab shows, the file is
    ///  selected once the tree is read, and with <paramref name="blame"/> it shows as blame.
    /// </summary>
    private async Task ShowInFileTreeAsync(string file, bool blame)
    {
        DetailTabs.SelectedItem = FileTreeTab;
        if (blame)
        {
            _fileTreeBlame = true;
        }

        await _fileTreeLoad;
        SelectInFileTree(file);
        if (blame)
        {
            await ShowSelectedTreeFileAsync();
        }
    }
}
