using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

// Upstream FormCommit's two file lists (FileStatusList: its toolbar, filter box, NoFiles text and item menu) and the
// toolbarStaged between them. What they list, their grouping and their buttons' and settings' commands are
// CommitViewModel's (CommitViewModel.FileLists.cs); the trees, the filters and the file menu stay here.
public partial class CommitWindow
{
    private FileListParts _unstaged = null!;
    private FileListParts _staged = null!;

    // HEAD's commit, the first revision of the staged files; read again after the changes change.
    private ObjectId? _headId;

    /// <summary>
    ///  The browse window's submodule commands for upstream's submodule items; null leaves them out (a commit window on a
    ///  submodule).
    /// </summary>
    internal FileListSubmodules? Submodules { get; init; }

    private void WireFileLists()
    {
        _unstaged = new FileListParts(Staged: false, UnstagedList, UnstagedFilterBox, UnstagedNoFilesText,
            UnstagedFilesMenu);
        _staged = new FileListParts(Staged: true, StagedList, StagedFilterBox, StagedNoFilesText, StagedFilesMenu);

        // Upstream's OtherColors.MergeConflictsColor.
        SolveConflictsButton.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(230, 99, 99));

        // The toolbars' flyouts give their items a data context only when they open (MVVM.md, Flyouts).
        foreach (FlyoutBase? flyout in new[]
                 {
                     UnstagedAsTreeButton.Flyout, StagedAsTreeButton.Flyout, UnstagedSettingsButton.Flyout,
                     StagedSettingsButton.Flyout,
                 })
        {
            foreach (Control item in ((MenuFlyout)flyout!).Items.OfType<Control>())
            {
                item.DataContext = ViewModel;
            }
        }

        foreach (FileListParts parts in new[] { _unstaged, _staged })
        {
            Wire(parts);
        }

        ViewModel.Grouping.Changed += (_, _) => ShowChanges();
        ViewModel.FilterClearRequested += (_, staged) => (staged ? _staged : _unstaged).Filter.Text = "";
        ViewModel.SelectionFilter.SelectRequested += (_, changes) => SelectChanges(_unstaged, changes);
        WireListSettings();
        return;

