using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using GitCommands;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

// Upstream FormCommit's two file lists (FileStatusList: its toolbar, filter box, NoFiles text and item menu) and the
// toolbarStaged between them.
public partial class CommitWindow
{
    // Upstream FileStatusList texts.
    private const string ResetSelectedChangesText = "Are you sure you want to reset all selected files to {0}?";
    private const string FirstRevisionText = "First: A ";
    private const string DeleteSelectedFilesText = "Are you sure you want to delete the selected file(s)?";
    private const string DeleteSelectedFilesCaption = "Delete";
    private const string DeleteFailedText = "Delete file failed";
    private const string StopTrackingFailText = "Fail to stop tracking the file '{0}'.";
    private const string RegexErrorTitle = "RegEx";

    // Upstream FormCommit's tooltips of the stage and unstage all buttons.
    private const string StageAllText = "Stage all";
    private const string StageFilteredText = "Stage filtered";
    private const string UnstageAllText = "Unstage all";
    private const string UnstageFilteredText = "Unstage filtered";

    private readonly IFileManager _fileManager =
        new SystemFileManager(new SystemProcessLauncher(), HostPlatform.Current);

    private FileListParts _unstaged = null!;
    private FileListParts _staged = null!;

    // The changes read with the working directory list's settings, while they ask for more than the repository's.
    private IReadOnlyList<FileChange>? _flaggedChanges;
    private int _flaggedChangesVersion;

    // Git's setting of upstream's initial "Show untracked files", and its value that turns them off.
    private const string ShowUntrackedFilesSetting = "status.showuntrackedfiles";
    private const string NoUntrackedFiles = "no";

    private void WireFileLists()
    {
        _unstaged = new FileListParts(Staged: false, UnstagedList, UnstagedFilterBox, UnstagedNoFilesText,
            UnstagedByPathButton, UnstagedByExtensionButton, UnstagedByStatusButton, UnstagedShowUntrackedMenuItem,
            UnstagedEditGitIgnoreMenuItem, UnstagedEditLocallyIgnoredMenuItem, UnstagedContextMenu,
            UnstagedStageFileMenuItem,
            UnstagedResetFileMenuItem, UnstagedResetFileToFirstMenuItem, UnstagedOpenWithDifftoolMenuItem,
            UnstagedOpenWorkingFileMenuItem, UnstagedEditWorkingFileMenuItem, UnstagedDeleteFileMenuItem,
            UnstagedCopyPathsMenuItem, UnstagedShowInFolderMenuItem, UnstagedFileHistoryMenuItem, UnstagedBlameMenuItem,
            UnstagedAddToGitIgnoreMenuItem, UnstagedAddToGitInfoExcludeMenuItem, UnstagedStopTrackingMenuItem,
            UnstagedRunScriptMenuItem);
        _staged = new FileListParts(Staged: true, StagedList, StagedFilterBox, StagedNoFilesText, StagedByPathButton,
            StagedByExtensionButton, StagedByStatusButton, StagedShowUntrackedMenuItem, StagedEditGitIgnoreMenuItem,
            StagedEditLocallyIgnoredMenuItem, StagedContextMenu, StagedUnstageFileMenuItem, StagedResetFileMenuItem,
            StagedResetFileToFirstMenuItem, StagedOpenWithDifftoolMenuItem, StagedOpenWorkingFileMenuItem,
            StagedEditWorkingFileMenuItem, StagedDeleteFileMenuItem, StagedCopyPathsMenuItem,
            StagedShowInFolderMenuItem,
            StagedFileHistoryMenuItem, StagedBlameMenuItem, AddToGitIgnore: null, AddToGitInfoExclude: null,
            StagedStopTrackingMenuItem, StagedRunScriptMenuItem);
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
        SolveConflictsButton.Click += (_, _) => _ = new ConflictsWindow(_repositoryPath, _repository, _actions, _preferences,
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
            parts.Filter.TextChanged += (_, _) => ShowChanges();
            parts.ByPath.Click += (_, _) => SetGroupBy(DiffListSortType.FilePath);
            parts.ByExtension.Click += (_, _) => SetGroupBy(DiffListSortType.FileExtension);
            parts.ByStatus.Click += (_, _) => SetGroupBy(DiffListSortType.FileStatus);
            parts.ShowUntracked.Click += (_, _) => ShowChanges();
            parts.EditGitIgnore.Click += (_, _) => Run(() => EditIgnoreFileAsync(localExclude: false));
            parts.EditLocallyIgnored.Click += (_, _) => Run(() => EditIgnoreFileAsync(localExclude: true));
            parts.Menu.Opening += (_, _) => ShowFileMenu(parts);
            parts.StageOrUnstage.Click += (_, _) => StageSelected(parts.Staged);

            // Upstream's Unstaged_DoubleClick and Staged_DoubleClick.
            parts.List.DoubleTapped += (_, _) => StageSelected(parts.Staged);
            parts.ResetToFirst.Click += (_, _) => Run(() => ResetSelectedAsync(parts));
            parts.OpenWithDifftool.Click += (_, _) => Run(() => OpenSelectedInDiffToolAsync(parts));
            parts.OpenWorkingFile.Click += (_, _) =>
                Run(() => Launcher.LaunchFileInfoAsync(new FileInfo(FullPath(Selected(parts)[0]))));
            parts.EditWorkingFile.Click += (_, _) => new EditorWindow(FullPath(Selected(parts)[0])).Show(this);
            parts.DeleteFile.Click += (_, _) => Run(() => DeleteSelectedAsync(parts));
            parts.ShowInFolder.Click += (_, _) => _fileManager.OpenFolder(
                Path.GetDirectoryName(FullPath(Selected(parts)[0])) ?? _repositoryPath);
            // Upstream's FileStatusList menu: the file history window, on its Blame tab for Blame.
            parts.FileHistory.Click += (_, _) => ShowFileHistory(Selected(parts)[0].Path, showBlame: false);
            parts.Blame.Click += (_, _) => ShowFileHistory(Selected(parts)[0].Path, showBlame: true);
            parts.AddToGitIgnore?.Click += (_, _) => Run(() => IgnoreSelectedAsync(localExclude: false));
            parts.AddToGitInfoExclude?.Click += (_, _) => Run(() => IgnoreSelectedAsync(localExclude: true));
            parts.StopTracking.Click += (_, _) => Run(() => StopTrackingSelectedAsync(parts));
            parts.CopyPaths.ItemsSource = CopyPathsMenu.Items(() => Selected(parts).Select(file => file.Path),
                () => _repositoryPath, CopyTextAsync);
        }
    }

