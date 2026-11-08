using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Ui;
using GitUI.ScriptsEngine;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream FileStatusList's item menu (<c>FileStatusList.ContextMenu.cs</c>) for a list of the files between two
///  revisions: the browse window's Diff tab and FormDiff. Which items show comes from upstream's rules (core
///  <see cref="DiffFileMenu"/>); the actions are upstream's handlers. What a window adds is its
///  <see cref="FileStatusListMenuBinding"/>, as upstream's <c>BindContextMenu</c>.
/// </summary>
public partial class FileStatusListMenu : ContextMenu
{
    // Upstream FileStatusList texts.
    private const string DeleteSelectedFilesText = "Are you sure you want to delete the selected file(s)?";
    private const string DeleteSelectedFilesCaption = "Delete";
    private const string DeleteFailedText = "Delete file failed";
    private const string StopTrackingFailText = "Fail to stop tracking the file '{0}'.";
    private const string SaveFileFilterCurrentFormat = "Current format";
    private const string SaveFileFilterAllFiles = "All files";
    private const string NewNameText = "New name";

    // Upstream FileStatusList's tooltips of "Skip worktree" and "Assume unchanged".
    private const string SkipWorktreeToolTip = "Hide already tracked files that will change but that you don't want to commit."
                                               + "\nSuitable for some config files modified locally.";
    private const string AssumeUnchangedToolTip = "Tell git to not check the status of this file for performance benefits."
                                                  + "\nUse this feature when a file is big and never change."
                                                  + "\nGit will never check if the file has changed that will improve status check performance.";

    private static readonly IFileManager FileManager =
        new SystemFileManager(new SystemProcessLauncher(), HostPlatform.Current);

    private FileStatusListMenuBinding? _binding;
    private bool _customDiffToolsLoaded;

    public FileStatusListMenu()
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FileStatusList");

        // Upstream FileStatusList's OnRuntimeLoad starts looking for Visual Studio.
        if (OperatingSystem.IsWindows())
        {
            _ = VisualStudio.InitAsync();
        }

        Opening += (_, _) =>
        {
            UpdateStatusOfMenuItems();
            if (!_customDiffToolsLoaded)
            {
                _customDiffToolsLoaded = true;
                Run(LoadCustomDifftoolsAsync);
            }
        };
        openSubmoduleMenuItem.Click += (_, _) => WithSubmodules((submodules, paths) => submodules.Open(paths[0]));
        tsmiUpdateSubmodule.Click += (_, _) => WithSubmodules((submodules, paths) => submodules.Update(paths));
        tsmiResetSubmoduleChanges.Click += (_, _) => WithSubmodules((submodules, paths) => submodules.Reset(paths));
        tsmiStashSubmoduleChanges.Click += (_, _) => WithSubmodules((submodules, paths) => submodules.Stash(paths));
        tsmiCommitSubmoduleChanges.Click += (_, _) => WithSubmodules((submodules, paths) => submodules.Commit(paths));
        tsmiStageFile.Click += (_, _) => Run(() => StageAsync(stage: true));
        tsmiUnstageFile.Click += (_, _) => Run(() => StageAsync(stage: false));
        tsmiSkipWorktree.Click += (_, _) => Run(() => SetIndexFlagAsync(IndexFlag.SkipWorktree, tsmiSkipWorktree));
        tsmiAssumeUnchanged.Click += (_, _) =>
            Run(() => SetIndexFlagAsync(IndexFlag.AssumeUnchanged, tsmiAssumeUnchanged));
        ToolTip.SetTip(tsmiSkipWorktree, UpstreamTranslation.ByText(SkipWorktreeToolTip));
        ToolTip.SetTip(tsmiAssumeUnchanged, UpstreamTranslation.ByText(AssumeUnchangedToolTip));

        // Upstream's ResetFile_Click: the parent item resets to the first revision when it can.
        tsmiResetFileTo.Click += (_, e) =>
        {
            if (e.Source == tsmiResetFileTo && tsmiResetFileToParent.IsVisible)
            {
                Run(() => ResetAsync(toFirst: true));
            }
        };
        tsmiResetFileToSelected.Click += (_, _) => Run(() => ResetAsync(toFirst: false));
        tsmiCherryPickChanges.Click += (_, _) => Run(CherryPickChangesAsync);
        tsmiResetChunkOfFile.Click += (_, _) => Run(() => InteractivePatchAsync(reset: true));
        tsmiInteractiveAdd.Click += (_, _) => Run(() => InteractivePatchAsync(reset: false));
        tsmiResetFileToParent.Click += (_, _) => Run(() => ResetAsync(toFirst: true));

        // Upstream's OpenFilesWithDiffTool: the parent item runs First -> Second.
        tsmiOpenWithDifftool.Click += (_, e) =>
        {
            if (e.Source == tsmiOpenWithDifftool)
            {
                Run(() => OpenWithDifftoolAsync(RevisionDiffKind.DiffAB));
            }
        };
        foreach ((MenuItem item, Action<string?> open) in CustomDiffToolItems())
        {
            CustomDiffTools.OnClick(item, open);
        }

        tsmiRememberSecondRevDiff.Click += (_, _) => Remember(first: false);
        tsmiRememberFirstRevDiff.Click += (_, _) => Remember(first: true);
        tsmiOpenWorkingDirectoryFile.Click += (_, _) => OpenWorkingDirectoryFile();
        tsmiOpenRevisionFile.Click += (_, _) => Run(OpenRevisionFileAsync);
        tsmiOpenWorkingDirectoryFileWith.Click += (_, _) => OpenWorkingDirectoryFileWith();
        tsmiOpenRevisionFileWith.Click += (_, _) => Run(OpenRevisionFileWithAsync);
        tsmiEditWorkingDirectoryFile.Click += (_, _) => EditWorkingDirectoryFile();
        tsmiOpenInVisualStudio.Click += (_, _) =>
        {
            if (OperatingSystem.IsWindows())
            {
                Run(OpenInVisualStudioAsync);
            }
        };
        tsmiSaveAs.Click += (_, _) => Run(SaveAsAsync);
        tsmiMove.Click += (_, _) => Run(MoveAsync);
        tsmiDeleteFile.Click += (_, _) => Run(DeleteAsync);
        tsmiCopyPaths.ItemsSource = CopyPathsMenu.Items(CopiedPaths, () => _binding?.RepositoryPath(), CopyTextAsync);
        tsmiShowInFolder.Click += (_, _) => ShowInFolder();
        tsmiShowInFileTree.Click += (_, _) => ShowInFileTree();
        tsmiFilterFileInGrid.Click += (_, _) => FilterFileInGrid();
        tsmiFileHistory.Click += (_, _) => StartFileHistory(showBlame: false);