        void Wire(FileListParts parts)
        {
            // Upstream's tree (FileTreeRows).
            parts.Tree = new FileTreeRows<FileChange>(parts.List, _preferences, change => change.Path,
                change => change.Image, change => change.Kind, ShowChanges);
            parts.Filter.WorkingDirectory = () => _repositoryPath;
            parts.Filter.FilterChanged += (_, _) => ShowChanges();

            // Upstream's Unstaged_DoubleClick and Staged_DoubleClick.
            parts.List.DoubleTapped += (_, _) => StageSelected(parts.Staged);
            BindFileMenu(parts);
        }
    }

    // Upstream FormCommit's BindContextMenu: the file list menu of each list with the window's refresh (RescanChanges),
    // its stage for the working directory's list and its unstage for the index's, and the list's hotkeys.
    private void BindFileMenu(FileListParts parts)
    {
        FileTreeRows<FileChange> tree = parts.Tree!;
        parts.Menu.Bind(new FileStatusListMenuBinding
        {
            Owner = this,
            RepositoryPath = () => _repositoryPath,
            Selection = () => MenuSelection(parts),
            DescribeRevision = FileStatusListMenu.DescribeByHash,
            Services = () => new FileListMenuServices(_actions, (repository, file, revision, showBlame) =>
                NewFileHistoryWindow(repository, file, revision, showBlame)),
            Refresh = () => Run(_refresh),
            Stage = parts.Staged ? null : () => StageSelected(staged: false),
            Unstage = parts.Staged ? () => StageSelected(staged: true) : null,
            RunScript = script => Run(() => RunScriptAsync(script)),
            FocusedPath = () => (FileStatusListMenu.FocusedItem(parts.List) as FileChange)?.Path,
            FindFile = new FileListFind(() => [.. parts.Shown.Select(change => (change.Path, (string?)null))],
                path => SelectPath(parts, path)),
            Tree = tree.MenuTree(),
            SelectFirstGroup = () => SelectAllShown(parts),
            Submodules = () => Submodules,
        });
        parts.Menu.AttachHotkeys(parts.List);
    }

    // Upstream's LoadGitItemStatuses for FormCommit's lists: the working directory's files between the index and the
    // working directory, the staged ones between HEAD and the index; a single selected folder stands for its files.
    private DiffMenuSelection? MenuSelection(FileListParts parts)
    {
        IFileListNode? node = FileListNodes.SelectedNode(parts.List);
        List<FileChange> selected = node is not null ? [.. node.Files.OfType<FileChange>()] : Selected(parts);
        if (selected.Count == 0)
        {
            return null;
        }

        ObjectId head = _headId ??= GitRepositoryService.HeadId(_repositoryPath);
        ObjectId? first = head.IsZero ? null : parts.Staged ? head : ObjectId.IndexId;
        ObjectId second = parts.Staged ? ObjectId.IndexId : ObjectId.WorkTreeId;
        IReadOnlyList<ObjectId> parents = parts.Staged ? head.IsZero ? [] : [head] : [ObjectId.IndexId];
        return new DiffMenuSelection(first, second, parents,
        [
            .. selected.Select(change => new DiffMenuFile(FileStatusList.StatusLetter(change.Kind), change.Path,
                OldPath: null, IsTracked: change.Kind != ChangeKind.Untracked)
            {
                IsSubmodule = change.IsSubmodule,
                IsSkipWorktree = change.IsSkipWorktree,
                IsAssumeUnchanged = change.IsAssumeUnchanged
            })
        ], node?.Folder);
    }

    // Upstream's SelectedGitItem setter after "Find file": the first shown row of the file is the only one selected.
    private static void SelectPath(FileListParts parts, string path)
    {
        if (parts.List.Items.OfType<FileChange>().FirstOrDefault(change => change.Path == path) is { } change)
        {
            parts.List.SelectedItem = change;
            parts.List.ScrollIntoView(change);
        }
    }

    // Upstream's FirstGroupItems of a list without diff groups: every file it shows, their folders expanded since the list
    // holds only the rows shown.
    private void SelectAllShown(FileListParts parts)
    {
        if (parts.Tree is not { } tree)
        {
            return;
        }

        tree.Expand(tree.NodeKeys(parts.Shown, ""));
        ShowChanges();
        tree.Select(parts.Shown);
    }

    // Upstream's settings menu of the working directory's list (BindContextMenu with canAutoRefresh): its items are the view
    // model's; the toolbar's own items are added to both lists' menus. "Refresh artificial commits on form focus" may have
    // changed in the settings since.
    private void WireListSettings()
    {
        FileListToolbar.Attach(UnstagedSettingsButton, _preferences, (MenuFlyout)UnstagedSettingsButton.Flyout!);
        FileListToolbar.Attach(StagedSettingsButton, _preferences, (MenuFlyout)StagedSettingsButton.Flyout!);
        ((MenuFlyout)UnstagedSettingsButton.Flyout!).Opening += (_, _) => ViewModel.RefreshOptions();
    }

    // Upstream's FileStatusList.SetSelectionFilter: exactly the matching files are selected, their folders expanded.
    private void SelectChanges(FileListParts parts, IReadOnlyList<FileChange> changes)
    {
        if (parts.Tree is not { } tree)
        {
            return;
        }

        tree.Expand(tree.NodeKeys(changes, ""));
        ShowChanges();
        tree.Select(changes);
    }

    // Upstream's lists: the files of the list in the group-by order, without untracked files when "Show untracked files" is
    // off, and only those the filter matches; NoFiles shows while a list is empty.
    private void ShowChanges()
    {
        IReadOnlyList<FileChange> changes = ViewModel.ListedChanges;
        Show(_unstaged, changes.Where(change => !change.Staged), ViewModel.ShowUntracked);
        Show(_staged, changes.Where(change => change.Staged), showUntracked: true);
        return;

        // Upstream's Unstaged_FilterChanged and Staged_FilterChanged: while a list is filtered, its button stages or
        // unstages the files shown.
        void Show(FileListParts parts, IEnumerable<FileChange> changes, bool showUntracked)
        {
            IReadOnlyList<FileChange> shown =
            [
                .. changes.Where(change => showUntracked || change.Kind != ChangeKind.Untracked)
                    .Where(change => parts.Filter.IsMatch(change.Path))
            ];
            parts.Shown = shown;
            List<object> items = [];
            parts.Tree?.BeginFill();
            parts.Tree?.Add(items, shown, depth: 0, keyPrefix: "");
            parts.List.ItemsSource = items;
            parts.Tree?.EndFill();
            parts.Filter.Shown(shown.Count);
            parts.NoFiles.IsVisible = shown.Count == 0;
            ViewModel.SetShown(parts.Staged, shown, IsFiltered(parts));
        }
    }

    // Upstream's FileStatusList.IsFilterActive.
    private static bool IsFiltered(FileListParts parts) => parts.Filter.IsActive;

    private static List<FileChange> Selected(FileListParts parts) => SelectedChanges(parts.List);

    // The controls of one of the two lists.
    private sealed record FileListParts(
        bool Staged,
        ListBox List,
        FileFilterBox Filter,
        TextBlock NoFiles,
        FileStatusListMenu Menu)
    {
        /// <summary>
        ///  The list's tree rows.
        /// </summary>
        public FileTreeRows<FileChange>? Tree { get; set; }

        /// <summary>
        ///  The files the list shows (the filter's and "Show untracked files"' choice), also those in collapsed folders.
        /// </summary>
        public IReadOnlyList<FileChange> Shown { get; set; } = [];
    }

    private FileHistoryWindow NewFileHistoryWindow(string repository, string file, string? revision, bool showBlame)
        => new(repository, _preferences, file, revision, showBlame,
            (path, hash) => _actions.CherryPickAsync(path, hash),
            (path, hash) => _actions.RevertAsync(path, hash));

    private void ShowFileHistory(string file, bool showBlame)
        => NewFileHistoryWindow(_repositoryPath, file, revision: null, showBlame).Show(this);
}