    // Upstream's settings menu of the working directory's list (BindContextMenu with canAutoRefresh): the ignored,
    // skip-worktree and assumed-unchanged files are read with git's flags while one is checked; "Show untracked files"
    // starts as git's status.showuntrackedfiles says; "Refresh artificial commits on form focus" is upstream's
    // RefreshArtificialCommitOnApplicationActivated.
    private void WireListSettings()
    {
        foreach (MenuItem item in new[] { UnstagedShowIgnoredMenuItem, UnstagedShowSkipWorktreeMenuItem,
                     UnstagedShowAssumeUnchangedMenuItem })
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
            Regex? filter = FileStatusList.Filter(parts.Filter.Text, out string? error);
            ToolTip.SetTip(parts.Filter, error is null ? null : $"{RegexErrorTitle}: {error}");
            bool showUntracked = parts.ShowUntracked.IsChecked;
            IReadOnlyList<FileChange> shown =
            [
                .. changes.Where(change => showUntracked || change.Kind != ChangeKind.Untracked)
                    .Where(change => filter?.IsMatch(change.Path) ?? true)
            ];
            parts.Shown = shown;
            List<object> items = [];
            parts.Tree?.BeginFill();
            parts.Tree?.Add(items, shown, depth: 0, keyPrefix: "");
            parts.List.ItemsSource = items;
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
    private static bool IsFiltered(FileListParts parts) => !string.IsNullOrEmpty(parts.Filter.Text);

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

    // Upstream's UpdateStatusOfMenuItems for the commit window's lists.
    private void ShowFileMenu(FileListParts parts)
    {
        List<FileChange> selected = Selected(parts);
        FileMenuState state = FileMenuState.For(selected, parts.Staged,
            path => File.Exists(Path.Combine(_repositoryPath, path)) ||
                    Directory.Exists(Path.Combine(_repositoryPath, path)));
        parts.StageOrUnstage.IsVisible = parts.Staged ? state.Unstage : state.Stage;
        parts.ResetFile.IsEnabled = state.ResetFile;
        parts.ResetToFirst.Header =
            FirstRevisionText + (parts.Staged ? "HEAD" : ResourceManager.TranslatedStrings.Index);
        parts.OpenWithDifftool.IsEnabled = selected.Count > 0;
        parts.OpenWorkingFile.IsVisible = state.OpenWorkingFile;
        parts.EditWorkingFile.IsVisible = state.EditWorkingFile;
        parts.DeleteFile.Header = ResourceManager.TranslatedStrings.GetDeleteFile(selected.Count);
        parts.DeleteFile.IsVisible = state.DeleteFile;
        parts.CopyPaths.IsEnabled = state.CopyPaths;
        parts.ShowInFolder.IsEnabled = state.ShowInFolder;
        parts.FileHistory.IsEnabled = state.History;
        parts.Blame.IsEnabled = state.Blame;
        parts.AddToGitIgnore?.IsVisible = state.Ignore;
        parts.AddToGitInfoExclude?.IsVisible = state.Ignore;
        parts.StopTracking.IsVisible = state.StopTracking;
        ShowFileListScripts(parts.Menu, parts.RunScript);
    }

    private async Task ResetSelectedAsync(FileListParts parts)
    {
        List<FileChange> selected = [.. Selected(parts).Where(change => change.Kind != ChangeKind.Untracked)];
        string revision = parts.Staged ? "HEAD" : ResourceManager.TranslatedStrings.Index;
        if (selected.Count == 0 || !await new ConfirmWindow(
                    string.Format(UpstreamTranslation.ByText(ResetSelectedChangesText), revision), "Yes",
                    cancelText: "No")
                .ShowDialog<bool>(this))
        {
            return;
        }

        if (!parts.Staged)
        {
            await _actions.DiscardChangesAsync(_repositoryPath, [.. selected.Select(change => change.Path)]);
            return;
        }

        // A file HEAD does not have is only taken out of the index, as upstream's ResetChanges does without deleting.
        List<string> added =
            [.. selected.Where(change => change.Kind == ChangeKind.Added).Select(change => change.Path)];
        List<string> others =
            [.. selected.Where(change => change.Kind != ChangeKind.Added).Select(change => change.Path)];
        if (added.Count > 0)
        {
            await _actions.UnstageAsync(_repositoryPath, added);
        }

        if (others.Count > 0)
        {
            await _actions.ResetFilesToAsync(_repositoryPath, "HEAD", others);
        }
    }

    private async Task OpenSelectedInDiffToolAsync(FileListParts parts)
    {
        if (Selected(parts).FirstOrDefault() is { } change)
        {
            await _actions.RunDiffToolAsync(_repositoryPath, change.Path, commit: null, parts.Staged, tool: null);
        }
    }

    // Upstream's DeleteFile_Click: after the question, staged files leave the index, then the files leave the disk.
    private async Task DeleteSelectedAsync(FileListParts parts)
    {
        List<FileChange> selected = Selected(parts);
        if (selected.Count == 0 || !await new ConfirmWindow(UpstreamTranslation.ByText(DeleteSelectedFilesText), "Yes",
                UpstreamTranslation.ByText(DeleteSelectedFilesCaption), cancelText: "No").ShowDialog<bool>(this))
        {
            return;
        }

        try
        {
            if (parts.Staged)
            {
                await _actions.UnstageAsync(_repositoryPath, [.. selected.Select(change => change.Path)]);
            }

            foreach (string path in selected.Select(FullPath))
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                else
                {
                    File.Delete(path);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError($"{UpstreamTranslation.ByText(DeleteFailedText)}{Environment.NewLine}{ex.Message}");
        }
        finally
        {
            await _refresh();
        }
    }

    private async Task StopTrackingSelectedAsync(FileListParts parts)
    {
        if (Selected(parts).FirstOrDefault() is { } change
            && !await _actions.StopTrackingAsync(_repositoryPath, change.Path))
        {
            ShowError(string.Format(UpstreamTranslation.ByText(StopTrackingFailText), change.Path));
        }
    }

    // Upstream's AddFileToIgnoreFile: each selected file anchored at the root, added to .gitignore or the local excludes.
    private async Task IgnoreSelectedAsync(bool localExclude)
    {
        List<string> patterns = [.. Selected(_unstaged).Select(change => GitIgnoreFile.PatternFor(change.Path))];
        if (patterns.Count > 0
            && await AddToGitIgnoreWindow.ShowAsync(this, _repositoryPath, localExclude, patterns))
        {
            await _refresh();
        }
    }

    private async Task EditIgnoreFileAsync(bool localExclude)
    {
        if (await GitIgnoreWindow.ShowAsync(this, _repositoryPath, localExclude))
        {
            await _refresh();
        }
    }

    private async Task CopyTextAsync(string text)
    {
        if (GetTopLevel(this)?.Clipboard is { } clipboard && text.Length > 0)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    private string FullPath(FileChange change) => Path.Combine(_repositoryPath, change.Path);

    private static List<FileChange> Selected(FileListParts parts) => SelectedChanges(parts.List);

    // The controls of one of the two lists.
    private sealed record FileListParts(
        bool Staged,
        ListBox List,
        TextBox Filter,
        TextBlock NoFiles,
        ToolToggleButton ByPath,
        ToolToggleButton ByExtension,
        ToolToggleButton ByStatus,
        MenuItem ShowUntracked,
        MenuItem EditGitIgnore,
        MenuItem EditLocallyIgnored,
        ContextMenu Menu,
        MenuItem StageOrUnstage,
        MenuItem ResetFile,
        MenuItem ResetToFirst,
        MenuItem OpenWithDifftool,
        MenuItem OpenWorkingFile,
        MenuItem EditWorkingFile,
        MenuItem DeleteFile,
        MenuItem CopyPaths,
        MenuItem ShowInFolder,
        MenuItem FileHistory,
        MenuItem Blame,
        MenuItem? AddToGitIgnore,
        MenuItem? AddToGitInfoExclude,
        MenuItem StopTracking,
        MenuItem RunScript)
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
    private void ShowFileHistory(string file, bool showBlame)
        => new FileHistoryWindow(_repositoryPath, _preferences, file, revision: null, showBlame,
            (repository, hash) => _actions.CherryPickAsync(repository, hash),
            (repository, hash) => _actions.RevertAsync(repository, hash)).Show(this);
}