        // Upstream's Blame: what the window binds (in the File tree tab, blame in place of the file's content), else the
        // file history on its Blame tab.
        tsmiBlame.Click += (_, _) =>
        {
            if (_binding?.Blame is { } blame)
            {
                blame();
            }
            else
            {
                StartFileHistory(showBlame: true);
            }
        };
        tsmiFindFile.Click += (_, _) => Run(FindFileAsync);
        tsmiOpenFindInCommitFilesGitGrepDialog.Click += (_, _) => _binding?.GitGrep?.ShowWindow("");

        // Upstream's ShowFindInCommitFilesGitGrep_Click.
        tsmiShowFindInCommitFilesGitGrep.Click += (_, _) =>
        {
            if (_binding?.GitGrep is { } grep)
            {
                bool show = !grep.BoxVisible;
                AppServices.Preferences.ShowFindInCommitFilesGitGrep = show;
                AppServices.Preferences.Save();
                grep.SetBoxVisible(show);
            }
        };
        tsmiAddFileToGitIgnore.Click += (_, _) => Run(() => AddToIgnoreFileAsync(localExclude: false));
        tsmiAddFileToGitInfoExclude.Click += (_, _) => Run(() => AddToIgnoreFileAsync(localExclude: true));
        tsmiStopTracking.Click += (_, _) => Run(StopTrackingAsync);
        _selectAll.Click += (_, _) => _binding?.Tree?.SelectAll();
        _collapseAll.Click += (_, _) => _binding?.Tree?.CollapseAll();
        _expandAll.Click += (_, _) => _binding?.Tree?.ExpandAll();
        _collapseRootFolders.Click += (_, _) => _binding?.Tree?.CollapseRootFolders();
    }

    protected override Type StyleKeyOverride => typeof(ContextMenu);

    /// <summary>
    ///  What the window's list adds to the menu, as upstream's <c>BindContextMenu</c>.
    /// </summary>
    internal void Bind(FileStatusListMenuBinding binding)
    {
        _binding = binding;

        // Upstream's BindContextMenu with stage and unstage (FormCommit's lists) makes both items bold.
        if (binding.Stage is not null || binding.Unstage is not null)
        {
            tsmiStageFile.FontWeight = FontWeight.Bold;
            tsmiUnstageFile.FontWeight = FontWeight.Bold;
        }
    }

    /// <summary>
    ///  The menu's items for the selection, or null without one; for the list's hotkeys.
    /// </summary>
    internal DiffFileMenuState? State()
        => _binding is { } binding && binding.RepositoryPath() is { } path && binding.Selection() is { } selection
            ? DiffFileMenu.For(selection, path, binding.DescribeRevision, supportLinePatching: SupportLinePatching())
            : null;

    // Upstream's getSupportLinePatching: the viewer shows the selected commit file's diff and can apply its lines.
    private bool SupportLinePatching()
        => _binding?.Viewer?.Invoke() is { LineAction: DiffLineAction.Apply } viewer && viewer.WholeRange() is not null;

    // Upstream's CherryPickChanges_Click (the viewer's CherryPickAllChanges): the whole diff applied to the working
    // directory and the index.
    private async Task CherryPickChangesAsync()
    {
        if (_binding is { } binding && binding.RepositoryPath() is { } path && binding.Services() is { } services
            && binding.Viewer?.Invoke() is { } viewer && SupportLinePatching() && viewer.WholeRange() is { } range
            && binding.Selection() is { Files: [{ } file] })
        {
            await DiffLinePatching.ApplyAsync(binding.Owner, services, path, range, reverse: false, file.Status);
        }
    }

    /// <summary>
    ///  Upstream FileStatusList's <c>ExecuteCommand</c> for a BrowseDiff hotkey: the menu's items are brought up to date,
    ///  then the command's item is clicked when it is shown and enabled (WinForms' <c>PerformClick</c>). False for a
    ///  command the menu does not handle.
    /// </summary>
    internal bool ExecuteCommand(BrowseDiffCommand command)
    {
        UpdateStatusOfMenuItems();
        if (command is BrowseDiffCommand.OpenWithDifftool or BrowseDiffCommand.OpenWithDifftoolFirstToLocal
            or BrowseDiffCommand.OpenWithDifftoolSelectedToLocal)
        {
            RevisionDiffKind kind = command switch
            {
                BrowseDiffCommand.OpenWithDifftoolFirstToLocal => RevisionDiffKind.DiffALocal,
                BrowseDiffCommand.OpenWithDifftoolSelectedToLocal => RevisionDiffKind.DiffBLocal,
                _ => RevisionDiffKind.DiffAB,
            };
            Run(() => OpenWithDifftoolAsync(kind));
            return true;
        }

        // Upstream's ResetSelectedFilesWithConfirmation: to the first revision when the menu offers it; the key is taken
        // either way.
        if (command == BrowseDiffCommand.ResetSelectedFiles)
        {
            if (State() is { ResetFile: true, ResetToFirst: not null })
            {
                Run(() => ResetAsync(toFirst: true));
            }

            return true;
        }

        // Upstream's SelectFirstGroupChangesIfFocused.
        if (command == BrowseDiffCommand.SelectFirstGroupChanges)
        {
            if (_binding?.SelectFirstGroup is not { } selectFirstGroup)
            {
                return false;
            }

            selectFirstGroup();
            return true;
        }

        // Upstream's git-grep hotkeys: each tab's own.
        if ((command == BrowseDiffCommand.FindInCommitFilesUsingGitGrepDiffTab && _binding?.IsFileTreeMode == true)
            || (command == BrowseDiffCommand.FindInCommitFilesUsingGitGrepFileTreeTab && _binding?.IsFileTreeMode != true))
        {
            return false;
        }

        MenuItem? item = command switch
        {
            BrowseDiffCommand.DeleteSelectedFiles => tsmiDeleteFile,
            BrowseDiffCommand.ShowHistory => tsmiFileHistory,
            BrowseDiffCommand.Blame => tsmiBlame,
            BrowseDiffCommand.EditFile => tsmiEditWorkingDirectoryFile,
            BrowseDiffCommand.OpenInVisualStudio => tsmiOpenInVisualStudio,
            BrowseDiffCommand.StageSelectedFile => tsmiStageFile,
            BrowseDiffCommand.UnStageSelectedFile => tsmiUnstageFile,
            BrowseDiffCommand.ShowFileTree => tsmiShowInFileTree,
            BrowseDiffCommand.OpenWorkingDirectoryFile => tsmiOpenWorkingDirectoryFile,
            BrowseDiffCommand.RenameMove => tsmiMove,
            BrowseDiffCommand.FindFile => tsmiFindFile,
            BrowseDiffCommand.OpenAsTempFile => tsmiOpenRevisionFile,
            BrowseDiffCommand.OpenAsTempFileWith => tsmiOpenRevisionFileWith,
            BrowseDiffCommand.OpenWorkingDirectoryFileWith => tsmiOpenWorkingDirectoryFileWith,
            BrowseDiffCommand.FilterFileInGrid => tsmiFilterFileInGrid,
            BrowseDiffCommand.AddFileToGitIgnore => tsmiAddFileToGitIgnore,
            BrowseDiffCommand.FindInCommitFilesUsingGitGrepDiffTab
                or BrowseDiffCommand.FindInCommitFilesUsingGitGrepFileTreeTab => tsmiOpenFindInCommitFilesGitGrepDialog,
            _ => null
        };
        if (item is null)
        {
            return false;
        }

        if (item.IsVisible && item.IsEffectivelyEnabled)
        {
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        }

        return true;
    }

    /// <summary>
    ///  Upstream FileStatusList's hotkeys (section "BrowseDiff", which every FileStatusList loads) while
    ///  <paramref name="list"/> has the focus, before the list's own keys (Ctrl+A selects all rows).
    /// </summary>
    internal void AttachHotkeys(ListBox list)
        => list.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (Hotkeys.BrowseDiff.Match(e) is { } command && ExecuteCommand(command))
            {
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);

    // Upstream's UpdateStatusOfMenuItems, InitResetFileToToolStripMenuItem and OpenWithDifftool_DropDownOpening.
    private void UpdateStatusOfMenuItems()
    {
        ShowTreeItems();
        if (_binding is not { } binding || binding.RepositoryPath() is not { } path
                                        || binding.Selection() is not { } selection)
        {
            foreach (Control item in Items.OfType<Control>().Where(item => !IsTreeItem(item)))
            {
                item.IsVisible = item == tsmiRunScript;
            }

            sepScripts.IsVisible = false;
            ShowScripts();
            return;
        }

        DiffFileMenuState state = DiffFileMenu.For(selection, path, binding.DescribeRevision,
            supportLinePatching: SupportLinePatching());
        bool submodules = binding.Submodules() is not null;
        openSubmoduleMenuItem.IsVisible = submodules && state.OpenSubmodule;

        // Upstream's DisableSubmoduleMenuItemBold in the commit window, where a double-click stages.
        bool opensSubmodule = state.OpenSubmodule && AppServices.Preferences.OpenSubmoduleDiffInSeparateWindow;
        openSubmoduleMenuItem.FontWeight = opensSubmodule && binding.Stage is null ? FontWeight.Bold : FontWeight.Normal;
        tsmiUpdateSubmodule.IsVisible = tsmiResetSubmoduleChanges.IsVisible = tsmiStashSubmoduleChanges.IsVisible
            = tsmiCommitSubmoduleChanges.IsVisible = sepSubmodule.IsVisible = submodules && state.Submodule;
        tsmiStageFile.IsVisible = state.Stage;
        tsmiUnstageFile.IsVisible = state.Unstage;
        tsmiResetFileTo.IsVisible = true;
        tsmiResetFileTo.IsEnabled = state.ResetFile;
        tsmiResetFileToSelected.IsVisible = state.ResetToSecond is not null;
        tsmiResetFileToSelected.Header = state.ResetToSecond;
        tsmiResetFileToParent.IsVisible = state.ResetToFirst is not null;
        tsmiResetFileToParent.Header = state.ResetToFirst;
        tsmiCherryPickChanges.IsVisible = state.CherryPick && binding.Services() is not null;
        tsmiResetChunkOfFile.IsVisible = tsmiInteractiveAdd.IsVisible
            = state.InteractiveAddReset;
        sepGit.IsVisible = true;
        tsmiOpenWithDifftool.IsVisible = true;
        tsmiOpenWithDifftool.IsEnabled = state.OpenWithDifftool;
        tsmiSecondDiffCaption.Header = state.DiffSecondCaption;
        tsmiFirstDiffCaption.Header = state.DiffFirstCaption;
        tsmiDiffFirstToSelected.IsEnabled = state.DiffFirstToSelected;
        tsmiDiffSelectedToLocal.IsEnabled = state.DiffSelectedToLocal;
        tsmiDiffFirstToLocal.IsEnabled = state.DiffFirstToLocal;
        tsmiDiffSelectedToLocal.IsVisible = tsmiDiffFirstToLocal.IsVisible = state.ShowToLocal;
        ShowRememberItems(DiffFileRemember.For(selection, path, binding.FocusedIndex(selection)));
        tsmiOpenWorkingDirectoryFile.IsVisible = state.OpenWorkingFile;
        tsmiOpenRevisionFile.IsVisible = state.OpenRevisionFile;
        tsmiOpenRevisionFile.IsEnabled = state.OpenRevisionFileEnabled;

        // Upstream's "with..." items show the system's "Open with" dialog, which Windows has.
        tsmiOpenWorkingDirectoryFileWith.IsVisible = OperatingSystem.IsWindows() && state.OpenWorkingFile;
        tsmiOpenRevisionFileWith.IsVisible = OperatingSystem.IsWindows() && state.OpenRevisionFile;
        tsmiOpenRevisionFileWith.IsEnabled = state.OpenRevisionFileEnabled;
        tsmiEditWorkingDirectoryFile.IsVisible = state.EditWorkingFile;
        tsmiOpenInVisualStudio.IsVisible = state.EditWorkingFile && OperatingSystem.IsWindows() && VisualStudio.IsInstalled;
        tsmiSaveAs.IsVisible = state.SaveAs;
        tsmiMove.IsVisible = state.RenameMove && DiffFileMenu.MoveTarget(selection) is not null;
        tsmiDeleteFile.Header = state.DeleteFileText;
        tsmiDeleteFile.IsVisible = state.DeleteFile;
        sepFile.IsVisible = state.FileSeparator;
        tsmiCopyPaths.IsVisible = true;
        tsmiCopyPaths.IsEnabled = state.CopyPaths;
        tsmiShowInFolder.IsVisible = state.ShowInFolder;
        tsmiShowInFolder.IsEnabled = state.ShowInFolderEnabled;
        sepBrowse.IsVisible = true;

        // Upstream shows "Show in File tree" and "Filter file in grid" only where BindContextMenu gave them an action, and
        // makes File history the default item there.
        tsmiShowInFileTree.IsVisible = binding.ShowInFileTree is not null && state.ShowInFileTree
                                                                         && binding.CanShowInFileTree(selection);
        tsmiFilterFileInGrid.IsVisible = binding.FilterFileInGrid is not null;
        tsmiFilterFileInGrid.IsEnabled = state.FilterFileInGrid;
        tsmiFileHistory.IsVisible = true;
        tsmiFileHistory.IsEnabled = state.FileHistory && binding.Services()?.FileHistory is not null;
        tsmiFileHistory.FontWeight = binding.ShowInFileTree is null || opensSubmodule ? FontWeight.Normal : FontWeight.Bold;
        tsmiBlame.IsVisible = true;
        tsmiBlame.IsEnabled = state.Blame && (binding.Blame is not null || binding.Services()?.FileHistory is not null);
        tsmiBlame.ToggleType = binding.BlameChecked is null ? MenuItemToggleType.None : MenuItemToggleType.CheckBox;
        tsmiBlame.IsChecked = binding.BlameChecked?.Invoke() ?? false;
        tsmiFindFile.IsVisible = binding.FindFile is not null;
        tsmiOpenFindInCommitFilesGitGrepDialog.IsVisible = binding.GitGrep is not null;
        tsmiShowFindInCommitFilesGitGrep.IsVisible = binding.GitGrep is not null;
        tsmiShowFindInCommitFilesGitGrep.IsChecked = binding.GitGrep?.BoxVisible ?? false;
        sepIgnore.IsVisible = state.IgnoreSeparator;
        tsmiAddFileToGitIgnore.IsVisible = state.AddToIgnore;
        tsmiAddFileToGitInfoExclude.IsVisible = state.AddToIgnore;
        tsmiStopTracking.IsVisible = state.StopTracking;
        tsmiSkipWorktree.IsVisible = tsmiAssumeUnchanged.IsVisible = state.IndexFlags;
        tsmiSkipWorktree.IsChecked = state.SkipWorktreeChecked;
        tsmiAssumeUnchanged.IsChecked = state.AssumeUnchangedChecked;
        ShowScripts();
    }

    // Upstream's UpdateStatusOfTreeContextMenuItems: the tree items for a selected node with nodes below it, and "Collapse
    // root folders" in the File tree tab while a root folder is expanded.
    private void ShowTreeItems()
    {
        bool hasSubnodes = _binding?.Tree?.HasSubnodes() ?? false;
        _selectAll.IsVisible = _collapseAll.IsVisible = _expandAll.IsVisible = hasSubnodes;
        _treeContextMenuSeparator.IsVisible = hasSubnodes;
        _collapseRootFolders.IsVisible = _binding?.Tree?.AnyRootExpanded() ?? false;
    }

    private bool IsTreeItem(Control item)
        => item == _selectAll || item == _collapseAll || item == _expandAll || item == _treeContextMenuSeparator
           || item == _collapseRootFolders;

    // Upstream's submodule items: each acts on the selected submodules' paths (Distinct, as upstream's).
    /// <summary>
    ///  Upstream FileStatusList's <c>FileStatusListView_DoubleClick</c>: a submodule opens where
    ///  <c>OpenSubmoduleDiffInSeparateWindow</c> says so, anything else shows its history. True when something was done.
    /// </summary>
    public bool RunDoubleClick()
    {
        UpdateStatusOfMenuItems();
        if (openSubmoduleMenuItem.IsVisible && AppServices.Preferences.OpenSubmoduleDiffInSeparateWindow)
        {
            WithSubmodules((submodules, paths) => submodules.Open(paths[0]));
            return true;
        }

        if (tsmiFileHistory.IsEnabled)
        {
            StartFileHistory(showBlame: false);
            return true;
        }

        return false;
    }

    private void WithSubmodules(Func<FileListSubmodules, IReadOnlyList<string>, Task> action)
    {
        if (_binding?.Submodules() is { } submodules && SelectedFiles() is { } selection
            && selection.Files.Where(file => file.IsSubmodule).Select(file => file.Path).Distinct().ToList() is
                { Count: > 0 } paths)
        {
            Run(() => action(submodules, paths));
        }
    }

    // The rest of upstream's OpenWithDifftool_DropDownOpening: diff two files, or one with the remembered file.
    private void ShowRememberItems(DiffRememberState remember)
    {
        sepDifftoolRemember.IsVisible = remember.Separator;
        tsmiDiffTwoSelected.IsVisible = remember.DiffTwoSelected;
        tsmiDiffTwoSelected.IsEnabled = remember.DiffTwoSelectedEnabled;
        tsmiDiffWithRemembered.IsVisible = remember.DiffWithRemembered is not null;
        tsmiDiffWithRemembered.Header = remember.DiffWithRemembered is { } text
            ? UpstreamTranslation.WithAccessKey(text)
            : null;
        tsmiDiffWithRemembered.IsEnabled = remember.DiffWithRememberedEnabled;
        tsmiRememberSecondRevDiff.IsVisible = remember.RememberSecond;
        tsmiRememberSecondRevDiff.IsEnabled = remember.RememberSecondEnabled;
        tsmiRememberFirstRevDiff.IsVisible = remember.RememberFirst;
        tsmiRememberFirstRevDiff.IsEnabled = remember.RememberFirstEnabled;
    }

    // Upstream's LoadCustomDifftools: these items, each with what opens the selection in a given tool.
    private IReadOnlyList<(MenuItem Item, Action<string?> Open)> CustomDiffToolItems()
        =>
        [
            (tsmiDiffFirstToSelected, tool => Run(() => OpenWithDifftoolAsync(RevisionDiffKind.DiffAB, tool))),
            (tsmiDiffSelectedToLocal, tool => Run(() => OpenWithDifftoolAsync(RevisionDiffKind.DiffBLocal, tool))),
            (tsmiDiffFirstToLocal, tool => Run(() => OpenWithDifftoolAsync(RevisionDiffKind.DiffALocal, tool))),
            (tsmiDiffWithRemembered, tool => Run(() => DiffWithRememberedAsync(tool))),
            (tsmiDiffTwoSelected, tool => Run(() => DiffTwoSelectedAsync(tool))),
        ];

    private Task LoadCustomDifftoolsAsync()
        => CustomDiffTools.LoadAsync(CustomDiffToolItems(), diff: true, AppServices.Preferences);

    // Upstream's AddUserScripts: the scripts shown in file lists are items of the menu, the others under "Run script".
    private void ShowScripts()
    {
        tsmiRunScript.IsVisible = true;
        sepScripts.IsVisible = _binding?.RunScript is { } run
                               && ScriptMenus.Fill(this, tsmiRunScript,
                                   script => script.OnEvent == ScriptEvent.ShowInFileList, run);
        if (_binding?.RunScript is null)
        {
            tsmiRunScript.IsEnabled = false;
        }
    }

    private DiffMenuSelection? SelectedFiles() => _binding?.Selection();

    // Upstream's InteractiveAdd_Click and ResetChunkOfFile_Click: git's interactive patch of the file in a window of its
    // own, then the list read again.
    private async Task InteractivePatchAsync(bool reset)
    {
        if (_binding is { } binding && binding.RepositoryPath() is { } path
                                    && SelectedFiles() is { Files: [{ } file] })
        {
            await InteractivePatch.RunAsync(path, file.Path, reset, HostPlatform.Current);
            binding.Refresh();
        }
    }

    private IEnumerable<string> SelectedPaths() => SelectedFiles()?.Files.Select(file => file.Path) ?? [];

    // Upstream's getSelectedFilePaths: the selected folder, else the selected files.
    private IEnumerable<string> CopiedPaths() => SelectedFiles() is { } selection ? DiffFileMenu.CopiedPaths(selection) : [];

    // The selected files shown as changed in the working directory or in the index.
    private IEnumerable<string> FilesShownAt(ObjectId second)
        => SelectedFiles() is { } selection
            ? selection.Files.Where(file => DiffFileMenu.RevisionsOf(selection, file).Second == second)
                .Select(file => file.Path)
            : [];

    private Window? Owner => _binding?.Owner;

    // Upstream's StageFile_Click and UnstageFile_Click: the selected working directory files to the index, or the selected
    // index files back.
    // Upstream's SkipWorktree_Click and AssumeUnchanged_Click: the item checks on the click (CheckOnClick), and git's flag
    // follows it on the selected files; then the list is read again.
    private async Task SetIndexFlagAsync(IndexFlag flag, MenuItem item)
    {
        if (_binding is { } binding && binding.RepositoryPath() is { } path && SelectedFiles() is { } selection)
        {
            bool value = flag == IndexFlag.SkipWorktree
                ? !selection.Files.Any(file => file.IsSkipWorktree)
                : !selection.Files.Any(file => file.IsAssumeUnchanged);
            item.IsChecked = value;
            await DiffFileMenu.SetIndexFlagAsync(selection, path, flag, value);
            binding.Refresh();
        }
    }

    // Upstream's StageFile_Click and UnstageFile_Click: the window's own stage or unstage when it gave one (FormCommit's),
    // else the selected working directory's or index's files.
    private async Task StageAsync(bool stage)
    {
        if ((stage ? _binding?.Stage : _binding?.Unstage) is { } own)
        {
            own();
            return;
        }

        if (_binding is not { } binding || binding.RepositoryPath() is not { } path
                                        || binding.Services() is not { } services)
        {
            return;
        }

        ObjectId from = stage ? ObjectId.WorkTreeId : ObjectId.IndexId;
        List<string> files = [.. FilesShownAt(from)];
        if (files.Count == 0)
        {
            return;
        }

        await (stage ? services.Actions.StageAsync(path, files) : services.Actions.UnstageAsync(path, files));
        binding.Refresh();
    }

    /// <summary>
    ///  Upstream's <c>ResetSelectedItemsWithConfirmation</c>: FormResetChanges with upstream's question, then the reset.
    /// </summary>
    internal async Task ResetAsync(bool toFirst)
    {
        if (_binding is not { } binding || binding.RepositoryPath() is not { } path
                                        || binding.Selection() is not { Files.Count: > 0 } selection)
        {
            return;
        }

        (string revision, bool hasExistingFiles, bool hasNewFiles) =
            DiffFileMenu.ResetQuestion(selection, toFirst, binding.DescribeRevision);
        ResetChangesWindow.ResetAction action = await ResetChangesWindow.ShowResetDialogAsync(binding.Owner,
            hasExistingFiles, hasNewFiles,
            string.Format(UpstreamTranslation.ByText(DiffFileMenu.ResetSelectedChangesText), revision));
        if (action == ResetChangesWindow.ResetAction.Cancel)
        {
            return;
        }

        try
        {
            string output = await DiffFileMenu.ResetAsync(selection, path, toFirst,
                resetAndDelete: action == ResetChangesWindow.ResetAction.ResetAndDelete);
            if (output.Length > 0)
            {
                ShowError(output);
            }
        }
        finally
        {
            binding.Refresh();
        }
    }

    private async Task OpenWithDifftoolAsync(RevisionDiffKind kind, string? tool = null)
    {
        if (_binding?.RepositoryPath() is { } path && SelectedFiles() is { Files.Count: > 0 } selection
                                                   && await DiffFileMenu.OpenWithDifftoolAsync(selection, path, kind,
                                                       tool) is { } error)
        {
            ShowError(error);
        }
    }

    // Upstream's DiffTwoSelected_Click.
    private async Task DiffTwoSelectedAsync(string? tool)
    {
        if (_binding is { } binding && binding.RepositoryPath() is { } path
                                    && binding.Selection() is { Files.Count: 2 } selection)
        {
            await DiffFileRemember.DiffTwoSelectedAsync(selection, path, binding.FocusedIndex(selection), tool);
        }
    }

    // Upstream's DiffWithRemembered_Click.
    private async Task DiffWithRememberedAsync(string? tool)
    {
        if (_binding?.RepositoryPath() is { } path && SelectedFiles() is { Files.Count: 1 } selection)
        {
            await DiffFileRemember.DiffWithRememberedAsync(selection, path, tool);
        }
    }

    // Upstream's RememberSecondRevDiff_Click and RememberFirstRevDiff_Click.
    private void Remember(bool first)
    {
        if (_binding?.RepositoryPath() is { } path && SelectedFiles() is { Files.Count: 1 } selection)
        {
            DiffFileRemember.Remember(selection, path, first);
        }
    }

    /// <summary>
    ///  Upstream's <c>Move_Click</c>: asks for the file's (or the selected folder's) new name until git has moved it or the
    ///  user cancels; a name git's move does not take (empty, the old one) is asked for again, and so is one git failed with
    ///  after its error.
    /// </summary>
    internal async Task MoveAsync()
    {
        if (_binding is not { } binding || binding.RepositoryPath() is not { } path
                                        || binding.Selection() is not { } selection
                                        || DiffFileMenu.MoveTarget(selection) is not var (oldName, isFolder))
        {
            return;
        }

        string title = WithoutAccessKey(tsmiMove.Header as string ?? "");
        string newName = oldName;
        while (await new PromptWindow(title, UpstreamTranslation.ByText(NewNameText), "OK", initialValue: newName,
                       allowEmpty: true)
                   .ShowDialog<PromptResult?>(binding.Owner) is { } result)
        {
            newName = result.Value;
            if (!DiffFileMenu.IsValidMove(path, oldName, newName, isFolder))
            {
                continue;
            }

            try
            {
                await DiffFileMenu.MoveAsync(path, oldName, newName, isFolder);
                return;
            }
            catch (Exception ex)
            {
                await new ErrorWindow(ex.Message) { Title = title }.ShowDialog(binding.Owner);
            }
            finally
            {
                binding.Refresh();
            }
        }
    }

    // Upstream's OpenWorkingDirectoryFile_Click: the file with the program the system opens it with.
    private void OpenWorkingDirectoryFile()
    {
        if (_binding?.RepositoryPath() is { } path && SelectedPaths().FirstOrDefault() is { } file)
        {
            Run(() => Launcher.LaunchFileInfoAsync(new FileInfo(Path.Combine(path, file))));
        }
    }

    // Upstream's OpenWorkingDirectoryFileWith_Click: the system's "Open with" dialog for the file.
    private void OpenWorkingDirectoryFileWith()
    {
        if (OperatingSystem.IsWindows() && _binding?.RepositoryPath() is { } path
                                        && SelectedPaths().FirstOrDefault() is { } file)
        {
            OsShellUtil.OpenAs(Path.GetFullPath(Path.Combine(path, file)));
        }
    }

    // Upstream's OpenRevisionFileWith_Click: the revision's file saved to the temporary folder, then "Open with".
    private async Task OpenRevisionFileWithAsync()
    {
        if (OperatingSystem.IsWindows() && _binding?.RepositoryPath() is { } path
                                        && SelectedFiles() is { Files.Count: > 0 } selection
                                        && await DiffFileMenu.SaveToTempFileAsync(selection, path) is { } file)
        {
            OsShellUtil.OpenAs(file);
        }
    }

    // Upstream's EditWorkingDirectoryFile_Click: the file in Git Extensions' editor.
    private void EditWorkingDirectoryFile()
    {
        if (_binding?.RepositoryPath() is { } path && SelectedPaths().FirstOrDefault() is { } file && Owner is { } owner)
        {
            new EditorWindow(Path.Combine(path, file)).Show(owner);
        }
    }

    // Upstream's OpenInVisualStudio_Click: the working directory file at the viewer's line, after upstream's question when
    // Visual Studio rejects the call.
    [SupportedOSPlatform("windows")]
    private async Task OpenInVisualStudioAsync()
    {
        if (!VisualStudio.IsInstalled || _binding is not { } binding
            || binding.RepositoryPath() is not { } path || SelectedPaths().FirstOrDefault() is not { } file)
        {
            return;
        }

        await VisualStudio.OpenFileAsync(Path.GetFullPath(Path.Combine(path, file)),
            binding.Viewer?.Invoke()?.CurrentFileLine ?? 0, async () =>
            {
                MessageBoxWindow question = new(UpstreamTranslation.ByText(VisualStudio.RetryOpenText),
                    UpstreamTranslation.ByText(VisualStudio.RetryCaption), System.Windows.Forms.MessageBoxButtons.YesNo);
                await question.ShowDialog(binding.Owner);
                return question.Result == System.Windows.Forms.DialogResult.Yes;
            });
    }

    // Upstream's OpenRevisionFile_Click: the file as it is in the selected revision, from the temporary folder.
    private async Task OpenRevisionFileAsync()
    {
        if (_binding?.RepositoryPath() is { } path && SelectedFiles() is { Files.Count: > 0 } selection
                                                   && await DiffFileMenu.SaveToTempFileAsync(selection, path) is
                                                       { } file)
        {
            await Launcher.LaunchFileInfoAsync(new FileInfo(file));
        }
    }

    // Upstream's SaveAs_Click: a file name for one file, a folder for several.
    private async Task SaveAsAsync()
    {
        if (_binding is not { } binding || binding.RepositoryPath() is not { } path
                                        || binding.Selection() is not { Files.Count: > 0 } selection)
        {
            return;
        }

        IStorageProvider storage = binding.Owner.StorageProvider;
        string suggestion = DiffFileMenu.SaveSuggestion(selection, path);
        string? target;
        if (selection.Files.Count == 1)
        {
            string extension = Path.GetExtension(suggestion).TrimStart('.');
            IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedStartLocation =
                    await storage.TryGetFolderFromPathAsync(Path.GetDirectoryName(suggestion) ?? path),
                SuggestedFileName = Path.GetFileName(suggestion),
                DefaultExtension = extension,
                FileTypeChoices = extension.Length > 0
                    ?
                    [
                        new FilePickerFileType(
                            $"{UpstreamTranslation.ByText(SaveFileFilterCurrentFormat)}(*.{extension})")
                        {
                            Patterns = [$"*.{extension}"]
                        },
                        new FilePickerFileType($"{UpstreamTranslation.ByText(SaveFileFilterAllFiles)}(*.*)")
                        {
                            Patterns = ["*"]
                        }
                    ]
                    : null
            });
            target = file?.TryGetLocalPath();
        }
        else
        {
            IReadOnlyList<IStorageFolder> folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(suggestion),
                AllowMultiple = false
            });
            target = folders.FirstOrDefault()?.TryGetLocalPath();
        }

        if (target is not null)
        {
            await DiffFileMenu.SaveAsync(selection, path, target);
        }
    }

    /// <summary>
    ///  Upstream's <c>DeleteFile_Click</c>: after the question, staged files leave the index, then the files leave the
    ///  disk.
    /// </summary>
    internal async Task DeleteAsync()
    {
        if (_binding is not { } binding || binding.RepositoryPath() is not { } path
                                        || binding.Selection() is not { Files.Count: > 0 } selection
                                        || !DiffFileMenu.RevisionsOf(selection, selection.Files[0]).Second.IsArtificial
                                        || binding.Services() is not { } services
                                        || !await new ConfirmWindow(UpstreamTranslation.ByText(DeleteSelectedFilesText),
                                                "Yes", UpstreamTranslation.ByText(DeleteSelectedFilesCaption),
                                                cancelText: "No")
                                            .ShowDialog<bool>(binding.Owner))
        {
            return;
        }

        try
        {
            if (FilesShownAt(ObjectId.IndexId).ToList() is { Count: > 0 } staged)
            {
                await services.Actions.UnstageAsync(path, staged);
            }

            foreach (string file in selection.Files.Select(file => Path.Combine(path, file.Path)))
            {
                if (Directory.Exists(file))
                {
                    Directory.Delete(file, recursive: true);
                }
                else
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError($"{UpstreamTranslation.ByText(DeleteFailedText)}{Environment.NewLine}{ex.Message}");
        }
        finally
        {
            binding.Refresh();
        }
    }

    // Upstream's FormBrowse.OpenContainingFolder: the folder of the first selected file that exists, or of its parent.
    private void ShowInFolder()
    {
        if (_binding?.RepositoryPath() is not { } path)
        {
            return;
        }

        foreach (string file in SelectedPaths().Select(file => Path.Combine(path, file)))
        {
            if (Path.GetDirectoryName(file) is { } folder && Directory.Exists(folder))
            {
                FileManager.OpenFolder(folder);
                return;
            }
        }
    }

    // Upstream's OpenInFileTreeTab: the selected folder, else the first selected file.
    private void ShowInFileTree()
    {
        if (_binding?.ShowInFileTree is { } show
            && (SelectedFiles()?.Folder is { Length: > 0 } folder ? folder : SelectedPaths().FirstOrDefault()) is { } path)
        {
            show(path);
        }
    }

    // Upstream's StartFileHistoryDialog: a tracked file's history from the selected revision, or a folder's from the
    // current checkout.
    private void StartFileHistory(bool showBlame)
    {
        if (_binding is not { } binding || binding.RepositoryPath() is not { } path
                                        || binding.Services()?.FileHistory is not { } fileHistory
                                        || binding.Selection() is not { } selection
                                        || DiffFileMenu.FileHistoryTarget(selection, binding.CurrentCheckout()) is not
                                            var (file, revision))
        {
            return;
        }

        fileHistory(path, file, revision, showBlame).Show(binding.Owner);
    }

    // Upstream RevisionDiffControl's FilterFileInGrid: the selected folder, else the selected files.
    private void FilterFileInGrid()
    {
        if (_binding?.FilterFileInGrid is { } filter && SelectedFiles() is { } selection
                                                     && (selection.Folder is not null || selection.Files.Count > 0))
        {
            filter(DiffFileMenu.GridPathFilter(selection));
        }
    }

    /// <summary>
    ///  Upstream's <c>FindFile_Click</c>: the search window over the list's files; the file taken is selected.
    /// </summary>
    internal async Task FindFileAsync()
    {
        if (_binding is not { FindFile: { } find } binding || binding.RepositoryPath() is not { } path)
        {
            return;
        }

        IReadOnlyList<(string Path, string? OldPath)> files = find.Files();
        if (await new SearchWindow(text => FindFile.Matches(files, text, path)).ShowAsync(binding.Owner) is { } file)
        {
            find.Select(file);
        }
    }

    // Upstream's AddFileToIgnoreFile: the selected folder or each selected file anchored at the root, added to .gitignore or
    // the local excludes.
    private async Task AddToIgnoreFileAsync(bool localExclude)
    {
        List<string> patterns = SelectedFiles() is { } selection ? [.. DiffFileMenu.IgnorePatterns(selection)] : [];
        if (_binding is { } binding && binding.RepositoryPath() is { } path && patterns.Count > 0
            && await AddToGitIgnoreWindow.ShowAsync(binding.Owner, path, localExclude, patterns))
        {
            binding.Refresh();
        }
    }

    private async Task StopTrackingAsync()
    {
        if (_binding is not { } binding || binding.RepositoryPath() is not { } path
                                        || binding.Services() is not { } services
                                        || SelectedPaths().FirstOrDefault() is not { } file)
        {
            return;
        }

        if (!await services.Actions.StopTrackingAsync(path, file))
        {
            ShowError(string.Format(UpstreamTranslation.ByText(StopTrackingFailText), file));
        }

        binding.Refresh();
    }

    private async Task CopyTextAsync(string text)
    {
        if (Owner?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    private ILauncher Launcher => Owner?.Launcher ?? throw new InvalidOperationException();

    private void ShowError(string message)
    {
        if (Owner is { } owner)
        {
            _ = new ErrorWindow(message).ShowDialog(owner);
        }
    }

    private void Run(Func<Task> action) => UiActions.Run(action, ex => ShowError(ex.Message));

    // Upstream's RemoveMnemonicMarker for an Avalonia header: "Rena_me" is "Rename", "a__b" is "a_b".
    private static string WithoutAccessKey(string header)
        => header.Replace("__", "\0").Replace("_", "").Replace("\0", "_");

    /// <summary>
    ///  The item of <paramref name="list"/> selected last (the selection's anchor, as upstream's <c>FocusedItem</c>) while it
    ///  is selected, else null.
    /// </summary>
    internal static object? FocusedItem(ListBox list)
        => list.Selection.AnchorIndex is var anchor and >= 0 && list.Selection.IsSelected(anchor)
                                                              && anchor < list.ItemCount
            ? list.Items[anchor]
            : null;

    /// <summary>
    ///  Upstream FileStatusList's <c>GetDescriptionForRevision</c> where no <c>DescribeRevision</c> is bound (FormDiff,
    ///  FormStash): the working directory and the index by name, a commit by its short hash.
    /// </summary>
    internal static string DescribeByHash(ObjectId id)
        => id == ObjectId.WorkTreeId ? ResourceManager.TranslatedStrings.Workspace
            : id == ObjectId.IndexId ? ResourceManager.TranslatedStrings.Index
            : id.ToShortString();
}

/// <summary>
///  What a window's file list adds to its <see cref="FileStatusListMenu"/>, as upstream's
///  <c>FileStatusList.BindContextMenu</c>.
/// </summary>
internal sealed class FileStatusListMenuBinding
{
    /// <summary>
    ///  The window the menu's dialogs open over.
    /// </summary>
    public required Window Owner { get; init; }

    public required Func<string?> RepositoryPath { get; init; }

    /// <summary>
    ///  The selected files with the revisions the list shows them between; null without a selection.
    /// </summary>
    public required Func<DiffMenuSelection?> Selection { get; init; }

    /// <summary>
    ///  Upstream's <c>DescribeRevision</c>, for the reset and difftool items.
    /// </summary>
    public required Func<ObjectId, string> DescribeRevision { get; init; }

    /// <summary>
    ///  The git actions and windows the items use; null leaves out the items that need them.
    /// </summary>
    public required Func<FileListMenuServices?> Services { get; init; }

    /// <summary>
    ///  Upstream's <c>RequestRefresh</c>: reads the list again after a change.
    /// </summary>
    public required Action Refresh { get; init; }

    /// <summary>
    ///  Upstream's <c>openInFileTreeTab</c>; null hides "Show in File tree" and keeps File history a normal item.
    /// </summary>
    public Action<string>? ShowInFileTree { get; init; }

    /// <summary>
    ///  Whether the file tree can show the selection's files.
    /// </summary>
    public Func<DiffMenuSelection, bool> CanShowInFileTree { get; init; } = _ => true;

    /// <summary>
    ///  Upstream's <c>filterFileInGrid</c> with the grid's path filter; null hides "Filter file in grid".
    /// </summary>
    public Action<string>? FilterFileInGrid { get; init; }

    /// <summary>
    ///  Upstream's <c>blame</c>; null opens the file history on its Blame tab.
    /// </summary>
    public Action? Blame { get; init; }

    /// <summary>
    ///  Whether Blame shows checked (upstream's <c>tsmiBlame.Checked</c>, in the File tree tab); null for a plain item.
    /// </summary>
    public Func<bool>? BlameChecked { get; init; }

    /// <summary>
    ///  Upstream's <c>getCurrentRevision</c>: the commit a folder's history starts from; null for the working directory.
    /// </summary>
    public Func<string?> CurrentCheckout { get; init; } = () => null;

    /// <summary>
    ///  The list's tree for upstream's tree items; null hides them.
    /// </summary>
    public FileListTree? Tree { get; init; }

    /// <summary>
    ///  The viewer of the selected file (upstream's <c>cherryPickChanges</c> and <c>getSupportLinePatching</c>); null hides
    ///  "Cherry pick changes".
    /// </summary>
    public Func<DiffView?>? Viewer { get; init; }

    /// <summary>
    ///  Upstream's submodule commands for the selected submodules; null hides the submodule items.
    /// </summary>
    public Func<FileListSubmodules?> Submodules { get; init; } = () => null;

    /// <summary>
    ///  The list's "Find in commit files using git-grep" (upstream's <c>CanUseFindInCommitFilesGitGrep</c>); null hides its
    ///  items.
    /// </summary>
    public GitGrepBox? GitGrep { get; init; }

    /// <summary>
    ///  Upstream's <c>stage</c> of <c>BindContextMenu</c> (FormCommit's working directory list): what "Stage selected" does
    ///  instead of staging the selected files itself; null for the menu's own.
    /// </summary>
    public Action? Stage { get; init; }

    /// <summary>
    ///  Upstream's <c>unstage</c> of <c>BindContextMenu</c> (FormCommit's staged list); null for the menu's own.
    /// </summary>
    public Action? Unstage { get; init; }

    /// <summary>
    ///  Upstream's <c>SelectFirstGroupChanges</c> hotkey (<c>FirstGroupItems</c>): selects the files of the list's first
    ///  diff group, or all its files when it shows no diff groups; null leaves the key to the list.
    /// </summary>
    public Action? SelectFirstGroup { get; init; }

    /// <summary>
    ///  Upstream's file tree mode (the File tree tab), for its own git-grep hotkey.
    /// </summary>
    public bool IsFileTreeMode { get; init; }

    /// <summary>
    ///  Runs a user script with the list's selection; null offers no scripts.
    /// </summary>
    public Action<ScriptDefinition>? RunScript { get; init; }

    /// <summary>
    ///  The list's files for "Find file" and how to select one; null hides the item.
    /// </summary>
    public FileListFind? FindFile { get; init; }

    /// <summary>
    ///  The path of the file selected last (upstream's <c>FocusedItem</c>), the second file of "Diff the selected files";
    ///  null when unknown.
    /// </summary>
    public Func<string?> FocusedPath { get; init; } = () => null;

    /// <summary>
    ///  The index of <see cref="FocusedPath"/> in the selection's files, or -1.
    /// </summary>
    public int FocusedIndex(DiffMenuSelection selection)
        => FocusedPath() is { } focused
            ? selection.Files.Select(file => file.Path).ToList().IndexOf(focused)
            : -1;
}

/// <summary>
///  Upstream FileStatusList's submodule items, each for the selected submodules' paths in the repository.
/// </summary>
/// <param name="Open">Upstream's <c>OpenSubmoduleAsync</c>: the submodule in a new instance.</param>
/// <param name="Update">Upstream's <c>UpdateSubmodule_Click</c>.</param>
/// <param name="Reset">Upstream's <c>ResetSubmoduleChanges_Click</c>, after its question.</param>
/// <param name="Stash">Upstream's <c>StashSubmoduleChanges_Click</c>.</param>
/// <param name="Commit">Upstream's <c>CommitSubmoduleChanges_Click</c>: a commit window on each submodule.</param>
internal sealed record FileListSubmodules(
    Func<string, Task> Open,
    Func<IReadOnlyList<string>, Task> Update,
    Func<IReadOnlyList<string>, Task> Reset,
    Func<IReadOnlyList<string>, Task> Stash,
    Func<IReadOnlyList<string>, Task> Commit);

/// <summary>
///  A file list shown as a tree, for upstream's tree items (<c>FileStatusList.TreeContextMenu.cs</c>).
/// </summary>
/// <param name="HasSubnodes">Whether a selected node has nodes below it.</param>
/// <param name="SelectAll">Selects the nodes below the selected ones.</param>
/// <param name="CollapseAll">Collapses the selected nodes and the nodes below them.</param>
/// <param name="ExpandAll">Expands the selected nodes and the nodes below them.</param>
/// <param name="AnyRootExpanded">Whether "Collapse root folders" shows: a root folder is expanded.</param>
/// <param name="CollapseRootFolders">Selects the selected node's root folder and collapses the root folders.</param>
internal sealed record FileListTree(
    Func<bool> HasSubnodes,
    Action SelectAll,
    Action CollapseAll,
    Action ExpandAll,
    Func<bool> AnyRootExpanded,
    Action CollapseRootFolders);

/// <summary>
///  What upstream's "Find file" searches in a file list and how it selects the file taken.
/// </summary>
/// <param name="Files">All the list's files (upstream's <c>GitItemStatuses</c>): path and path before a rename.</param>
/// <param name="Select">Selects the file with the path in the list.</param>
internal sealed record FileListFind(Func<IReadOnlyList<(string Path, string? OldPath)>> Files, Action<string> Select);

/// <summary>
///  The git actions and windows of the browse window that file list menus use (upstream's <c>UICommands</c>).
/// </summary>
/// <param name="Actions">Stage, unstage and stop tracking.</param>
/// <param name="FileHistory">A file history window: repository, file, revision (null for the working directory),
/// whether to show its blame; null leaves out File history and Blame.</param>
/// <param name="OfferConflicts">Upstream's <c>HandleMergeConflicts</c> over a window, after a patch left conflicts.</param>
internal sealed record FileListMenuServices(
    RepositoryOperationsViewModel Actions,
    Func<string, string, string?, bool, Window>? FileHistory = null,
    Func<Window, string, Task>? OfferConflicts = null);
