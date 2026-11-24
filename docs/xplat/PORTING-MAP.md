# Porting map

One row per upstream WinForms form or control that the cross-platform shell reimplements. The rule
and the reason are in `docs/xplat/PLAN.md` section 6. Upstream files are never edited, so each row is
what keeps a reimplementation traceable to the upstream code it copies.

## Columns

- **Upstream file:** the WinForms file under `src/app` that the port follows.
- **New file:** the cross-platform file that reimplements it.
- **Based on:** the upstream commit the port was written against. `tests/xplat/port-drift.sh` lists
  upstream commits that touched the upstream file after this commit.
- **Status:**
  - `ported`: the behavior the port covers matches the upstream file.
  - `partial`: some of the upstream behavior is not ported yet; the gaps are listed in the row's notes.
  - `skipped: <reason>`: deliberately not ported, with the reason.
  - `windows-only`: the upstream behavior exists only on Windows, so the port does not cover it.
  - `stale`: upstream changed after "Based on" and the change has not been decided yet. Milestone exit
    criteria fail while any row is stale.

## Rows

| Upstream file | New file | Based on | Status |
|---|---|---|---|
| `src/app/GitUI/CommandsDialogs/FormBrowse.cs` | `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml`, `MainWindow.axaml.cs` (view: menu, toolbar, left panel, grid, Commit and Diff tabs); `src/xplat/GitExtensions.Xplat.Core/CommitHistory/CommitListViewModel.cs` (state) | `bd69886b7` | ported |
| `src/app/GitUI/UserControls/RevisionGrid/RevisionGridControl.cs` | `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (`CommitList`), `MainWindow.GridSelection.cs`; `src/xplat/GitExtensions.Xplat.Core/CommitHistory/GitCommitHistory.cs` (reads); `CommitListViewModel.cs`, `GridSelection.cs`, `StashRows.cs`, `RevisionFilter.cs` (`ShowStashes`) | `bd69886b7` | ported |
| `src/app/GitUI/UserControls/RevisionGrid/RevisionGridControl.Designer.cs` (mainContextMenu), `RevisionGridControl.cs` (`_contextMenuItems`, `ContextMenuOpening`, `GetRefUnambiguousName`, the compare items), `RevisionGridMenuCommands.cs` (View), `GitRefListsForRevision.cs`, `CopyContextMenuItem.cs`; `CommandsDialogs/FormBrowse.InitRevisionGrid.cs` (`SelectInLeftPanel`) | `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (`CommitContextMenu`), `MainWindow.GridMenu.cs`, `MainWindow.Compare.cs`, `RefLabelView.cs` (`LabelAt`), `Hotkeys.cs`; `src/xplat/GitExtensions.Xplat.Core/CommitHistory/GridMenus.cs`, `GridMenuViewModel.cs` (the items' state, MVVM, `MVVM.md`), `GridCommand.cs`, `ICommitHistory.cs` (`CommitRow` dates), `Repository/BrowseToolbarViewModel.GridMenu.cs` (the ref and copy submenus' entries and the items' commands), `Repository/IBrowseWindow.cs` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormCommit.cs` | `src/xplat/GitExtensions.Xplat.App/CommitWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Core/Repository/CommitViewModel.cs`, `CommitViewModel.FileLists.cs`, `CommitViewModel.Menus.cs` (MVVM, `MVVM.md`), `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (commit), `RepositoryOperationsViewModel.cs` | `bd69886b7` | ported |
| `src/app/GitUI/UserControls/FileStatusList*.cs` (toolbar, filter, NoFiles, item menu, `GetItemImageKey`); `src/app/GitUI/CommandsDialogs/RevisionDiffController.cs` (menu rules); `CommandsDialogs/Menus/CopyPathsToolStripMenuItem.cs`; `FormCommit.Designer.cs` (`toolbarStaged`, `SolveMergeconflicts`); `GitIgnoreDialog/GitLocalExcludeModel.cs` | `src/xplat/GitExtensions.Xplat.Core/Repository/FileFilterViewModel.cs` (the filter box, MVVM, `MVVM.md`), `Diff/FileListGroupingViewModel.cs` (the group-by and tree buttons, MVVM), `Diff/FileListToolbarViewModel.cs` (the collapse button and the diff status filter, MVVM), `Diff/FileListToolbarChoicesViewModel.cs` (the Toolbar submenu, MVVM), `Diff/CopyPathsMenuViewModel.cs` (Copy path(s), MVVM), `Diff/FileListMenuViewModel.cs` (the item menu's state, MVVM), `src/xplat/GitExtensions.Xplat.Core/Repository/FileStatusList.cs` (`FileStatusList`, `FileMenuState`), `GitIgnoreFile.cs`, `Operations/GitOperations.cs` (reset files to, stop tracking); `src/xplat/GitExtensions.Xplat.App/CommitWindow.FileLists.cs`, `CommitWindow.axaml`, `GitIgnoreWindow.axaml.cs`, `FileStatusListMenu.axaml(.cs)` (the item menu of a list of the files between two revisions) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormPush.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/PushDialog.cs`, `PushViewModel.cs` (MVVM, `MVVM.md`), `PushRejection.cs`, `GitOperations.cs` (push), `RepositoryOperationsViewModel.cs`, `RemoteRequests.cs`; `src/xplat/GitExtensions.Xplat.App/PushWindow.axaml(.cs)`, `PushRejectedFlow.cs`, `MainWindow.axaml(.cs)` (Push), `CommitWindow.axaml.cs` (Commit & push) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormPull.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/PullDialog.cs`, `PullViewModel.cs` (MVVM, `MVVM.md`), `GitOperations.cs` (pull, `RebasesMergeCommitAsync`), `RepositoryOperationsViewModel.cs`, `RemoteRequests.cs`; `src/xplat/GitExtensions.Xplat.App/PullWindow.axaml(.cs)`, `HelpImageView.cs`, `MainWindow.axaml(.cs)` (Pull), `PullAutoStashFlow.cs`; `src/xplat/GitExtensions.Xplat.Core/Operations/PullAutoStash.cs` (auto stash) | `bd69886b7` | ported |
| `src/app/GitUI/UserControls/HelpImageDisplayUserControl.cs`; `src/app/GitUI/GitExtensionsDialog.cs` (button strip colors and line); WinForms `GroupBox` | `src/xplat/GitExtensions.Xplat.App/HelpImageView.cs`; `App.axaml` (`Border.dialogFooter`, `HeaderedContentControl.groupBox`), `ThemeApplier.cs` (`DialogFooterBrush`, `DialogFooterLineBrush`) | `52d08e996` | ported |
| `src/app/GitExtUtils/GitUI/Theming/LightnessCorrection.cs`; `ColorHelper.AdaptLightness`; `SystemColorMode.Dark` of WinForms (as `src/app/GitUI/Themes/dark.css` lists it) | `src/xplat/GitExtensions.Xplat.Core/Settings/ImageLightness.cs`, `AppTheme.cs` (`GetNonEmpty`); `src/xplat/GitExtensions.Xplat.App/ThemedImages.cs` | `0174ba1cc` | ported |
| `src/app/GitUI/CommandsDialogs/FormClone.cs`; `src/app/GitUI/BrowseForPrivateKey.cs` (browse only) | `src/xplat/GitExtensions.Xplat.Core/Operations/CloneDialog.cs`, `CloneViewModel.cs` (MVVM, `MVVM.md`), `GitOperations.cs` (clone, `RunCloneAsync`), `RepositoryOperationsViewModel.cs`; `src/xplat/GitExtensions.Xplat.Core/Repository/RemoteUrlHistory.cs` (`AddAsMostRecentAsync`); `src/xplat/GitExtensions.Xplat.App/CloneWindow.axaml(.cs)`, `ThemeApplier.cs` (info label colors) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormCreateBranch.cs`; `src/app/GitUI/UserControls/CommitPickerSmallControl.cs`, `CommitSummaryUserControl.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/CreateBranchDialog.cs`, `CreateBranchViewModel.cs` (MVVM, `MVVM.md`), `GitOperations.cs` (create, `RunCommandsAsync`), `RepositoryOperationsViewModel.cs`; `src/xplat/GitExtensions.Xplat.App/CreateBranchWindow.axaml(.cs)`, `MainWindow.axaml.cs`, `MainWindow.LeftPanel.cs`, `CommitWindow.Bottom.cs`, `src/xplat/GitExtensions.Xplat.Core/CommitHistory/CommitPickerViewModel.cs` (the commit picker, MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormCheckoutBranch.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/CheckoutBranchDialog.cs`, `CheckoutBranchViewModel.cs` (MVVM, `MVVM.md`), `GitOperations.cs` (checkout, `RunCommandsAsync`), `RepositoryOperationsViewModel.cs`; `src/xplat/GitExtensions.Xplat.App/CheckoutBranchWindow.axaml(.cs)`, `CheckoutBranchFlow.cs`, `MainWindow.axaml(.cs)`, `MainWindow.Toolbar.cs`, `MainWindow.LeftPanel.cs` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormCheckoutRevision.cs`; `src/app/GitUI/UserControls/CommitPickerSmallControl.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/CheckoutRevisionDialog.cs`, `CheckoutRevisionViewModel.cs` (MVVM, `MVVM.md`); `src/xplat/GitExtensions.Xplat.App/CheckoutRevisionWindow.axaml(.cs)`, `CommitPickerView.axaml(.cs)`, `MainWindow.axaml(.cs)`, `MainWindow.LeftPanel.cs` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormDeleteBranch.cs`; `src/app/GitUI/UserControls/BranchComboBox.cs`; `src/app/GitUI/HelperDialogs/FormSelectMultipleBranches.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (delete), `RepositoryOperationsViewModel.cs`, `DeleteBranchDialog.cs`; `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (Delete), `DeleteBranchWindow.axaml(.cs)`, `DeleteBranchViewModel.cs` (MVVM, `MVVM.md`), `SelectMultipleBranchesWindow.axaml(.cs)`, `SelectMultipleBranchesViewModel.cs` (MVVM, `MVVM.md`), `MainWindow.DeleteRefs.cs` | `52d08e996` | ported |
| `src/app/GitUI/Editor/FileViewer.cs` | `src/xplat/GitExtensions.Xplat.Core/Diff/DiffParser.cs`, `DiffService.cs`, `DiffViewModel.cs` (MVVM, `MVVM.md`), `DiffLineAction.cs`, `DiffCopy.cs`, `FileContent.cs`, `GitColoring.cs`, `Difftastic.cs`, `UpstreamTextEditorMarkers.cs`; `src/xplat/GitExtensions.Xplat.App/DiffView.axaml(.cs)`, `DiffView.Toolbar.cs`, `ISettingsWindowHost.cs`, `DiffView.FileContent.cs`, `DiffView.ContinuousScroll.cs`, `DiffView.Find.cs`, `DiffWindow.axaml(.cs)` | `bd69886b7` | ported |
| `src/app/GitUI/CommandsDialogs/RevisionDiffControl.cs` | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/GitCommitHistory.cs` (`LoadFilesAsync`), `CommitListViewModel.cs` (`CommitFiles`); `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (changed files list, Show diff), `MainWindow.DiffRange.cs` (diff groups of a merge or several selected commits), `MainWindow.DiffSettings.cs` with `src/xplat/GitExtensions.Xplat.Core/Diff/DiffFilesSettingsViewModel.cs` (the files' settings menu, MVVM, `MVVM.md`), `RevisionFileList.cs` (group nodes), `FileTreeRows.cs` (the file tree, shared with the commit and stash windows), `MainWindow.DiffFileMenu.cs` (binds the item menu), `FileStatusListMenu.axaml(.cs)` (item menu, shared with FormDiff), `CopyPathsMenu.cs`; `src/xplat/GitExtensions.Xplat.Core/Diff/DiffFileMenu.cs` (item menu rules through the linked upstream `RevisionDiffController`, `ContextMenuSelectionInfo`, `FileStatusListContextMenuController`, `FileStatusItem` and `RevisionDiffInfoProvider`), `Diff/DiffGroups.cs` (the linked upstream `FileStatusDiffCalculator` and `FileStatusWithDescription`; `Diff/UpstreamDiffTexts.cs` stands in for its texts), `Diff/DiffParser.cs` (`ParseCombined`), `Diff/DiffFileRemember.cs` (the remembered file, through the linked upstream `RememberFileContextMenuController`); `src/xplat/GitExtensions.Xplat.Core/Repository/FileStatusList.cs` (`ImageForStatus`, `KindOf`, `Sort<T>`), `Repository/FileStatusTree.cs` | `bd69886b7` | ported |
| `src/app/GitUI/CommandsDialogs/FormFileHistory.cs`; `src/app/GitUI/UserControls/RevisionGrid/RevisionGridControl.cs` (`BuildPathFilter`, `FilePathByObjectId`), `FilterInfo.cs` (full history); `src/app/GitUI/CustomDiffMergeToolProvider.cs` | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/FileHistoryDialog.cs`, `FileHistoryViewModel.cs` (MVVM, `MVVM.md`), `FileBrowserViewModel.cs`, `GitCommitHistory.cs` (`LoadTreeAsync`, `LoadFileHistoryAsync`, `PathFilter`), `CommitListViewModel.FilePaths`, `RevisionFilter.cs` (`FullHistory`, `SimplifyMerges`); `src/xplat/GitExtensions.Xplat.App/FileHistoryWindow.axaml(.cs)`, `FileHistoryWindow.Filter.cs`, `FileHistoryWindow.GridMenus.cs`, `FileHistoryWindow.BuildServer.cs`, `MenuEntriesConverter.cs` (the difftool items' tools, from core `Diff/CustomDiffToolsViewModel.cs`, MVVM), `RevisionGrid.axaml`, `FileBrowserWindow.axaml(.cs)` (bound to `FileBrowserViewModel`, MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormBlame.cs`; `src/app/GitUI/UserControls/BlameControl.cs`, `GitBlameParser.cs` | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/BlameParser.cs`, `BlameMenu.cs`, `GitCommitHistory.cs` (`LoadBlameAsync`), `BlameViewModel.cs` and `BlameWindowViewModel.cs` (MVVM, `MVVM.md`); `src/xplat/GitExtensions.Xplat.App/BlameView.axaml(.cs)`, `BlameView.Find.cs`, `BlameWindow.axaml(.cs)` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormMergeBranch.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/MergeBranchDialog.cs`, `MergeBranchViewModel.cs` (MVVM, `MVVM.md`), `GitOperations.cs` (merge, `RunCommandsAsync`, `AbortMergeAsync`), `RepositoryOperationsViewModel.cs`, `RepositoryViewModel.IsMerging`, `Settings/Confirmations.cs` (resolve conflicts); `src/xplat/GitExtensions.Xplat.App/MergeBranchWindow.axaml(.cs)`, `MainWindow.axaml(.cs)` (Merge branches, Abort merge), `MainWindow.LeftPanel.cs` | `bd69886b7` | ported |
| `src/app/GitUI/CommandsDialogs/FormStash.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/StashDialog.cs`, `StashViewModel.cs` (MVVM, `MVVM.md`), `GitOperations.cs` (save, apply, pop, drop by name), `RepositoryOperationsViewModel.cs`; `src/xplat/GitExtensions.Xplat.Core/Repository/GitRepositoryService.cs` (stash list); `src/xplat/GitExtensions.Xplat.App/StashWindow.axaml(.cs)`, `FileStatusListMenu.axaml(.cs)` (the file list's menu), `MainWindow.axaml(.cs)`, `MainWindow.Toolbar.cs`, `MainWindow.LeftPanel.cs`, `PullWindow.axaml.cs` | `bd69886b7` | ported |
| `src/app/GitUI/CommandsDialogs/FormCreateTag.cs`; `src/app/GitCommands/Git/Tag/GitTagController.cs` (`CreateTag`) | `src/xplat/GitExtensions.Xplat.App/CreateTagWindow.axaml(.cs)`, `MainWindow.axaml.cs` (`ShowCreateTagAsync`); `src/xplat/GitExtensions.Xplat.Core/Operations/CreateTagDialog.cs`, `CreateTagViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormDeleteTag.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (delete), `RepositoryOperationsViewModel.cs`; `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (Delete), `DeleteTagWindow.axaml(.cs)`, `DeleteTagViewModel.cs` (MVVM, `MVVM.md`), `MainWindow.DeleteRefs.cs` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormCherryPick.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (cherry-pick), `RepositoryOperationsViewModel.cs`, `CherryPickDialog.cs`, `CherryPickViewModel.cs` (MVVM, `MVVM.md`), `Settings/AppPreferences.cs` (`CommitAutomaticallyAfterCherryPick`, `AddCommitReferenceToCherryPick`); `src/xplat/GitExtensions.Xplat.App/CherryPickWindow.axaml(.cs)`, `MainWindow.CommandsMenu.cs`, `MainWindow.axaml` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormResetChanges.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (reset), `RepositoryOperationsViewModel.cs`; `MainWindow.axaml` (Reset soft here, Reset mixed here) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormRebase.cs`; `src/app/GitUI/UserControls/PatchGrid.cs`, `PatchFile.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/RebaseDialog.cs`, `RebaseViewModel.cs` (MVVM, `MVVM.md`), `RebasePatches.cs`, `GitOperations.cs` (rebase, `RunRebaseAsync`, interactive rebase, abort, continue, skip, edit todo), `GitEditorCommand.cs`, `RepositoryOperationsViewModel.cs`, `RepositoryViewModel.IsRebasing`; `src/xplat/GitExtensions.Xplat.App/RebaseWindow.axaml(.cs)`, `MainWindow.axaml` (Commands > Rebase..., grid menu "Rebase current branch on", warning bar buttons) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormEditor.cs` (and the `fileeditor` verb of `GitUICommands.RunCommand`) | `src/xplat/GitExtensions.Xplat.App/EditorWindow.axaml(.cs)`, `Program.cs`, `App.axaml.cs`; `src/xplat/GitExtensions.Xplat.Core/Editing/EditorFile.cs`, `EditorViewModel.cs` (MVVM, `MVVM.md`), `EditorHighlighting.cs`; `src/xplat/GitExtensions.Xplat.App/EditorColorizer.cs` | `52d08e996` | ported |
| `src/native/GitExtSshAskPass` (native askpass dialog, set as `SSH_ASKPASS` by `EnvironmentConfiguration`) | `src/xplat/GitExtensions.Xplat.App/AskPassWindow.axaml(.cs)`, `AskPassViewModel.cs` (MVVM, `MVVM.md`), `Program.cs`, `App.axaml.cs`; `src/xplat/GitExtensions.Xplat.Core/Operations/GitAskPass.cs`, `GitOperations.cs` (remote environment) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormRemotes.cs`; `src/app/GitUI/LeftPanel/RemoteBranchTree.cs`, `RemoteRepoNode.cs`, `RemoteRepoFolderNode.cs` (inactive remotes) | `src/xplat/GitExtensions.Xplat.Core/Operations/RemotesDialog.cs`, `RemotesViewModel.cs` (MVVM, `MVVM.md`), `Repository/RemoteUrlHistory.cs`, `Repository/GitRepositoryService.cs` (remote list, inactive remotes), `Repository/RepoObjectsTree.cs`; `src/xplat/GitExtensions.Xplat.App/RemotesWindow.axaml(.cs)`, `MainWindow.LeftPanel.cs`, `MainWindow.axaml(.cs)` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormReflog.cs` | `src/xplat/GitExtensions.Xplat.Core/Repository/Reflog.cs` (references, a reference's reflog), `ReflogViewModel.cs` (MVVM, `MVVM.md`); `src/xplat/GitExtensions.Xplat.App/ReflogWindow.axaml(.cs)`; `MainWindow.axaml(.cs)` (Reflog...) | `bd69886b7` | ported |
| `src/app/GitUI/HelperDialogs/FormResetCurrentBranch.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/ResetCurrentBranchDialog.cs`, `ResetCurrentBranchViewModel.cs` (MVVM, `MVVM.md`); `src/xplat/GitExtensions.Xplat.App/ResetCurrentBranchWindow.axaml(.cs)`, `CommitSummaryView.axaml(.cs)`; `MainWindow.axaml(.cs)`, `MainWindow.LeftPanel.cs` (its callers) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormDiff.cs` | `src/xplat/GitExtensions.Xplat.App/RevisionDiffWindow.axaml(.cs)`, `MainWindow.Compare.cs`, `FileStatusListMenu.axaml(.cs)` (the file list's menu), `DiffView.axaml.cs` (`ShowDiffAsync`); `src/xplat/GitExtensions.Xplat.Core/Diff/RevisionDiff.cs`, `RevisionDiffViewModel.cs` (MVVM, `MVVM.md`), `DiffViewModel.cs` | `bd69886b7` | ported |
| `src/app/GitUI/CommandsDialogs/FormLog.cs`; `src/app/GitUI/GitUICommands.cs` (`StartCompareRevisionsDialog`); `src/app/GitUI/UserControls/RevisionGrid/RevisionGridControl.cs` (`ViewSelectedRevisions` with no selection) | `src/xplat/GitExtensions.Xplat.App/LogWindow.axaml(.cs)`, `MainWindow.GridView.cs` (`ViewSelectedRevision`); `src/xplat/GitExtensions.Xplat.Core/CommitHistory/LogViewModel.cs` (MVVM, `MVVM.md`), `GridRevisions.cs` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormCompareToBranch.cs`; `UserControls/BranchSelector.cs` | `src/xplat/GitExtensions.Xplat.App/CompareToBranchWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Core/Operations/CompareToBranchViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/UserControls/RevisionGrid/RevisionGridControl.cs` (`LaunchRebase`, fixup, squash and amend items); `src/app/GitUI/CommandsDialogs/FormCommit.cs` (`CommitKind`, `modifyCommitMessageButton`) | `src/xplat/GitExtensions.Xplat.Core/Operations/RewriteCommit.cs`, `Repository/CommitKinds.cs`, `GitOperations.cs` (`RunRebaseAsync` sequence editor); `src/xplat/GitExtensions.Xplat.App/MainWindow.GridMenu.cs`, `CommitWindow.axaml(.cs)` | `52d08e996` | ported |
| `src/app/GitUI/HelperDialogs/FormResetAnotherBranch.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/ResetAnotherBranchDialog.cs`, `ResetAnotherBranchViewModel.cs` (MVVM, `MVVM.md`); `src/xplat/GitExtensions.Xplat.App/ResetAnotherBranchWindow.axaml(.cs)`, `MainWindow.GridMenu.cs` (`ShowResetAnotherBranchAsync`) | `bd69886b7` | ported |
| `src/app/GitUI/CommandsDialogs/FormRevertCommit.cs`; `MergeConflictHandler.cs` (`HandleMergeConflicts`) | `src/xplat/GitExtensions.Xplat.App/RevertCommitWindow.axaml(.cs)`, `MainWindow.CommandsMenu.cs` (`RevertSelectedRevision`, `OfferToResolveConflictsAsync`); `src/xplat/GitExtensions.Xplat.Core/Operations/RevertCommitDialog.cs`, `RevertCommitViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormRenameBranch.cs`; `src/app/GitUI/GitUICommands.cs` (`StartRenameDialog`) | `src/xplat/GitExtensions.Xplat.App/RenameBranchWindow.axaml(.cs)`, `RenameBranchViewModel.cs` (MVVM, `MVVM.md`), `MainWindow.LeftPanel.cs` (`RenameBranchAsync`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormResolveConflicts.cs` | `src/xplat/GitExtensions.Xplat.App/ConflictsWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Core/Operations/ConflictsDialog.cs`, `ConflictsViewModel.cs` (MVVM, `MVVM.md`), `GitOperations.cs` (`RunMergeToolAsync`), `RepositoryOperationsViewModel.cs` | `bd69886b7` | ported |
| `src/app/GitUI/CommandsDialogs/FormMergeSubmodule.cs`; `FormCheckoutBranch.cs` (`containObjectIds`) | `src/xplat/GitExtensions.Xplat.App/MergeSubmoduleWindow.axaml(.cs)`, `CheckoutBranchFlow.cs` (`containObjectIds`); `src/xplat/GitExtensions.Xplat.Core/Operations/MergeSubmoduleDialog.cs`, `MergeSubmoduleViewModel.cs` (MVVM, `MVVM.md`), `CheckoutBranchDialog.cs` (`ContainingBranches`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/BrowseDialog/FormGitCommandLog.cs` | `src/xplat/GitExtensions.Xplat.App/CommandLogWindow.axaml(.cs)` (reads upstream `CommandLog` and `GitModule.GitCommandCache` as is); `src/xplat/GitExtensions.Xplat.Core/Repository/CommandLogDialog.cs`, `CommandLogViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/HelperDialogs/FormRemoteProcess.cs`, `FormProcess.cs` | `src/xplat/GitExtensions.Xplat.App/ProcessWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Core/Operations/ProcessViewModel.cs` (MVVM, `MVVM.md`), `GitOutputRunner.cs`, `RepositoryOperationsViewModel.cs` (`OutputLines`, `Cancel`), `PuttyFailure.cs`; `src/xplat/GitExtensions.Xplat.App/PuttyPrompts.cs` | `52d08e996` | ported |
| `src/app/GitUI/FormPuttyError.cs`, `FormPuttyError.Designer.cs`; `src/app/GitUI/BrowseForPrivateKey.cs` (`BrowseAndLoad`) | `src/xplat/GitExtensions.Xplat.App/PuttyErrorWindow.axaml(.cs)`, `PuttyPrompts.cs`; `src/xplat/GitExtensions.Xplat.Core/Operations/PuttyErrorViewModel.cs` (MVVM, `MVVM.md`), `PuttyFailure.cs` | `0174ba1cc` | ported |
| `src/app/GitUI/CommandsDialogs/FormSettings.cs` (pages: appearance, commit dialog, git, git config) | `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml(.cs)`, `ThemeApplier.cs`; `src/xplat/GitExtensions.Xplat.Core/Settings/SettingsViewModel.cs`, `SettingsOptions.cs`, `SettingsChoices.cs`, `RevisionLinksPageViewModel.cs`, `DetailedPageViewModel.cs`, `Scripts/ScriptsPageViewModel.cs`, `Plugins/PluginsPageViewModel.cs`, `BuildServer/BuildServerPageViewModel.cs`, `Settings/GitConfigPageViewModel.cs`; `src/xplat/GitExtensions.Xplat.App/PluginSettingRowTemplate.cs` (MVVM, `MVVM.md`), `src/xplat/GitExtensions.Xplat.Core/Settings/AppPreferences.cs`, `GitConfigService.cs` | `52d08e996` | ported |
| `src/app/GitUI/Theming/ThemeModule.cs`, `ThemePathProvider.cs`; `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/AppearanceSettingsPage.cs` (theme choice) | `src/xplat/GitExtensions.Xplat.Core/Settings/AppTheme.cs` (`UpstreamThemeService`, `AppThemePaths`); `src/xplat/GitExtensions.Xplat.App/ThemeApplier.cs`, `ThemeBrushes.cs`, `SettingsWindow.axaml(.cs)` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/ColorsSettings/ColorsSettingsPage.cs` (revision graph group); `src/app/GitUI/UserControls/RevisionGrid/RevisionDataGridView.cs` (`GetForeground`, `GetCommitBodyForeground`) | `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml(.cs)` (Colors page), `RevisionGrid.axaml`, `ThemeApplier.cs`, `MainWindow.GridView.cs`; `src/xplat/GitExtensions.Xplat.Core/CommitHistory/CommitListViewModel.cs` (`DrawNonRelativesTextGray`), `CommitGraph.cs` (`GraphRowRef.IsRelative`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormAbout.cs`, `EnvironmentInfo.cs`, `AboutBoxDialog/FormContributors.cs`; `src/app/GitUI/UserEnvironmentInformation.cs` (`GetInformation`, `GetGitVersionInfo`, `CopyInformation`) | `src/xplat/GitExtensions.Xplat.App/AboutWindow.axaml(.cs)`, `ContributorsWindow.axaml(.cs)`, `ContributorsViewModel.cs` (MVVM, `MVVM.md`), `UpstreamResources.cs`; `src/xplat/GitExtensions.Xplat.Core/Platform/EnvironmentInformation.cs`, `Contributors.cs`, `AboutViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/BrowseDialog/DashboardControl/Dashboard.cs`, `DashboardTheme.cs`, `UserRepositoriesList.cs`, `UserRepositoriesListController.cs`, `FormDashboardCategoryTitle.cs` | `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (`DashboardPanel`, Start > Recent repositories), `MainWindow.Dashboard.cs`, `ThemeApplier.cs` (dashboard colors); `src/xplat/GitExtensions.Xplat.Core/Repository/RecentRepositories.cs`, `RecentRepositoriesViewModel.cs`, `Dashboard.cs` (`DashboardViewModel`, bound by the dashboard's templates, MVVM, `MVVM.md`), `DashboardViewModel.Menus.cs` (the tile and group menus and their commands, `IDashboardWindow`) | `52d08e996` | ported |
| `src/app/GitUI/LeftPanel/RepoObjectsTree.cs`, `RepoObjectsTree.Designer.cs`, `RepoObjectsTree.ContextActions.cs`, `ContextMenu/MenuItemsGenerator.cs`, `MenuItemsStrings.cs`, `LocalBranchTree.cs`, `RemoteBranchTree.cs`, `TagTree.cs`, `StashTree.cs`, `SubmoduleTree.cs`, `WorktreeTree.cs` and their nodes | `src/xplat/GitExtensions.Xplat.Core/Repository/RepoObjectsTree.cs`, `GitRepositoryService.cs` (ahead and behind, worktrees, remote URLs, merged branches); `src/xplat/GitExtensions.Xplat.App/MainWindow.LeftPanel.cs`, `MainWindow.MergedBranches.cs`, `MainWindow.HiddenRefs.cs`, `MainWindow.SubmoduleTree.cs`, `MainWindow.axaml` (`LeftPanel`); core `Repository/RepoObjectSearch.cs`, `Repository/RepoObjectSelection.cs`, `Repository/SubmoduleStructure.cs`, `Repository/RepoObjectMenuViewModel.cs` (the context menu's state, MVVM, `MVVM.md`), `Presentation/MenuItemState.cs` | `afc512395` | ported |
| `src/app/GitUI/Hotkey/HotkeySettingsManager.cs` (FormBrowse and FormCommit defaults) | `src/xplat/GitExtensions.Xplat.App/Hotkeys.cs`, `HotkeyEditor.cs`; `src/xplat/GitExtensions.Xplat.Core/Settings/UpstreamHotkeys.cs`, `HotkeysPageViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/SplitterManager.cs`, `NestedSplitterManager.cs`; `FormBrowse.cs` (`SetSplitterPositions`), `FormCommit.cs` (`RestoreSplitters`), `RevisionDiffControl.cs` (`InitSplitterManager`) | `src/xplat/GitExtensions.Xplat.Core/Settings/Splitters.cs`; `src/xplat/GitExtensions.Xplat.App/SplitterTracker.cs`, `MainWindow.Toolbar.cs` (`TrackSplitters`), `CommitWindow.axaml.cs` | `bd69886b7` | ported |
| `src/app/GitUI/CommandsDialogs/FormGitIgnore.cs`, `FormAddToGitIgnore.cs`, `GitIgnoreDialog/*.cs` | `src/xplat/GitExtensions.Xplat.App/GitIgnoreWindow.axaml(.cs)`, `AddToGitIgnoreWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Core/Repository/GitIgnoreDialog.cs`, `GitIgnoreFile.cs`, `GitIgnoreViewModel.cs`, `AddToGitIgnoreViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/UserControls/RevisionGrid/FilterInfo.cs`, `FormRevisionFilter.cs` | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/RevisionFilter.cs`; `src/xplat/GitExtensions.Xplat.App/FilterWindow.axaml(.cs)`, `src/xplat/GitExtensions.Xplat.Core/CommitHistory/FilterViewModel.cs` (MVVM, `MVVM.md`); `MainWindow.axaml` (branch choice, Filter...) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/WorktreeDialog/FormManageWorktree.cs`, `FormCreateWorktree.cs`; `src/app/GitUI/GitUICommands.cs` (`WorktreeSwitch`, `WorktreeCreate`, `WorktreeDelete`); `FormBrowse.cs` (worktree drop-down) | `src/xplat/GitExtensions.Xplat.App/WorktreesWindow.axaml(.cs)`, `CreateWorktreeWindow.axaml(.cs)` (with core `Operations/CreateWorktreeViewModel.cs`, MVVM, `MVVM.md`), `WorktreeFlow.cs`, `MainWindow.Toolbar.cs` (drop-down); `src/xplat/GitExtensions.Xplat.Core/Operations/ManageWorktreeDialog.cs`, `WorktreesViewModel.cs` (MVVM, `MVVM.md`), `CreateWorktreeDialog.cs`, `Repository/GitRepositoryService.cs` (`GetWorktreesAsync`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormSubmodules.cs` | `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (Submodules section); `src/xplat/GitExtensions.Xplat.Core/Repository/GitRepositoryService.cs` (`GetSubmodules`), `Operations/GitOperations.cs` (update, sync) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormSparseWorkingCopy.cs`, `FormSparseWorkingCopyViewModel.cs`; `FormBrowse.cs` (`menuitemSparseWorkingCopy_Click`) | `src/xplat/GitExtensions.Xplat.App/SparseWorkingCopyWindow.axaml(.cs)`, `MainWindow.CommandsMenu.cs`; `src/xplat/GitExtensions.Xplat.Core/Repository/SparseWorkingCopy.cs`, `SparseWorkingCopyViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormBrowse.cs` (`RepoSettingsToolStripMenuItemClick`); `src/app/GitUI/GitUICommands.cs` (`StartRepoSettingsDialog`) | `src/xplat/GitExtensions.Xplat.App/MainWindow.CommandsMenu.cs`, `MainWindow.axaml` (`RepositorySettingsMenuItem`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/BrowseDialog/FormChangeLog.cs`, `FormDonate.cs`; `Menus/HelpToolStripMenuItem.cs` (Changelog, Donate) | `src/xplat/GitExtensions.Xplat.App/ChangeLogWindow.axaml(.cs)`, `ChangeLogViewModel.cs` (MVVM, `MVVM.md`), `DonateWindow.axaml(.cs)`, `DonateViewModel.cs` (MVVM, `MVVM.md`), `MainWindow.StartMenu.cs`, `GitExtensions.Xplat.App.csproj` (ChangeLog.md linked) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/Menus/ToolsToolStripMenuItem.cs` (PuTTY); `src/app/GitUI/Infrastructure/PuttyHelpers.cs` (`StartPageant`, `StartPuttygen`) | `src/xplat/GitExtensions.Xplat.App/MainWindow.CommandsMenu.cs`, `MainWindow.axaml` (`PuttyMenuItem`) | `52d08e996` | ported |
| `src/app/GitUI/ScriptsEngine/ScriptsManager.cs`, `ScriptsManager.ScriptRunner.cs`, `ScriptOptionsParser.cs`, `ScriptInfo.cs`, `SimplePrompt.cs`, `FormFilePrompt.cs`; `src/app/GitUI/GitModuleForm.cs` (`ExecuteScriptCommand`) | `src/xplat/GitExtensions.Xplat.Core/Scripts/` (`ScriptDefinition`, `ScriptStore.cs` with `ScriptsXml`, `ScriptRunner`, `ScriptVariables`, `RepositoryScriptContext`); `src/xplat/GitExtensions.Xplat.App/ScriptHost.cs`; `MainWindow.axaml(.cs)` (user menu bar, grid Run script, event hooks); `CommitWindow.axaml.cs` (commit events) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/ScriptsSettingsPage.cs` | `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml(.cs)` (Scripts tab); `src/xplat/GitExtensions.Xplat.Core/Scripts/ScriptListEditor.cs`, `ScriptHelp.cs` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/SimpleHelpDisplayDialog.cs` | `src/xplat/GitExtensions.Xplat.App/HelpWindow.axaml(.cs)`, `TextViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/AdvancedSettingsPage.cs`; `FormCheckoutBranch.cs` (default action), `FormCreateBranch.cs`, `FormRenameBranch.cs`, `FormCreateWorktree.cs` (name normalising), `FormCommit.cs` (`PushForced`) | `src/xplat/GitExtensions.Xplat.Core/Repository/BranchNames.cs`, `Settings/AppPreferences.cs`; `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml.cs`, `CommitWindow.axaml.cs`, `WorktreesWindow.axaml.cs`, `SettingsWindow.axaml(.cs)` (Advanced tab) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/GitSettingsPage.cs`, `FormFixHome.cs`, `SshSettingsPage.cs`; `src/app/GitCommands/Settings/AppSettings.cs` (`LoadSettings` sets `GIT_SSH`) | `src/xplat/GitExtensions.Xplat.Core/Settings/HomeSettings.cs`, `SshClients.cs`, `AppPreferences.cs`; `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml(.cs)` (Git and SSH tabs), `App.axaml.cs` (startup) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/SortingSettingsPage.cs`; `src/app/GitUI/LeftPanel/BaseRefTree.cs` (`OrderByPriority`), `LocalBranchTree.cs`, `RemoteBranchTree.cs` (order), `ContextMenu/GitRefsSortByContextMenuItem.cs`, `GitRefsSortOrderContextMenuItem.cs` | `src/xplat/GitExtensions.Xplat.Core/Repository/RefSorting.cs`, `BranchTree.cs`, `GitRepositoryService.cs`; `CommitHistory/RevisionSorting.cs`, `GitCommitHistory.cs`; `Settings/SortingTexts.cs`, `AppPreferences.cs`; `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml(.cs)` (Sorting tab), `MainWindow.axaml(.cs)` (branch tree menu) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/RevisionLinksSettingsPage.cs`; `src/app/GitUI/CommitInfo/CommitInfo.cs` (related links) | `src/xplat/GitExtensions.Xplat.Core/Settings/RevisionLinks.cs`, `UpstreamImages.cs` (stand-in); `CommitHistory/GitCommitHistory.cs` (links in the details); `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml(.cs)` (Revision links tab), `MainWindow.axaml(.cs)` (Related links) | `52d08e996` | ported |
| `src/app/GitUI/CommitInfo/CommitInfo.cs`, `CommitInfoHeader.cs`, `RefsFormatter.cs`; `src/app/GitUI/CommandsDialogs/RevisionGpgInfoControl.cs`; `FormBrowse.InitCommitDetails.cs` (tab images) | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/CommitInfo.cs` (`CommitInfoText`, `CommitInfoReader`, `BranchComparer`), `CommitInfoViewModel.cs` (MVVM, `MVVM.md`), `GitCommitHistory.cs` (commit data and message), `Settings/AppPreferences.cs` (`CommitInfoOptions`, `ShowGpgInformation`); `src/xplat/GitExtensions.Xplat.App/CommitInfoView.axaml(.cs)`, `MainWindow.CommitInfo.cs`, `InfoTextBlock.cs`, `MainWindow.axaml` | `52d08e996` | ported |
| `src/app/GitUI/UserControls/RevisionGrid/RevisionGridMenuCommands.cs` (`CreateNavigateMenuCommands`), `NavigationHistory.cs`, `RevisionGridControl.cs` (`ToggleBetweenArtificialAndHeadCommits`, `GoToFirstParent`, `GoToLastParent`, `NavigateBackward`, `NavigateForward`) | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/NavigationHistory.cs`, `CommitListViewModel.cs` (`IndexOfNextArtificialOrHead`, `IndexOfLastParent`); `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (Navigate menu), `MainWindow.axaml.cs` (`RunGridHotkey`), `Hotkeys.cs` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/BrowseDialog/FormRecentReposSettings.cs`; `Menus/WorkingDirectoryToolStripSplitButton.cs` (`FillDropDown`, the filter, `MouseUpHandler`, `RefreshContent`'s width); `src/app/GitUI/RepositoryHistoryUIService.cs` (`OpenRepo`) | `src/xplat/GitExtensions.Xplat.App/RecentRepositoriesSettingsWindow.axaml(.cs)`, `MainWindow.Toolbar.cs` (`ShowWorkingDirMenu`), `MainWindow.StartMenu.cs` (`RepositoryMenuItem`); `src/xplat/GitExtensions.Xplat.Core/Repository/RecentRepositoriesSettings.cs`, `RecentRepositoriesSettingsViewModel.cs` (MVVM, `MVVM.md`), `RecentRepositories.cs` (`SaveRecentAsync`), `Settings/AppPreferences.cs` (`RecentRepositoryMenu`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/BrowseDialog/FormGoToCommit.cs`; `RevisionGridMenuCommands.cs` (`GotoCommitExecute`); `RevisionGridControl.cs` (`GoToMergeBase`) | `src/xplat/GitExtensions.Xplat.App/GoToCommitWindow.axaml(.cs)`, `MainWindow.Navigate.cs`; `src/xplat/GitExtensions.Xplat.Core/CommitHistory/GoToCommit.cs`, `GoToCommitViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormGitAttributes.cs`, `FormMailMap.cs`; `FormBrowse.cs` (`undoLastCommitToolStripMenuItem_Click`); `Menus/ToolsToolStripMenuItem.cs` (Git GUI, GitK) | `src/xplat/GitExtensions.Xplat.App/RepositoryTextFileWindow.axaml(.cs)`, `MainWindow.CommandsMenu.cs`; `src/xplat/GitExtensions.Xplat.Core/Repository/RepositoryTextFiles.cs`, `RepositoryTextFileViewModel.cs` (MVVM, `MVVM.md`), `Settings/Confirmations.cs` (`UndoLastCommit`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormCleanupRepository.cs` | `src/xplat/GitExtensions.Xplat.App/CleanupRepositoryWindow.axaml(.cs)`, `MainWindow.CommandsMenu.cs`; `src/xplat/GitExtensions.Xplat.Core/Operations/CleanupDialog.cs`, `CleanupRepositoryViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormArchive.cs`; `src/app/GitUI/HelperDialogs/FormChooseCommit.cs` | `src/xplat/GitExtensions.Xplat.App/ArchiveWindow.axaml(.cs)`, `ChooseCommitWindow.axaml(.cs)` (with `Core/CommitHistory/ChooseCommitViewModel.cs`, MVVM, `MVVM.md`), `RevisionGridColumns.cs` (`ShowAsSettingsSay`), `MainWindow.CommandsMenu.cs`; `src/xplat/GitExtensions.Xplat.Core/Operations/ArchiveDialog.cs`, `ArchiveViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormDeleteRemoteBranch.cs`; `LeftPanel/RemoteBranchNode.cs` (`Delete`) | `src/xplat/GitExtensions.Xplat.App/DeleteRemoteBranchWindow.axaml(.cs)` (with core `Operations/DeleteRemoteBranchViewModel.cs`, MVVM, `MVVM.md`), `MainWindow.DeleteRefs.cs`, `MainWindow.axaml.cs` (`DeleteSelectedBranchAsync`); `src/xplat/GitExtensions.Xplat.Core/Operations/DeleteRemoteBranchDialog.cs`, `RepositoryOperationsViewModel.cs` (`DeleteRemoteBranchesAsync`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormFormatPatch.cs`, `FormApplyPatch.cs`, `FormViewPatch.cs`, `FormAddFiles.cs` | `src/xplat/GitExtensions.Xplat.App/FormatPatchWindow.axaml(.cs)`, `FormatPatchViewModel.cs` (MVVM, `MVVM.md`), `ApplyPatchWindow.axaml(.cs)`, `ViewPatchWindow.axaml(.cs)`, `ViewPatchViewModel.cs` (MVVM, `MVVM.md`), `AddFilesWindow.axaml(.cs)`, `AddFilesViewModel.cs` (MVVM, `MVVM.md`), `MainWindow.CommandsMenu.cs`; `src/xplat/GitExtensions.Xplat.Core/Operations/PatchDialogs.cs`, `ApplyPatchDialog.cs`, `ApplyPatchViewModel.cs` (MVVM, `MVVM.md`), `Settings/AppPreferences.cs` (`LastFormatPatchDir`, `ApplyPatchIgnoreWhitespace`, `ApplyPatchSignOff`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormVerify.cs`, `FormVerify.LostObject.cs`, `FormVerify.SortableLostObjectsList.cs`; `src/app/GitUI/HelperDialogs/FormEdit.cs`; `FormBrowse.cs` (Git maintenance menu) | `src/xplat/GitExtensions.Xplat.App/VerifyDatabaseWindow.axaml(.cs)`, `ViewTextWindow.axaml(.cs)`, `MainWindow.Maintenance.cs`, `EditorWindow.axaml(.cs)` (`showWarning`), `ThemeApplier.cs` (`WarningPanelBrush`); `src/xplat/GitExtensions.Xplat.Core/Repository/LostObjects.cs`, `VerifyDatabaseViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormSubmodules.cs`, `SubmodulesDialog/FormAddSubmodule.cs`; `FormBrowse.cs` (`ManageSubmodulesToolStripMenuItemClick`) | `src/xplat/GitExtensions.Xplat.App/SubmodulesWindow.axaml(.cs)`, `AddSubmoduleWindow.axaml(.cs)`, `MainWindow.Submodules.cs`; `src/xplat/GitExtensions.Xplat.Core/Operations/SubmodulesDialog.cs`, `AddSubmoduleViewModel.cs`, `SubmodulesViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/HelperDialogs/FormCommitDiff.cs`; `src/app/GitUI/UserControls/CommitDiff.cs`; `src/app/GitUI/UserControls/RevisionGrid/RevisionGridControl.cs` (`ViewSelectedRevisions`); `src/app/GitUI/CommandsDialogs/FormFileHistory.cs` (`FileChangesDoubleClick`, the Commit tab) | `src/xplat/GitExtensions.Xplat.App/CommitDiffWindow.axaml(.cs)`, `CommitDiffView.axaml(.cs)`, `MainWindow.GridView.cs` (`ViewSelectedRevision`), `FileHistoryWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Core/CommitHistory/CommitDiffViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/AppearanceFontsSettingsPage.cs`; `src/app/GitExtensions.Extensibility/FontParser.cs` (format) | `src/xplat/GitExtensions.Xplat.Core/Settings/FontSetting.cs`, `AppPreferences.cs`; `src/xplat/GitExtensions.Xplat.App/FontApplier.cs`, `SettingsWindow.axaml(.cs)` (Fonts tab), `App.axaml` and the views' font resources | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/FormBrowseRepoSettingsPage.cs`; `src/app/GitUI/UserControls/RevisionGrid/Columns/MessageColumnProvider.cs`, `AuthorNameColumnProvider.cs`, `DateColumnProvider.cs`, `CommitIdColumnProvider.cs` (`TryGetToolTip`) | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/RevisionTooltips.cs`, `GitCommitHistory.cs`; `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml(.cs)` (grid tooltips), `SettingsWindow.axaml(.cs)` (Browse repository window tab) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/BlameViewerSettingsPage.cs`; `src/app/GitUI/UserControls/BlameControl.cs` (`BuildAuthorLine`, gutter); `src/app/GitUI/CommandsDialogs/FormFileHistory.cs` (blame settings menu); `GitModule.Blame` (flags) | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/BlameOptions.cs`, `BlameParser.cs`, `GitCommitHistory.cs`; `src/xplat/GitExtensions.Xplat.App/BlameWindow.axaml(.cs)`, `SettingsWindow.axaml(.cs)` (Blame viewer tab) | `52d08e996` | ported |
| `src/app/GitUI/UserControls/RevisionGrid/Graph/Rendering/GraphRenderer.cs`, `SegmentRenderer.cs`; `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/DetailedSettingsPage.cs` (revision graph group) | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/GraphPainter.cs`, `CommitGraph.cs` (feeds upstream's `RevisionGraph`, linked unchanged), `Settings/UpstreamGraphLaneColor.cs` (stand-in); `src/xplat/GitExtensions.Xplat.App/GraphCell.cs`, `SettingsWindow.axaml(.cs)` (Detailed tab) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/GeneralSettingsPage.cs`, `AppearanceSettingsPage.cs`; `src/app/GitUI/UserControls/RevisionGrid/Columns/DateColumnProvider.cs`; `RevisionGridMenuCommands.cs` (author and relative date toggles) | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/CommitDateStyle.cs`, `GitCommitHistory.cs`; `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml(.cs)` (View menu, startup, commit button); `CloneWindow.axaml.cs`; `SettingsWindow.axaml(.cs)` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/DiffViewerSettingsPage.cs`; `src/app/GitUI/Editor/FileViewer.cs` (whitespace, context lines, entire file, `GetExtraDiffArguments`) | `src/xplat/GitExtensions.Xplat.Core/Diff/DiffOptions.cs`, `Settings/AppPreferences.cs`; `src/xplat/GitExtensions.Xplat.App/DiffView.axaml(.cs)`; `SettingsWindow.axaml(.cs)` (Diff viewer tab) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/CommitDialogSettingsPage.cs` | `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml(.cs)` (Commit dialog tab); `src/xplat/GitExtensions.Xplat.Core/Settings/AppPreferences.cs` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/ConfirmationsSettingsPage.cs`; `src/app/GitUI/MessageBoxes.cs` (`ConfirmSuppressible`, `ConfirmBranchCheckout`) | `src/xplat/GitExtensions.Xplat.Core/Settings/Confirmations.cs`, `AppPreferences.cs` (`Asks`, `SetAsks`); `src/xplat/GitExtensions.Xplat.App/ConfirmWindow.axaml(.cs)` (`AskAsync`); `SettingsWindow.axaml(.cs)` (Confirmations tab) | `52d08e996` | ported |
| `src/app/GitUI/Plugin/PluginRegistry.cs`, `FailedPluginWrapper.cs`; `src/app/GitUI/CommandsDialogs/FormBrowse.cs` (`RegisterPlugins`, `UpdatePluginMenu`, plugin parts of `SetGitModule`) | `src/xplat/GitExtensions.Xplat.Core/Plugins/PluginCatalog.cs`; `src/xplat/GitExtensions.Xplat.App/PluginHost.cs` (`Register`, `Unregister`); `MainWindow.axaml(.cs)` (Plugins menu) | `52d08e996` | ported |
| `src/app/GitUI/GitUICommands.cs` (the `IGitUICommands` plugins see) | `src/xplat/GitExtensions.Xplat.App/PluginHost.cs`, `IPluginFlows.cs`, `MainWindow.PluginFlows.cs`; `src/xplat/GitExtensions.Xplat.Ui/ModalWindow.cs` (`Wait`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Plugins/PluginSettingsPage.cs`; `src/app/GitUI/SettingControlBindings/*.cs` | `src/xplat/GitExtensions.Xplat.Core/Plugins/PluginSettings.cs`; `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml(.cs)` (Plugins tab) | `52d08e996` | ported |
| `src/app/GitUI/UserControls/InteractiveGitActionControl.cs` | `src/xplat/GitExtensions.Xplat.Core/Repository/GitActionBanner.cs`, `GitRepositoryService.cs` (state), `RepositoryViewModel.cs`; `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml(.cs)` (`BisectBanner`, `StateBanner`) | `bd69886b7` | ported |
| `src/app/GitUI/CommandsDialogs/BrowseDialog/FormBisect.cs`; `src/app/GitUI/UserControls/RevisionGrid/RevisionGridControl.cs` (bisect items) | `src/xplat/GitExtensions.Xplat.App/BisectWindow.axaml(.cs)`, `MainWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (bisect),`RepositoryOperationsViewModel.cs`, `BisectViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/plugins/CreateLocalBranches/CreateLocalBranchesForm.cs` | `src/xplat/plugins/CreateLocalBranches/CreateLocalBranchesForm.cs` (stand-in), `CreateLocalBranchesWindow.axaml(.cs)` | `52d08e996` | ported |
| `src/plugins/ProxySwitcher/ProxySwitcherForm.cs` | `src/xplat/plugins/ProxySwitcher/ProxySwitcherForm.cs` (stand-in), `ProxySwitcherWindow.axaml(.cs)` | `bd69886b7` | ported |
| `src/plugins/FindLargeFiles/FindLargeFilesForm.cs` | `src/xplat/plugins/FindLargeFiles/FindLargeFilesForm.cs` (stand-in), `FindLargeFilesWindow.axaml(.cs)`, `LargeFileFinder.cs`, `LargeFileRow.cs`, `FindLargeFilesScript.cs` | `52d08e996` | ported |
| `src/plugins/DeleteUnusedBranches/DeleteUnusedBranchesForm.cs` | `src/xplat/plugins/DeleteUnusedBranches/DeleteUnusedBranchesForm.cs` (stand-in), `DeleteUnusedBranchesWindow.axaml(.cs)`, `BranchRow.cs` | `52d08e996` | ported |
| `src/plugins/ReleaseNotesGenerator/ReleaseNotesGeneratorForm.cs`, `HtmlFragment.cs` (writing to the clipboard) | `src/xplat/plugins/ReleaseNotesGenerator/ReleaseNotesGeneratorForm.cs` (stand-in), `ReleaseNotesGeneratorWindow.axaml(.cs)`, `HtmlClipboard.cs` | `52d08e996` | ported |
| `src/plugins/Gource/GourceStart.cs` | `src/xplat/plugins/Gource/GourceStart.cs` (stand-in), `GourceStartWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Ui/PluginAvatars.cs`; `src/xplat/plugins/Shared/UpstreamMessageBoxes.cs` | `52d08e996` | ported |
| `src/plugins/Statistics/GitImpact/FormImpact.cs`, `ImpactControl.cs` | `src/xplat/plugins/GitImpact/FormImpact.cs` (stand-in), `ImpactWindow.axaml(.cs)`, `ImpactGraph.cs` | `52d08e996` | ported |
| `src/plugins/Statistics/GitStatistics/FormGitStatistics.cs`, `PieChart/*.cs` | `src/xplat/plugins/GitStatistics/FormGitStatistics.cs` (stand-in), `StatisticsWindow.axaml(.cs)`, `PieChart.cs` | `52d08e996` | ported |
| `src/app/GitExtensions.Extensibility/Translations/Xliff/TranslationUtil.cs` (translating a form by its fields); `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/AppearanceSettingsPage.cs` (language) | `src/xplat/GitExtensions.Xplat.Ui/UpstreamTranslation.cs`; the plugin and repository host windows; `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml(.cs)` (Appearance tab) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/FormChooseTranslation.cs`; `src/app/GitExtensions/Program.cs` (asked at startup); `ChecklistSettingsPage.cs` (`translationConfig_Click`) | `src/xplat/GitExtensions.Xplat.App/ChooseTranslationWindow.axaml(.cs)`, `BitmapFileConverter.cs`, `MainWindow.axaml.cs` (`OnOpenedAsync`), `SettingsWindow.Checklist.cs`; `src/xplat/GitExtensions.Xplat.Core/Settings/ChooseTranslationViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SearchWindow.cs`, `SearchControl.cs`; `src/app/GitUI/FindFilePredicateProvider.cs` (linked as is); `FileStatusList.ContextMenu.cs` (`FindFile_Click`); `FormBrowse.cs` (`FindFileInSelectedCommit`) | `src/xplat/GitExtensions.Xplat.App/SearchWindow.axaml(.cs)`, `FileStatusListMenu.axaml(.cs)` (`tsmiFindFile`), `MainWindow.FindFile.cs`, `CommandLineVerbs.cs` (the `searchfile` verb), `RevisionFileList.cs` (`SelectPath`); `src/xplat/GitExtensions.Xplat.Core/Repository/FindFile.cs`, `SearchViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/BuildServerIntegration/BuildServerWatcher.cs` | `src/xplat/GitExtensions.Xplat.Core/BuildServer/BuildServerWatcher.cs`, `BuildServerCatalog.cs`, `BuildServerCredentialStore.cs`; `src/xplat/GitExtensions.Xplat.App/MainWindow.BuildServer.cs` | `52d08e996` | ported |
| `src/app/GitUI/UserControls/RevisionGrid/Columns/BuildStatusColumnProvider.cs`; `RevisionGridControl.cs` (build report items, cell click); `RevisionGridMenuCommands.cs` (build status toggles) | `src/xplat/GitExtensions.Xplat.Core/BuildServer/BuildStatusCell.cs`, `CommitHistory/CommitListViewModel.cs`; `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml`, `MainWindow.BuildServer.cs`, `BuildStatusBrushConverter.cs` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/BuildServerIntegrationSettingsPage.cs` | `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml`, `SettingsWindow.BuildServer.cs` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormSettings.cs`, `FormSettings.Designer.cs` (page tree, title, buttons, instant save notice); `src/app/GitUI/CommandsDialogs/SettingsDialog/SettingsTreeViewUserControl.cs`, `SettingsGroupPages.cs`, `Pages/GitRootIntroductionPage.cs`, `Plugins/PluginRootIntroductionPage.cs` | `src/xplat/GitExtensions.Xplat.Core/Settings/SettingsPageTree.cs`, `SettingsPageTreeViewModel.cs`, `SettingsPageNode.cs` (MVVM, `MVVM.md`); `src/xplat/GitExtensions.Xplat.App/SettingsWindow.Tree.cs`, `SettingsWindow.axaml(.cs)` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/ShellExtensionSettingsPage.cs`; `SettingsDialog/ShellExtension/ShellExtensionManager.cs`; `CheckSettingsLogic.cs` (`SolveGitExtensionsDir`) | `src/xplat/GitExtensions.Xplat.Core/Settings/ShellExtensionPageViewModel.cs` (MVVM, `MVVM.md`); `src/xplat/GitExtensions.Xplat.Core/Platform/WindowsInstallation.cs`; `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml` (`ShellExtensionTab`), `ShellMenuCheckBox.cs`, `AppServices.cs` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/SettingsPageHeader.cs`, `SettingsPageWithHeader.cs`, `DistributedSettingsPage.cs`; `Pages/DetailedSettingsPage.cs` (`gbRevisionGraph` enabled at the global level) | `src/xplat/GitExtensions.Xplat.App/SettingsSourceHeader.cs`, `SettingsWindow.Headers.cs`, `SettingsWindow.BuildServer.cs`, `SettingsWindow.axaml.cs` (revision links and plugin levels) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/GitConfigSettingsPage.cs`, `GitConfigBaseSettingsPage.cs`; `src/app/GitCommands/DiffMergeTools/DiffMergeToolConfigurationManager.cs` (`ConfiguredMergeTool`, `ConfiguredDiffTool`) | `src/xplat/GitExtensions.Xplat.App/SettingsWindow.GitConfig.cs`, `SettingsWindow.axaml` (`GitConfigTab`); `src/xplat/GitExtensions.Xplat.Core/Settings/GitConfigService.cs` (`ConfigScope.Effective`, `ConfigScope.System`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/GitConfigAdvancedSettingsPage.cs` | `src/xplat/GitExtensions.Xplat.Core/Settings/GitConfigAdvancedPageViewModel.cs` (MVVM, `MVVM.md`); `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml` (`GitConfigAdvancedTab`), `SettingsWindow.GitConfig.cs`, `SettingsWindow.Headers.cs`, `SettingsWindow.Tree.cs` | `bd69886b7` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/ChecklistSettingsPage.cs`; `src/app/GitExtUtils/GitUI/Theming/OtherColors.cs` (row colors); `src/app/GitExtensions/Program.cs` and `CheckSettingsLogic.cs` (the startup check) | `src/xplat/GitExtensions.Xplat.Core/Settings/SettingsChecklist.cs`, `ChecklistPageViewModel.cs` (MVVM, `MVVM.md`; with `ChecklistFactsReader`), `StartupSettingsCheck.cs`; `src/xplat/GitExtensions.Xplat.App/SettingsWindow.Checklist.cs`, `SettingsWindow.axaml` (`ChecklistTab`), `DrawingColorBrushConverter.cs`, `MainWindow.axaml.cs` (`OnOpenedAsync`), `App.axaml.cs` | `52d08e996` | ported |
| `src/app/GitUI/HelperDialogs/FormBuildServerCredentials.cs` | `src/xplat/GitExtensions.Xplat.App/BuildServerCredentialsWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Core/BuildServer/BuildServerCredentialsViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/plugins/BuildServerIntegration/AppVeyorIntegration/Settings/AppVeyorSettingsUserControl.cs` | `src/xplat/plugins/BuildServerIntegration/AppVeyorIntegration/Settings/AppVeyorSettingsUserControl.axaml(.cs)` | `52d08e996` | ported |
| `src/plugins/BuildServerIntegration/AzureDevOpsIntegration/Settings/SettingsUserControl.cs` | `src/xplat/plugins/BuildServerIntegration/AzureDevOpsIntegration/Settings/SettingsUserControl.axaml(.cs)` | `52d08e996` | ported |
| `src/plugins/BuildServerIntegration/GitHubActionsIntegration/Settings/GitHubActionsSettingsUserControl.cs` | `src/xplat/plugins/BuildServerIntegration/GitHubActionsIntegration/Settings/GitHubActionsSettingsUserControl.axaml(.cs)` | `52d08e996` | ported |
| `src/plugins/BuildServerIntegration/GitlabIntegration/Settings/GitlabSettingsUserControl.cs` | `src/xplat/plugins/BuildServerIntegration/GitlabIntegration/Settings/GitlabSettingsUserControl.axaml(.cs)` | `52d08e996` | ported |
| `src/plugins/BuildServerIntegration/JenkinsIntegration/Settings/JenkinsSettingsUserControl.cs` | `src/xplat/plugins/BuildServerIntegration/JenkinsIntegration/Settings/JenkinsSettingsUserControl.axaml(.cs)` | `52d08e996` | ported |
| `src/plugins/BuildServerIntegration/TeamCityIntegration/Settings/TeamCitySettingsUserControl.cs`, `TeamCityBuildChooser.cs` | `src/xplat/plugins/BuildServerIntegration/TeamCityIntegration/Settings/TeamCitySettingsUserControl.axaml(.cs)`, `TeamCityBuildChooser.axaml(.cs)` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/RepoHosting/ForkAndCloneForm.cs` | `src/xplat/GitExtensions.Xplat.App/RepositoryHosts/ForkAndCloneWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Core/RepositoryHosts/ForkAndCloneViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/RepoHosting/ViewPullRequestsForm.cs`, `DiscussionHtmlCreator.cs` | `src/xplat/GitExtensions.Xplat.App/RepositoryHosts/ViewPullRequestsWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Core/RepositoryHosts/ViewPullRequestsViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/RepoHosting/CreatePullRequestForm.cs` | `src/xplat/GitExtensions.Xplat.App/RepositoryHosts/CreatePullRequestWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Core/RepositoryHosts/CreatePullRequestViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormBrowse.cs` (repository hosts menu); `src/app/GitUI/GitUICommands.cs` (repository host methods); `src/app/GitUI/UserControls/BlameControl.cs` (`ConfigureRepositoryHostPlugin`) | `src/xplat/GitExtensions.Xplat.App/MainWindow.RepositoryHosts.cs`, `PluginHost.cs`, `RepositoryHosts/RepositoryHostMenus.cs`, `BlameWindow.axaml.cs` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormCommit.cs` (commit templates menu, `AssignCommitMessageFromTemplate`); `src/app/GitCommands/CommitTemplateManager.cs` (used as is) | `src/xplat/GitExtensions.Xplat.Core/Repository/CommitTemplates.cs`, `GitCommitTemplate.cs`; `src/xplat/GitExtensions.Xplat.App/CommitWindow.axaml(.cs)`, `PluginHost.cs` (`AddCommitTemplate`) | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/CommitDialog/FormCommitTemplateSettings.cs` | `src/xplat/GitExtensions.Xplat.App/CommitTemplateSettingsWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Core/Settings/CommitTemplateSettingsViewModel.cs` (MVVM, `MVVM.md`), `Repository/CommitTemplates.cs`, `CommitMessageValidation.cs` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/FormAvailableEncodings.cs` | `src/xplat/GitExtensions.Xplat.App/AvailableEncodingsWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Core/Settings/DiffMergeToolSettings.cs`, `AvailableEncodingsViewModel.cs` (MVVM, `MVVM.md`) | `0174ba1cc` | ported |
| `src/app/GitUI/CommandsDialogs/FormCommit.cs` ("Commit message" menu, reset buttons, `IsCommitMessageValid`, `FormatAllText`, conventional commits) | `src/xplat/GitExtensions.Xplat.Core/Repository/PreviousCommitMessages.cs`, `CommitMessageValidation.cs`, `ConventionalCommits.cs`; `src/xplat/GitExtensions.Xplat.App/CommitWindow.axaml(.cs)`, `ResetChangesWindow.axaml(.cs)`, `ResetChangesViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/SpellChecker/EditNetSpell.cs` (auto-completion, spelling), `SpellCheckEditControl.cs`; `src/app/GitUI/AutoCompletion/*.cs`, `SpellChecker/WordAtCursorExtractor.cs`, `SpellCheckerHelper.cs` (linked as is); `externals/NetSpell.SpellChecker` (compiled unchanged) | `src/xplat/GitExtensions.Xplat.Core/Repository/CommitMessageAutoCompletion.cs`, `Spelling/CommitSpellChecker.cs`, `Spelling/SpellingDictionarySetting.cs`; `src/xplat/NetSpell.SpellChecker/`; `src/xplat/GitExtensions.Xplat.App/CommitWindow.AutoComplete.cs`, `CommitWindow.Spelling.cs`, `SpellingUnderlines.cs` | `52d08e996` | ported |
| `src/app/GitUI/Avatars/*.cs` (`AvatarService` and its providers; `TemplateFormatter.cs`, `HexString.cs` linked as is); `src/app/GitUI/UserControls/AvatarControl.cs` | `src/xplat/GitExtensions.Xplat.Core/Avatars/AvatarService.cs`, `AuthorInitials.cs`; `src/xplat/GitExtensions.Xplat.App/AvatarView.cs`, `SettingsWindow.Appearance.cs` | `52d08e996` | ported |
| `src/app/GitUI/UserControls/PathFormatter.cs` | `src/xplat/GitExtensions.Xplat.Core/Repository/FilePathDisplay.cs`; `src/xplat/GitExtensions.Xplat.App/FilePathTrimming.cs` | `52d08e996` | ported |
| `src/app/GitUI/RepositoryCurrentBranchNameProvider.cs`; `DashboardControl/UserRepositoriesListController.cs` (`GetCurrentBranchName`) | `src/xplat/GitExtensions.Xplat.Core/Repository/RecentRepositories.cs` (`RecentRepositoryBranches`), `RecentRepositoriesViewModel.cs` | `52d08e996` | ported |
| `src/app/GitUI/UserControls/RevisionGrid/QuickSearchProvider.cs`; `src/app/GitCommands/Git/GitRevisionTester.cs` (`Matches`) | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/QuickSearch.cs`; `src/xplat/GitExtensions.Xplat.App/MainWindow.QuickSearch.cs` | `52d08e996` | ported |
| `src/app/GitUI/UserControls/RevisionGrid/Graph/LaneInfoProvider.cs`; `Graph/BranchFinder.cs` (linked as is) | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/LaneInfoProvider.cs`, `CommitGraph.cs`; `src/xplat/GitExtensions.Xplat.App/GraphCell.cs` | `52d08e996` | ported |
| `src/app/GitUI/UserControls/RevisionGrid/ArtificialCommitChangeCount.cs`; `RevisionGridControl.cs` (artificial rows) | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/ArtificialCommits.cs`, `GitCommitHistory.cs`, `CommitListViewModel.cs` (`ArtificialChanges`); `src/xplat/GitExtensions.Xplat.App/MainWindow.GraphStyles.cs`, `RevisionGrid.axaml` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/BuildReportTabPageExtension.cs` | `src/xplat/GitExtensions.Xplat.App/MainWindow.BuildServer.cs`, `MainWindow.axaml` (`BuildReportTab`) | `bd69886b7` | ported |
| `src/app/GitUI/CommandsDialogs/FormBrowse.Designer.cs` (menus), `FormBrowse.cs` (menu visibility on the dashboard); `Menus/StartToolStripMenuItem.cs`, `ToolsToolStripMenuItem.cs`, `HelpToolStripMenuItem.cs`; `src/app/GitUI/RepositoryHistoryUIService.cs` (`PopulateRecentRepositoriesMenu`, `PopulateFavouriteRepositoriesMenu`) | `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (menus), `MainWindow.StartMenu.cs`, `MainWindow.axaml.cs`; `src/xplat/GitExtensions.Xplat.Core/Repository/RepositoryMenus.cs` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormBrowse.Designer.cs` (`ToolStripMain`), `FormBrowse.cs` (toolstrip parts of `InitMenusAndToolbars`, `RefreshDefaultPullAction`, the layout toggles, the title); `Menus/WorkingDirectoryToolStripSplitButton.cs`; `ToolStripPushButton.cs`; `src/app/GitUI/UserControls/RepoStateVisualiser.cs` | `src/xplat/GitExtensions.Xplat.App/MainWindow.Toolbar.cs`, `MainWindow.axaml` (toolstrip), `UpstreamImages.cs`; `src/xplat/GitExtensions.Xplat.Core/Repository/RepoState.cs`, `RecentRepositories.cs` (`Caption`), `BrowseWindowTitle.cs`, `BrowseToolbarViewModel.cs` (MVVM, `MVVM.md`) | `52d08e996` | ported |
| `src/app/GitUI/UserControls/FilterToolBar.cs`, `FilterToolBar.Designer.cs`; `src/app/GitUI/UserControls/RevisionGrid/FilterInfo.cs` (stored options) | `src/xplat/GitExtensions.Xplat.App/FilterToolBar.axaml(.cs)`, `MainWindow.FilterToolBar.cs`, `FileHistoryWindow.Filter.cs`; `src/xplat/GitExtensions.Xplat.Core/CommitHistory/FilterToolBarViewModel.cs` (MVVM, `MVVM.md`), `IFilterToolBarGrid.cs`, `RevisionFilter.cs`, `StoredRevisionFilter.cs` | `afc512395` | ported |
| `src/app/GitUI/Shells/ShellProvider.cs`, `BashShell.cs`, `CmdShell.cs`, `PwshShell.cs`, `PowerShellShell.cs`; `FormBrowse.InitMenusAndToolbars.cs` (`FillUserShells`), `FormBrowse.cs` (`userShell_Click`) | `src/xplat/GitExtensions.Xplat.Core/Platform/TerminalChoices.cs`; `src/xplat/GitExtensions.Xplat.App/MainWindow.Toolbar.cs` (`FillTerminals`) | `afc512395` | ported |
| `src/app/GitUI/ConsoleEmulation/` (ConEmu and Mintty emulators, `ConsoleEmulatorsRegistry.cs`); `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/ConsoleStyleSettingsPage.cs`; `FormBrowse.cs` (the Console tab); `FormBrowseRepoSettingsPage.cs` ("Show the Console tab", "Default shell") | none: the shell has no Console tab | `0174ba1cc` | windows-only |
| `src/app/GitUI/UserControls/RevisionGrid/Columns/MessageColumnProvider.cs`, `RevisionGridRefRenderer.cs`, `RefLabelShape.cs`, `RefLabelIcon.cs`, `RevisionDataGridView.cs` (row colors), `AuthorRevisionHighlighting.cs`, `RevisionGridMenuCommands.cs` (`GetViewMenuCommands`), `SuperProjectInfo.cs` and `RevisionGridControl.cs` (`GetSuperprojectCheckoutAsync`), `GitModule.cs` (`GetSubmoduleItemsForEachRefAsync`) | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/GridLabels.cs`, `SuperprojectLabels.cs`, `CommitListViewModel.cs` (`CommitListItem`, `ArtificialChanges`), `GitCommitHistory.cs` (`LoadRefLabels`), `GridViewMenuViewModel.cs` (the View menu, MVVM, `MVVM.md`); `src/xplat/GitExtensions.Xplat.App/RefLabelView.cs`, `MainWindow.GridView.cs`, `MainWindow.Superproject.cs`, `MainWindow.axaml` (row template, View menu), `ThemeApplier.cs` (grid colors) | `afc512395` | ported |
| `src/app/GitUI/Models/OutputHistoryModel.cs`; `src/app/GitUI/UserControls/OutputHistoryControl.cs`, `OutputHistoryControllerBase.cs`, `OutputHistoryTabController.cs`, `OutputHistoryPanelController.cs`; `src/app/GitUI/HelperDialogs/FormStatus.cs` (`Done`, the recording) | `src/xplat/GitExtensions.Xplat.Core/Operations/OutputHistory.cs`, `RepositoryOperationsViewModel.cs` (`History`); `src/xplat/GitExtensions.Xplat.App/MainWindow.OutputHistory.cs`, `MainWindow.axaml` (tab and panel), `AppServices.cs` (`OutputHistory`), `SettingsWindow.axaml(.cs)` (Browse repository window page) | `0174ba1cc` | ported |
| `src/app/GitUI/NBugReports/BugReportInvoker.cs`, `UIReporter.cs`, `ExceptionExtensions.cs`, `OperationInfo.cs`; `src/app/GitExtensions/Program.cs` (`Application.ThreadException`, `AppDomain.UnhandledException`) | `src/xplat/GitExtensions.Xplat.App/ErrorReporter.cs`, `Program.cs` (`ErrorReporter.Install`), `MainWindow.axaml.cs` (`ShowError`: dubious ownership); `src/xplat/GitExtensions.Xplat.Core/Diagnostics/ErrorReports.cs` | `0174ba1cc` | ported |
| `src/app/BugReporter/BugReportForm.cs`, `ExceptionDetails.cs`, `ExceptionDetailView.cs`; `ErrorReportUrlBuilder.cs`, `GitHubUrlBuilder.cs`, `Serialization/*.cs`, `Info/GeneralInfo.cs` (linked as is) | `src/xplat/GitExtensions.Xplat.App/BugReportWindow.axaml(.cs)`, `ExceptionDetailWindow.axaml(.cs)`, `ExceptionDetailViewModel.cs` (MVVM, `MVVM.md`); `src/xplat/GitExtensions.Xplat.Core/Diagnostics/ExceptionDetails.cs`, `BugReportViewModel.cs` (MVVM, `MVVM.md`) | `0174ba1cc` | ported |
| `src/app/GitUI/GitUICommands.cs` (`RunCommand`, `RunCommandBasedOnArgument`, `InitializeArguments`, the `Run...Command` methods); `src/app/GitExtensions/Program.cs` (`GetWorkingDir`, the exit code); `src/app/GitUI/CommandsDialogs/FormCommandlineHelp.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/CommandLine.cs` (the verbs, their checks, the options, the working directory, the usage text), `CloneArguments.cs`; `src/xplat/GitExtensions.Xplat.Core/Operations/GitEditorUninstall.cs`, `CommandLineHelpViewModel.cs` (FormCommandlineHelp, MVVM); `src/xplat/GitExtensions.Xplat.App/CommandLineVerbs.cs` (each verb's window on its own), `CommandLineHelpWindow.axaml(.cs)`, `MainWindow.CommandLine.cs` (the verbs run by an unseen browse window), `Program.cs`, `App.axaml.cs` | `0174ba1cc` | ported |
| `src/app/GitUI/CommandsDialogs/FormInit.cs`, `FormInit.Designer.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/InitRepositoryViewModel.cs` (MVVM, `MVVM.md`); `src/xplat/GitExtensions.Xplat.App/InitRepositoryWindow.axaml(.cs)`, `MainWindow.CommandLine.cs` (`InitAsync`), `MainWindow.axaml.cs` (Start menu) | `0174ba1cc` | ported |
| `src/app/GitUI/CommandsDialogs/BrowseDialog/FormOpenDirectory.cs`, `FormOpenDirectory.Designer.cs` | `src/xplat/GitExtensions.Xplat.Core/Repository/OpenDirectoryViewModel.cs` (MVVM, `MVVM.md`); `src/xplat/GitExtensions.Xplat.App/OpenDirectoryWindow.axaml(.cs)`, `MainWindow.axaml.cs` (`PickAndOpenRepositoryAsync`) | `0174ba1cc` | ported |
| `src/app/GitUI/UserControls/RevisionGrid/EmptyRepoControl.cs`, `EmptyRepoControl.Designer.cs`; `RevisionGridControl.cs` (`OnRevisionReadCompleted`) | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/CommitListViewModel.cs` (`IsEmptyRepository`), `src/xplat/GitExtensions.Xplat.Core/Repository/BrowseToolbarViewModel.cs`; `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (`EmptyRepoPanel`) | `0174ba1cc` | ported |
| `src/app/GitUI/HelperDialogs/FormStatus.cs` (`ShowPassword`, `PasswordInput_PasswordEntered`), `FormProcess.cs` (`DataReceived`); `src/app/GitUI/UserControls/PasswordInput.cs`; `src/app/GitUI/ConsoleEmulation/PlainText/PlainTextConsoleCommandRunner.cs` (`WriteCommandProcessInput`) | `src/xplat/GitExtensions.Xplat.Core/Operations/ProcessViewModel.cs` (MVVM, `MVVM.md`), `GitOutputRunner.cs` (`IGitInputTarget`, prompts), `RepositoryOperationsViewModel.cs` (`SendInput`, `InputRequested`); `src/xplat/GitExtensions.Xplat.App/ProcessWindow.axaml(.cs)` | `0174ba1cc` | ported |

## Notes per row

What each port covers where the table cannot say it: where it deliberately differs from upstream, what is left out and
why, and the fork code that carries the behavior. Trimmed on 2026-11-24 to the current state; the dated history of each
row (when parts were added, audits, timings) is in git history.

**FormBrowse** (`ported`). Upstream's browse window: the menus with upstream's default hotkeys, the toolbar, the left
panel, the grid, the Commit, Diff, File tree, GPG, Build Report and Output tabs, the status bar, the action bars, and the
dashboard while no repository is open. Left out: the Console tab (ConEmu, `windows-only`, see ConsoleEmulation).
Differences: window size, position and maximized state are kept in upstream's `WindowPositions.xml` format under the
shell's own window names (`WindowPlacementTracker`); the status is read with `--no-optional-locks`, as upstream's
`GitStatusMonitor`, so a refresh never holds git's index lock; opening a repository reads its state while the commits
load and takes the submodule list from `.gitmodules` (upstream's `HasSubmodules`) instead of `git submodule status`; the
left panel is built once both reads are in (`OpenRepositoryAsync`). Not ported: upstream's `IndexWatcher` (and its WSL
check on activation), as the shell watches no files; a newer read of the commits supersedes an older one by a version
counter, as upstream's grid now cancels a running refresh.

**RevisionGridControl** (`ported`). The grid with upstream's columns, ref labels, lanes, quick search, working directory
and index rows, multi-selection (core `GridSelection`; the latest selected is upstream's `LatestSelectedRevision`), the
selection kept across reads as upstream's `GetToBeSelectedRevisions` and `SetToBeSelectedFromParents`, "Load more" by
pages up to `maxrevisiongraphcommits`, stash rows (core `StashRows`) and the autostash label. A path filter is read as
upstream's `BuildPathFilter`, and one file's renames are followed as `FollowRenamesInFileHistory` says
(`CommitListViewModel.FilePaths`, upstream's `FilePathByObjectId`). Differences: the bisect good and bad marks are green
and red labels where upstream draws icons; the author's avatar is at the start of the author cell where upstream has a
column of its own; only the browse window's grid shows stash rows (upstream's file history and choose commit grids would
too). The rows' template is shared by every grid (`RevisionGrid.axaml`). The menu's Advanced submenu has upstream's "Edit
commit by creating a fixup commit and squash-rebase" (the commit window's `CommitCreated`, then the autosquash rebase); a
right-clicked remote branch label offers, to merge, the local branch tracking it (core `GridMenus.OnlyOrTracking`).

**FormStash** (`ported`). `StashWindow` (core `StashDialog`): upstream's form, with the file tree, grouping, filter and
upstream FileStatusList's item menu as FormStash binds it (core `DiffMenuRevisions`). Differences: the "Commit index" and
"Working directory" headers are not collapsible nodes, so the collapse button collapses only the extension and status
groups; the loading animation is an indeterminate progress bar where upstream has its `LoadingControl`.

**FormCreateTag** (`ported`). `CreateTagWindow` (core `CreateTagDialog`). The message goes through a TAGMESSAGE file as
upstream's `GitTagController`; the push is upstream's `Commands.PushTag` between the push scripts. Upstream's Force check
box is never added to its form, so it is not here either.

**FormDeleteTag** (`ported`). `DeleteTagWindow`: the local tag, the tag on a chosen remote
(`push <remote> :refs/tags/<tag>`), or both (see the "Delete branch and Delete tag windows" note).

**FormCherryPick** (`ported`). `CherryPickWindow` (core `CherryPickDialog`), with upstream's FormChooseCommit, a merge's
parents, `-x` and `--no-commit`; conflicts ask upstream's MergeConflictHandler question. Several selected commits get one
window each from the bottom of the grid until one is aborted (upstream's `StartCherryPickDialog(revisions)`), the options
carried over through their saved settings as upstream's `CopyOptions`.

**FormResetChanges** (`ported`). `ResetChangesWindow` with upstream's texts and its rule for "Also delete new files and/or
directories" (`git clean -d`), opened wherever upstream starts the form.

**FormRebase** (`ported`). `RebaseWindow` (core `RebaseDialog`, `RebasePatches`): upstream's form with its options, the
specific range and From picker, the PatchGrid rows, Add files, the steps while a rebase is stopped, and Continue as
upstream's `ResolvedClick` (a reused rerere resolution goes on by itself). Differences: an interactive rebase sets
`GIT_SEQUENCE_EDITOR` and `GIT_EDITOR` for that one git call (`GitEditorCommand`) instead of upstream's global
`core.editor`, so the user's git config is not changed; the commit and conflicts windows are not modal, so the state is
read again when the rebase window is activated. Skipped: upstream's "Current branch a is up to date" message (it compares
with a fixed text that only a branch named "a" matches), and the rebase state of other tools (only git's own folders are
read).

**FormEditor** (`ported`). `EditorWindow` for the `fileeditor` verb and Edit .git/config (with upstream's warning panel):
AvaloniaEdit with line numbers and upstream's `SetHighlightingForFile` (core `Editing/EditorHighlighting.cs`,
`EditorColorizer`: the rebase todo, a commit message's summary and comments with `core.commentchar`, the commit message
marks with upstream's tooltips, other files in the syntax of their extension). The exit code is upstream's (0 saved or
unchanged, -1 discarded, so git aborts the rebase); the file keeps its line ending and byte order mark, and is read and
written in the repository's `FilesEncoding` unless a byte order mark says otherwise (`EditorFile`). Difference: upstream's
search for the description after the blank line tests `descriptionStartLineNumber != -1`, which never holds there, so
upstream marks long description lines only when the summary has no blank line after it; here the description starts at
its first line after the blank line, as the code means.

**FormRevertCommit** (`ported`). `RevertCommitWindow` (core `RevertCommitDialog`), from the grid and the file history (as
upstream's `StartRevertCommitDialog`): the commit's summary and a merge's parents, "Automatically create a commit" (off,
as upstream's), upstream's `Commands.Revert`, a prepared commit message kept above git's revert message
(`CommitMessageManager`), and upstream's resolve conflicts question (`OfferToResolveConflictsAsync`, shared with cherry
pick, with `offerCommit`).

**FormResetAnotherBranch** (`ported`). `ResetAnotherBranchWindow` (core `ResetAnotherBranchDialog`): upstream's branch
list and proposal, "Checkout branch after reset" (`CheckoutOtherBranchAfterReset`) and Force; OK only for a forced move or
a fast-forward (`git merge-base --is-ancestor`), then upstream's `Commands.UpdateRef`.

**FormLog** (`ported`). `LogWindow`, opened by a double-click on the grid with nothing selected (upstream's
`StartCompareRevisionsDialog`), with upstream's layout: the grid from the stored filter, the selected commits' files in
upstream's diff groups (`FileStatusList.SetDiffs`, core `LogViewModel`, `DiffGroups.Calculate`) and the diff. The file
list has no item menu, as upstream's FormLog binds none.

**FormDiff** (`ported`). `RevisionDiffWindow` (core `Diff/RevisionDiff.cs`) for the grid's Compare submenu: BASE and
Compare with their pickers (the choose commit window lists the artificial commits as the grid's setting says), swap,
"Compare to merge base", the directory diff tool, and the files (upstream's `GetDiffFilesWithUntracked`) with upstream
FileStatusList's toolbar and item menu as FormDiff has it (no "Show in File tree" or "Filter file in grid", Blame opening
the file history window on its Blame tab, revisions by short hash). Not modal, as upstream's `ShowFormDiff`. Differences:
after a change from the menu (reset, delete, ignore) the list is read again, where upstream's FormDiff binds no refresh;
user scripts run with the compared revision and the selected files, where upstream's FormDiff gives them no options. The
`compare first second` command line verb opens it modal (`MainWindow.CommandLine.cs`, core `CommandLine.Compare`).

**FormCompareToBranch** (`ported`). `CompareToBranchWindow` with upstream's `BranchSelector` (remote branches first, the
commit count of `GetCommitCountString`); Compare without a branch keeps the focus on the list.

**FormRenameBranch** (`ported`). `RenameBranchWindow`: the name normalised as upstream's `BranchNameTextBox_Leave`, the
same name closing without a rename, upstream's `Commands.RenameBranch` with its output; a failed rename keeps the window
open.

**FormRemotes** (`ported`). `RemotesWindow` (core `RemotesDialog` over upstream's `ConfigFileRemoteSettingsManager`):
active and inactive remotes, New, Delete, Activate and Deactivate, the details (URL, push URL, name proposed from the URL,
prefix, color, the PuTTY key with Load SSH key and Test connection, core `Platform/Putty.cs`), upstream's remote URL
history and generated proposals, the save and fetch, and the default pull behavior per branch. The grid's labels take the
remotes' colors (`GitModule.GetRemoteColors`). Difference: WinForms' color dialog is the basic colors and an HTML box.

**FormReflog** (`ported`). `ReflogWindow` (core `Reflog`): upstream's form, `git reflog --no-abbrev` with every entry, the
references, the menu on the row under the mouse (copy, create branch with upstream's fixed options, reset current branch
after upstream's question), sorting by a column as upstream's `SortableRefLineList`.

**FormResetCurrentBranch** (`ported`). `ResetCurrentBranchWindow` (core `ResetCurrentBranchDialog`): the five reset types
in upstream's colors (dimmed in dark mode), the question before a hard reset, the submodule update after a hard reset
that moved HEAD or when `UpdateSubmodulesOnCheckout` is on, and F1 and "?" for git's documentation of the chosen type.
Differences: the reset runs quietly without the process window, and the submodules are updated once where upstream may
run its update twice.

**BugReportInvoker, UIReporter** (`ported`). `ErrorReporter.Install` (from `Program.Main`, after Avalonia's setup, so
headless tests still fail on an exception) adds upstream's two handlers: the UI thread's (marked handled) and the
process's (its thread waits while the UI thread shows the report, as upstream's separate BugReporter process outlives the
app). Upstream's rules are in core `ErrorReports`: the error log, a file that failed to load, no report for a
cancellation, the dubious ownership report (also when a repository fails to open, then opened again once trusted, as
upstream's `ShowGitRepo`), the bug report for a terminating exception, and the task dialog with the failed command's
details. `GitOperationException` carries upstream's `ExternalOperationException` details from the shell's git runners,
and `ErrorWindow` shows them. Differences: one report at a time (exceptions raised while one is open are traced, so an
error on every render does not stack windows); the expanders' texts are always shown under the text; "Open repository in
Explorer" shows the dialog again after opening the folder, where upstream's button keeps it open. Left out: upstream's
skip of a WinForms accessibility exception, which the shell cannot raise; the command details of failures raised by
upstream's own module methods or as plain messages, which carry no command. A terminating exception on the UI thread
itself is only traced (`PLAN.md` section 10).

**BugReportForm** (`ported`). `BugReportWindow`: the General and Exception tabs (`ExceptionDetailWindow` on a double
click), Copy, Ignore, Send and Quit (the GitHub issue of upstream's `GitHubUrlBuilder`), Quit; a report that cannot be
ignored does not close from the title bar. Upstream's non-UI files are linked into the core as they are. Differences:
upstream's tree puts every deeper inner exception under the root's first child (`FillInnerExceptionTree` passes
`Nodes[0]`); here each is under the exception it is inside. The images are GitUI's, not BugReporter's own copies.
Upstream removes its "Report Contents" tab at run time, so it is not here.

**Dialog keys and minimum sizes**. `DialogKeysParityTests` reads each row that names an upstream form and a window, and
checks the form's `AcceptButton`, `CancelButton` and `MinimumSize` against the window.

**Accessible names**. WinForms announces a toolstrip item by its text even when it shows only its image; the shell's
tool buttons name themselves by their text, else their tooltip, below a name set in markup, and upstream's five
`AccessibleName`s are set on the settings window. Every `ListBoxItem` and `TreeViewItem` is named by what it shows
(`AccessibleNames`, a style in `App.axaml`), as WinForms reads a row by its text. Left unnamed, as upstream: the image
buttons that clear the file filter and the git-grep box, and the lists themselves. Not named: controls drawn without
items (the graph cell, the diff's lines, the blame gutter). Tests: `ToolButtonNamesTests`, `AccessibleNamesTests`. Not
checked with a real screen reader (`MACOS.md` section 10, `PLAN.md` section 12.2).

**FormCommit** (`ported`). `CommitWindow` (core `CommitViewModel`), not modal: upstream's layout, both lists as upstream's
FileStatusList with the file tree, toolbar, filter and the shared item menu as FormCommit binds it
(`BindContextMenu(RescanChanges, canAutoRefresh, stage, unstage)`, `CommitWindow.BindFileMenu`), the bottom panel and
options, the status strip, the commit kinds (core `Repository/CommitKinds.cs`), "Stage in Superproject", the progress bar,
the selection filter (core `SelectionFilterViewModel`), "Add selection to commit message", and the next file selected
after a stage or unstage as upstream's `StoreNextItemToSelect` (core `FileStatusList.NextToSelect`). The draft is upstream's
`CommitMessageManager` as is (`.git/COMMITMESSAGE`, or `.git/MERGE_MSG` during a merge), so both apps share it.
Differences:
- An assumed-unchanged or skip-worktree file without a change shows the modified image, where upstream has its unknown
  status image.
- "Generate a list of changes in submodules" leaves out a submodule added or removed in the commit, where upstream throws.
- The GPG key box shows for "Sign with specific GPG"; upstream shows it for "Sign with default GPG", which looks like a
  slip.
- The button column grows past upstream's 171 pixels when the font needs it.
- After a double-click stages the last unstaged file, upstream moves the focus to the message; the shell does not.
- The window is not modal, so an open window takes a special commit (fixup, squash, amend), its typed message replaced.
- Staging uses `git add` where upstream feeds `update-index` through standard input, so long command lines are split for
  both staging and unstaging (`GitOperations.RunForFilesAsync`); upstream splits only when unstaging.
- Staging and unstaging read only the changed files afterwards (`RepositoryChangedEventArgs.OnlyFileStatus`), unless a
  submodule is among the changes.
- Choosing an earlier selection filter from its list applies after the typing pause, where upstream applies it at once.
- Without "Compose commit messages in Commit dialog", git asks for the message in the editor (`GitEditorCommand`).
Not shown, as upstream's FormCommit binds no file tree, grid or viewer for them: show in file tree, filter file in grid,
cherry pick changes, the git-grep items.

**FormPush** (`ported`). `PushWindow` (core `PushDialog`): push to a remote or a URL (with upstream's URL history), the
three tabs (branches with upstream's options, tags, multiple branches with the remote's heads from `ls-remote` as
upstream's `GetRemoteBranchesDirectlyFromRemote`), Pull over it, upstream's checks before pushing, the Azure DevOps pull
request link, and Pageant given the remote's PuTTY key before every pull, fetch and push (upstream's `LoadPuttyKey` and
`StartPageant`; core `Putty.LoadKeys`). The toolbar's Push stays upstream's quick push. A rejected push is handled as
upstream's `HandlePushOnExit` (`PushRejection`, `PushRejectedFlow`), with upstream's task dialog, for the dialog, the quick
push, Commit & push and a multi-branch push (`--force-with-lease` inserted for the retry).

**FormPull** (`ported`). `PullWindow` (core `PullDialog`) as a GitExtensionsDialog: the help picture, "Pull from" (remote,
"[ All ]", a URL or folder with upstream's URL history), the branch, the merge, tag, history and prune options, Solve
conflicts, Stash changes, Auto stash; upstream's rules and checks (the detached HEAD task dialog with "Checkout branch",
the rebase of a merge commit, a missing remote branch), `FetchCmd` and `PullCmd`, the submodule initialization and update
after a pull, and the prune offer when a pull is rejected for a removed branch. Auto stash is upstream's own (`PullChanges`,
`CalculateStashedValue`, `PopStash`; core `PullAutoStash`, App `PullAutoStashFlow`) for the dialog and the quick pulls.
Difference: the dialog's prune does not ask first; upstream asks only for the quick "Fetch and prune all".

**FormClone** (`ported`). `CloneWindow` (core `CloneDialog`): upstream's form and `OnRuntimeLoad` start (a given URL, else
the clipboard's, else the open repository's remote), the remote history, the destination and subdirectory, the branch list
from `GetRemoteServerRefs` with upstream's PuTTY prompts (core `PuttyFailure`), the options, `Commands.Clone`, the PuTTY key
set on origin and given to Pageant, and upstream's "open the new repository now?" question. `StartCloneDialog` for plugins
(`PluginHost.NewCloneWindow`) and the command line (`CloneArguments`, the `clone` verb and `github-*://openRepo/`).
Differences: the location line's labels have no access key mark; from the command line the window opens over the
dashboard, where upstream shows it alone. No protocol is registered with the OS, so a browser link does not reach it;
upstream's installer does that on Windows.

**FormCreateBranch** (`ported`). `CreateBranchWindow` (core `CreateBranchDialog`): the name (proposed as upstream's and
normalised with `AutoNormaliseBranchName`), the commit picker (`CommitPickerView`, upstream's `CommitPickerSmallControl`
with its button) and summary (`CommitSummaryView`), "Checkout after create", the orphan group (only an orphan without
commits, as `ConfigureForOrphanBranch`), upstream's checks and `Commands.Branch` or `Commands.CreateOrphan`, and the
submodule update. Opened as upstream's `StartCreateBranchDialog` from every place upstream opens it.

**FormCheckoutBranch** (`ported`). `CheckoutBranchWindow` (core `CheckoutBranchDialog`, `CheckoutBranchFlow`): local or
remote branches, the reset, local or custom name choices, detached checkout, the local changes group with "Set as
default", and upstream's `DoDefaultActionOrShow` and `PerformCheckout` flow (the stash and its pop question, the scripts
and plugin events, the submodule update). The branches containing given commits come from the merge submodule window.
A tag or a commit is checked out with the checkout revision window.

**FormCheckoutRevision** (`ported`). `CheckoutRevisionWindow` (core `CheckoutRevisionDialog`): the commit picker, Force,
upstream's `Commands.Checkout` between the checkout scripts and plugin events, and the submodule update; opened as
upstream's `StartCheckoutRevisionDialog`.

**FormDeleteBranch** (`ported`). See the "Delete branch and Delete tag windows" and "FormDeleteRemoteBranch" notes.

**FileViewer** (`ported`). `DiffView` over AvaloniaEdit (`DiffTextView`), read only, in every window with a viewer:
upstream's toolbar (`fileviewerToolbar`, shown while the pointer is over the viewer) and menu; old and new line numbers;
syntax highlighting from upstream's `.xshd` definitions with `AdaptForeColor`; the line actions (stage, unstage, reset,
apply and undo a commit's lines with `git apply --3way --index`, through upstream's `PatchManager`); find
(`FindWindow` in find mode, as upstream hides replace for a read-only viewer) and go to line; next and previous
occurrence; treat all files as text; the encoding box; the settings button, opened by the browse window that owns the
viewer's window (`ISettingsWindowHost`, as upstream's `StartSettingsDialog(ParentForm, ...)`); continuous scroll where
upstream wires it; images and binary content (core `Diff/FileContent.cs`; Skia decodes `.webp`, while `.avif`, which
upstream shows through a Windows codec, shows "Cannot view image"); git's coloring with upstream's
`AnsiEscapeUtilities` and `DiffLineNumAnalyzer` linked unchanged over stand-ins (`Diff/UpstreamTextEditorMarkers.cs`); the
diff appearance choices (patch, git word diff, Difftastic, core `Diff/Difftastic.cs`); the combined diff; inline
differences (core `Diff/InlineDifferences.cs` over the linked `LinesMatcher`, with the zero-width mark, core `DiffAnchor`);
submodule diffs (core `Diff/SubmoduleDiff.cs`). Replace is in the editor window only, the one place upstream offers it.
`ThemeApplier` sets `ColorHelper.ThemeSettings`, which upstream's `DimColor` and `GetTextColor` read. Differences:
- A line with more than 1,000 syntax runs or marked occurrences keeps its own color: AvaloniaEdit's cost grows with the
  square of a line's colored parts (a 400 KB JSON line took minutes). Upstream colors such a line in full.
- A search that wraps around goes on in the list's next file, as upstream's `GetNextPatchFile`, while the find window
  shows.
- The file's syntax colors show only on lines git leaves uncolored; upstream's show around partial colors too.
- Difftastic's second column has no vertical ruler.
- The hex dump of a binary file is made from its bytes, where upstream's goes through the viewer's encoding; TIFF gives
  "Cannot view image", as Avalonia's decoder does not read it; a new text file shows as a diff, where upstream shows the
  file; there is no menu over a picture.
- A file's text (the File tree tab, blame) is read by its window, so the encoding box does not change it; upstream reads
  the blob in the viewer's encoding.

**RevisionDiffControl** (`ported`). The Diff and File tree tabs (`RevisionFileList`, `FileTreeRows`, `FileStatusTree`,
`MainWindow.DiffFileMenu.cs`, `MainWindow.FileTreeMenu.cs`), whose list, toolbar, filter (`FileFilterBox`) and tree are
shared with FormDiff, FormStash, FormLog and the commit window. Upstream's code linked unchanged into the core: the diff
groups (`FileStatusDiffCalculator`, core `DiffGroups`, with a stand-in for its four `TranslatedStrings` texts), the menu
rules (`RevisionDiffController`, `FileStatusListContextMenuController`, core `DiffFileMenu`), the remembered file
(`RememberFileContextMenuController`, core `DiffFileRemember`) and the diff kinds (`RevisionDiffInfoProvider`). The item
menu (`FileStatusListMenu`) has every item of upstream's, including the submodule items, the "with..." items (Windows only,
upstream's `OsShellUtil.OpenAs`), Visual Studio (Windows only, core `Platform/VisualStudio.cs`), skip worktree and assume
unchanged, "Reset chunk of file" and "Interactive add" (core `Diff/InteractivePatch.cs`), the git-grep box and window
(core `Diff/GitGrep.cs`), the range diff, blame in the File tree tab or the Diff tab (`UseDiffViewerForBlame`), and the
BrowseDiff hotkeys in every list. The toolbar's items follow upstream's `FileStatusList.Toolbar.Visibility.<item>`
settings, shared with the WinForms app. Differences:
- Folder, group and diff group nodes are rows of a flat list with an expander glyph, not tree nodes; a click selects, the
  glyph or a double-click toggles.
- The status groups follow the shell's status order (conflicts, modified, renamed, added, deleted), where upstream orders
  them by its image list; extension groups and the File tree tab's files have upstream's generic file image, as the shell
  has no per-extension system icons.
- A group node is upstream's root folder with an empty path: upstream offers "Rename / move" with a name its
  `MoveCommand` never takes and copies or filters by an empty path; here rename is hidden for it, and copy paths and filter
  in grid take its files.
- An item that takes one file (open, edit, stop tracking) acts on the one file of a selected folder; upstream's handler
  does nothing there (its `SelectedItem` is null).
- The remembered file is not offered in another repository (upstream forgets it when the browse window opens another
  one), and is told from the selected file by name and revisions, not by reference.
- A submodule update runs once per selected submodule, where upstream runs one `git submodule update`; "Open with Git
  Extensions" opens the submodule on its own HEAD, where upstream selects the commits of the change.
- Off Windows, "Reset chunk of file" and "Interactive add" run git in a terminal (`TERMINAL`, else
  `x-terminal-emulator`; on macOS a generated `.command` script that leaves git's exit code in a file,
  `InteractivePatch.MacScript`), since a program started from the desktop has no console.
- "Reset selected line(s)" of a new file is not ported; upstream resets such lines from the file's text.
- Visual Studio: an empty `vswhere` output is no Visual Studio, where upstream takes it as a path; the path given to a new
  instance is quoted, where upstream's splits at a space.
- `SelectFirstGroupChanges` (Ctrl+A) expands the collapsed folders and the first diff group first in the lists that hold
  only the rows shown; upstream selects the files of collapsed nodes without showing them.

**FormFileHistory** (`ported`). `FileHistoryWindow` (core `FileHistoryDialog`, `FileHistoryViewModel`): upstream's title,
toolstrip with the shared filter toolbar first, the file's grid with multi-selection, its menu (copy with
`CopyContextMenuItem`, the difftool items with the custom tools, save as, revert, cherry pick, the rename choices), the
grid's Navigate and View menus, and the Commit, Diff, View, Blame and Build Report tabs. Differences: it opens in this
process, where upstream starts a new one; the View menu has no build status toggles (they are the browse window's).
Not ported (decided with the user 2026-11-04): `UseBrowseForFileHistory`, which upstream turns on by default to start a
filtered browse window instead; the shell always opens this window, and the setting is not on the settings page.

**FormBlame** (`ported`). `BlameView` (shared by the blame window, the file history's Blame tab and the File tree tab):
upstream's BlameControl with its menu (`BlameMenu`: blame this or the previous revision, show changes, copy), the commit
information above the lines (hidden in the File tree tab, as upstream's `HideCommitInfo`), the gutter's tooltip and
highlight, the line kept across revisions (`GitBlameParser.GetOriginalLineInPreviousCommit`, linked), find and go to line,
syntax colors and the encodings. Differences: the text is a list, not upstream's FileViewer, so find selects the line
found without a mark in it and has no "Find & highlight all"; the "previous revision" item is enabled just after the menu
opens, as reading the real parents takes git, where upstream blocks; a double-click in the blame window shows the line's
commit, where upstream's FormBlame does nothing.

**FormMergeBranch** (`ported`). `MergeBranchWindow` (core `MergeBranchDialog`): upstream's form with its help pictures,
options and merge message (MERGE_MSG through `CommitMessageManager`), upstream's `Commands.MergeBranch` between the merge
scripts, the repository's merge settings (`IMergeSettingsStore`), and upstream's resolve conflicts question
(`DontConfirmResolveConflicts`, on the Confirmations page), and after a squash merge without "Do not commit" the commit
window with the merge message. Difference: the log message settings are kept when merging,
not on each change as upstream.

**FormResolveConflicts** (`ported`). `ConflictsWindow` (core `ConflictsDialog`, `ConflictsViewModel`): upstream's form,
menu, buttons and questions; Merge runs the configured tool itself as upstream's `InitMergetool` (kdiff3's default
command, the two-way fallback without a base); upstream's "Solve merge conflict" task dialog for deleted, created and
binary files, with the "Diff base with modified" link that keeps it open (the shim's `TaskDialogButton.AllowCloseDialog`);
"Open in mergetool" with git's tools; a conflicted submodule opens `MergeSubmoduleWindow`. Differences:
with the tool list shown, "Open in mergetool" itself does not run (an Avalonia item with a submenu does not click), so
its first, bold entry is the default tool. Not ported: upstream's custom merge scripts for Word and OpenOffice files
(`merge-doc.js`, `merge-ods.vbs`, run with Windows' `wscript` from the `Diff-Scripts` folder, which the shell does not
ship).

**FormGitCommandLog** (`ported`). `CommandLogWindow`: upstream's two tabs (the command log from upstream's `CommandLog`,
the command cache from `GitModule.GitCommandCache`), the menus, "Always on top", "Word wrap" and "Capture call stacks"; one
window at a time. Differences: the save dialog's title is the window's title (upstream's is the form's name); the
shortcuts use the platform's command key.

**FormRemoteProcess / FormProcess** (`ported`). `ProcessWindow` over `GitOutputRunner`, which starts git itself because
upstream's `IExecutable` returns standard error only after git exits; like upstream's `Executable` it sets the
environment first and records the call in `CommandLog`. Lines git ends with a carriage return are redrawn in place;
Abort stops git and its children; "Keep dialog open" (`CloseProcessDialog`); upstream's answers to plink's failures
(core `PuttyFailure`, the retry in `RepositoryOperationsViewModel`, `PuttyPrompts` in the app). Differences: git's input
is not redirected, so plink cannot wait to trust a host and fails, and upstream's question comes once git has stopped,
where upstream stops git as soon as plink asks; upstream reads the remote's key setting with an unformatted name
(`remote.{0}.puttykeyfile`) and so always overwrote the key, while here a key the remote has is kept, as upstream's
comment says. Not tried against a real PuTTY server (`PLAN.md` section 12.2).

**FormPuttyError** (`ported`). `PuttyErrorWindow` (`PuttyErrorViewModel`): upstream's "Authentication error" dialog with
Load SSH key (given to Pageant as upstream's `BrowseAndLoad`), Retry and Cancel. Upstream sets no Accept or Cancel button.

**FormSettings** (`ported`). Every value is stored under upstream's key, so both apps share it. The diff and merge tools
are chosen from those git finds (upstream's `CustomDiffMergeToolCache`) and stored in the `guitool` keys (see
GitConfigSettingsPage); `credential.helper` offers git's and the OS's helpers (git's own helpers on Linux and macOS,
`PLAN.md` question 6; a value set more than once is left alone). Not ported, as not applicable: the Appearance page's
"Show current branch in Visual Studio" off Windows and "Auto scale user interface when high DPI is used" (WinForms'
scaling; Avalonia scales by itself).

**FormSettings page tree** (`ported`). Upstream's window and page tree (`SettingsPageTree.Entries`, compared with
`FormSettings.cs` by a test), the "Type to find" box, the title, the page shown last in the session, and OK, Cancel and
Apply. Every page of upstream's is in the tree but the `windows-only` Console style; the Shell extension page is there on
Windows only. Differences: the plugin files upstream's loader passed over are listed after the plugins, with the reason (a
fork addition); the Discard button is left out (upstream shows it in debug builds only).

**ShellExtensionSettingsPage** (`ported`, Windows only). Upstream's page: Enable and Disable (`regsvr32` as administrator
for the 64-bit and 32-bit files), the cascaded menu's 18 items as three-state check boxes, the preview, and upstream's
registry values `CascadeShellMenuItems` and `AlwaysShowAllCommands`. The app's executable is `GitExtensions.exe`, so the
extension starts this app with upstream's verbs when the install folder is this app's. Differences: upstream stores its own
folder as the install folder and registers the files found there, failing when it has none; the shell does that only when
its folder has the files, else it registers the installed files and leaves the install folder
(`ShellExtensionFiles.ToRegister`); a declined elevation prompt shows no error; the items are a column of three-state
check boxes where upstream's page is a list with check boxes. The shell's own build has no shell extension files (no
installer, `PLAN.md` section 12.3), so the buttons work with an installed Git Extensions' files.

**SettingsPageHeader** (`ported`). Upstream's "Settings source:" header on every page but the groups' introductions, with
the levels, the "<<" arrows and Effective read only. Difference: the plugin pages share one level, as they are one page
here (see PluginSettingsPage).

**GitConfigAdvancedSettingsPage** (`ported`). Git > Advanced: upstream's twelve settings at the level the header
chooses, each in an editable list of the values git knows (empty for not set) with upstream's help icon opening git's
documentation, "Rebase also dependent branches" only for git 2.38 or newer. The values are read once the Config page's
are in, not after its tool lists (git takes seconds to list them), and the settings search finds the texts before the
page was shown. Difference: upstream's `PageToSettings` writes every value shown; here only a changed value is written
(empty removes the key).

**GitConfigSettingsPage** (`ported`). The Config page in upstream's order at the level its header chooses (Effective,
Local, Global and System; Effective and System read only), the tool rows with path, command, Browse and Suggest (core
`Settings/DiffMergeToolSettings.cs`), the files content encoding, and the warning without git; upstream's
`ConfiguredMergeTool` and the `guitool` keys. Differences: upstream writes a tool's path and command on every save, which
turns what it found on this computer into the user's setting; here a tool that stayed writes only what was edited, and a
newly chosen tool writes both as upstream. The git command typed on the Paths page is tried at once but stored on OK or
Apply, where upstream stores it at once. The file dialog has no `.exe` filter off Windows.

**LightnessCorrection and the dark system colors** (`ported`). Upstream's class works on a GDI+ bitmap, which .NET supports
only on Windows, so `ImageLightness` does its arithmetic on a row of pixels and `ThemedImages` gives it Avalonia's; the
pictures upstream adapts (its 33 calls) are adapted in the same places, which a test checks against upstream's calls.
Upstream's dark themes do not list the system colors: WinForms switches `SystemColorMode.Dark` on and .NET's
`SystemColors` then answer with dark values. Without WinForms they answer with light ones on every OS, so
`AppThemeColors.GetNonEmpty` answers from the table in `dark.css`, which a test compares with the file.

**FormAvailableEncodings** (`ported`). `AvailableEncodingsWindow` (core `AvailableEncodingsDialog`). Differences: upstream's
`CanRemove` compares exact types, which today's `Encoding.ASCII` and `Encoding.UTF8` do not have, so its button wrongly
stays enabled for them; the list is saved when the dialog closes with OK, as this window has no later save of
`AppSettings`.

**ChecklistSettingsPage** (`ported`). The Checklist with upstream's rows, texts and colors, Repair, "Save and rescan",
"Check settings at startup" (upstream's `CheckSettings`, in the registry on Windows and in `XplatRegistryKey`'s file
elsewhere) and the startup check (core `StartupSettingsCheck`). On Windows the install and shell extension rows (core
`InstallationFacts`, `IWindowsInstallation`) and upstream's `SolveGitCommand`, `SolveLinuxToolsDir` and
`GetGitLocations` (core `Platform/WindowsGit.cs`), run at each start as upstream's `Program.Main`. Differences:
- A tool git starts itself (listed by `git mergetool --tool-help`) counts as configured even when upstream knows no
  command for it, since upstream knows only its Windows tools; the diff tool falls back to `diff.tool` as the browse
  window does.
- Off Windows the git command comes from the shell's discovery at start (`XplatGitDiscovery`); where upstream asks for git
  and exits without it, the dashboard says git was not found.
- The editor is solved on every system, as the app is git's editor everywhere; upstream solves it on Windows only.
- The startup check is skipped without git, for a screenshot or benchmark run and for the tests' windows; the settings
  open on the Checklist, where upstream's dialog opens on its first page.
- Upstream's loop over its git locations in `SolveLinuxToolsDir` checks the git command's path instead of each location's
  and never finds anything, so it is left out; `sh` in the current folder does not count as on the PATH.

**FormAbout** (`ported`). `AboutWindow` and `ContributorsWindow`: upstream's layout, the contributor lists from upstream's
`GitUI/Properties/Resources.resx` linked into the app (`UpstreamResources`), the build's commit from GitInfo, and
`EnvironmentInfo`, which Help > Report an issue and the dashboard's Issues link copy first. Differences: the copy leaves
out upstream's list of Windows desktop runtimes (`GetDotnetVersionInfo`); the DPI line is the window's scaling times 96.

**Dashboard** (`ported`). Upstream's dashboard (core `Dashboard.cs`, `DashboardViewModel`): the background picture, the
Start and Contribute links, the search box, the recent and favourite repositories in groups with upstream's
`RecentRepoSplitter` captions and branches, the tile and group menus with upstream's questions, dropping a folder, and
the colors of `DashboardTheme`. Difference: the left column grows past upstream's 213 pixels when the font needs it.

**RepoObjectsTree** (`ported`). Upstream's left panel as one tree (core `RepoObjectsTree`, `BranchTree`): the six trees in
upstream's order and settings, its images, ahead and behind text (`AheadBehindDataProvider`, linked), inactive remotes,
the menu as `contextMenu_Opening` and `MenuItemsStrings`, double-click and the LeftPanel hotkeys, selecting a ref's commit
in the grid (Ctrl to add, Alt for the related branch), the merged-branch images, hidden commits, upstream's search (core
`RepoObjectSearch`), the multi-selection (core `RepoObjectSelection`), and the submodule tree (core `SubmoduleStructure`
over upstream's `SubmoduleStatusProvider`, linked). The focused node keeps the keyboard focus across a refill
(`FocusSelectedRepoObject`). Differences:
- "Create worktree..." opens the worktree window; a tag also has "Delete on remote...", which upstream offers in its push
  dialog; a remote branch's fetch items fetch the whole remote.
- Alt and Ctrl are read on the click, where upstream reads Alt whenever the selection changes.
- The grid loads 500 commits a page where upstream loads the whole history, so a ref below the last loaded page shows as
  hidden until a later page loads it. Hidden refs are found by the grid's loaded rows' labels, not by object id.
- Upstream keeps its search's "changed" flag after the first search with new text, so its second search starts over; the
  shell goes on to the second match.
- Upstream's menu counts only the marked nodes, so opened from the keyboard on a node that was not clicked it acts on the
  node clicked last, or offers nothing; with fewer than two marked nodes the shell's menu acts on the selected node.
- A submodule whose holder is missing is left out, where upstream also shows a message box; Open does not select the work
  tree and the recorded commit (upstream's `SetWorkingDir` with those revisions), and a new instance does not get the
  grid's selection, as the shell takes no revisions on its command line.

**HotkeySettingsManager** (`ported`). Every section and command of upstream's with a feature in the shell (`Hotkeys.cs`),
read from and written to upstream's `SerializedHotkeys` under upstream's section names, matched by section and command
code as upstream's `MergeIntoDefaultSettings`, so a hotkey changed in either app applies in both; "Reset all Hotkeys to
defaults" as upstream. macOS has its own defaults (`MACOS.md`, MAC3). Not ported: `FocusGitConsole` (the Console tab,
Windows only). Differences: the tab commands default to Cmd+Shift+] and Cmd+Shift+[ on macOS, as Cmd+Tab is the
application switcher's; `SelectNextForkPointAsDiffBase` (core `CommitListViewModel.NextForkPoint`) keeps the earliest
selected commit last selected, where upstream's order makes the second latest the latest; `DeleteRef` and `RenameRef`
(core `BrowseToolbarViewModel.QuickRefAction`) offer a menu at the selected row in place of upstream's
`FormQuickGitRefSelector`. `ToggleOrderRevisionsByDate` is on the page but runs nothing, as upstream's grid has no handler
for it.

**FormBrowse menus** (`ported`). Upstream's menus from its designers in its order, texts, access keys and images
(`MainMenuParityTests` checks them), the Dashboard menu while the dashboard shows, and the Start menu's repositories as
upstream's `PopulateFavouriteRepositoriesMenu` and `PopulateRecentRepositoriesMenu` (core `RepositoryMenus`). Quick pull,
push and fetch and Push tags are on the toolbar only, as upstream's (their hotkeys work). "Browse files..." is the shell's
own. Not ported on purpose: the telemetry choice and Check for updates.

**Initial focus** (`ported`). A WinForms form opens with a control focused, an Avalonia window with none, and the first Tab
would reach the dialog's buttons. `InitialFocus` focuses upstream's control when the window opens, and again once it is
enabled after loading.

**Title bar help button** (`ported`, adapted). An Avalonia window cannot have the title bar's "?" button. Decided with the
user 2026-11-04: F1 and a small "?" button at the left of the button strip open the same page (`DialogHelp`, tooltip as
upstream's `GotoUserManualControl`) in every dialog that has upstream's `HelpButton = true`.

**FormCommitDiff and CommitDiff** (`ported`). `CommitDiffView` and `CommitDiffWindow`, with upstream's CommitInfo as its
own control (`CommitInfoView`): the commit as git has it (not as a filtered grid shows its parents), its files with
upstream's images and the selected file's diff, continuous scroll; opened by a double-click on a real commit of the
browse or file history grid. Links to commits in the view do nothing, as upstream's without a `CommandClicked` handler.

**Navigate menu** (`ported`). Upstream's menu with its texts, keys, images and groups, upstream's `NavigationHistory`, and
the mouse's back and forward buttons over the grid and the commit details (`MainWindow.MouseNavigation.cs`).

**FormGoToCommit and Go to common ancestor** (`ported`). `GoToCommitWindow` (upstream's
`SetSelectedRevisionByFocusedControl` for which control gives the revision) and upstream's `merge-base` arguments (core
`GoToCommit.MergeBaseArguments`). The help text wraps, as the font is wider than upstream's.

**Working directory drop-down and FormRecentReposSettings** (`ported`). Upstream's `FillDropDown` with its search, and
`RecentRepositoriesSettingsWindow` (core `RecentRepositoriesSettingsDialog`) with upstream's keys, so both apps split and
sort the lists alike (`RecentRepoSplitter`). A repository clicked with Ctrl held opens in a new instance, as upstream's
`OpenRepo`.

**Delete branch and Delete tag windows** (`ported`). `DeleteBranchWindow` with upstream's BranchComboBox and
`SelectMultipleBranchesWindow`, upstream's `HandleWorktreeBranches` (core `DeleteBranchDialog.ClassifyWorktreeBranches`)
and unmerged question; `DeleteTagWindow` with the remotes. The app sets upstream's documentation address at startup
(`AppSettings.SetDocumentationBaseUrl`), which every help link needs.

**Undo last commit, Git GUI, GitK, .gitattributes and .mailmap** (`ported`). Undo last commit asks upstream's question
(`DontConfirmUndoLastCommit`) and resets softly to HEAD~1; Git GUI and GitK run upstream's `RunGui` and `RunGitK`;
`RepositoryTextFileWindow` (core `RepositoryTextFiles`) is upstream's FormGitAttributes and FormMailMap in one window.

**Clean working directory, Archive and FormChooseCommit** (`ported`). `CleanupRepositoryWindow` (core `CleanupDialog`),
`ArchiveWindow` (core `ArchiveDialog`, two selected commits give the second revision) and `ChooseCommitWindow`, with
upstream's arguments. Difference: the cleanup's git output goes to the window's log; upstream also shows each run in its
process window.

**FormDeleteRemoteBranch** (`ported`). `DeleteRemoteBranchWindow` (core `DeleteRemoteBranchDialog`): upstream's form,
question (default No) and `Commands.DeleteRemoteBranches` per remote between the push scripts. Differences: the delete
branch window for the tracking branches opens once, where upstream opens it after each remote's push; the refs and the
merged branches are read in one background pass, as upstream's module is not safe for two threads at once.

**Patch windows** (`ported`). `FormatPatchWindow`, `ApplyPatchWindow` (with `AddFilesWindow`) and `ViewPatchWindow` (core
`FormatPatchDialog`, `ApplyPatchDialog`, `ViewPatchDialog`), with upstream's commands; git's output goes to the browse
window's output panel (`RunGitWithOutputAsync`), as upstream's FormProcess. Difference: the apply window refreshes when it
is activated again, where upstream refreshes after its modal conflicts dialog.

**Git maintenance menu and FormVerify** (`ported`). Upstream's four items: `git gc` with its output, upstream's
`UnlockIndex(includeSubmodules: true)`, Edit .git/config in the editor window with upstream's warning panel, and
`VerifyDatabaseWindow` (core `LostObjects.cs`: upstream's fsck line parser, options, type guess and LOST_FOUND_ tags) with
upstream's list, menu and actions. Differences: fsck runs with the window disabled, where upstream shows its process
dialog; the tags are created quietly, where upstream shows a process dialog per tag; a zero parent shows empty instead of
zeros; the check marks survive a filter change (upstream's are lost).

**FormSubmodules and FormAddSubmodule** (`ported`). `SubmodulesWindow` (core `SubmodulesDialog`) and `AddSubmoduleWindow`:
upstream's forms, `Commands.SubmoduleSync`, `SubmoduleUpdate` and `AddSubmodule`, Remove as upstream's (the folder left),
the splitter under upstream's key. Differences: pulling a submodule runs no pull scripts and offers no conflict solving;
Synchronize's and Update's texts are not translated (an icon and text in one button). The left panel's submodule
"Manage..." is not added: upstream enables it only on the node of the open repository, which the shell's submodule tree
does not have.

**FormSparseWorkingCopy** (`ported`). `SparseWorkingCopyWindow` over core `SparseWorkingCopy` (upstream's view model, with
its texts): the parts upstream builds in code, in its order, and upstream's save, `read-tree` and questions. Save is also
Ctrl+Enter, as the editor takes Enter. Differences: the rules are edited in a plain text box; the header, footer and
separators use the dialog footer brushes and the strip the info colors, where upstream uses ControlLightLight,
ControlDark and Info.

**Repository settings...** (`ported`). Opens the settings window on the Git Config page, as upstream's
`StartRepoSettingsDialog`.

**FormChangeLog and FormDonate** (`ported`). `ChangeLogWindow` shows upstream's `GitUI/Resources/ChangeLog.md`, linked into
the app as an asset; `DonateWindow` its text and badge.

**Tools > PuTTY** (`ported`, Windows only as upstream's). Pageant and PuTTYgen of Settings > SSH, or upstream's "Is PuTTY
installed?" error.

**Help > telemetry choice and Check for updates** (`not ported`, on purpose; decided 2026-10-31). The fork sends no
telemetry, so upstream's "Yes, I allow telemetry" item and the General page's telemetry box would ask for a consent that
means nothing; Check for updates (FormUpdates) reads gitextensions/gitextensions releases and offers its Windows installer,
which are not the fork's builds.

**SplitterManager** (`ported`). The splitter positions under upstream's keys (`<form>.<splitter>_Size`, `_Dpi`,
`_Distance`, `_Panel1Collapsed`, `_Panel2Collapsed`), so both apps share them; restored as upstream's
`RestoreFromSettings` (core `SplitterLayout`) the first time each splitter is laid out, once more at the next size when the
stored distance did not fit (upstream's `RestorePendingSplitters`), saved when the window closes. Not
kept: `RevisionsSplitContainer` (upstream resets the commit info's width on every layout), the Diff tab's
`LeftSplitContainer` and the blame splitters (the shell has no such splitters), and `_FontSize`, which upstream writes but
never reads.

**ScriptsManager / ScriptRunner / ScriptOptionsParser** (`ported`). Upstream's `ownScripts` (same XML and defaults), every
option with upstream's quoting and prompts, `{openurl}`, `navigateTo:`, PowerShell (pwsh outside Windows), the user menu
bar, the grid's and file lists' menus with upstream's icons, the script events wherever upstream runs them, plugin
commands, and the scripts' hotkeys (section "Scripts", code = `HotkeyCommandIdentifier`). Difference: a key without Ctrl
(Cmd) or Alt typed into a text box stays text unless it is a function key; upstream has no such rule.

**ScriptsSettingsPage** (`ported`). The scripts in run order with Add, Delete, Up, Down, the arguments help (kept equal to
upstream's by a test) and the icon choice (core `ScriptsPageViewModel`). Differences: plain fields instead of WinForms'
property grid; the page is stored only when the scripts changed, where upstream stores it on every save.

**SimpleHelpDisplayDialog** (`ported`). A help text in its own window, not modal, closed with its owner.

**AdvancedSettingsPage** (`ported`). Upstream's checkout, branch name, Commit & Push, help images and advanced options
settings. Not ported: "Use Console Emulator for console output in command dialogs" (Windows only, see ConsoleEmulation)
and the update checks (on purpose, see Help > telemetry choice and Check for updates). Differences: the shell asks about
local changes only when there are some (upstream's dialog also picks the branch); a remote branch checkout does not go
through that question.

**GitSettingsPage / FormFixHome / SshSettingsPage** (`ported`). Settings > Git: the git command, the Linux tools folder and
"Download Git" (Windows only, as upstream), the line about `GIT_CONFIG_GLOBAL` or HOME, and FormFixHome's choices and
upstream's `CheckHomePath` after a save (core `HomeSettings`; Windows only: elsewhere HOME is always set, and changing the
app's own HOME would also move its settings folders). Settings > SSH: OpenSSH, PuTTY or another client with upstream's
`gitssh` rule, PuTTY's paths and Pageant's autostart; `GIT_SSH` is set on save and at startup as upstream's
`AppSettings.LoadSettings`. Differences: FormFixHome is the Git page here, so after Yes the settings window stays open on
that page with the choice made and the user saves again, where upstream's dialog saves the choice itself; PuTTY is offered
on Windows only, since upstream's rule reads a Linux plink path back as another client.

**SortingSettingsPage / left panel order** (`ported`). Upstream's revision order, branch sort key and order
(`Commands.GetRefs`), the prioritized branches and remotes (`OrderByPriority`, also in the commit info), the left panel's
"Sort by" and "Sort order", and the help links as "?" buttons. Difference: an invalid expression matches nothing instead
of failing the tree.

**RevisionLinksSettingsPage / CommitInfo related links** (`ported`). Upstream's `RevisionLinkDefs` through its
`ExternalLinksManager` at the header's level, the GitHub and Azure DevOps templates (upstream's extractors, linked),
and the commit details' "Related links:" by upstream's `GitRevisionExternalLinksParser`. Differences: the templates are
buttons instead of the Add drop-down, and their icons are upstream's images by name, as the templates' own `Icon` is a
WinForms image.

**AppearanceFontsSettingsPage** (`ported`). Upstream's four fonts in its stored format, parsed and written by `FontSetting`,
since upstream's `System.Drawing.Font` works only on Windows; end-of-line markers as text or glyphs (`ShowEolMarkerAsGlyph`).
Differences: Avalonia has no font dialog, so the family is chosen from the installed fonts and the size, bold and italic
set beside it; "(default)" removes the key so the app's own font applies (upstream cannot unset a font); only a changed
font is written; the fonts apply at once, without upstream's restart.

**FormBrowseRepoSettingsPage / revision grid tooltips** (`ported`). Upstream's page, with the revision tooltips
(`GitRevisionSummaryBuilder`), the git-grep box, the GPG tab and the output history settings applied when saved, so their
"(restart required)" is left out. Not ported: the Console tab and "Default shell" (`windows-only`, see ConsoleEmulation)
and "Show file history in the main window" (`UseBrowseForFileHistory`, decided with the user, see FormFileHistory).

**BlameViewerSettingsPage / blame gutter and menu** (`ported`). Upstream's blame flags (`-M`, `-C`, `-w`), gutter, age
colors (`CalculateBlameGutterData`, their lightness turned around on a dark theme where upstream adapts them), avatars,
the menu's settings and the page. The text is read as upstream reads blame (the file's lines in `FilesEncoding`, the
headers in `LogOutputEncoding`). Difference: the short hash stays in its own column, shown once per run like the gutter
line.

**GraphRenderer / SegmentRenderer / DetailedSettingsPage** (`ported`). Upstream's graph model linked unchanged and its
renderer ported line by line from WinForms' Graphics to shapes (`GraphPainter`), with its draw styles, lane colors
(`GetBrushForLaneInfo`) and tooltips; the Detailed page with upstream's `DetailedSettings` at the header's level, the
revision graph group at the global level only, as upstream's. Differences: upstream's 1/8-pixel anti-aliasing shift for
GDI+ is left out; the two lane states upstream throws on are skipped instead; colors come from the theme's lane colors by
index (stand-in `RevisionGraphLaneColor`); the graph column is only as wide as the visible rows need. Not needed:
upstream's row bitmap cache (`GraphCache`).

**GeneralSettingsPage / AppearanceSettingsPage / DateColumnProvider** (`ported`). Every setting of upstream's two pages
whose feature the shell has, under upstream's keys: dates, the last working directory on startup (upstream's `Program`
rule), the counts, the clone destination, the pull, stash and submodule settings, histogram diff, limits, the submodule
status (enabled as upstream's `SetSubmoduleStatus`), the spelling dictionary, and on Windows "Show console window when
executing git process" (`GitOutputRunner.StartInfo`) and "Show current branch in Visual Studio". Not ported: telemetry
(on purpose) and "Auto scale user interface" (Avalonia scales by itself). Difference: the dictionary list is filled at once,
where upstream fills it when it drops down.

**DiffViewerSettingsPage / FileViewer options** (`ported`). Every option of upstream's page, kept as upstream's runtime
settings, and "Save current view settings as default", which saves every runtime setting of `AppSettings` as upstream's
`SaveCurrentViewSettingsAsDefault` (by reflection over its static fields). The combined diff runs upstream's
`GetCombinedDiffContent`, which reads `OmitUninterestingDiff` itself. Difference: the four whitespace modes are one box
on the page, where upstream has three toggle buttons with the same states (the viewer's toolbar has upstream's toggles).

**CommitDialogSettingsPage** (`ported`). Every control of upstream's page, under its keys; the message goes through
upstream's `FormatCommitMessage` before the commit.

**ConfirmationsSettingsPage / ConfirmSuppressible** (`ported`). Every confirmation of upstream's page, under its keys and
labels, three-state as upstream's (`ToCheckboxStateInverted`, core `Confirmations.CheckState`), asked where upstream asks
it. "Update submodules on checkout" here is upstream's `dontConfirmUpdateSubmodulesOnCheckout`, a different setting from the
General page's `updateSubmodulesOnCheckout` with the same label. Differences, by choice: the question buttons name the
action where upstream has Yes and No, and the "not on a branch" question has no branch buttons.

**PluginRegistry / FailedPluginWrapper** (`ported`). Upstream's `ManagedExtensibility` (compiled unchanged in the shadow
`GitUIPluginInterfaces`) loads the plugins once, off the UI thread, from the Plugins folder and `UserPluginsPath`; each gets
upstream's `GitPluginSettingsContainer` (linked, so the keys are upstream's); a plugin that fails shows upstream's "Copy
error details to clipboard?" box; plugins are registered and moved as upstream's `SetGitModule`. Upstream's BackgroundFetch
and AutoCompileSubmodules are compiled unchanged in `src/xplat/plugins` (only their bitmap resources replaced), and its
WinForms plugins are built for the shell (see "Plugin forms"). Settings > Plugins lists the plugin files upstream's loader
passed over, with the reason; third-party WinForms plugins are not loadable (`PLAN.md` question 5). Not ported: loading the
commit-form plugins for a stand-alone commit window (the shell has none) and the composition cache (off upstream too).

**GitUICommands (plugin side)** (`ported`). `PluginHost` is the `IGitUICommands` the plugins get: the module,
`RepoChangedNotifier`, upstream's events (a "pre" handler can cancel), settings, the commit, process and batch dialogs
(batch files as sh scripts off Windows), commit templates, the repository host methods with `WrapRepoHostingCall`,
`GetService` over upstream's container without its WinForms services (`PluginServices`; upstream's app registrations
reimplemented, as that class is internal to upstream's exe), `BrowseRepo` (`MainWindow.BrowseRepo.cs`), and `RunCommand`
and every `Start...Dialog` method through the browse window's own flows (`IPluginFlows`, `MainWindow.PluginFlows.cs`),
waiting in a nested dispatcher frame (`ModalWindow.Wait`) as a WinForms `ShowDialog`. Not portable: `ShowModelessForm`,
whose WinForms form cannot show in the shell (the shim's `Form` is no window), throws `NotSupportedException`; the plugins
built for the shell show their windows themselves (`PluginDialog`). Differences: `StartCommandLineProcessDialog` returns
once the command started, not when it ended; `AddUpstreamRemote` fetches the added remote where upstream opens its pull
dialog to fetch it; the flows that exist only for the open repository act on it whatever the host's module.

**Command line** (`ported`). Upstream's `RunCommand` without the plugin side: core `CommandLine` (the 40 verbs, their
checks, exit codes, `InitializeArguments`, `GetParameterOrEmptyStringAsDefault`, `Program.GetWorkingDir`, the usage text in
`CommandLineHelpWindow`), and the app's `CommandLineVerbs`, which opens a verb's window alone or runs the browse window's
flow in a browse window that never shows itself (`MainWindow.CommandLine.cs`: one transparent pixel, out of the taskbar,
without placement, menu bar or recent repositories, closed once nothing runs). Differences: a repeated option keeps the
last value, where upstream's dictionary throws; `settings` loads no plugin, so their pages are missing; a verb that needs a
repository, started outside one, says so, where upstream ends with -1 and no word; `searchfile` reads HEAD's files without
upstream's `GetFullTree` cache, which is keyed by the arguments alone.

**FormInit** (`ported`). `InitRepositoryWindow` for the Start menu, the `init` verb and the plugins'
`StartInitializeDialog`: upstream's form, checks (`IsRootedDirectoryPath`) and `GitModule.Init(bare, shared)`; the new
repository opens and joins the recent list.

**FormOpenDirectory** (`ported`). `OpenDirectoryWindow` ("Open local repository") with upstream's `GetDirectories` list,
Go up, Browse and Open, its images and sizes. Difference: the folder box has no completion of folder names as typed
(upstream's `AutoCompleteSource.FileSystemDirectories`). `FormQuickItemSelector`, the base of `FormQuickGitRefSelector` and
`FormQuickStringSelector`, is ported as `ChooseViewModel`.

**EmptyRepoControl** (`ported`). The grid's page for a repository without commits, by upstream's rule
(`OnRevisionReadCompleted`), with "Edit .gitignore" and "Commit", both hidden in a bare repository.

**FormStatus password input / PasswordInput** (`ported`). The output window's "Show password input" (upstream's
`ShowProcessDialogPasswordInput`) and the box for git's standard input, shown by itself when git writes text without a
line end and waits, as upstream's `FormProcess.DataReceived`; the remote operations' git has its standard input redirected
and kept open (`IGitInputTarget`), and text without a line end is shown as it comes (`GitOutputLine.IsPrompt`). Abort is
the cancel button, as upstream's `CancelButton`. Difference: upstream's box takes text dropped on it; the shell's is an
ordinary text box.

**PluginSettingsPage / SettingControlBindings** (`ported`). The plugins' settings with the editor of upstream's binding for
each type, links through the shim's `RaiseClick`, and credentials (in Windows' credential store as upstream; off Windows
they say why they cannot be edited, `PLAN.md` question 6). Differences: settings with a WinForms control of their own are
shown as not editable; the plugin pages share one level, as they are one page.

**FormGitIgnore / FormAddToGitIgnore** (`ported`). `GitIgnoreWindow` (core `GitIgnoreDialog` with upstream's
`GitIgnoreModel` and `GitLocalExcludeModel` texts) for .gitignore or .git/info/exclude, with "Add default ignores" (upstream's
`DefaultIgnorePatterns` or the user's `DefaultIgnorePatterns.txt`), "Add pattern", Save and the close question; and
`AddToGitIgnoreWindow` with upstream's preview (`GetIgnoredFiles`). A selected folder is added as `/folder/`. Difference: a
text's line ending is its own first one, where upstream uses the platform's, which mixes endings in a file with the other
one.

**FilterInfo / FormRevisionFilter** (`ported`). Core `RevisionFilter` builds upstream's arguments in its order (branches,
reflog, `-G`, `--` options, ignore case, simplify by decoration, full history and simplify merges, `--boundary`), with
upstream's `HasFilter`, `ResetAllFilters` and `GetSummary`; `FilterWindow` is upstream's FormRevisionFilter with its
`UpdateFilters` rules and settings. Differences: the limit stops the list's reading instead of git's `--max-count`, which
would also limit each page the list reads; an unchecked filter's text is not kept for the next time the window opens; the
dates are whole days (Until includes the day), where upstream's pickers start at midnight. Upstream's `--parents` with a
revision filter is not added, as the shell's log format always reads the parents.

**FormManageWorktree / FormCreateWorktree** (`ported`). `WorktreesWindow` (core `ManageWorktreeDialog`) and
`CreateWorktreeWindow` (core `CreateWorktreeDialog`) with upstream's rules and command (`-c worktree.useRelativePaths=true`
when git's config does not set it); `WorktreeFlow` ports upstream's `WorktreeSwitch`, `WorktreeCreate` and
`WorktreeDelete`; the toolbar's worktree drop-down is upstream's. Differences: `ManageWorktreeDialog.IsCurrent` follows links
(`RealPath`) off Windows, where upstream compares the text: git prints the real folder, so a repository opened through a link
did not count as open and could be deleted; the commands run without the process window.

**Revision grid context menu** (`ported`). Upstream's mainContextMenu in the order its constructor gives it at run time
(`_contextMenuItems`, which `GridMenuTests` reads), with `ContextMenuOpening`, `GitRefListsForRevision`, `SetEnabled` and
`UpdateSeparators`, the focused menu on a right-clicked ref label, multi-selection, the stash items, View, Advanced (Edit
and Reword through upstream's `LaunchRebase`), Compare and the difftool tools. "Browse files at this commit..." is the
shell's own. Differences: macOS's sed lacks the `0,/re/` address that upstream's `LaunchRebase` command uses, so perl does
the same there (not checked on macOS); a click on the virtual ahead and behind label counts as its branch's, where upstream
offers its tracked branch.

**FormSubmodules** (`ported`). See "FormSubmodules and FormAddSubmodule" and the RepoObjectsTree note. Upstream's
`GetSubmodulesInfo` parser needs the describe suffix that `git submodule status` leaves out for a submodule that is not
initialized, so such a submodule has no status and shows as not initialized; an upstream behavior worth reporting, which
the fork does not change.

**GitExtSshAskPass** (`ported`). `AskPassWindow`: the app answers ssh's and git's prompts during remote operations, started
through `SSH_ASKPASS` (with `SSH_ASKPASS_REQUIRE=force`, as upstream) and recognizing the run by a marker variable, as ssh
passes only the prompt; Cancel exits 1, which stops git. Difference: upstream sets `SSH_ASKPASS` for the whole process;
the shell sets it only for remote git calls. Upstream's installer placement of the native helper is not needed.

**ThemeModule, ThemePathProvider, appearance page** (`ported`). The theme is loaded as upstream's
`ThemeModule.LoadThemeSettings` through upstream's loader files linked unchanged; `AppThemePaths` repeats
`ThemePathProvider`'s folder rules because the upstream class asserts GitExtensions.exe in Debug builds. A theme's system
color overrides (upstream's `SysColorValues`) go to Fluent's text, disabled text and accent resources; "Open theme folder"
as upstream's Colors page (upstream has had no theme editor since 2019). Not applicable: the other system colors (Fluent
draws the controls from its light or dark variant), `UseSystemVisualStyle`, and the restart prompt (a theme applies at
once). Differences: the theme folder itself opens, where upstream selects it in its parent's window; for a portable app
the user folder item is off, where upstream's does nothing.

**ColorsSettingsPage (revision graph group)** (`ported`). Upstream's six check boxes, applied to the grid on save;
"Draw non relatives text gray" with upstream's `RevisionDataGridView` colors in every grid. `multicolorbranches` is stored
only, as upstream reads it nowhere else.

**InteractiveGitActionControl** (`ported`). Upstream's two notification bars (bisect; a stopped rebase, merge or patch, or
conflicts) with its texts, buttons, colors and `IconBox`, the state read with upstream's `InTheMiddleOf...` checks in its
order, Continue and Abort as upstream's, More... opening the rebase, bisect or apply patch window. Difference: conflicts are
taken from the status the window already reads, not from a second `ls-files --unmerged`.

**FormBisect** (`ported`). `BisectWindow` with upstream's five buttons and rules; each step's output in the process window,
which stays open after a mark whatever the close setting says (upstream passes `useDialogSettings: false`); several
selected commits start upstream's `BisectRange`.

**Plugin forms (general)**. Upstream plugins with WinForms forms are built for the shell by shadow projects in
`src/xplat/plugins` that set `XplatPluginUi`: the plugin class and the files without WinForms are compiled unchanged, and the
form is replaced by a stand-in of the same name and constructor (`GitExtensions.Xplat.Ui.PluginDialog`) whose `ShowDialog`
shows an Avalonia window modally (`ModalWindow`, a nested dispatcher frame). The windows' controls are named as the form's
fields, so upstream's translations apply. The BuildServerIntegration plugins replace their settings controls the same way;
GitHub3 has no form of its own (see the repository host rows).

**CreateLocalBranchesForm** (`ported`). As upstream, a branch git cannot create is passed over, and the count in the
message is that of every branch git listed.

**ProxySwitcherForm** (`ported`). Sets and unsets through upstream's `SetGitSetting`, which reads the config again and
lets unsetting a proxy that is not set pass.

**FindLargeFilesForm** (`ported`). The scan runs upstream's `LargeFileFinder` commands off the UI thread. Delete runs
upstream's batch file on Windows, character for character (a test compares it with upstream's snapshots), and the same
steps as a POSIX shell script elsewhere, which `PluginHost` runs with sh.

**DeleteUnusedBranchesForm** (`ported`). Differences: the saved settings are put in the window before its change handlers
are added, so opening it does not warn about unmerged branches (upstream's load triggers its own handler); a failed
deletion shows its message instead of a bug report.

**ReleaseNotesGeneratorForm** (`ported`). HTML goes to the clipboard as plain text and as HTML: upstream's CF_HTML text on
Windows, the platform's own HTML format elsewhere (`text/html`, `public.html`).

**GourceStart** (`ported`). The plugin class is upstream's, with its download of Gource for Windows; off Windows an empty
path is filled with the `gource` on the PATH. The authors' pictures for `$(AVATARS)` come from the app's avatar service
(`PluginAvatars` in the shared Ui project, as a plugin cannot reference the app). Differences: the pictures are read
without blocking the window; a PNG is written as it came, other formats are converted with Avalonia's `Bitmap.Save`.

**FormImpact / ImpactControl** (`ported`). Upstream's `ImpactLoader` compiled unchanged; `ImpactGraph` ports the drawing.
Differences: the graph scrolls in a ScrollViewer; the hit test uses the blocks and the same curves instead of the platform
geometry. Upstream bug, not fixed here because `ImpactLoader` is compiled unchanged: its parse loop steps over the header
line that ends a commit's numstat, so every commit that follows another in git's output is skipped (in a repository with
one commit per author per turn, half the authors vanish). Candidate for an upstream fix.

**FormGitStatistics** (`ported`). The four tabs with upstream's `GetCommitsByContributor` and `LineCounter`. Off Windows the
ignored directories are turned back into the platform's separators (the plugin class writes them with `\`). Difference:
upstream's 3D pie chart (`PieChart/`, System.Drawing) is a flat pie in upstream's colors with the slice's text as tooltip.

**Translations** (`ported`). The app ships upstream's Transifex files (`Translation/*.xlf`, linked from
`src/app/GitUI/Translation`) and its language choice (`translation`, shared with the WinForms app).
`UpstreamTranslation.Apply` translates a window whose controls are named as the form's fields, as upstream's
`TranslationUtil` does by type and field names; `UpstreamTranslation.Text` gives a `TranslationString` field's text;
upstream's `&` mnemonics become access keys; plugins translate themselves (`GitPluginBase.Translate`). Every other window is
translated by its English texts (`UpstreamTranslation.ApplyByText`): a text that matches an upstream source text takes its
translation; lists filled with data are left alone. Texts upstream does not have stay English (`PLAN.md` question 4). As
upstream, a change applies to windows opened later; the plugins' own texts need a restart.

**SearchWindow / Find file** (`ported`). `SearchWindow` with upstream's SearchControl (the first 20 matches, the latest text
winning as upstream's `AsyncLoader`) and upstream's `FindFilePredicateProvider`, linked (core `FindFile`), for the file
lists' "Find file...", `FindFileInSelectedCommit` and the `searchfile` verb. Differences: the matches show the path, where
upstream's list shows `GitItemStatus.ToString()` (with "Renamed ... to ...", the staged state and the similarity); the
window has the system's thin border, where upstream's has none.

**FormChooseTranslation** (`ported`). `ChooseTranslationWindow` with upstream's flags (the four languages upstream ships a
blank flag for show it blank), asked once at startup while no language is set, before the browse window opens its
repository, and from the Checklist's language row (upstream's `translationConfig_Click`).

**BuildServerWatcher** (`ported`). Upstream's choice of adapter and detection, its polling, `ReplaceVariables` and the
credentials in upstream's text format; a new watcher (with a new adapter) after each read of the grid, so upstream's
`OnRepositoryChanged` is not needed. Credentials: on Windows upstream's store (isolated storage, DPAPI, one file per
server; the WinForms app reads the same file when the isolation identity matches); on Linux the Secret Service through
`secret-tool` when installed; otherwise (macOS, Linux without `secret-tool`) for the session only, so a secret is never
written in plain text. Difference: upstream writes the file without truncating it, so a shorter text could leave old
bytes; the port truncates. As upstream, an auto-detected server reads its settings from the type-less path `BuildServer.`.

**BuildStatusColumnProvider** (`ported`). The "Build Status" column with upstream's settings, colors (lighter on a dark
theme, where upstream lightens them on a selected row), tooltip, hand cursor and menu items; results kept per commit, so
later pages show them. Upstream's grid hides its column headers, so the column's width when resized does not apply; the
symbol column is 24 px (drawn as a character) where upstream's is 16 px.

**BuildServerIntegrationSettingsPage** (`ported`). Upstream's page at the header's level, with the plugin's settings
control for the type. Difference: the control's values for a type are read from that type's path even before it is saved
(upstream reads them from the saved type's path).

**FormBuildServerCredentials** (`ported`). Asked from the adapter's thread, shown on the UI thread until answered.

**Build server settings controls** (`ported`). AppVeyor, Azure DevOps, GitHub Actions, GitLab, Jenkins and TeamCity: each
plugin's control is replaced by an Avalonia control of the same name, exported with upstream's metadata, with upstream's
fields, keys and checks. The adapters, API clients and detectors are compiled unchanged, except the Azure DevOps
`ApiClient`'s credentials (seam S9, `PLAN.md` section 5). The WinForms `TaskDialog` the Azure DevOps adapter shows for a
bad token goes through the shim to the app's message box window.

**ForkAndCloneForm** (`ported`). Through the host plugin, off the UI thread, and the shell's clone. Difference: the default
folder is the default clone folder or the open repository's folder, where upstream takes the most recent repository's
(usually the same).

**ViewPullRequestsForm / DiscussionHtmlCreator** (`ported`). The hosted remotes, their pull requests, the patch split into
files (each marked modified, as upstream's), the discussion, fetch, add remote and close. Differences: the discussion is a
list of entries (author, date, commit, body) instead of upstream's HTML page; the comment box and its Refresh and Post
buttons work, where upstream's have no handlers.

**CreatePullRequestForm** (`ported`). Upstream's targets, branches, title, the body from
`.github/PULL_REQUEST_TEMPLATE.md`, and its messages.

**Repository host menu, commands and blame items** (`ported`). The menu named after the first host plugin (upstream
supports one), through `PluginHost` as upstream through `GitUICommands`; the blame menu gets the host plugin's items, which
the plugin builds in a shim `ContextMenuStrip` with upstream's `GitBlameContext` as tag.

**Commit templates** (`ported`). The "Commit templates" menu: the plugins' templates (upstream's `CommitTemplateManager`,
used as is) with their icons, the user's own (`CommitTemplates`), upstream's regex placeholders from the branch name, the
Conventional Commits items, and git's `commit.template` (`GitCommitTemplate`, upstream's `LoadGitCommitTemplate` used as
is) as upstream's `AssignCommitMessageFromTemplate`. Differences: a `commit.template` path starting with "~/" is in the home
folder, as git reads it, where upstream resolves it against the working directory and reports the template missing; a
plugin template's System.Drawing icon is drawn as PNG where it can be (Windows), else the registering plugin's embedded icon
is used.

**FormCommitTemplateSettings** (`ported`). `CommitTemplateSettingsWindow`: the user's templates (upstream's
`CommitTemplateItem` and serializer, shared with the WinForms app) and the `CommitValidation*` settings.

**FormCommit: "Commit message" menu, reset buttons, message checks, Conventional Commits** (`ported`).
`PreviousCommitMessages` (upstream's `GetPreviousCommitMessages` and its merge rule), `CommitMessageValidation`
(`IsCommitMessageValid`, `FormatAllText`, upstream's `WordWrapper` linked) and `ConventionalCommits`, with upstream's cases as
tests.

**EditNetSpell (auto-completion and spelling)** (`ported`). Upstream's providers linked unchanged
(`CommitAutoCompleteProvider`, `CommitMessageMetadataProvider`) with its word-at-cursor rules, pause and keys, and the
tooltip while the words are read (`AutoCompleteToolTip`). The spelling checker is upstream's engine
(`externals/NetSpell.SpellChecker`, compiled unchanged by `src/xplat/NetSpell.SpellChecker`; a stand-in type for its one
designer attribute) with upstream's options and dictionaries (copied next to the app; the folder rule is repeated, as
upstream's `GetDictionaryDir` asserts GitExtensions.exe); the marks of `SpellCheckEditControl` drawn by an adorner
(`SpellingUnderlines`), "Mark ill formed lines", and upstream's message box menu; added words go to NetSpell's user word list,
shared with upstream on the same machine. Differences:
- Up and Down move in the completion list, which upstream's list leaves to the text box.
- The menu is for the word under the pointer when it is opened with the pointer, where upstream's is for the word at the
  caret (a right click there does not move it).
- "Ignore word" ignores the word for the window's life; upstream calls NetSpell's `IgnoreWord`, which only moves past it,
  so its next check marks the word again.
- The suggestions show whatever "Provide auto completion" says; upstream shows them only while it is on.

**Avatars** (`ported`). Upstream's provider chain (GitHub, Gravatar, `CustomAvatarTemplate` through upstream's
`TemplateFormatter`, the initials or Gravatar's style as fallback) without its System.Drawing images, which do not load off
Windows; upstream's cache folder and file names, so both apps share it. Differences: the file is kept as the provider sent
it, where upstream re-encodes it as PNG; a bot's GitHub avatar (upstream looks it up through the GitHub API) is not looked
up; upstream's last-resort user picture is the initials here; GitHub's identicon is recognized by the width in its PNG
header.

**PathFormatter** (`ported`). "Truncate long filenames" for the file lists, the full path as the tooltip. Difference:
"Compact" uses Avalonia's path ellipsis, where upstream uses Windows' `PathCompactPathEx`.

**RepositoryCurrentBranchNameProvider** (`ported`). The branch through upstream's `Commands.GetSelectedBranch`; none for a
folder that is gone, not a repository, or bare, as upstream.

**QuickSearchProvider** (`ported`). Upstream's `GitRevisionTester.Matches` rules, timeout and keys. Difference: the label
takes the theme's warning color when nothing matches, where upstream's is red.

**LaneInfoProvider** (`ported`). The lane's tooltip with upstream's text and `BranchFinder`.

**ArtificialCommitChangeCount / artificial rows** (`ported`). The working directory and index rows, their counts and icons
grouped as upstream's `Update` (core `ArtificialChanges`, with the submodule states), the unknown state until the status is
read (upstream's `DataValid`), and `GetSummary` as the tooltip of the row and of the left panel's shown project.

**BuildReportTabPageExtension** (`ported`, adapted). The "Build Report" tab while `ShowBuildResultPage` is on and the
selected build has a report. Difference: the report is a link that opens in the browser, as Avalonia has no embedded
browser (upstream shows the same link for a build not shown in its browser); the page's favicon on the tab is missing too.

**FormBrowse toolstrip** (`ported`). Upstream's `ToolStripMain` in its order with its images by resource name
(`UpstreamImages`): the toggles, the submodules (`PopulateToolbar`, core `SubmoduleStructure.MenuItems`), worktrees,
working directory and branch drop-downs (`CurrentBranchDropDownOpening`), the pull split button, push with
`AheadBehindData.ToDisplay`, "Commit (n)" with `RepoStateVisualiser`, the stash, folder, terminal and settings buttons, the
commit info position, then the filter toolstrip; upstream's overflow (`OverflowToolStrip`: the items that do not fit move
into a "»" drop-down while it is open). The caption and window title are upstream's `RecentRepoSplitter`,
`PathUtil.GetDisplayPath` and `AppTitleGenerator`, linked unchanged. Difference: "Update current submodule" gives git the
submodule's path in the superproject, where upstream passes its full path (git takes either).

**FilterToolBar** (`ported`). Upstream's filter toolstrip as a control of its own (`FilterToolBar`, core
`FilterToolBarViewModel`), bound to a grid through `IFilterToolBarGrid` (upstream's `IRevisionGridFilter`): the browse
window's and the file history's. Upstream's order, images, menus, `SetShortcutKeys`, "No results found",
`RevisionFilterDropdowns` and its presets, Ctrl+E between the boxes, `ResizeDropDownWidth`, and `SetRevisionFilter`. The
filter options upstream keeps in settings are read and stored by core `StoredRevisionFilter`, which both grids use. As
upstream's `FilterInfo.GetLimitingRevisionFilter`, a filter that may hide parents adds `--parents`, so git rewrites the
parents and the shown commits connect in the graph.

**ConsoleEmulation / the Console tab** (`windows-only`). Upstream's Console tab runs a terminal inside the browse window
through ConEmu or Git for Windows' Mintty, both Windows-only programs, with the Console style settings page, the "Show the
Console tab" and "Default shell" settings and the `FocusGitConsole` hotkey. Decided 2026-11-20 with the user: not ported,
kept Windows-only. The shell opens terminals outside the window instead (see Terminals), and upstream's plain-text runner,
which its process dialog uses without ConEmu, is the output window (see FormRemoteProcess / FormProcess). Its settings are
left as they are in the settings file, so the WinForms app reads them unchanged.

**Terminals** (`ported`). On Windows upstream's terminal split button (`userShell`): the installed shells in upstream's order
and images, found with `PathUtil.TryFindShellPath`. On Linux and macOS the system terminal (`TERMINAL` or
`x-terminal-emulator`; Terminal on macOS), or the fork's own "Terminal command" on the Paths page
(`xplat.TerminalCommand`, `{folder}` for the repository's folder, `TerminalCommand.Parse`; `PLAN.md` question 10).

**Revision grid rows and View menu** (`ported`). No column header row, as upstream's grid; the labels laid out as
`MessageColumnProvider` (core `GridLabels`) and drawn as `RevisionGridRefRenderer` (`RefLabelView`), with the tracked
branches and `AheadBehindDataProvider`, the hover frame and tooltips (core `RefLabelTooltips`), the merge source arrow,
the fixup and amend markers, the commit body, `MultilineIndicator`, the git notes and their column
(`NotesColumnProvider`), the session refs (`showSessionRefs`) and the superproject's labels and refs (`SuperprojectLabels`
over upstream's `SuperProjectInfo`); the row colors of `RevisionDataGridView`; the View menu as upstream's
`GetViewMenuCommands`; the columns at upstream's providers' widths (`RevisionGridColumns`; upstream keeps no widths, as its
grid has no header row). Differences: upstream drops the multiline indicator in a cell narrower than twice its width, here
it stays; only the browse window's grid shows the superproject labels (upstream's other grids would too); the hover
highlight of a commit's ancestry also starts on them. The `ls-tree` read of the superproject's refs is reimplemented,
because upstream's `GetSubmoduleItemsForEachRefAsync` reads `AppSettings` itself.

**CommitInfo and the detail tabs** (`ported`). Upstream's layout and renderers (`CommitDataHeaderRenderer`,
`CommitDataBodyRenderer`, `LinkFactory`), whose XHTML is read back (`CommitInfoText.Parse`) into selectable text with links
(`InfoTextBlock`), as upstream renders it into rich text boxes; `CommitInfo.UpdateRevisionInfo`'s parts; the menu; the git
notes and "Add notes" (`git notes edit`); the GPG tab (`RevisionGpgInfoControl` over upstream's `GpgInfoProvider` and
`GitGpgController`); the texts upstream keeps in English code translated through `UpstreamTranslation.ByText`. Left out:
the Console tab (`windows-only`). Difference: git edits the notes in the app's editor (`GitEditorCommand`, for this call
only), where upstream uses `core.editor` and a console window for a terminal editor.

**macOS notes** (no upstream form to compare with; `MACOS.md`, MAC3 and MAC4). Three hotkey defaults differ on macOS, where
the system takes upstream's key (`HotkeyTable`'s macOS table: Commit, Merge, the left panel's MultiSelect, which always
toggles there). The browse window's menus are copied into the system menu bar (`SystemMenuBar`: each submenu built again
from the window's `MainMenu` when macOS opens it, a click raising the window item's click; the window's own menu hides once
the copy shows). Not copied: the keys beside the items (a key macOS also gave a menu item might run twice) and the icons.
Only the browse window has the copy. "New instance" goes through `NewInstanceCommand` (`open -n` in a bundle). Difference
on every OS: a typing pause while the completion words are still being read shows the list when they arrive; upstream shows
nothing until the next key.

**Linux notes** (no upstream form to compare with). Upstream starts itself again through `Application.ExecutablePath`
(new instance, restart, git's editor, the password prompt, scripts). Here they ask `AppExecutable.Current`, which in an
AppImage is the AppImage file (`$APPIMAGE`), only when the executable is inside its mount (`$APPDIR`): the mount lives
only while the first process runs, under a new name each time, so a second instance or a `core.editor` would otherwise
point nowhere.

**OutputHistory** (`ported`). One output history for the app (`OutputHistory`, reimplementing GitUI's internal
`OutputHistoryModel`): `OutputHistoryDepth`, upstream's entry format and what it records (each process window run, the
trace output through upstream's `ISubscribableTraceListener`), shown as upstream's `OutputHistoryControl`, as a tab or a
panel, with its hotkey and settings. Differences: a run is recorded with the process window's title (e.g. "Fetch origin")
instead of git's command line, as an operation here may run several git commands; the panel has a fixed height (upstream's
follows its splitters); the shell runs fewer commands in a process window (a commit has none), so fewer runs are recorded.

## Not in this map

- The repository path box and the git discovery at startup have no upstream form to port; they are new code in
  `App.axaml.cs` and `XplatGitDiscovery.cs`. On Windows the discovery is upstream's own `SolveGitCommand` (core
  `Platform/WindowsGit.cs`; see ChecklistSettingsPage).
- The `RevisionReader` and `GitModule` engine code is used as is from the shadow projects, not reimplemented, so it has no
  row.
