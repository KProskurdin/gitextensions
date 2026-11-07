using Avalonia.Controls;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

// Upstream FormCommit's two file lists (FileStatusList: its toolbar, filter box, NoFiles text and item menu) and the
// toolbarStaged between them.
public partial class CommitWindow
{
    // Upstream FormCommit's tooltips of the stage and unstage all buttons.
    private const string StageAllText = "Stage all";
    private const string StageFilteredText = "Stage filtered";
    private const string UnstageAllText = "Unstage all";
    private const string UnstageFilteredText = "Unstage filtered";

    // Git's setting of upstream's initial "Show untracked files", and its value that turns them off.
    private const string ShowUntrackedFilesSetting = "status.showuntrackedfiles";
    private const string NoUntrackedFiles = "no";

    private FileListParts _unstaged = null!;
    private FileListParts _staged = null!;

    // The changes read with the working directory list's settings, while they ask for more than the repository's.
    private IReadOnlyList<FileChange>? _flaggedChanges;
    private int _flaggedChangesVersion;

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
            UnstagedByPathButton, UnstagedByExtensionButton, UnstagedByStatusButton, UnstagedShowUntrackedMenuItem,
            UnstagedEditGitIgnoreMenuItem, UnstagedEditLocallyIgnoredMenuItem, UnstagedFilesMenu);
        _staged = new FileListParts(Staged: true, StagedList, StagedFilterBox, StagedNoFilesText, StagedByPathButton,
            StagedByExtensionButton, StagedByStatusButton, StagedShowUntrackedMenuItem, StagedEditGitIgnoreMenuItem,
            StagedEditLocallyIgnoredMenuItem, StagedFilesMenu);
        _unstaged.TreeButtons = TreeButtons(UnstagedAsTreeButton, UnstagedPathTreeMenuItem,
            UnstagedPathFlatMenuItem, UnstagedExtensionTreeMenuItem, UnstagedExtensionFlatMenuItem,
            UnstagedStatusTreeMenuItem, UnstagedStatusFlatMenuItem, UnstagedDenseTreeMenuItem,
            UnstagedGroupNodesInFlatListMenuItem);
        _staged.TreeButtons = TreeButtons(StagedAsTreeButton, StagedPathTreeMenuItem,
            StagedPathFlatMenuItem, StagedExtensionTreeMenuItem, StagedExtensionFlatMenuItem, StagedStatusTreeMenuItem,
            StagedStatusFlatMenuItem, StagedDenseTreeMenuItem, StagedGroupNodesInFlatListMenuItem);

        UnstagedRefreshButton.Click += (_, _) => Run(_refresh);

        // Upstream's OtherColors.MergeConflictsColor.
        SolveConflictsButton.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(230, 99, 99));
        // Upstream's FormCommit opens the conflicts window without its offer to commit.
        SolveConflictsButton.Click += (_, _) => _ = new ConflictsWindow(_repositoryPath, _repository, _actions,
            _preferences,
            (_, file) => ShowFileHistory(file, showBlame: false), offerCommit: false).ShowDialog(this);
        foreach (FileListParts parts in new[] { _unstaged, _staged })
        {
            Wire(parts);
        }

        ShowGroupBy();
        WireListSettings();
        return;

        void Wire(FileListParts parts)
        {
            // Upstream's tree (FileTreeRows), its tree button and its collapse button.
            parts.Tree = new FileTreeRows<FileChange>(parts.List, _preferences, change => change.Path,
                change => change.Image, change => change.Kind, ShowChanges);
            parts.TreeButtons?.Wire(_preferences, () =>
            {
                ShowGroupBy();
                ShowChanges();
            });
            parts.Filter.WorkingDirectory = () => _repositoryPath;
            parts.Filter.FilterChanged += (_, _) => ShowChanges();
            parts.ByPath.Click += (_, _) => SetGroupBy(DiffListSortType.FilePath);
            parts.ByExtension.Click += (_, _) => SetGroupBy(DiffListSortType.FileExtension);
            parts.ByStatus.Click += (_, _) => SetGroupBy(DiffListSortType.FileStatus);
            parts.ShowUntracked.Click += (_, _) => ShowChanges();
            parts.EditGitIgnore.Click += (_, _) => Run(() => EditIgnoreFileAsync(localExclude: false));
            parts.EditLocallyIgnored.Click += (_, _) => Run(() => EditIgnoreFileAsync(localExclude: true));

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

    // Upstream's settings menu of the working directory's list (BindContextMenu with canAutoRefresh): the ignored,
    // skip-worktree and assumed-unchanged files are read with git's flags while one is checked; "Show untracked files"
    // starts as git's status.showuntrackedfiles says; "Refresh artificial commits on form focus" is upstream's
    // RefreshArtificialCommitOnApplicationActivated.
    private void WireListSettings()
    {
        foreach (MenuItem item in new[]
                 {
                     UnstagedShowIgnoredMenuItem, UnstagedShowSkipWorktreeMenuItem,
                     UnstagedShowAssumeUnchangedMenuItem
                 })
        {
            item.Click += (_, _) => ReadChanges();
        }

        ((MenuFlyout)UnstagedSettingsButton.Flyout!).Opening += (_, _) =>
            UnstagedRefreshOnFormFocusMenuItem.IsChecked = _preferences.RefreshCommitDialogOnFocus;
        UnstagedRefreshOnFormFocusMenuItem.Click += (_, _) =>
        {
            _preferences.RefreshCommitDialogOnFocus = UnstagedRefreshOnFormFocusMenuItem.IsChecked;
            _preferences.Save();
        };
        Opened += (_, _) => Run(async () =>
        {
            string showUntracked = await Task.Run(() =>
                GitModules.Open(_repositoryPath).GetEffectiveSetting(ShowUntrackedFilesSetting));
            UnstagedShowUntrackedMenuItem.IsChecked = showUntracked != NoUntrackedFiles;
            ShowChanges();
        });
    }

    // The repository's changes, or the working directory read again with the list's settings while they ask for more.
    private void ReadChanges()
    {
        _headId = null;
        int version = ++_flaggedChangesVersion;
        bool ignored = UnstagedShowIgnoredMenuItem.IsChecked;
        bool skipWorktree = UnstagedShowSkipWorktreeMenuItem.IsChecked;
        bool assumeUnchanged = UnstagedShowAssumeUnchangedMenuItem.IsChecked;
        if (!ignored && !skipWorktree && !assumeUnchanged)
        {
            _flaggedChanges = null;
            ShowChanges();
            return;
        }

        Run(async () =>
        {
            IReadOnlyList<FileChange> changes = await GitRepositoryService.GetWorkTreeChangesAsync(_repositoryPath,
                ignored, assumeUnchanged, skipWorktree);
            if (version == _flaggedChangesVersion)
            {
                _flaggedChanges = changes;
                ShowChanges();
            }
        });
    }

    // Upstream's lists: the files of the list in the group-by order, without untracked files when "Show untracked files" is
    // off, and only those the filter matches; NoFiles shows while a list is empty.
    private void ShowChanges()
    {
        IReadOnlyList<FileChange> changes = _flaggedChanges ?? _repository.Changes;
        Show(_unstaged, changes.Where(change => !change.Staged));
        Show(_staged, changes.Where(change => change.Staged));

        // Upstream's Unstaged_FilterChanged and Staged_FilterChanged: while a list is filtered, its button stages or
        // unstages the files shown.
        bool unstagedFiltered = IsFiltered(_unstaged);
        StageAllButton.Image = unstagedFiltered ? "StageAllFiltered" : "StageAll";
        ToolTip.SetTip(StageAllButton, UpstreamTranslation.ByText(unstagedFiltered ? StageFilteredText : StageAllText));
        bool stagedFiltered = IsFiltered(_staged);
        UnstageAllButton.Image = stagedFiltered ? "UnstageAllFiltered" : "UnstageAll";
        ToolTip.SetTip(UnstageAllButton,
            UpstreamTranslation.ByText(stagedFiltered ? UnstageFilteredText : UnstageAllText));
        return;

        void Show(FileListParts parts, IEnumerable<FileChange> changes)
        {
            bool showUntracked = parts.ShowUntracked.IsChecked;
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
        }
    }

    // The lists' trees: without upstream's collapse button, which it shows only for diff groups.
    private static FileListTreeButtons TreeButtons(ToolSplitButton asTree, MenuItem pathTree,
        MenuItem pathFlat, MenuItem extensionTree, MenuItem extensionFlat, MenuItem statusTree, MenuItem statusFlat,
        MenuItem denseTree, MenuItem groupNodesInFlatList)
        => new(CollapseGroups: null, asTree,
            [
                (pathTree, DiffListSortType.FilePath), (pathFlat, DiffListSortType.FilePathFlat),
                (extensionTree, DiffListSortType.FileExtension), (extensionFlat, DiffListSortType.FileExtensionFlat),
                (statusTree, DiffListSortType.FileStatus), (statusFlat, DiffListSortType.FileStatusFlat),
            ],
            denseTree, groupNodesInFlatList);

    // Upstream's FileStatusList.IsFilterActive.
    private static bool IsFiltered(FileListParts parts) => parts.Filter.IsActive;

    // Upstream's group-by buttons choose the grouping and keep the tree or flat variant.
    private void SetGroupBy(DiffListSortType sorting)
    {
        _preferences.DiffListSorting = FileListTreeButtons.GroupBy(_preferences.DiffListSorting, sorting);
        _preferences.Save();
        ShowGroupBy();
        ShowChanges();
    }

    private void ShowGroupBy()
    {
        DiffListSortType sorting = _preferences.DiffListSorting;
        foreach (FileListParts parts in new[] { _unstaged, _staged })
        {
            parts.ByPath.IsChecked = sorting is DiffListSortType.FilePath or DiffListSortType.FilePathFlat;
            parts.ByExtension.IsChecked =
                sorting is DiffListSortType.FileExtension or DiffListSortType.FileExtensionFlat;
            parts.ByStatus.IsChecked = sorting is DiffListSortType.FileStatus or DiffListSortType.FileStatusFlat;
            parts.TreeButtons?.Show(_preferences);
        }
    }

    private async Task EditIgnoreFileAsync(bool localExclude)
    {
        if (await GitIgnoreWindow.ShowAsync(this, _repositoryPath, localExclude))
        {
            await _refresh();
        }
    }

    private static List<FileChange> Selected(FileListParts parts) => SelectedChanges(parts.List);

    // The controls of one of the two lists.
    private sealed record FileListParts(
        bool Staged,
        ListBox List,
        FileFilterBox Filter,
        TextBlock NoFiles,
        ToolToggleButton ByPath,
        ToolToggleButton ByExtension,
        ToolToggleButton ByStatus,
        MenuItem ShowUntracked,
        MenuItem EditGitIgnore,
        MenuItem EditLocallyIgnored,
        FileStatusListMenu Menu)
    {
        /// <summary>
        ///  Upstream's collapse and tree buttons of the list's toolbar.
        /// </summary>
        public FileListTreeButtons? TreeButtons { get; set; }

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
