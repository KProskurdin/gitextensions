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
| `src/app/GitUI/CommandsDialogs/FormBrowse.cs` | `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml`, `MainWindow.axaml.cs` (view: menu, toolbar, left panel, grid, Commit and Diff tabs); `src/xplat/GitExtensions.Xplat.Core/CommitHistory/CommitListViewModel.cs` (state) | `52d08e996` | partial |
| `src/app/GitUI/UserControls/RevisionGrid/RevisionGridControl.cs` | `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (`CommitList`); `src/xplat/GitExtensions.Xplat.Core/CommitHistory/GitCommitHistory.cs` (reads); `CommitListViewModel.cs` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormCommit.cs` | `src/xplat/GitExtensions.Xplat.App/CommitWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (commit), `RepositoryOperationsViewModel.cs` | `52d08e996` | partial |
| `src/app/GitUI/UserControls/FileStatusList*.cs` (toolbar, filter, NoFiles, item menu, `GetItemImageKey`); `src/app/GitUI/CommandsDialogs/RevisionDiffController.cs` (menu rules); `CommandsDialogs/Menus/CopyPathsToolStripMenuItem.cs`; `FormCommit.Designer.cs` (`toolbarStaged`, `SolveMergeconflicts`); `GitIgnoreDialog/GitLocalExcludeModel.cs` | `src/xplat/GitExtensions.Xplat.Core/Repository/FileStatusList.cs` (`FileStatusList`, `FileMenuState`), `GitIgnoreFile.cs`, `Operations/GitOperations.cs` (reset files to, stop tracking); `src/xplat/GitExtensions.Xplat.App/CommitWindow.FileLists.cs`, `CommitWindow.axaml`, `GitIgnoreWindow.axaml.cs` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormPush.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/PushDialog.cs`, `PushRejection.cs`, `GitOperations.cs` (push), `RepositoryOperationsViewModel.cs`, `RemoteRequests.cs`; `src/xplat/GitExtensions.Xplat.App/PushWindow.axaml(.cs)`, `PushRejectedFlow.cs`, `MainWindow.axaml(.cs)` (Push), `CommitWindow.axaml.cs` (Commit & push) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormPull.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/PullDialog.cs`, `GitOperations.cs` (pull, `RebasesMergeCommitAsync`), `RepositoryOperationsViewModel.cs`, `RemoteRequests.cs`; `src/xplat/GitExtensions.Xplat.App/PullWindow.axaml(.cs)`, `HelpImageView.cs`, `MainWindow.axaml(.cs)` (Pull) | `52d08e996` | partial |
| `src/app/GitUI/UserControls/HelpImageDisplayUserControl.cs`; `src/app/GitUI/GitExtensionsDialog.cs` (button strip colors and line); WinForms `GroupBox` | `src/xplat/GitExtensions.Xplat.App/HelpImageView.cs`; `App.axaml` (`Border.dialogFooter`, `HeaderedContentControl.groupBox`), `ThemeApplier.cs` (`DialogFooterBrush`, `DialogFooterLineBrush`) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormClone.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (clone), `RepositoryOperationsViewModel.cs`; `src/xplat/GitExtensions.Xplat.App/CloneWindow.axaml(.cs)` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormCreateBranch.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (create), `RepositoryOperationsViewModel.cs`; `MainWindow.axaml` (new branch box) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormCheckoutBranch.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (checkout, remote checkout), `RepositoryOperationsViewModel.cs`; `MainWindow.axaml` (Checkout) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormDeleteBranch.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (delete), `RepositoryOperationsViewModel.cs`; `MainWindow.axaml` (Delete) | `52d08e996` | partial |
| `src/app/GitUI/Editor/FileViewer.cs` | `src/xplat/GitExtensions.Xplat.Core/Diff/DiffParser.cs`, `DiffService.cs`, `DiffViewModel.cs`, `DiffCopy.cs`; `src/xplat/GitExtensions.Xplat.App/DiffView.axaml(.cs)`, `DiffView.Toolbar.cs`, `DiffWindow.axaml(.cs)` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/RevisionDiffControl.cs` | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/GitCommitHistory.cs` (`LoadFilesAsync`), `CommitListViewModel.cs` (`CommitFiles`); `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (changed files list, Show diff) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormFileHistory.cs` | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/FileBrowserViewModel.cs`, `GitCommitHistory.cs` (`LoadTreeAsync`, `LoadFileHistoryAsync`); `src/xplat/GitExtensions.Xplat.App/FileBrowserWindow.axaml(.cs)` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormBlame.cs` | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/BlameParser.cs`, `GitCommitHistory.cs` (`LoadBlameAsync`); `src/xplat/GitExtensions.Xplat.App/BlameWindow.axaml(.cs)` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormMergeBranch.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (`MergeAsync`, `AbortMergeAsync`), `RepositoryOperationsViewModel.cs`, `RepositoryViewModel.IsMerging`; `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (Merge into current, Abort merge) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormStash.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (apply, pop, drop by name), `RepositoryOperationsViewModel.cs`; `src/xplat/GitExtensions.Xplat.Core/Repository/GitRepositoryService.cs` (stash list); `MainWindow.axaml` (Stash, Apply, Pop, Drop) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormCreateTag.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (create), `RepositoryOperationsViewModel.cs`; `MainWindow.axaml` (tag list, Create) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormDeleteTag.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (delete), `RepositoryOperationsViewModel.cs`; `MainWindow.axaml` (Delete) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormCherryPick.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (cherry-pick), `RepositoryOperationsViewModel.cs`; `MainWindow.axaml` (Cherry-pick on the selected commit) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormResetChanges.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (reset), `RepositoryOperationsViewModel.cs`; `MainWindow.axaml` (Reset soft here, Reset mixed here) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormRebase.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (rebase, interactive rebase, abort, continue, skip, edit todo), `GitEditorCommand.cs`, `RepositoryOperationsViewModel.cs`, `RepositoryViewModel.IsRebasing`; `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (Rebase onto selected, grid menu "Rebase current branch on", warning bar buttons) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormEditor.cs` (and the `fileeditor` verb of `GitUICommands.RunCommand`) | `src/xplat/GitExtensions.Xplat.App/EditorWindow.axaml(.cs)`, `Program.cs`, `App.axaml.cs`; `src/xplat/GitExtensions.Xplat.Core/Editing/EditorFile.cs` | `52d08e996` | partial |
| `src/native/GitExtSshAskPass` (native askpass dialog, set as `SSH_ASKPASS` by `EnvironmentConfiguration`) | `src/xplat/GitExtensions.Xplat.App/AskPassWindow.axaml(.cs)`, `Program.cs`, `App.axaml.cs`; `src/xplat/GitExtensions.Xplat.Core/Operations/GitAskPass.cs`, `GitOperations.cs` (remote environment) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormRemotes.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (add and remove a remote), `RepositoryOperationsViewModel.cs`; `src/xplat/GitExtensions.Xplat.Core/Repository/GitRepositoryService.cs` (remote list); `MainWindow.axaml` (Remotes section) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormReflog.cs` | `src/xplat/GitExtensions.Xplat.Core/Repository/Reflog.cs` (entries of HEAD); `src/xplat/GitExtensions.Xplat.App/ReflogWindow.axaml(.cs)`; `MainWindow.axaml` (Reflog..., reset to an entry) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormRevertCommit.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (revert), `RepositoryOperationsViewModel.cs`; `MainWindow.axaml` (Revert) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormRenameBranch.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (rename), `RepositoryOperationsViewModel.cs`; `MainWindow.axaml` (Rename selected, with the new name in the branch box) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormResolveConflicts.cs` | `src/xplat/GitExtensions.Xplat.App/ConflictsWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (`RunMergeToolAsync`, `ResolveConflictsAsync`), `RepositoryOperationsViewModel.cs` (`MarkResolvedAsync`) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/BrowseDialog/FormGitCommandLog.cs` | `src/xplat/GitExtensions.Xplat.App/CommandLogWindow.axaml(.cs)` (reads upstream `CommandLog` as is) | `52d08e996` | partial |
| `src/app/GitUI/HelperDialogs/FormRemoteProcess.cs`, `FormProcess.cs` | `src/xplat/GitExtensions.Xplat.App/ProcessWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Core/Operations/GitOutputRunner.cs`, `RepositoryOperationsViewModel.cs` (`OutputLines`, `Cancel`) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormSettings.cs` (pages: appearance, commit dialog, git, git config) | `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml(.cs)`, `ThemeApplier.cs`; `src/xplat/GitExtensions.Xplat.Core/Settings/AppPreferences.cs`, `GitConfigService.cs` | `52d08e996` | partial |
| `src/app/GitUI/Theming/ThemeModule.cs`, `ThemePathProvider.cs`; `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/AppearanceSettingsPage.cs` (theme choice) | `src/xplat/GitExtensions.Xplat.Core/Settings/AppTheme.cs` (`UpstreamThemeService`, `AppThemePaths`); `src/xplat/GitExtensions.Xplat.App/ThemeApplier.cs`, `ThemeBrushes.cs`, `SettingsWindow.axaml(.cs)` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormAbout.cs` | `src/xplat/GitExtensions.Xplat.App/AboutWindow.axaml(.cs)` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/BrowseDialog/DashboardControl/Dashboard.cs`, `DashboardTheme.cs`, `UserRepositoriesList.cs`, `UserRepositoriesListController.cs`, `FormDashboardCategoryTitle.cs` | `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (`DashboardPanel`, Start > Recent repositories), `MainWindow.Dashboard.cs`, `ThemeApplier.cs` (dashboard colors); `src/xplat/GitExtensions.Xplat.Core/Repository/RecentRepositories.cs`, `RecentRepositoriesViewModel.cs`, `Dashboard.cs` | `52d08e996` | partial |
| `src/app/GitUI/LeftPanel/RepoObjectsTree.cs`, `RepoObjectsTree.Designer.cs`, `RepoObjectsTree.ContextActions.cs`, `ContextMenu/MenuItemsGenerator.cs`, `MenuItemsStrings.cs`, `LocalBranchTree.cs`, `RemoteBranchTree.cs`, `TagTree.cs`, `StashTree.cs`, `SubmoduleTree.cs`, `WorktreeTree.cs` and their nodes | `src/xplat/GitExtensions.Xplat.Core/Repository/RepoObjectsTree.cs`, `GitRepositoryService.cs` (ahead and behind, worktrees, remote URLs, merged branches); `src/xplat/GitExtensions.Xplat.App/MainWindow.LeftPanel.cs`, `MainWindow.axaml` (`LeftPanel`) | `afc512395` | partial |
| `src/app/GitUI/Hotkey/HotkeySettingsManager.cs` (FormBrowse and FormCommit defaults) | `src/xplat/GitExtensions.Xplat.App/Hotkeys.cs`, `HotkeyEditor.cs`; `src/xplat/GitExtensions.Xplat.Core/Settings/UpstreamHotkeys.cs` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormGitIgnore.cs`, `FormAddToGitIgnore.cs` | `src/xplat/GitExtensions.Xplat.App/GitIgnoreWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Core/Repository/GitIgnoreFile.cs` | `52d08e996` | partial |
| `src/app/GitUI/UserControls/RevisionGrid/FilterInfo.cs`, `FormRevisionFilter.cs` | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/RevisionFilter.cs`; `src/xplat/GitExtensions.Xplat.App/FilterWindow.axaml(.cs)`; `MainWindow.axaml` (branch choice, Filter...) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/WorktreeDialog/FormManageWorktree.cs`, `FormCreateWorktree.cs` | `src/xplat/GitExtensions.Xplat.App/WorktreesWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Core/Repository/GitRepositoryService.cs` (`GetWorktreesAsync`), `Operations/GitOperations.cs` (add, remove, prune) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormSubmodules.cs` | `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (Submodules section); `src/xplat/GitExtensions.Xplat.Core/Repository/GitRepositoryService.cs` (`GetSubmodules`), `Operations/GitOperations.cs` (update, sync) | `52d08e996` | partial |
| `src/app/GitUI/ScriptsEngine/ScriptsManager.cs`, `ScriptsManager.ScriptRunner.cs`, `ScriptOptionsParser.cs`, `ScriptInfo.cs`, `SimplePrompt.cs`, `FormFilePrompt.cs`; `src/app/GitUI/GitModuleForm.cs` (`ExecuteScriptCommand`) | `src/xplat/GitExtensions.Xplat.Core/Scripts/` (`ScriptDefinition`, `ScriptStore.cs` with `ScriptsXml`, `ScriptRunner`, `ScriptVariables`, `RepositoryScriptContext`); `src/xplat/GitExtensions.Xplat.App/ScriptHost.cs`; `MainWindow.axaml(.cs)` (user menu bar, grid Run script, event hooks); `CommitWindow.axaml.cs` (commit events) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/ScriptsSettingsPage.cs` | `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml(.cs)` (Scripts tab); `src/xplat/GitExtensions.Xplat.Core/Scripts/ScriptListEditor.cs`, `ScriptHelp.cs` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/SimpleHelpDisplayDialog.cs` | `src/xplat/GitExtensions.Xplat.App/HelpWindow.axaml(.cs)` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/AdvancedSettingsPage.cs`; `FormCheckoutBranch.cs` (default action), `FormCreateBranch.cs`, `FormRenameBranch.cs`, `FormCreateWorktree.cs` (name normalising), `FormCommit.cs` (`PushForced`) | `src/xplat/GitExtensions.Xplat.Core/Repository/BranchNames.cs`, `Settings/AppPreferences.cs`; `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml.cs`, `CommitWindow.axaml.cs`, `WorktreesWindow.axaml.cs`, `SettingsWindow.axaml(.cs)` (Advanced tab) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/GitSettingsPage.cs`, `FormFixHome.cs`, `SshSettingsPage.cs`; `src/app/GitCommands/Settings/AppSettings.cs` (`LoadSettings` sets `GIT_SSH`) | `src/xplat/GitExtensions.Xplat.Core/Settings/HomeSettings.cs`, `SshClients.cs`, `AppPreferences.cs`; `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml(.cs)` (Git and SSH tabs), `App.axaml.cs` (startup) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/SortingSettingsPage.cs`; `src/app/GitUI/LeftPanel/BaseRefTree.cs` (`OrderByPriority`), `LocalBranchTree.cs`, `RemoteBranchTree.cs` (order), `ContextMenu/GitRefsSortByContextMenuItem.cs`, `GitRefsSortOrderContextMenuItem.cs` | `src/xplat/GitExtensions.Xplat.Core/Repository/RefSorting.cs`, `BranchTree.cs`, `GitRepositoryService.cs`; `CommitHistory/RevisionSorting.cs`, `GitCommitHistory.cs`; `Settings/SortingTexts.cs`, `AppPreferences.cs`; `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml(.cs)` (Sorting tab), `MainWindow.axaml(.cs)` (branch tree menu) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/RevisionLinksSettingsPage.cs`; `src/app/GitUI/CommitInfo/CommitInfo.cs` (related links) | `src/xplat/GitExtensions.Xplat.Core/Settings/RevisionLinks.cs`, `UpstreamImages.cs` (stand-in); `CommitHistory/GitCommitHistory.cs` (links in the details); `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml(.cs)` (Revision links tab), `MainWindow.axaml(.cs)` (Related links) | `52d08e996` | partial |
| `src/app/GitUI/CommitInfo/CommitInfo.cs`, `CommitInfoHeader.cs`, `RefsFormatter.cs`; `src/app/GitUI/CommandsDialogs/RevisionGpgInfoControl.cs`; `FormBrowse.InitCommitDetails.cs` (tab images) | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/CommitInfo.cs` (`CommitInfoText`, `CommitInfoReader`, `BranchComparer`), `GitCommitHistory.cs` (commit data and message), `Settings/AppPreferences.cs` (`CommitInfoOptions`, `ShowGpgInformation`); `src/xplat/GitExtensions.Xplat.App/MainWindow.CommitInfo.cs`, `InfoTextBlock.cs`, `MainWindow.axaml` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/AppearanceFontsSettingsPage.cs`; `src/app/GitExtensions.Extensibility/FontParser.cs` (format) | `src/xplat/GitExtensions.Xplat.Core/Settings/FontSetting.cs`, `AppPreferences.cs`; `src/xplat/GitExtensions.Xplat.App/FontApplier.cs`, `SettingsWindow.axaml(.cs)` (Fonts tab), `App.axaml` and the views' font resources | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/FormBrowseRepoSettingsPage.cs`; `src/app/GitUI/UserControls/RevisionGrid/Columns/MessageColumnProvider.cs`, `AuthorNameColumnProvider.cs`, `DateColumnProvider.cs`, `CommitIdColumnProvider.cs` (`TryGetToolTip`) | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/RevisionTooltips.cs`, `GitCommitHistory.cs`; `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml(.cs)` (grid tooltips), `SettingsWindow.axaml(.cs)` (Browse repository window tab) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/BlameViewerSettingsPage.cs`; `src/app/GitUI/UserControls/BlameControl.cs` (`BuildAuthorLine`, gutter); `src/app/GitUI/CommandsDialogs/FormFileHistory.cs` (blame settings menu); `GitModule.Blame` (flags) | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/BlameOptions.cs`, `BlameParser.cs`, `GitCommitHistory.cs`; `src/xplat/GitExtensions.Xplat.App/BlameWindow.axaml(.cs)`, `SettingsWindow.axaml(.cs)` (Blame viewer tab) | `52d08e996` | partial |
| `src/app/GitUI/UserControls/RevisionGrid/Graph/Rendering/GraphRenderer.cs`, `SegmentRenderer.cs`; `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/DetailedSettingsPage.cs` (revision graph group) | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/GraphPainter.cs`, `CommitGraph.cs` (feeds upstream's `RevisionGraph`, linked unchanged), `Settings/UpstreamGraphLaneColor.cs` (stand-in); `src/xplat/GitExtensions.Xplat.App/GraphCell.cs`, `SettingsWindow.axaml(.cs)` (Detailed tab) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/GeneralSettingsPage.cs`, `AppearanceSettingsPage.cs`; `src/app/GitUI/UserControls/RevisionGrid/Columns/DateColumnProvider.cs`; `RevisionGridMenuCommands.cs` (author and relative date toggles) | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/CommitDateStyle.cs`, `GitCommitHistory.cs`; `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml(.cs)` (View menu, startup, commit button); `CloneWindow.axaml.cs`; `SettingsWindow.axaml(.cs)` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/DiffViewerSettingsPage.cs`; `src/app/GitUI/Editor/FileViewer.cs` (whitespace, context lines, entire file, `GetExtraDiffArguments`) | `src/xplat/GitExtensions.Xplat.Core/Diff/DiffOptions.cs`, `Settings/AppPreferences.cs`; `src/xplat/GitExtensions.Xplat.App/DiffView.axaml(.cs)`; `SettingsWindow.axaml(.cs)` (Diff viewer tab) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/CommitDialogSettingsPage.cs` | `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml(.cs)` (Commit dialog tab); `src/xplat/GitExtensions.Xplat.Core/Settings/AppPreferences.cs` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/ConfirmationsSettingsPage.cs`; `src/app/GitUI/MessageBoxes.cs` (`ConfirmSuppressible`, `ConfirmBranchCheckout`) | `src/xplat/GitExtensions.Xplat.Core/Settings/Confirmations.cs`, `AppPreferences.cs` (`Asks`, `SetAsks`); `src/xplat/GitExtensions.Xplat.App/ConfirmWindow.axaml(.cs)` (`AskAsync`); `SettingsWindow.axaml(.cs)` (Confirmations tab) | `52d08e996` | partial |
| `src/app/GitUI/Plugin/PluginRegistry.cs`, `FailedPluginWrapper.cs`; `src/app/GitUI/CommandsDialogs/FormBrowse.cs` (`RegisterPlugins`, `UpdatePluginMenu`, plugin parts of `SetGitModule`) | `src/xplat/GitExtensions.Xplat.Core/Plugins/PluginCatalog.cs`; `src/xplat/GitExtensions.Xplat.App/PluginHost.cs` (`Register`, `Unregister`); `MainWindow.axaml(.cs)` (Plugins menu) | `52d08e996` | partial |
| `src/app/GitUI/GitUICommands.cs` (the `IGitUICommands` plugins see) | `src/xplat/GitExtensions.Xplat.App/PluginHost.cs` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Plugins/PluginSettingsPage.cs`; `src/app/GitUI/SettingControlBindings/*.cs` | `src/xplat/GitExtensions.Xplat.Core/Plugins/PluginSettings.cs`; `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml(.cs)` (Plugins tab) | `52d08e996` | partial |
| `src/app/GitUI/UserControls/InteractiveGitActionControl.cs` | `src/xplat/GitExtensions.Xplat.Core/Repository/GitActionBanner.cs`, `GitRepositoryService.cs` (state), `RepositoryViewModel.cs`; `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml(.cs)` (`BisectBanner`, `StateBanner`) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/BrowseDialog/FormBisect.cs`; `src/app/GitUI/UserControls/RevisionGrid/RevisionGridControl.cs` (bisect items) | `src/xplat/GitExtensions.Xplat.App/BisectWindow.axaml(.cs)`, `MainWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (bisect), `RepositoryOperationsViewModel.cs` | `52d08e996` | partial |
| `src/plugins/CreateLocalBranches/CreateLocalBranchesForm.cs` | `src/xplat/plugins/CreateLocalBranches/CreateLocalBranchesForm.cs` (stand-in), `CreateLocalBranchesWindow.axaml(.cs)` | `52d08e996` | ported |
| `src/plugins/ProxySwitcher/ProxySwitcherForm.cs` | `src/xplat/plugins/ProxySwitcher/ProxySwitcherForm.cs` (stand-in), `ProxySwitcherWindow.axaml(.cs)` | `52d08e996` | ported |
| `src/plugins/FindLargeFiles/FindLargeFilesForm.cs` | `src/xplat/plugins/FindLargeFiles/FindLargeFilesForm.cs` (stand-in), `FindLargeFilesWindow.axaml(.cs)`, `LargeFileFinder.cs`, `LargeFileRow.cs`, `FindLargeFilesScript.cs` | `52d08e996` | ported |
| `src/plugins/DeleteUnusedBranches/DeleteUnusedBranchesForm.cs` | `src/xplat/plugins/DeleteUnusedBranches/DeleteUnusedBranchesForm.cs` (stand-in), `DeleteUnusedBranchesWindow.axaml(.cs)`, `BranchRow.cs` | `52d08e996` | ported |
| `src/plugins/ReleaseNotesGenerator/ReleaseNotesGeneratorForm.cs`, `HtmlFragment.cs` (writing to the clipboard) | `src/xplat/plugins/ReleaseNotesGenerator/ReleaseNotesGeneratorForm.cs` (stand-in), `ReleaseNotesGeneratorWindow.axaml(.cs)`, `HtmlClipboard.cs` | `52d08e996` | ported |
| `src/plugins/Gource/GourceStart.cs` | `src/xplat/plugins/Gource/GourceStart.cs` (stand-in), `GourceStartWindow.axaml(.cs)`; `src/xplat/plugins/Shared/UpstreamMessageBoxes.cs` | `52d08e996` | partial |
| `src/plugins/Statistics/GitImpact/FormImpact.cs`, `ImpactControl.cs` | `src/xplat/plugins/GitImpact/FormImpact.cs` (stand-in), `ImpactWindow.axaml(.cs)`, `ImpactGraph.cs` | `52d08e996` | ported |
| `src/plugins/Statistics/GitStatistics/FormGitStatistics.cs`, `PieChart/*.cs` | `src/xplat/plugins/GitStatistics/FormGitStatistics.cs` (stand-in), `StatisticsWindow.axaml(.cs)`, `PieChart.cs` | `52d08e996` | partial |
| `src/app/GitExtensions.Extensibility/Translations/Xliff/TranslationUtil.cs` (translating a form by its fields); `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/AppearanceSettingsPage.cs` (language) | `src/xplat/GitExtensions.Xplat.Ui/UpstreamTranslation.cs`; the plugin and repository host windows; `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml(.cs)` (Appearance tab) | `52d08e996` | partial |
| `src/app/GitUI/BuildServerIntegration/BuildServerWatcher.cs` | `src/xplat/GitExtensions.Xplat.Core/BuildServer/BuildServerWatcher.cs`, `BuildServerCatalog.cs`, `BuildServerCredentialStore.cs`; `src/xplat/GitExtensions.Xplat.App/MainWindow.BuildServer.cs` | `52d08e996` | partial |
| `src/app/GitUI/UserControls/RevisionGrid/Columns/BuildStatusColumnProvider.cs`; `RevisionGridControl.cs` (build report items, cell click); `RevisionGridMenuCommands.cs` (build status toggles) | `src/xplat/GitExtensions.Xplat.Core/BuildServer/BuildStatusCell.cs`, `CommitHistory/CommitListViewModel.cs`; `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml`, `MainWindow.BuildServer.cs`, `BuildStatusBrushConverter.cs` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/BuildServerIntegrationSettingsPage.cs` | `src/xplat/GitExtensions.Xplat.App/SettingsWindow.axaml`, `SettingsWindow.BuildServer.cs` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormSettings.cs`, `FormSettings.Designer.cs` (page tree, title, buttons, instant save notice); `src/app/GitUI/CommandsDialogs/SettingsDialog/SettingsTreeViewUserControl.cs`, `SettingsGroupPages.cs`, `Pages/GitRootIntroductionPage.cs`, `Plugins/PluginRootIntroductionPage.cs` | `src/xplat/GitExtensions.Xplat.Core/Settings/SettingsPageTree.cs`; `src/xplat/GitExtensions.Xplat.App/SettingsWindow.Tree.cs`, `SettingsWindow.axaml(.cs)` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/SettingsPageHeader.cs`, `SettingsPageWithHeader.cs`, `DistributedSettingsPage.cs`; `Pages/DetailedSettingsPage.cs` (`gbRevisionGraph` enabled at the global level) | `src/xplat/GitExtensions.Xplat.App/SettingsSourceHeader.cs`, `SettingsWindow.Headers.cs`, `SettingsWindow.BuildServer.cs`, `SettingsWindow.axaml.cs` (revision links and plugin levels) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/GitConfigSettingsPage.cs`, `GitConfigBaseSettingsPage.cs`; `src/app/GitCommands/DiffMergeTools/DiffMergeToolConfigurationManager.cs` (`ConfiguredMergeTool`, `ConfiguredDiffTool`) | `src/xplat/GitExtensions.Xplat.App/SettingsWindow.GitConfig.cs`, `SettingsWindow.axaml` (`GitConfigTab`); `src/xplat/GitExtensions.Xplat.Core/Settings/GitConfigService.cs` (`ConfigScope.Effective`, `ConfigScope.System`) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/SettingsDialog/Pages/ChecklistSettingsPage.cs`; `src/app/GitExtUtils/GitUI/Theming/OtherColors.cs` (row colors) | `src/xplat/GitExtensions.Xplat.Core/Settings/SettingsChecklist.cs`; `src/xplat/GitExtensions.Xplat.App/SettingsWindow.Checklist.cs`, `SettingsWindow.axaml` (`ChecklistTab`) | `52d08e996` | partial |
| `src/app/GitUI/HelperDialogs/FormBuildServerCredentials.cs` | `src/xplat/GitExtensions.Xplat.App/BuildServerCredentialsWindow.axaml(.cs)` | `52d08e996` | ported |
| `src/plugins/BuildServerIntegration/AppVeyorIntegration/Settings/AppVeyorSettingsUserControl.cs` | `src/xplat/plugins/BuildServerIntegration/AppVeyorIntegration/Settings/AppVeyorSettingsUserControl.axaml(.cs)` | `52d08e996` | ported |
| `src/plugins/BuildServerIntegration/AzureDevOpsIntegration/Settings/SettingsUserControl.cs` | `src/xplat/plugins/BuildServerIntegration/AzureDevOpsIntegration/Settings/SettingsUserControl.axaml(.cs)` | `52d08e996` | ported |
| `src/plugins/BuildServerIntegration/GitHubActionsIntegration/Settings/GitHubActionsSettingsUserControl.cs` | `src/xplat/plugins/BuildServerIntegration/GitHubActionsIntegration/Settings/GitHubActionsSettingsUserControl.axaml(.cs)` | `52d08e996` | ported |
| `src/plugins/BuildServerIntegration/GitlabIntegration/Settings/GitlabSettingsUserControl.cs` | `src/xplat/plugins/BuildServerIntegration/GitlabIntegration/Settings/GitlabSettingsUserControl.axaml(.cs)` | `52d08e996` | ported |
| `src/plugins/BuildServerIntegration/JenkinsIntegration/Settings/JenkinsSettingsUserControl.cs` | `src/xplat/plugins/BuildServerIntegration/JenkinsIntegration/Settings/JenkinsSettingsUserControl.axaml(.cs)` | `52d08e996` | ported |
| `src/plugins/BuildServerIntegration/TeamCityIntegration/Settings/TeamCitySettingsUserControl.cs`, `TeamCityBuildChooser.cs` | `src/xplat/plugins/BuildServerIntegration/TeamCityIntegration/Settings/TeamCitySettingsUserControl.axaml(.cs)`, `TeamCityBuildChooser.axaml(.cs)` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/RepoHosting/ForkAndCloneForm.cs` | `src/xplat/GitExtensions.Xplat.App/RepositoryHosts/ForkAndCloneWindow.axaml(.cs)` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/RepoHosting/ViewPullRequestsForm.cs`, `DiscussionHtmlCreator.cs` | `src/xplat/GitExtensions.Xplat.App/RepositoryHosts/ViewPullRequestsWindow.axaml(.cs)` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/RepoHosting/CreatePullRequestForm.cs` | `src/xplat/GitExtensions.Xplat.App/RepositoryHosts/CreatePullRequestWindow.axaml(.cs)` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormBrowse.cs` (repository hosts menu); `src/app/GitUI/GitUICommands.cs` (repository host methods); `src/app/GitUI/UserControls/BlameControl.cs` (`ConfigureRepositoryHostPlugin`) | `src/xplat/GitExtensions.Xplat.App/MainWindow.RepositoryHosts.cs`, `PluginHost.cs`, `RepositoryHosts/RepositoryHostMenus.cs`, `BlameWindow.axaml.cs` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormCommit.cs` (commit templates menu); `src/app/GitCommands/CommitTemplateManager.cs` (used as is) | `src/xplat/GitExtensions.Xplat.Core/Repository/CommitTemplates.cs`; `src/xplat/GitExtensions.Xplat.App/CommitWindow.axaml(.cs)` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/CommitDialog/FormCommitTemplateSettings.cs` | `src/xplat/GitExtensions.Xplat.App/CommitTemplateSettingsWindow.axaml(.cs)`; `src/xplat/GitExtensions.Xplat.Core/Repository/CommitTemplates.cs`, `CommitMessageValidation.cs` | `52d08e996` | ported |
| `src/app/GitUI/CommandsDialogs/FormCommit.cs` ("Commit message" menu, reset buttons, `IsCommitMessageValid`, `FormatAllText`, conventional commits) | `src/xplat/GitExtensions.Xplat.Core/Repository/PreviousCommitMessages.cs`, `CommitMessageValidation.cs`, `ConventionalCommits.cs`; `src/xplat/GitExtensions.Xplat.App/CommitWindow.axaml(.cs)`, `ResetChangesWindow.axaml(.cs)` | `52d08e996` | ported |
| `src/app/GitUI/SpellChecker/EditNetSpell.cs` (auto-completion); `src/app/GitUI/AutoCompletion/*.cs`, `SpellChecker/WordAtCursorExtractor.cs`, `SpellCheckerHelper.cs` (linked as is) | `src/xplat/GitExtensions.Xplat.Core/Repository/CommitMessageAutoCompletion.cs`; `src/xplat/GitExtensions.Xplat.App/CommitWindow.AutoComplete.cs` | `52d08e996` | partial |
| `src/app/GitUI/Avatars/*.cs` (`AvatarService` and its providers; `TemplateFormatter.cs`, `HexString.cs` linked as is); `src/app/GitUI/UserControls/AvatarControl.cs` | `src/xplat/GitExtensions.Xplat.Core/Avatars/AvatarService.cs`, `AuthorInitials.cs`; `src/xplat/GitExtensions.Xplat.App/AvatarView.cs`, `SettingsWindow.Appearance.cs` | `52d08e996` | partial |
| `src/app/GitUI/UserControls/PathFormatter.cs` | `src/xplat/GitExtensions.Xplat.Core/Repository/FilePathDisplay.cs`; `src/xplat/GitExtensions.Xplat.App/FilePathTrimming.cs` | `52d08e996` | ported |
| `src/app/GitUI/RepositoryCurrentBranchNameProvider.cs`; `DashboardControl/UserRepositoriesListController.cs` (`GetCurrentBranchName`) | `src/xplat/GitExtensions.Xplat.Core/Repository/RecentRepositories.cs` (`RecentRepositoryBranches`), `RecentRepositoriesViewModel.cs` | `52d08e996` | ported |
| `src/app/GitUI/UserControls/RevisionGrid/QuickSearchProvider.cs`; `src/app/GitCommands/Git/GitRevisionTester.cs` (`Matches`) | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/QuickSearch.cs`; `src/xplat/GitExtensions.Xplat.App/MainWindow.QuickSearch.cs` | `52d08e996` | ported |
| `src/app/GitUI/UserControls/RevisionGrid/Graph/LaneInfoProvider.cs`; `Graph/BranchFinder.cs` (linked as is) | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/LaneInfoProvider.cs`, `CommitGraph.cs`; `src/xplat/GitExtensions.Xplat.App/GraphCell.cs` | `52d08e996` | ported |
| `src/app/GitUI/UserControls/RevisionGrid/ArtificialCommitChangeCount.cs`; `RevisionGridControl.cs` (artificial rows) | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/ArtificialCommits.cs`, `GitCommitHistory.cs`, `CommitListViewModel.cs` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/BuildReportTabPageExtension.cs` | `src/xplat/GitExtensions.Xplat.App/MainWindow.BuildServer.cs`, `MainWindow.axaml` (`BuildReportTab`) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormBrowse.Designer.cs` (`ToolStripMain`), `FormBrowse.cs` (toolstrip parts of `InitMenusAndToolbars`, `RefreshDefaultPullAction`, the layout toggles, the title); `Menus/WorkingDirectoryToolStripSplitButton.cs`; `ToolStripPushButton.cs`; `src/app/GitUI/UserControls/RepoStateVisualiser.cs` | `src/xplat/GitExtensions.Xplat.App/MainWindow.Toolbar.cs`, `MainWindow.axaml` (toolstrip), `UpstreamImages.cs`; `src/xplat/GitExtensions.Xplat.Core/Repository/RepoState.cs`, `RecentRepositories.cs` (`Caption`), `BrowseWindowTitle.cs` | `52d08e996` | partial |
| `src/app/GitUI/UserControls/FilterToolBar.cs`, `FilterToolBar.Designer.cs` | `src/xplat/GitExtensions.Xplat.App/MainWindow.FilterToolBar.cs`, `MainWindow.axaml` (`FilterToolStrip`); `src/xplat/GitExtensions.Xplat.Core/CommitHistory/RevisionFilter.cs` | `afc512395` | partial |
| `src/app/GitUI/Shells/ShellProvider.cs`, `BashShell.cs`, `CmdShell.cs`, `PwshShell.cs`, `PowerShellShell.cs`; `FormBrowse.InitMenusAndToolbars.cs` (`FillUserShells`), `FormBrowse.cs` (`userShell_Click`) | `src/xplat/GitExtensions.Xplat.Core/Platform/TerminalChoices.cs`; `src/xplat/GitExtensions.Xplat.App/MainWindow.Toolbar.cs` (`FillTerminals`) | `afc512395` | partial |
| `src/app/GitUI/UserControls/RevisionGrid/Columns/MessageColumnProvider.cs`, `RevisionGridRefRenderer.cs`, `RefLabelShape.cs`, `RefLabelIcon.cs`, `RevisionDataGridView.cs` (row colors), `AuthorRevisionHighlighting.cs`, `RevisionGridMenuCommands.cs` (`GetViewMenuCommands`) | `src/xplat/GitExtensions.Xplat.Core/CommitHistory/GridLabels.cs`, `CommitListViewModel.cs` (`CommitListItem`, `ArtificialChanges`), `GitCommitHistory.cs` (`LoadRefLabels`); `src/xplat/GitExtensions.Xplat.App/RefLabelView.cs`, `MainWindow.GridView.cs`, `MainWindow.axaml` (row template, View menu), `ThemeApplier.cs` (grid colors) | `afc512395` | partial |

## Notes per row

**FormBrowse** (`partial`). Layout reworked 2026-10-06 to follow the upstream form. Ported: the menu bar (Start,
Repository, Commands, Tools, Help) with upstream's default hotkeys; a toolbar (path, Open, Refresh, Commit with the number of
changes, Pull, Push, Fetch, Clone, folder, terminal, filter, search); the left panel; the revision grid; a Commit tab
(details, copy hash and message) and a Diff tab (changed files with the selected file's diff inline); a status bar; a
warning bar while a merge or rebase is stopped or conflicts exist; the dashboard of recent repositories while none is open;
Close repository; the repository refresh after every operation; and, as upstream, a status refresh when the window is
activated again (at most every 2 seconds), so files edited in other programs show up. Not ported: the GPG and Console
tabs, the branch and repository dropdowns of the toolbar, and the Navigate and View menus (Plugins: see the PluginRegistry row; user
scripts: see the ScriptsManager row). Window size, position and maximized state are kept (upstream's `WindowPositions.xml` format, under the new shell's own
window names, `WindowPlacementTracker`). The File tree tab is ported (the folder tree at the selected commit, and the selected
file's content). Added 2026-10-10: the View menu's graph items (draw non-relatives gray, highlight the selected branch,
show the artificial commits), the Build Report tab, the status bar's ahead/behind counts and stash count, and the Pull
button following the default pull action. Added 2026-10-11: the author's avatar beside the commit details
(`showauthorgravatar`).

**RevisionGridControl** (`partial`). Added 2026-10-06: columns (graph and message, author, date, commit); HEAD, branch,
remote-branch and tag labels colored by kind; a context menu (copy hash or message, create a branch or tag here, browse
files, cherry-pick, revert, reset soft, mixed or hard); lanes that keep one color for their whole length; upstream'"'"'s bisect items at the top of the context menu (mark the selected
revision bad, good or skipped, stop bisect), shown only while a bisect runs, and the good and bad marks as labels in green
and red where upstream draws icons, with upstream'"'"'s tooltip texts (2026-10-09); 5000 commits
loaded and scrolled at 60 fps (PLAN.md M3.5). Earlier: a read-only list of the first 500 commits from `HEAD`,
with short hash, subject, author and date, using the shared `RevisionReader.GetLog`. Selecting a row
loads its details through `RevisionReader.GetRevision`. Also ported: search of the whole history by message, ignoring case (since 2026-10-13 the filter toolstrip's "Filter:" box, see **FilterToolBar**; git log --grep); a lane graph beside each commit (since 2026-10-07 upstream's `RevisionGraph` with the ported renderer, see
GraphRenderer below). Added 2026-10-10: "Load more" reads only the next page (`--skip`), up to upstream's commit limit
(`maxrevisiongraphcommits`); typing in the grid searches it as upstream's quick search (QuickSearchProvider row); the
working directory and index rows; the selected row is kept when the rows are read again. Added 2026-10-11: the author's
avatar at the start of the author cell (`showrevisiongridauthoravatarcolumn`; upstream has a column of its own). Not
ported: column layout and sorting, and the upstream filter bar (the box in the window filters only the loaded commits, by
hash, subject or author).

**FormStash** (`partial`). Ported: save with an optional message, a stash list, and apply, pop and drop of the selected
stash by its name. Also ported: including untracked files. Also ported: stashing only the selected files (Stash selected). Also ported: keeping the index (Keep index). Not ported: branching from a
stash. Also ported: the whole stash as a diff (Show diff, first parent only). Changed 2026-10-13: the left panel's stash
boxes went with the left panel rebuild (see RepoObjectsTree); stashing from the toolbar, menu and left panel is upstream's
`StashSave` (no message, untracked files as `includeUntrackedFilesInManualStash` says), and the stash message and keep
index choices wait for the FormStash window, which is not ported.

**FormCreateTag** (`partial`). Ported: a lightweight tag at the selected commit (or HEAD), listed in the window. Also ported: annotated tags when a message is entered. Not ported: signing and the tag-on-remote option.

**FormDeleteTag** (`partial`). Ported: delete a local tag selected in the list, and (2026-10-06) delete it on the remote the
branch tracks after a confirmation, with upstream's `push <remote> :refs/tags/<tag>`; the local tag is kept. Not ported: the
remote choice in the dialog and deleting both at once.

**FormCherryPick** (`partial`). Ported: cherry-pick the selected commit onto the current branch. Not ported: the
multi-commit selection, the no-commit option, the append-to-message option, and the commit-message edit step.

**FormResetChanges** (`partial`). Ported: reset the current branch to the selected commit with the soft and mixed
modes. Also ported: deleting selected untracked files after a confirmation (Delete untracked), the hard mode, behind a confirmation (Reset hard here), and
discarding the unstaged edits of selected tracked files, after a confirmation. Added 2026-10-11: the form itself
(`ResetChangesWindow`, upstream's texts and its rule for the "Also delete new files and/or directories" check box) for the
commit window's "Reset all changes" and "Reset unstaged changes", which then delete untracked files and directories
(`git clean -d`) when asked.

**FormRebase** (`partial`). Ported: rebase the current branch onto a selected local or remote branch, abort a stopped
rebase, and continue one after the conflicts are staged. Added 2026-10-06: the grid menu "Rebase current branch on" >
"Selected commit" and "Selected commit interactively..." (with upstream's confirmation, skipped when upstream's
`DontConfirmRebase` is set), and Skip and Edit todo while a rebase is stopped, as the upstream form shows them for any rebase in
progress (since 2026-10-09 under More... in the action bar, where upstream'"'"'s More... opens this form). The interactive rebase uses upstream's `Commands.Rebase` (autosquash from git's
`rebase.autosquash`, as the form's default) and runs with live output. Difference: upstream makes git call it through the
global `core.editor` (written by its settings check); the new shell sets `GIT_SEQUENCE_EDITOR` and `GIT_EDITOR` for the one
git call instead (`GitEditorCommand`), so the user's git config is not changed. As upstream, continue and skip then open the
editor for messages (a resolved commit, a reword or squash). Not ported: the rebase dialog itself (range, `--onto`,
ignore date, committer date, update refs, autostash, preserve merges), and the rebase-in-progress state from other tools
(only git's own folders are read).

**FormEditor** (`partial`). Ported 2026-10-06: the app's `fileeditor <file>` verb opens only the editor window; it shows the
file, saves with the toolbar button or Ctrl+S (Cmd+S on macOS), and on close with unsaved changes asks Save, Don't save or
Cancel. The exit code is upstream's: 0 when the file was saved or left unchanged, -1 when changes were discarded, so git
aborts the rebase. The file keeps its line ending and byte order mark (`EditorFile`). Not ported: the rebase todo syntax
highlighting and line numbers (no editor control for Avalonia 12, see PLAN.md M3), the read-only and line-number options,
and `i18n.filesEncoding` (files are read as UTF-8).

**FormRevertCommit** (`partial`). Ported: revert the selected commit with git's default message. Not ported: the
no-commit option, the mainline choice for merge commits, and the message edit step.

**FormRenameBranch** (`partial`). Ported: rename a local branch, including the checked-out one. Not ported: the
remote branch rename, and the check that the new name is valid before git runs.

**FormRemotes** (`partial`). Ported: list the remotes, add one by name and URL or path, remove the selected one after a
confirmation, and (2026-10-06) rename it (`git remote rename`, as upstream's `GitModule.RenameRemote`) and change its URL
(the current URL is read from the repository's config). Not ported: the push URL, pruning, the per-remote settings, and
disabling a remote.

**FormReflog** (`partial`). Ported: the list of HEAD's reflog (the last 200 entries, with hash, selector and action), and a mixed
reset of the branch to the chosen entry. Not ported: the reflog of other refs, the reset modes other than mixed, the
branch and tag actions from the list, and the entry count setting.

## Not in this map

- The repository path box and the git discovery at startup have no upstream form to port; they are
  new code in `App.axaml.cs` and `XplatGitDiscovery.cs`.
- The `RevisionReader` and `GitModule` engine code is used as-is from the shadow projects, not
  reimplemented, so it has no row.

**FormCommit** (`partial`). Moved 2026-10-06 from the main window into its own window, laid out as upstream: unstaged
files above staged files, the selected file's diff, and the message below it. Ported: stage and unstage of selected
changes or of all changes (Ctrl+S stages all, as upstream), discard, delete untracked, use ours or theirs for conflicts,
stash of the selected files, commit with a message, Ctrl+Enter in the message box, commit and push (the push runs only
after a successful commit), an optional author, amend (checking it loads the HEAD message and unchecking restores the
typed one), sign-off, the upstream setting that closes the window after a commit, a typed message that survives closing
the window, and the repository refresh after the commit. Later on 2026-10-06: staging and unstaging of selected lines
(also staging lines of an untracked file and unstaging lines of a newly added one, as upstream's FileViewer),
Ignore... for untracked files (see FormAddToGitIgnore), and Open in diff tool. The message is kept where upstream keeps it,
through upstream's `CommitMessageManager` used as is (`.git/COMMITMESSAGE`, or `.git/MERGE_MSG` during a merge, so a stopped
merge starts from git's merge message and both apps share the draft). Added 2026-10-10: upstream's commit message checks
before a commit (each failed check asks Yes or No, upstream's texts) and the formatting while typing (empty second line,
wrapping), the message box menu's "Word wrap (except subject line)", the Conventional Commits items of the templates menu
(Ctrl+T, Ctrl+Shift+T with a scope, the footers), the user scripts in the file lists' menus, and the before and after
commit script events. Added 2026-10-11: the "Commit message" menu (the last messages on HEAD, `CommitDialogNumberOfPreviousMessages`
of them, and the last message committed from the window in any repository first, `lastCommitMessage`; "Show only my
messages" with upstream's `--author` pattern; "Generate a list of changes in submodules" for the staged submodules, which
leaves out a submodule added or removed in the commit where upstream throws), "Reset all changes" and "Reset unstaged
changes" (upstream's `FormResetChanges` question, then `GitModule.ResetAllChanges`'s steps with their errors reported),
staging without git's line ending check when "Show errors when staging files" is off (`-c core.safecrlf=false`), the
message box's auto-completion (see the EditNetSpell row) and, with "Compose commit messages in Commit dialog" off, a
commit whose message git asks for in the editor (`GitEditorCommand`). Not ported: GPG signing, skipping hooks, the
spelling checker, and the commit kinds other than normal and amend (fixup, squash) with their "modify commit message"
button.
Changed 2026-10-13 (UI parity U3.1): each list is upstream's FileStatusList without its tree view: the toolbar (refresh
on the working directory list; group by path, extension or status, kept in upstream's `DiffListSortType` and shared by
both lists; the settings menu with "Show untracked files", "Edit ignored files" and "Edit locally ignored files"), the
regular expression filter box (an invalid expression shows its error as a tooltip and filters nothing), upstream's status
images before each path, and the NoFiles texts. `toolbarStaged` sits between the lists (Unstage all and Unstage on the
left, Stage and Stage all on the right); the rows of buttons under the lists are gone. The lists' menus are upstream's
item menu in its order, with the items its rules show for the selection: stage or unstage selected (bold), reset file(s)
to the index or to HEAD (a file HEAD does not have is only unstaged), open with the diff tool, open and edit the working
directory file, delete file(s) (after upstream's question; staged files are unstaged first), copy paths (relative POSIX
and native, full native, WSL and Cygwin), show in folder, file history, blame, add to .gitignore or .git/info/exclude,
stop tracking, and the scripts. "Stash selected" stays in the working directory list's menu, though upstream has it only
in its stash window, which the new shell lacks. Upstream's "There are unresolved merge conflicts" button over the diff
opens the conflicts window, replacing the "Use ours" and "Use theirs" buttons. Not ported: the tree views and their
options (dense tree, group nodes), collapsing groups, show ignored, skip-worktree and assumed-unchanged files, the
submodule items, "Reset chunk of file" and "Interactive add", "Open ... with", rename or move, find file, skip worktree
and assume unchanged, the git-grep search, and refresh on form focus.
Changed 2026-10-13 (UI parity U3.2): the bottom panel is upstream's: the column of commit buttons (Commit with upstream's
image, a key while the commit is signed; Commit & push, which reads "Commit & force push" after an amend with
`CommitAndPushForcedWhenAmend` and "Push", only pushing, when there is nothing to commit; Amend commit with upstream's
AmendPanel: Reset author and Reset soft, which after the amend question resets softly to HEAD~1; Stash staged changes; the
two reset buttons), and the message toolstrip (Commit message, Commit templates, Create branch, and Options on the right).
Options holds upstream's items: close after each commit, close when all changes are committed (on by default, as upstream:
the window closes after a commit that leaves no unstaged changes), refresh on focus, select a staged file on entering the
message box, sign-off, the author box with its label (which clears it), no verify (cleared after each commit), and the GPG
choice with the key box (shown for "Sign with specific GPG"; upstream shows it for "Sign with default GPG", which looks
like a slip). The commit passes `--no-verify`, the GPG flags and `--reset-author` as upstream's `Commands.Commit` builds
them. The button column grows past upstream's 171 pixels when the font needs it. The message box's placeholder is
upstream's "Enter commit message". Not ported: "Stage in superproject" and hiding Stash staged for a git without
`stash --staged`.
Changed 2026-10-13 (UI parity U3.3): upstream's status strip (`CommitStatusBar` in core): the committer as upstream's
`UpdateAuthorInfo` writes it (with the author when one is typed, "/user.name not configured/" for a missing setting; read
on opening, activation and leaving the author box), the branch with an arrow and where it pushes (the tracked branch, or
origin or the first remote with "(untracked)", or "(remote not configured)"), the staged count of all changes ("1/3"), and
the caret's line and column in the message. The title is upstream's "Commit to <branch> (<working directory>)". The
operation messages, which upstream shows in its process dialog, follow the committer. Not ported: the committer and
remote links (upstream opens its Git config settings page and its remotes dialog, which the new shell lacks) and the
progress bar.

**FormPush** (`partial`). Ported: push of the current branch to the remote it tracks (origin when it tracks none), with upstream tracking. Not ported:
recursive submodules, pushing several branches at once, and the tags tab. Added 2026-10-06: the progress dialog (see
FormRemoteProcess); Push tags uses the upstream `Commands.PushTag` builder; a push dialog (`PushWindow`, Commands > Push...,
Ctrl+Up as upstream) with the remote, the local and remote branch, force or force with lease, and tracking, built with
upstream's `Commands.Push`. The toolbar's Push stays a quick push of the current branch (upstream's QuickPush,
Ctrl+Shift+Up).
Rebuilt 2026-10-14 (UI-PARITY U5): the push window is upstream's FormPush: "Push to" a remote (with Manage remotes) or a
URL (with Browse), and its three tabs. "Push branches": the branch list ("[ All ]", HEAD and the local branches), the
remote branch proposed as upstream's `BranchSelectedValueChanged` (the remote's push target, the tracked branch, or the
remote's prefix and the branch's name) with the remote's other branches, and the options behind "Show options" (or always,
upstream's `AlwaysShowAdvOpt`): force with lease and force push (excluding each other), recursive submodules (stored as
upstream's `RecursiveSubmodules`), replace tracking reference, and create pull request after push (enabled when a
repository host plugin knows the repository). "Push tags": a tag or "[ All ]", with the force box bound to force with
lease as upstream. "Push multiple branches": upstream's grid (local and remote branch, ahead/behind or "=" and "<>", Push,
Force and Delete excluding each other, the remote's branches without a local branch to delete) and the Push column's
menu (Unselect all, Select tracked, Select all). Pull on the left opens the pull dialog over it. The checks before
pushing are upstream's (`PushDialog`): the URL, a configured remote, a tag, a branch; a branch the remote does not have
is confirmed (`PushNewBranch`), a branch without a tracking reference is offered one (`AddTrackingRef`), and a force push
is offered as force with lease. A rejected push is handled as upstream's `HandlePushOnExit` (`PushRejection`,
`PushRejectedFlow`), for the dialog, the quick push and the commit window's Commit & push: when the current branch was
rejected, upstream's task dialog over the process window offers a pull with the default action, with rebase or with
merge, or a force push with lease (another rejected ref: the force push only); the pull or force is followed by the push
again, the merge commit check comes first when the default action is rebase, and "Don't show me this message again"
keeps the choice in upstream's `AutoPullOnPushRejectedAction` (a kept force push asks again, as upstream). The task
dialog itself got upstream's command links and verification box (shim `TaskDialogCommandLinkButton`,
`TaskDialogVerificationCheckBox`; `MessageBoxWindow`). Not ported: Manage remotes (disabled until the remotes window
exists), the URL history, Load SSH key and Pageant, `GetRemoteBranchesDirectlyFromRemote` (the rows come from the known
remote branches), the Azure DevOps pull request link, and the rejection handling of a "Push multiple branches" push
(upstream adds the force to its command line).

**FormPull** (`partial`). Ported: pull of the current branch from the remote it tracks (origin when it tracks none), merge or rebase. Also ported: prune of remote-tracking branches on fetch (the Prune checkbox, next to Fetch). Not ported:
fetch tags, unshallow, fetch all remotes, and the merge-commit message options. Added 2026-10-06: the progress dialog
(see FormRemoteProcess), and a pull dialog (`PullWindow`, Commands > Pull/Fetch..., Ctrl+Down as upstream) with the remote and
branch, merge, rebase or fetch only, prune, and auto stash. Upstream's FormPull stashes and pops itself; the port passes
git's `rebase.autoStash` and `merge.autoStash` for that one command instead.
Rebuilt 2026-10-14 (UI-PARITY U5): the pull window is upstream's FormPull as a GitExtensionsDialog (the button strip at the
bottom): the help picture on the left (`HelpImageView`, upstream's `HelpImageDisplayUserControl`: the picture per action,
the fast forward one on hover for a merge, "Hide help" and the button that shows it again, remembered per dialog as
upstream's `HelpIsExpanded<dialog>`, hidden by `DontShowHelpImages`), the "Pull from" group (Remote with "[ All ]", or a
URL or folder), the "Branch" group (local branch, editable for a fetch; remote branch with the remote's known branches),
the merge options with their images, the tag options, "Download full history" for a shallow clone, the two prune options,
and Solve conflicts, Stash changes, Auto stash and Pull (with its arrow, "Fetch" for a fetch) at the bottom; the title
"Pull (path)" or "Fetch (path)". Upstream's rules (`PullDialog`): the action from Settings > General, the remote asked
for, the branch's or the first; which options apply to which action; the checks before pulling (a missing source or
remote, "*" without fetch, the rebase of a merge commit asked first, a pull on a detached HEAD asked first, a missing
remote branch offered as the local branch's name, upstream's `CalculateLocalBranch`); and its fetch and pull arguments
(`FetchCmd`, `PullCmd` with tags, unshallow, local branch and prune tags). Auto stash and the action are stored as
upstream's `autostash` and `FormPullAction`. Changed: the dialog's prune no longer asks first (upstream asks only for the
quick "Fetch and prune all"). Not ported: Manage remotes and Stash changes (shown disabled until the remotes and stash
windows exist), "Checkout branch" in the detached HEAD question, the URL history, the help pictures' dark theme
adaptation, the submodule update after a pull and the prune offer when a pull is rejected for a removed branch.

**FormClone** (`partial`). Ported: clone from a URL or local path into a new folder, then open it, with a folder picker
and git's progress shown while it runs (see FormRemoteProcess). Not ported: branch choice, depth, and recursive submodules.

**FormCreateBranch** (`partial`). Ported: create a branch at HEAD and check it out. Not ported: creating from a
specific revision, the branch name rules shown in the form, and the no-checkout option.

**FormCheckoutBranch** (`partial`). Ported: check out a local branch, and a remote branch as a tracking branch.
Since 2026-10-06 also the local-change handling: when tracked files have changes, a dialog (`CheckoutWindow`) offers keep,
merge, stash (stash, checkout, pop, as upstream does around the checkout) or discard, built with upstream's
`Commands.Checkout(LocalChangesAction)`; the choice is kept in upstream's `checkoutbranchaction` setting. Not ported: the
local-change choice for remote branches, the "always use the default" option, and checkout of tags. A commit can be checked
out detached from the grid's context menu (upstream's Checkout revision), with the same local-change dialog.

**FormDeleteBranch** (`partial`). Ported: delete a local branch that is not checked out, with a force delete after a confirmation, and
delete a remote branch on its own remote after a confirmation (`git push --delete`); origin/HEAD is never deleted. Not ported: force delete
from the window, the merged-branches list, and the confirmation dialog.

## Review against upstream 52d08e996 (2026-10-03)

Upstream changes to the mapped files between `0174ba1cc` and `52d08e996` were read and found to be outside the
reimplemented behavior, so the rows now use `52d08e996` as their base:

- `f425acf4a`, `35ae6a77f` (GPG verification display in `FormBrowse` and `RevisionGpgInfoControl`): skipped,
  the commit details panel does not show signatures.
- `1c98cad37` (grid tooltip in `RevisionGridControl`): skipped, the commit list has no tooltips.
- `36d8b34f6`, `d2b1e3528` (`FormCommit` focus scrolling and status bar height): skipped, layout of the WinForms file lists.
- `af375e1be` (`FormClone` option to init submodules): skipped, the clone form does not expose submodule options.

**FileViewer** (`partial`). Ported: the diff of one file, for a commit or the working tree (staged or unstaged), with
added, removed, hunk and header lines colored, with an Ignore whitespace toggle (git diff -w). An untracked file is shown as added, from /dev/null. For a merge commit the
diff is against the first parent, not the combined diff that upstream shows. Since 2026-10-06 the diff is a reusable
control (`DiffView`), used inline by the Diff tab, the commit window and the conflicts window, with colors that follow the
light or dark theme. Also since 2026-10-06: old and new line numbers (read from the hunk headers), a file's content with line
numbers (the File tree tab), and staging or unstaging of selected lines of a tracked file, with the patch built by upstream's
`PatchManager.GetSelectedLinesAsPatch` and applied as upstream's FileViewer does; the external diff tool for a working-tree,
staged or commit file (`git difftool`, with `diff.guitool` or `diff.tool` required, as for the merge tool). Added
2026-10-10: syntax highlighting from upstream's own definitions (ICSharpCode's `.xshd` files, embedded and read by a port of
its line parser), with the colors made readable on the theme as upstream's `AdaptForeColor` does; the histogram algorithm
(General page). Added 2026-10-11: "Show nonprinting characters" (spaces, tabs and line ends marked as upstream's editor does)
and "Show syntax highlighting" (a diff is highlighted only while it is on, a file's text always), both kept as upstream's
runtime settings, the vertical ruler (`diffverticalrulerposition`), and the menu of every diff tool git can start beside
"Open in diff tool" (`difftools.showavailable`). Not ported: line reset, the other patch-line actions (cherry-pick or
revert of lines), binary-file handling (the content view says a binary file is not shown), the diff appearance choices
(git word diff, difftastic), git's own coloring, the combined diff of merges, continuous scroll to the next file, the
submodule diff window, and end-of-line glyphs.
Changed 2026-10-13 (UI parity U3.4): the toolbar is upstream's `fileviewerToolbar`: icons at the top right over the
lines, shown while the pointer is on the viewer (next and previous change, more and fewer lines of context, entire file,
nonprinting characters, syntax highlighting, and the three whitespace choices as toggles in place of the whitespace box);
none for a file's text. The menu is upstream's `contextMenu`: Stage or Unstage selected line(s) where the window stages
(the commit window: the working directory's diff stages, the index's unstages), Copy (without the diff's +, - and space),
Copy patch, Copy new version and Copy old version (the selected lines, or all), and the view options as items. The commit
window's buttons under the diff are gone, as upstream has none. Not ported: the encoding box and the settings button,
"Reset selected line(s)", diff appearance (git word diff, difftastic), treat all files as text, automatic continuous
scroll, find, replace and go to line, and the commit window's choice of another diff tool.

**RevisionDiffControl** (`partial`). Ported: the files a selected commit changed, with status letters, the selected
file's diff beside the list (the Diff tab), and an action that opens it in its own window. Added 2026-10-10: the
working directory's and the index's files for the artificial rows (unstaged changes with the untracked files, staged
changes), upstream's BrowseDiff hotkeys for the commands the shell has (parents, difftool, history, blame, file tree, edit,
open, stage, unstage, reset, delete) and the user scripts in the list's menu. Added 2026-10-11: the paths follow "Truncate
long filenames" (PathFormatter row). Not ported: the per-file tree, filtering, the file history view, and the diff of merge
commits against each parent ("Show file differences for all parents") with the combined diff.

**FormFileHistory** (`partial`). Ported: the files at a commit (or HEAD), the commits that changed a selected file,
and a diff of that file in a chosen commit. Added 2026-10-10: the history follows renames as upstream's
`BuildPathFilter` does (`followrenamesinfilehistory`, `followrenamesinfilehistoryexactonly`, toggles in the window), and
the diff of a commit uses the file's name in that commit; blame from the window. Not ported: the tree view with folders
(the list is flat), the file's content view, and the branch and revision filters of the upstream form.

**FormBlame** (`partial`). Ported: per-line blame of a file as of a commit (line number, short hash, author, date, text),
read from `git blame --line-porcelain`; since 2026-10-06 a double-click or Enter on a line shows that commit's change to the
file. The blame settings, the gutter and its age colors and avatars: see the BlameViewerSettingsPage row. Not ported:
selecting the commit in the grid and blame of a diff against the parent.

**FormMergeBranch** (`partial`). Ported: merge the selected branch into the current one with git's fast-forward
default, abort a stopped merge, and show "merge in progress" while MERGE_HEAD exists. Conflicting files appear in the
Changes list with the Conflict kind. Not ported: the strategy and strategy options, squash, no-commit, the
allow-unrelated-histories option, and the merge message editor. Conflicts are resolved in the conflicts window (see
FormResolveConflicts).

**FormResolveConflicts** (`partial`). Ported: the list of unresolved files, choose ours or theirs for the selected files,
open the configured merge tool on one file (`merge.guitool`, then `merge.tool`; without one the app asks for a tool to be
configured rather than letting git fall back to a terminal tool), mark the selected files as resolved, the diff of the
selected file, and the hint to commit or continue the rebase once the list is empty. Not ported: the base, local and remote
description of each file (upstream `a4d51838f` changed those texts; skipped, as the port does not show them), keeping or
deleting a file deleted on one side, the per-file context menu, and opening the base, local or remote version.

**FormGitCommandLog** (`partial`). Ported: the git commands the app ran, newest first, the detail of the selected one, and a
copy of its command line, read from upstream's `CommandLog` as is. Not ported: the capture-call-stacks option and the tab
for commands run with output.

**FormRemoteProcess / FormProcess** (`partial`). Ported: git's output while fetch, pull, push, push tags and clone run (lines
that git ends with a carriage return are redrawn in place, as in a console), Abort (git and its child processes are
stopped), Close once git is done, and the upstream `closeprocessdialog` setting. Upstream's `IExecutable` returns standard
error only after git exits, so `GitOutputRunner` starts git itself; like upstream's `Executable` it sets the environment
first and records the call in `CommandLog`. Not ported: the authentication retry (PuTTY key loading) and the keep-open
checkbox.

**FormSettings** (`partial`). Ported pages: theme (upstream's CSS themes, user themes and the colorblind variation, see the ThemeModule row; stored as upstream `uitheme_v2`), close the
commit window after a commit (`closecommitdialogaftercommit`), close the output window after a remote operation
(`closeprocessdialog`), recent repository count (`history size`), the git executable (`gitcommand`), and git config
`user.name`, `user.email` and `merge.tool`, global and for the open repository. Every value uses an upstream key, so both
apps share it. Added 2026-10-06: `diff.tool`, and a choice of the diff and merge tools git finds on this computer (upstream's
`CustomDiffMergeToolCache`, as its diff viewer page uses it). Difference: upstream's page writes `diff.guitool` and
`merge.guitool` with a path and command per tool; the new shell writes `diff.tool` and `merge.tool` by name and leaves the
command to git's built-in tool definitions (its launches read the `guitool` keys first). The other pages have rows of their
own below. Added 2026-10-10 to the Git config tab: `credential.helper` with the helpers git and the OS offer (on Linux and
macOS git's own helpers are the credential store, PLAN.md decision 6; a value set more than once is left alone),
`core.editor` (with this app's `fileeditor` command offered), `commit.template` and `core.autocrlf`. Not ported: the tool
path and command fields. Changed 2026-10-14: the tools are stored in the `guitool` keys by name, and the page shows one
level at a time (see GitConfigSettingsPage).

**FormSettings page tree** (`partial`). Ported 2026-10-14 (UI-PARITY U4.1): the window is upstream's, 958 by 646, with the
page tree (`SettingsTreeViewUserControl`) on the left: upstream's three groups with their images, the pages in
`OnRuntimeLoad`'s order and nesting (`SettingsPageTree.Entries`, compared with `FormSettings.cs` by a test), the groups'
own pages (Checklist, "Git Settings", "Plugins Settings"), a node per plugin that has settings ordered by title, and the
"Type to find" box (a page is found by its title or when every word is in one of its controls' texts; found pages are
marked in the selection colors and their parents expanded; Enter shows them in turn). The window title names the page
("Plugin: " before a plugin's), the window opens on the page shown last in the session, and OK, Cancel and Apply are
upstream's buttons in its order (Cancel after Apply reports the save, as upstream's `_saved`). The pages are the former
tabs, now chosen by the tree (the tab strip is hidden), and the theme moved from Appearance to upstream's Colors page.
Pages taller than the window scroll. Differences: the plugin files upstream's loader passed over are listed after the
plugins, with the reason (a fork addition). Not ported: the pages the new shell has no page for (Console style, Shell
extension, Git > Advanced) are left out of the tree; the Discard button (shown in upstream's debug builds only).

**SettingsPageHeader** (`partial`). Ported 2026-10-14 (UI-PARITY U4.2): every page but the groups' introductions has
upstream's "Settings source:" header. The pages of the user's settings show only "Global for all repositories"; the
repository pages (Revision links, Build server integration, Detailed and the plugins' pages) offer Effective, Local,
Distributed and Global while a repository is open, with upstream's "<<" arrows while Effective is chosen. As upstream,
Effective is read-only (the page is disabled and nothing is stored from it), the values edited at a level are stored there
on OK, and the Detailed page's revision graph settings are edited at the global level only. Changed: the fork had let the
effective settings be edited (a value the repository sets changed there, others in the user's settings) and had a level box
on the Revision links and Plugins tabs only; the Build server page now stores at the chosen level instead of the effective
settings. Differences: the plugin pages share one level, as they share one page.

**GitConfigSettingsPage** (`partial`). Rebuilt 2026-10-14 (UI-PARITY U4.2): the Config page is upstream's one column in its
order (user name, user email, credential helper, editor, mergetool, difftool, path to commit template with Browse, the line
endings group with upstream's four choices) at the level its header chooses: Effective, Local, Global and System while a
repository is open (`IGitConfigSettingsPage`), Global otherwise. Effective and System are read only (`ConfigScope.Effective`
reads git's value with no scope option, `ConfigScope.System` uses `--system`); the values edited at Local and Global are
written there on OK, only the changed ones. As upstream, the effective credential helper is hidden (git gives only its last
value) and a level with several helpers shows them without editing. The editor, tool and helper boxes are editable lists
of what this computer and git offer. Changed: the fork showed the global and repository values side by side, with a
column of choices; and it stored the tools in `merge.tool` and `diff.tool`. The page now shows upstream's
`ConfiguredMergeTool` (`merge.guitool`, else `merge.tool`; the diff tool likewise) and writes a changed tool to the
`guitool` key, as upstream's page does. Not ported: the tool path and command rows (with Browse and Suggest), the files
content encoding, the warning when git cannot be found.

**ChecklistSettingsPage** (`partial`). Ported 2026-10-14 (UI-PARITY U4.3): the Checklist page on the Git Extensions node,
shown first as upstream, with upstream's rows, texts and colors (`OtherColors`, dimmed toward the editor background in a
dark theme): git found and its version against upstream's supported and recommended versions, user name and email, merge
tool, diff tool, language, and on Windows the linux tools (sh), the ssh client and the obsolete winstore credential helper.
The checks run each time the page shows; "Save and rescan" saves the window (as Apply) and checks again; a red or yellow
row's Repair button, and the row itself, shows the page to fix the setting on (the tools rescan when one is configured;
the winstore row opens upstream's wiki page). The instant save notice shows on the page. Differences: a tool git starts
itself (listed by `git mergetool --tool-help`) counts as configured even when upstream knows no command for it, since
upstream knows only its Windows tools; git is asked only for such a tool, as the list takes seconds; the diff tool falls
back to `diff.tool` as the browse window does. Repair shows the page where upstream first tries to locate git, sh or
PuTTY itself. Not ported: the Git Extensions install and shell extension rows (the Windows installer's registration),
"Check settings at startup" and the startup check behind it (upstream keeps the flag in the registry).

**FormAbout** (`partial`). Ported: name, version, operating system, runtime and git version. Not ported: the contributor list
and the links.

**Dashboard** (`partial`). Ported: the recent repositories, newest first, from upstream's own history
(`RepositoryHistoryManager`, setting key `history`), shown while no repository is open and in Start > Recent repositories;
open with Enter or a double-click; remove an entry. Added 2026-10-11: each repository's checked out branch beside it, in
the dashboard and the menu (`dashboardshowcurrentbranch`, see the RepositoryCurrentBranchNameProvider row), read after the
list shows. (The original list and its Remove button are gone.)
Rebuilt 2026-10-13 as upstream's dashboard: on the window color with upstream's background picture, a left column with the
wide logo, the Start links (create, open, clone, and "Clone <host> repository" per repository host plugin) and the
Contribute links (develop, donate, translate, issues), and the repository list with its header, the search box (Enter opens
the first repository, Down moves into the list) and tiles in groups: the recent repositories, then the favourites by
category (`AssignCategoryAsync` and the favourite history in upstream's settings). A tile shows the caption upstream's
`RecentRepoSplitter` gives the path, a star for a repository in a category, the folder or error image, and the branch;
a click opens it. The tile menu is upstream's (show in folder, categories with "(none)" and "Add new...", remove from the
list, remove missing projects); a group's "Actions" link renames or deletes a category or clears the recent list, with
upstream's questions. Colors as `DashboardTheme`, light or dark. The left column grows past upstream's 213 pixels when the
font needs it. Not ported: dropping a folder on the dashboard, "Recent repositories settings" (the shortening and sorting
settings are read as upstream stores them), and copying the environment information before "Issues" opens.

**RepoObjectsTree** (`partial`). Rebuilt 2026-10-13 as upstream's single tree (`RepoObjectsTree` in core, with `BranchTree`
for the branch grouping): the six trees (Branches, Remotes, Worktrees, Tags, Submodules, Stashes) in upstream's order and
with its root images, shown and ordered by upstream's `RepoObjectsTreeShow*` and `RepoObjectsTree*Index` settings; branch
and tag folders at '/'; upstream's node images (local and remote branch, the host's logo on a remote, tag, stash,
worktree, submodule changed or not); the checked-out branch and the current worktree in bold; upstream's ahead and behind
text after a local branch and, reversed, after the remote branch it tracks (`AheadBehindDataProvider`, linked code); remotes
without branches; the expanded nodes and the selection kept across a refresh. The toolstrip collapses the tree and shows or
hides each tree; the search box selects the next node that matches. The context menu is upstream's `menuMain` in its order,
each item shown for the nodes upstream shows it for (`contextMenu_Opening`), with the ref texts and tooltips of
`MenuItemsStrings` per kind of ref; the checked-out branch only offers create branch and rename. Double-click as upstream's
`OnDoubleClick` (checkout, create branch from a tag, open a stash, worktree or submodule); Delete deletes a branch or tag.
Deleting a local branch asks upstream's `DeleteUnmergedBranch` question when HEAD does not contain it and then deletes with
-D, as `FormDeleteBranch` (without that dialog's branch list). The old per-section buttons and boxes are gone.
Differences: upstream's FormRemotes is not ported, so the Remotes tree offers "Add remote..." and a remote "Rename...",
"Set URL..." and "Remove..." in place of "Manage..."; "Reset current branch to here..." offers soft, mixed and hard in a
choice window (FormResetCurrentBranch is not ported); "Create worktree..." opens the worktree window; a tag also has
"Delete on remote...", which upstream offers in its push dialog; the remote branch fetch items fetch the whole remote.
Not ported: multi-selection, "Copy to clipboard" (upstream's revision copy submenu), the merged-branch images, hidden
commits (EyeClosed), remote activation, the submodule tree of super- and subprojects with its detailed status images and
reset, stash and commit items, "Manage stashes...", the user scripts submenu, and the search's highlighting of all matches.

**HotkeySettingsManager** (`partial`). Ported: the FormBrowse defaults for open, close, commit, refresh, pull and push dialogs,
quick pull, push and fetch, terminal (upstream's Git bash), settings, focus the filter, the grid and the left panel, create a
branch or tag, merge, rebase, stash and stash pop, with Cmd in place of Ctrl on macOS; from the FormCommit defaults, Ctrl+S
(stage all), Ctrl+Enter and F5. Since 2026-10-06 the FormBrowse hotkeys are read from and written to upstream's
`SerializedHotkeys` setting (the `HotkeySettings[]` XML, by upstream command code and name; other forms' entries are kept
as they are), and can be changed in Settings > Hotkeys, so a hotkey changed in either app applies in both. Not ported:
the commands of the RevisionGrid, FileViewer, LeftPanel and FormMergeConflicts sections that the new shell has no feature for, the Stash and BrowseDiff sections, the alternative next and previous keys of FormCommit, and the reset-to-defaults button. Added 2026-10-06 (later): the FormCommit section (focus panes, stage all, diff tool, refresh, next and previous file), editable in Settings. Fixed then: the sections are stored under upstream's names ("Browse", "Commit"); the fork had used "FormBrowse", so hotkeys were not actually shared before. Added after that: the RevisionGrid (parent, child, HEAD, filter, branch scope, merges, first parent), FileViewer (next and previous change, ignore whitespace, stage and unstage lines), LeftPanel (Delete) and FormMergeConflicts (merge tool, ours, theirs, rescan) commands the new shell has. Added 2026-10-07: the Scripts section (see ScriptsManager). Changed then: stored hotkeys are matched by section and command code, as upstream's `MergeIntoDefaultSettings` does, and written in place of the entry with the same code, so a renamed script keeps its hotkey; the fork had matched by name. Added 2026-10-10: the BrowseDiff section (the Diff tab's
file list, see RevisionDiffControl) and the Stash section (on the left panel's stashes: next, previous, refresh), the
commit window's Conventional Commits keys, and the grid's ToggleHighlightSelectedBranch and quick search next and previous
keys. Still not ported: the commands of every section that the new shell has no feature for, and the reset-to-defaults
button.

**ScriptsManager / ScriptRunner / ScriptOptionsParser** (`partial`). Ported: the scripts in upstream's `ownScripts` setting
(same XML, upstream's defaults when none are stored, the older separator format, duplicate identifiers renumbered), every
option of upstream's arguments help with the same quoting and prompts (`{UserInput}`, `{UserFiles}`, branch and remote
choices), confirmation, `{openurl}`, `navigateTo:`, PowerShell (pwsh outside Windows), run in the foreground with the output
window or in the background; the user menu bar (`ShowInUserMenuBar`), the grid's Run script menu, the before and after
events of commit, pull, push, fetch, checkout and merge (a failed "before" script stops the operation, as upstream); and
since 2026-10-07 the scripts' hotkeys (upstream section "Scripts", one command per named script, code = its
`HotkeyCommandIdentifier`), in the browse and commit windows after the window's own hotkeys, enabled or not, as upstream's
`GitModuleForm`. A key without Ctrl (Cmd) or Alt typed into a text box stays text, unless it is a function key; upstream
has no such rule. Since 2026-10-07 plugin commands (`plugin:name`, `{plugin:name}` lowered as upstream) run the loaded plugin of
that name, ignoring case, from the browse and commit windows. Added 2026-10-10: `ShowInFileList` scripts in the file lists'
menus (the Diff tab, the commit window's lists) with the selected files' options (`{SelectedRelativePaths}`, the line and
column of the diff), and the script icons in the menus, from upstream's own images (`Images.resx`, linked) or the script's
icon file.

**ScriptsSettingsPage** (`partial`). Ported 2026-10-07: Settings > Scripts lists the scripts in run order (enabled check
box, name, event, command line), with Add (upstream's next identifier and "<New Script>" name), Delete, Up, Down and the
arguments help (upstream's text, kept equal by a test that reads upstream's source); the selected script's name, command,
arguments, event and options are edited below the list. Stored on OK, dropped on Cancel; upstream stores the page on every
save, the port only when the scripts changed. Added 2026-10-10: the icon choice, from upstream's embedded images by name.
Not ported: the property grid itself (plain fields instead).

**SimpleHelpDisplayDialog** (`ported`). A help text in its own window, not modal, closed with its owner.

**AdvancedSettingsPage** (`partial`). Ported 2026-10-07: the checkout default action and "always show", branch name
normalising with upstream's normaliser and symbols, and force-with-lease for Commit & Push after an amend. Differences: the
shell asks about local changes only when there are some (upstream's dialog also picks the branch); the remote branch
checkout does not go through the question, as before. Not ported: update checks, help images, advanced options, console
emulator.

**GitSettingsPage / FormFixHome / SshSettingsPage** (`partial`). Ported 2026-10-07: Settings > Git has the command used to
run git, the Linux tools folder (Windows only, as upstream clears it elsewhere) and upstream's line about
`GIT_CONFIG_GLOBAL` or HOME (variables written as `%NAME%` on Windows, `$NAME` elsewhere). FormFixHome's choices (upstream's
default, USERPROFILE, another folder, with its two checks) are on the tab instead of a separate window, and on Windows only:
elsewhere HOME is always set, and changing the app's own HOME would also move its settings folders. Settings > SSH chooses
OpenSSH, PuTTY or another client with upstream's `gitssh` rule (a path ending in plink.exe is PuTTY), PuTTY's three paths
(filled from upstream's folder list on Windows) and the Pageant autostart setting; `GIT_SSH` is set on save and at startup,
as upstream's `AppSettings.LoadSettings` does. PuTTY is offered on Windows only, since upstream's rule reads a Linux plink
path back as another client. Not ported: the "Download Git" link, validating the git command while it is typed
(`CheckSettingsLogic`), the startup HOME check (`CheckHomePath`) and FormFixHome's search for a `.gitconfig` in other
folders, the registry location of an old PuTTY installer, and what upstream does with PuTTY in remote operations (starting
Pageant with a remote's key, answering plink's host key question, the PuTTY key buttons): the setting is kept but the shell
does not act on it yet.

**SortingSettingsPage / left panel order** (`partial`). Ported 2026-10-07: Settings > Sorting has upstream's revision order
(`RevisionSortOrder`: git's default, `--author-date-order`, `--topo-order`; also used by the file history and the search),
the branch sort key and order (`RefsSortBy`, `RefsSortOrder`, given to `for-each-ref --sort` through upstream's
`Commands.GetRefs`) and the prioritized branch and remote expressions with upstream's tooltips; the branch tree's context
menu has upstream's "Sort by" and "Sort order" choices. The tree puts prioritized branches (matched by the name without the
remote) and remotes first as `OrderByPriority` does, and lists remotes by name. Difference: an invalid expression matches
nothing instead of failing the tree. Not ported: the help links to the user manual, the priorities in the commit info panel
(the shell's commit details list no branches yet).

**RevisionLinksSettingsPage / CommitInfo related links** (`partial`). Ported 2026-10-07: Settings > Revision links edits
upstream's link definitions (`RevisionLinkDefs`) through upstream's `ExternalLinksManager`, so they are stored at the levels
upstream uses (the repository's effective settings, or the user's without a repository): the categories, Add (upstream's new
definition), the GitHub and Azure DevOps templates (upstream's extractors, linked unchanged; buttons instead of the Add
drop-down; upstream's remote preference), Remove, and every field of a definition with its link rows. The commit details
show "Related links:" for the selected commit, found by upstream's `GitRevisionExternalLinksParser` in the message and the
local and remote branches at the commit, each address once; a link opens in the browser. Added 2026-10-11: the settings
level box of upstream's page header (effective, local, distributed, global; the definitions edited at each level are
stored on OK) and the templates' icons (upstream's `GitHub` and `VisualStudioTeamServices` images, by name, since the
templates' own `Icon` is a WinForms image). Not ported: the other commit info parts that upstream renders as links
(branches, tags). Changed 2026-10-14: the level is chosen in upstream's header, read-only at Effective (see
SettingsPageHeader).

**AppearanceFontsSettingsPage** (`partial`). Ported 2026-10-07: Settings > Fonts edits upstream's application, commit, code
and monospace fonts (`font`, `commitfont`, `difffont`, `monospacefont`) in upstream's stored format. Upstream reads them
through `System.Drawing.Font`, which works only on Windows, so `FontSetting` parses and writes the same text. Avalonia has no
font dialog: the family is chosen from the installed fonts (a stored one that is not installed is listed too) and the size
typed in points. "(default)" removes the key, so the app's own font applies (upstream cannot unset a font). Only a font the
user changed is written. The fonts apply at once, without upstream's restart: the application font to the whole UI through
Fluent's font resources, the commit font to the commit message box and the commit details, the code font to the diff,
editors, blame, git output, command log and help, the monospace font to commit hashes and the reflog. Added 2026-10-11:
bold and italic, as check boxes beside the size (upstream's font dialog), shown through weight and style resources (the
application font through the windows' style). Not ported: "Show end-of-line markers as glyph".

**FormBrowseRepoSettingsPage / revision grid tooltips** (`partial`). Ported 2026-10-07: "Show revision tooltips"
(`ShowRevisionGridTooltips`, on by default) and the tooltips it controls, with upstream's texts: the message column shows
the message summary (upstream's `GitRevisionSummaryBuilder`) when it has more lines or the commit has refs, then the refs in
brackets (local branches, remote branches, tags); the author column the author and committer; the date column both dates;
the hash column the full hash. Upstream needs a restart; here the setting applies when the settings are saved. Not ported:
ahead/behind counts in the ref lines and the tooltips of a hovered ref label (the shell has no ahead/behind data per ref),
and the page's other settings: the Console tab and its shell, GPG information, find
in commit files with git grep, output history (the shell has none of them); "Show file history in the main window" and
"Show blame in diff viewer" (the shell shows file history and blame in their own windows only).

**BlameViewerSettingsPage / blame gutter and menu** (`partial`). Ported 2026-10-07: the blame window blames with upstream's
flags (`-M`, `-C`, `-w` from `DetectCopyInFileOnBlame`, `DetectCopyInAllOnBlame`, `IgnoreWhitespaceOnBlame`) and shows
upstream's gutter line (author and date in the chosen order, the time, the original file path) on the first line of each run
of lines from one commit, with line numbers when chosen. Its context menu has the blame settings of upstream's file history
window, with upstream's rules (the author or the date always shows; the time only with the date); Settings > Blame viewer has
the page with upstream's labels and warning tooltip. Differences: the short hash stays in its own column (a column the new
shell had before), shown once per run like the gutter line. Added 2026-10-11: upstream's age colors (`CalculateBlameGutterData`:
seven greens from the oldest line, at least three years back, to now; their lightness turned around on a dark theme where
upstream adapts them) and the author's avatar on the first line of a commit's run (`Blame.ShowAuthorAvatar`, in the menu and
on the page; see the Avatars row). Not ported: blaming the line's commit or its parent from the menu.

**GraphRenderer / SegmentRenderer / DetailedSettingsPage** (`partial`). Ported 2026-10-07: upstream's graph model is linked
unchanged; its renderer is ported line by line from WinForms' Graphics to shapes (`GraphPainter`): lanes, shared lanes,
diagonals or curves, perpendicular junctions and bows, square nodes for commits with refs, the outline of HEAD, upstream's
sizes. Differences: upstream's 1/8-pixel anti-aliasing shift for GDI+ is left out; the two lane states upstream throws on
are skipped instead; colors come from the theme's lane colors by index (stand-in `RevisionGraphLaneColor`). Added
2026-10-10: upstream's draw styles (non-relatives drawn gray by default, `revisiongraphdrawnonrelativesgray`; highlight
the selected branch; highlight while the pointer is on a branch's label), upstream's lane colors for them
(`GetBrushForLaneInfo`), lane tooltips (LaneInfoProvider row), the artificial working directory and index rows (their own
row), and a graph column only as wide as the visible rows need. Not ported: the cached row bitmaps (`GraphCache`, not
needed for the frame times in PLAN.md M3.5) and the Detailed page's push and merge window options (the shell has no
multi-branch push view or merge dialog; PLAN.md M8).

**GeneralSettingsPage / AppearanceSettingsPage / DateColumnProvider** (`partial`). Ported 2026-10-07: relative or full date
of the author or the committer in the grid (also in search results), with upstream's View menu toggles; open the last
working directory on startup (upstream's `Program` rule, only for a valid repository); the changed-file count on the
Commit button; the default clone destination. Added 2026-10-10 from the General page: the default pull action (the Pull
button and the rebase check box follow it), autostash with untracked files, submodule update on checkout (with upstream's
question while undecided), histogram diff, the commit limit, the quick search timeout, the uncommitted changes check of
checkout, and the status bar's ahead and behind counts and stash count. Added 2026-10-11 from the Appearance page: the
author images group (Avatars row), the branch names of the recent repositories (Dashboard row) and "Truncate long
filenames" (PathFormatter row). Not ported: the console window and telemetry settings (General), the spelling
dictionary (no spelling checker, PLAN.md M8), and the Windows-only "Show current branch in Visual Studio" and "Auto scale
user interface" (Avalonia scales by the system's setting).

**DiffViewerSettingsPage / FileViewer options** (`partial`). Ported 2026-10-07: the four whitespace modes (one box instead
of upstream's three toggle buttons, same states), fewer and more lines of context (down to none, buttons off while the
entire file is shown) and the entire file, with upstream's diff arguments and hotkeys (IgnoreAllWhitespace, Increase and
DecreaseNumberOfVisibleLines, ShowEntireFile), kept for the next view in this run as upstream's runtime settings; the three
"Remember" settings and "Save current view settings as default". Added 2026-10-11: remember non-printing characters and
syntax highlighting, the vertical ruler position and "Show all available difftools" (FileViewer row). Not ported: diff
appearance, combined diff, continuous scroll, submodule diff, all parents and Git coloring, whose FileViewer features the
shell does not have (PLAN.md M8).

**CommitDialogSettingsPage** (`partial`). Ported 2026-10-07: the second-line setting, the remembered Amend check box and
the Commit & Push button, under upstream's keys and labels. In the commit window (FormCommit row): the message goes through
upstream's `FormatCommitMessage` before the commit (it was given to git as typed before), Amend is put back checked when it
was left checked (not during a merge, and keeping the draft, as upstream), and Commit and push hides when turned off. Added
2026-10-11, with their features in the commit window (FormCommit row): show errors when staging, compose in the commit
dialog, the number of previous messages, the two reset buttons and auto-completion. The page is complete.

**ConfirmationsSettingsPage / ConfirmSuppressible** (`partial`). Ported 2026-10-07: the confirmations whose action the
new shell has (amend, commit with no branch, rebase, fetch with prune, push of a new branch, force delete of an unmerged
branch, left panel checkout, stash drop, worktree switch), under upstream's keys and labels, asked with upstream's texts
and "Don't show me this message again" (not for the left panel checkout, as upstream). Where they are asked: the commit
window (amend; no branch unless a rebase is under way), the grid's rebase, quick fetch with Prune and the pull dialog's
fetch with prune, quick push and the push dialog when the remote branch is not among the fetched remote branches, Force
delete, the left panel's checkout, Drop stash, and opening a worktree. Not ported: undo last commit, the tracking reference
question, the automatic stash pop after pull and checkout, the conflict resolution questions, the second abort question
(features the shell does not have, PLAN.md M8), the branch buttons of the "not on a branch" question, and upstream's Yes/No buttons (the
new shell names the action on its button).

**PluginRegistry / FailedPluginWrapper** (`partial`). Ported 2026-10-07: the plugins are loaded once, off the UI thread,
by upstream's `ManagedExtensibility` (compiled unchanged in the shadow `GitUIPluginInterfaces`): `GitExtensions.Plugins.*.dll`
in the Plugins folder next to the app and `GitExtensions.*.dll` in the user's plugins folder (`UserPluginsPath`). Each plugin
gets upstream's settings container (`GitPluginSettingsContainer`, linked unchanged, so the keys are `<id>.<name>` with the
older `<description><name>` fallback). A plugin that fails to construct is listed as "Plugin loading failure" and shows its
error when run. The plugins are registered with the open repository, or the dashboard, and moved when the repository
changes, as upstream's `SetGitModule` does; they are unregistered when the window closes. The Plugins menu lists them by
name with their icon above "Plugins settings...", and turns off repository plugins on the dashboard. Not ported: the
repository hosts menu (`GitHosters`), loading the commit-form plugins when the commit window opens on its own (the new shell
has no stand-alone commit window), the clipboard copy of a load error, and the composition cache (off upstream too).
Plugins built for the new shell: upstream's BackgroundFetch and AutoCompileSubmodules, compiled unchanged by shadow projects
in `src/xplat/plugins` (only their bitmap resources are replaced, see PLAN.md M6); the WinForms plugins of upstream's
repository were built for it since (see "Plugin forms"). Added 2026-10-10: Settings > Plugins also lists the plugin files
upstream's loader passed over, with the reason (a third-party plugin built on WinForms, another interface version), as
PLAN.md decision 5 chose: such plugins are not loadable in the new shell.

**GitUICommands (plugin side)** (`partial`). `PluginHost` is the `IGitUICommands` the plugins get: the current module,
`RepoChangedNotifier` (from any thread; it refreshes the window on the UI thread through `PostRepositoryChanged`, as
upstream), the events `PreCommit` and `PostCommit` (around the commit window, as `StartCommitDialog`), `PreCheckoutBranch`,
`PostCheckoutBranch`, `PreCheckoutRevision`, `PostCheckoutRevision` (around a checkout; a "pre" handler can cancel it),
`PostSettings`, `PostUpdateSubmodules`, `PostEditGitIgnore`, `PostRegisterPlugin`, `PostBrowseInitialize`; settings
(`StartSettingsDialog` for a plugin opens its page), the commit window, and command and git process windows. Since 2026-10-09
`StartBatchFileProcessDialog` runs the text as upstream's batch file on Windows and as a sh script elsewhere. `GetService` gives
`IGitExecutorProvider`. Unlike
upstream, `StartCommandLineProcessDialog` returns once the command started, not when it ended. Every other dialog throws
`NotSupportedException`, which the Plugins menu shows as an error. Since 2026-10-09 also: commit templates
(`AddCommitTemplate`, see the commit templates row) and the repository host methods (`StartCloneForkFromHoster`,
`StartPullRequestsDialog`, `StartCreatePullRequest`, `AddUpstreamRemote`, with upstream's `WrapRepoHostingCall`: a host that
is not set up is run first, and a failure is shown with upstream's text); `AddUpstreamRemote` fetches the added remote where
upstream opens its pull dialog to fetch it. Not ported: `RunCommand`, services through `GetService` other than `IGitExecutorProvider`, and `BrowseRepo`.

**PluginSettingsPage / SettingControlBindings** (`partial`). Ported 2026-10-07: Settings > Plugins lists the plugins and
shows the selected plugin's settings with the editor of upstream's binding for its type: three-state check box, text box
(with upstream's "<empty string>" marker and placeholder), password box, number box (red while not a number of the
setting's type; an invalid number is stored as no value, as upstream), drop-down list, read-only note, and "There are no
settings available for this plugin.", and (2026-10-09) links (a pseudo setting with a link label, such as the GitHub
plugin's token pages; the click runs the plugin's own handler through the shim's `RaiseClick`). The values are the repository's effective settings when one is open (a value the
repository sets is changed there, any other in the user's settings, as upstream's `DistributedSettings` does) and the user's
settings otherwise; unchanged values are not written. Added 2026-10-10: the settings level choice of upstream's page
header (effective, local, distributed, global; the values edited at a level go to it), and credentials settings (user name
and password, kept in Windows' credential store as upstream does; on Linux and macOS they say why they cannot be edited,
PLAN.md decision 6). Not ported: settings with their own WinForms control (shown as not editable). Changed 2026-10-14:
the level is chosen in upstream's header, and the effective settings are read-only as upstream's (see SettingsPageHeader).
The settings tree lists the plugins that have settings, each as its own node.

**FormGitIgnore / FormAddToGitIgnore** (`partial`). Ported: edit the repository's top-level `.gitignore` as text (Repository >
Edit .gitignore...), and add the selected untracked files from the commit window (Ignore...), each as a root-anchored pattern
on a line of its own, skipping patterns already there and keeping the file's line ending. Not ported: the default ignore
templates, the preview of matched files, the "info/exclude" choice, and patterns for whole folders or extensions.

**FilterInfo / FormRevisionFilter** (`partial`). Ported: all branches (the upstream default) or the current branch only, kept
in upstream's `ShowCurrentBranchOnly` setting; the advanced filter for author, committer, message (ignoring case), since,
until, path, hide merges and first parent only, built in upstream's argument order; the all-branches view leaves out notes,
the stash and agent session refs as upstream does with its default settings (including `c16194306`). The status line says
when the list is filtered. 2026-10-13, with the filter toolstrip: the branch filter ("Filtered branches": revisions, ranges
and options as they are, words with wildcards as `--branches=`), `--reflog`, the diff content search (`-G`), a message
filter starting with `--` passed as git log options, `HasFilter`, `ResetAllFilters` (keeps the current branch and reflog
choices) and `GetSummary`. Not ported: simplify-by-decoration, full history and simplify merges, the commit count limit,
`--boundary` for a message and diff filter together, and the dialog's own fields for the branch filter, diff content and
reflog (`FilterWindow` keeps what the toolstrip set).

**FormManageWorktree / FormCreateWorktree** (`partial`). Ported: the worktrees as git lists them (upstream's
`GitModule.GetWorktrees`, used as is), open one in the browse window, add one at a folder starting at the current commit
(on a new branch when a name is given, detached otherwise), remove one after a confirmation, and prune. As upstream
`065da3680` does, the first worktree is the main one and is never offered for removal. Not ported: creating from another
branch or commit, the open-in-new-window choice, and deleting the folder of a worktree git no longer knows.

**FormSubmodules** (`partial`). Ported: the submodules in a left-panel section (shown only when the repository has any) with
their state (not initialized, changed), open one as a repository, update the selected one or all (upstream's
`Commands.SubmoduleUpdate`: init and recursive, with live output), and sync (upstream's `Commands.SubmoduleSync`). The list
comes from `.gitmodules` (upstream's `GetSubmodulesLocalPaths`): upstream's `GetSubmodulesInfo` parser needs the describe
suffix that `git submodule status` leaves out for a submodule that is not initialized, so such a submodule has no status
and is shown as not initialized (an upstream behavior worth reporting; the fork does not change it). Not ported: adding and
removing submodules, the per-submodule details, and the recursive submodule tree.

**GitExtSshAskPass** (`partial`). Ported 2026-10-06: the app answers ssh's and git's prompts during remote operations. It is
started through `SSH_ASKPASS` (with `SSH_ASKPASS_REQUIRE=force`, as upstream) and recognizes the run by a marker variable,
because ssh passes only the prompt. Passwords, passphrases and PINs are typed hidden; Cancel exits 1, which stops git. Upstream
sets `SSH_ASKPASS` for the whole process; the new shell sets it only for remote git calls. Not ported: upstream's Windows
installer placement of the helper; the new shell needs none. Not checked on macOS.

**ThemeModule, ThemePathProvider, appearance page** (`partial`). Ported 2026-10-06: the theme is loaded as upstream's
`ThemeModule.LoadThemeSettings` does (light is upstream's default theme, a theme that fails to load falls back to it, the
system mode resolves to dark or light), through upstream's own loader files linked into the core unchanged. `AppThemePaths`
repeats `ThemePathProvider`'s folder rules because the upstream class asserts GitExtensions.exe in Debug builds. The
appearance choice lists the same themes and the colorblind variation. Added 2026-10-10: the panel, editor, line number and
selection colors (windows' background, the diff and file views, their numbers, selected diff lines). Added 2026-10-11: the
system color overrides a theme sets (upstream's `SysColorValues`; upstream's own themes set none and list the dark ones only
for reference, as WinForms' dark mode supplies them): WindowText, GrayText and Highlight go to Fluent's text, disabled text
and accent resources, and come back to the app's own when a theme without them is applied. Not ported: the other system
colors (Fluent draws the controls from its light or dark variant), the theme editor, `UseSystemVisualStyle`, and the
restart prompt (the new shell applies a theme at once).

**InteractiveGitActionControl** (`partial`). Ported 2026-10-09: upstream's two notification bars above the grid, one for a
bisect and one for a stopped rebase, merge or patch (`git am`) or for conflicts left by any command, with upstream's texts,
its buttons in its order (Resolve... while conflicts remain, else Continue; Abort; More...) and its colors (light sky blue,
orange with conflicts; darker in a dark theme). The state is read with upstream's `GitModule.InTheMiddleOfRebase`,
`InTheMiddleOfMerge`, `InTheMiddleOfPatch` and `InTheMiddleOfBisect`, checked in upstream's order, so a stopped `git am` is
now a patch and no longer shown as a rebase. Continue runs upstream's command for the action (`rebase --continue`,
`merge --continue` with the app as the message editor, `am --3way --resolved`). Differences: Abort asks first, as the shell
already did; conflicts are taken from the status the window already reads, not from a second `ls-files --unmerged`. More...
for a rebase offers Skip commit and Edit todo, and for a patch Skip patch (`am --3way --skip`), instead of opening
`FormRebase` or `FormApplyPatch`, which are not ported; for a bisect it opens the bisect window.

**FormBisect** (`partial`). Ported 2026-10-09: Commands > Bisect... and the bisect bar's More... open the bisect window with
upstream's five buttons and their enabled rules; Start runs while the window stays open, the marks and Stop close it, as
upstream. Each step runs with git's output in the process window (upstream's process dialog); a mark's window stays open
whatever the close setting says, because its output names the next commit to test or the first bad one (upstream passes
`useDialogSettings: false`). Not ported: starting with a range from two selected commits (the grid selects one commit),
and the toolbar's bisect icons.

**Plugin forms (general)**. Since 2026-10-09 upstream plugins with WinForms forms are built for the new shell by shadow
projects in `src/xplat/plugins` that set `XplatPluginUi`: the plugin class and the files without WinForms are compiled
unchanged, and the form is replaced by a stand-in of the same name and constructor (`GitExtensions.Xplat.Ui.PluginDialog`)
whose `ShowDialog` shows an Avalonia window modally (`ModalWindow`, a nested dispatcher frame, as WinForms runs a modal
dialog). The window reimplements the form; its rows below. Since 2026-10-09 the windows are translated: their controls
are named as the form's fields, so upstream's Transifex entries for the form apply (see the translations row). The
BuildServerIntegration plugins replace their settings controls the same way (exported with upstream's metadata); GitHub3 has
no form of its own (its windows are upstream GitUI forms, see the repository host rows).

**CreateLocalBranchesForm** (`ported`). Creates or updates a tracking branch for every branch of the remote, with upstream's
commands and messages; as upstream, a branch git cannot create is passed over, and the count in the message is that of every
branch git listed.

**ProxySwitcherForm** (`ported`). Shows the local (effective) and global `http.proxy` with the password hidden, sets it from
the plugin's settings and unsets it, locally or globally. Difference: the module's config cache is invalidated after a change,
because upstream shows the cached (old) value until the next refresh.

**FindLargeFilesForm** (`ported`). The scan (`LargeFileFinder`) runs upstream's commands off the UI thread and reports each
file to the list as upstream's binding list does; the list sorts by a column header and is read-only until the scan ends.
Delete runs upstream's script through `StartBatchFileProcessDialog`: on Windows upstream's batch file, character for character
(the test compares it with upstream's verified snapshots); elsewhere the same steps as a POSIX shell script, which the
`PluginHost` runs with sh.

**DeleteUnusedBranchesForm** (`ported`). Upstream's search (merged into a branch or all, older than a number of days, a
remote, a regex), the header check box, the status line and the deletion one branch at a time, with upstream's messages.
Differences: the saved settings are put in the window before its change handlers are added, so opening it does not warn
about unmerged branches (upstream's load triggers its own handler); a failed deletion shows its message instead of a bug
report.

**ReleaseNotesGeneratorForm** (`ported`). The range, the log arguments, the result and its count, and the four copies, with
upstream's text tables and HTML. HTML goes to the clipboard as plain text and as HTML: on Windows upstream's CF_HTML text,
elsewhere the platform's own HTML format (`text/html`, `public.html`).

**GourceStart** (`partial`). The path, the repository and the arguments, Start, the two links and the pickers. The plugin
class is upstream's, with its download of Gource for Windows. Not ported: authors' pictures for `$(AVATARS)` (the new shell
has no avatar service; the folder is created empty). Off Windows an empty path is filled with the `gource` on the PATH.

**FormImpact / ImpactControl** (`ported`). Upstream's `ImpactLoader` (the git reads) is compiled unchanged; `ImpactGraph`
ports the drawing control: a band per author with upstream's block heights (log scale), curves between weeks, changed-lines
and week labels, the author under the pointer outlined and named with commits and changed lines, and the submodules option.
Differences: the graph scrolls in a ScrollViewer (it keeps to the newest weeks while loading, as upstream's scroll bar); the
hit test uses the blocks and the same curves instead of the platform geometry. Upstream bug, not fixed here because
`ImpactLoader` is compiled unchanged: its parse loop steps over the header line that ends a commit's numstat, so every commit
that follows another in git's output is skipped (in a repository with one commit per author per turn, half the authors
vanish). Candidate for an upstream fix from `master`.

**FormGitStatistics** (`partial`). The four tabs with upstream's texts: commits per contributor (`GetCommitsByContributor`),
and lines of code per language, per type and in tests, counted by upstream's `LineCounter` off the UI thread. The plugin
asks the host for `IGitExecutorProvider`, which `PluginHost.GetService` now gives. Off Windows the ignored directories are
turned back into the platform's separators (the plugin class writes them with `\`). Not ported: upstream's 3D pie chart
(`PieChart/`, System.Drawing); `PieChart.cs` draws a flat pie in upstream's colors with the slice's text as tooltip.

**Translations** (`partial`). Since 2026-10-09 the app ships upstream's Transifex files (`Translation/*.xlf`, linked from
`src/app/GitUI/Translation`), and Settings > Appearance has upstream's language choice (`translation` setting, shared with Git
Extensions for Windows). Upstream translates a form through `TranslationUtil` by its type name and its fields' names;
`UpstreamTranslation.Apply` does the same for a window whose controls are named as the form's fields (the title from
`$this`, then text blocks, buttons, check boxes, tabs, groups, menu items, column headers and tooltips), and
`UpstreamTranslation.Text` gives a `TranslationString` field's text. Upstream's `&` mnemonics become Avalonia access keys.
Upstream plugin classes translate themselves as upstream (`GitPluginBase.Translate`), so plugin names, setting captions and
messages follow too. As upstream, a change applies to windows opened later; the plugins' own texts need a restart. Added
2026-10-10: every other window is translated by its English texts (`UpstreamTranslation.ApplyByText`, when a window is
loaded): a text that matches one of upstream's English source texts takes that text's translation, so labels copied from
upstream follow the language; lists filled with data are left alone. Texts of the new shell that upstream does not have
stay English (PLAN.md decision 4). Not translated: the first-start language dialog (`FormChooseTranslation`).

**BuildServerWatcher** (`partial`). Upstream's choice of adapter (the configured type unless integration is turned off; with
no type, detection from the remotes in `PrioritizedBuildServerRemoteNames` order while the integration setting is untouched,
and the matching detector filling in its settings), upstream's polling (running builds every 10 seconds while there are any,
finished ones of the last three days, then all, then every two minutes), `ReplaceVariables`, and the credentials in upstream's
text format. The watcher is relaunched after each read of the grid, as upstream. Credentials: on Windows upstream's store (the
user's isolated storage, DPAPI-encrypted, one file per server; Git Extensions for Windows reads the same file when the
isolation identity matches); on Linux the desktop's Secret Service through `secret-tool` when installed; otherwise (macOS,
Linux without `secret-tool`) they are kept for the session only, so a secret is never written in plain text. Difference:
upstream writes the file without truncating it, so a shorter text could leave old bytes; the port truncates. As upstream, an
auto-detected server reads its settings from the type-less path `BuildServer.` (a settings tab entry for the type is not used
until the type is chosen there).

**BuildStatusColumnProvider** (`partial`). The "Build Status" column, last as upstream: the status symbol, the description or
both (View > "Show build status icon" and "Show build status text", upstream's settings), colored by status (upstream's lighter
colors on a dark theme instead of on a selected row), with upstream's tooltip; shown only while a build server gives results.
A click on the cell opens the build report, and the grid menu has "View build report in a browser" and "View pull request in
a browser". A build replaces the shown one unless it started earlier; results are kept per commit, so commits read later
(another page) show them too. Added 2026-10-10: the Build Report tab (BuildReportTabPageExtension row: the selected
commit's report as a link to open in the browser, since the shell has no embedded browser; shown while
`ShowBuildResultPage` is on and the commit has a report). Not ported: the hand cursor over a cell with a report, and the column's width when resized.

**BuildServerIntegrationSettingsPage** (`partial`). Settings > Build server integration on the repository's settings (as the
revision links): the three-state "Enable build server integration", the type (None and the plugins' types, with upstream's
reason when one cannot be loaded), and the plugin's settings control for the type, given the repository's folder name and
remote URLs. The plugin control's values for a type are read from that type's path even before it is saved (upstream reads
them from the saved type's path). Changed 2026-10-14: the page has upstream's settings source header and stores at the
chosen level, read-only at Effective (see SettingsPageHeader). Not ported: "Show build result page" (the shell has no build
report tab).

**FormBuildServerCredentials** (`ported`). Guest access, user name and password, or bearer token, filled with the known ones;
asked from the adapter's thread, shown on the UI thread until answered.

**Build server settings controls** (`ported`). AppVeyor, Azure DevOps, GitHub Actions, GitLab, Jenkins and TeamCity: each
plugin's control is replaced by an Avalonia control of the same name, exported with upstream's metadata, with upstream's
fields, keys and checks (three-state check boxes, regex checks, the GitLab project lookup and token page, the Azure DevOps and
TeamCity extraction from a build URL on the clipboard, TeamCity's build chooser with its lazily read project tree). The
adapters, API clients and detectors are compiled unchanged. The WinForms `TaskDialog` the Azure DevOps adapter shows for a bad
token goes through the shim to the app's message box window.

**ForkAndCloneForm** (`ported`). The user's repositories and the search (by text or user) through the host plugin, off the UI
thread; fork; and clone with upstream's options (folder, directory, upstream remote from the parent's owner, protocol, depth)
through the shell's clone, which opens the clone; the parent is added as a remote afterwards. Differences: the default folder
is the default clone folder or the folder of the open repository (upstream: of the most recent repository, usually the same);
"Open github page" opens the homepage as upstream.

**ViewPullRequestsForm / DiscussionHtmlCreator** (`partial`). The hosted remotes (the current branch's first), their pull
requests (moving on from an empty one on the first load, as upstream), the selected one's patch split into files as upstream
and shown in the diff view, the discussion, Fetch to the pr/ branch, Add remote and fetch (then checkout), and Close pull
request. Differences: the discussion is a list of entries (author, date, commit, body) instead of upstream's HTML page; the
comment box and its Refresh and Post buttons work (upstream's have no handlers). Not ported: the file list's status icons.

**CreatePullRequestForm** (`ported`). The foreign hosted remotes as targets, both branch lists with the default branch chosen,
the title from the branch's last commit until typed, the body from `.github/PULL_REQUEST_TEMPLATE.md`, and upstream's
messages.

**Repository host menu, commands and blame items** (`ported`). The "(Repository hosts)" menu named after the first host plugin
(upstream supports one) with Fork/Clone repository, View pull requests, Create pull requests and Add upstream remote, through
`PluginHost` as upstream through `GitUICommands`; the blame window's menu gets the host plugin's items (the GitHub plugin's
"View in GitHub" for the line), which the plugin builds in a shim `ContextMenuStrip` with upstream's `GitBlameContext` as tag.

**Commit templates** (`partial`). The commit window's "Commit templates" menu: the plugins' templates (upstream's
`CommitTemplateManager` registry, used as is; the GitHub plugin adds the user's assigned issues), then the user's own
(upstream's `CommitTemplates` setting); a template replaces the message, with upstream's regex placeholders filled from the
branch name. Added 2026-10-10: the Conventional Commits items and the template settings window (FormCommitTemplateSettings
row: the user's templates and the commit message settings, stored in upstream's format). Not ported: git's
`commit.template` as the initial message, and the plugin templates' icons.

**FormCommitTemplateSettings** (`ported`). 2026-10-10: the user's templates (upstream's `CommitTemplates` setting, read and
written with upstream's `CommitTemplateItem` and serializer, so both apps share them) and the commit message settings
(`CommitValidation*`: subject and line lengths, empty second line, indentation, auto wrap, the regular expression), with
upstream's texts, translated through the form's fields.

**FormCommit: "Commit message" menu, reset buttons, message checks, Conventional Commits** (`ported`). The commit window
parts listed in the FormCommit note: `PreviousCommitMessages` uses upstream's `GitModule.GetPreviousCommitMessages` and
upstream's merge rule for the last committed message; `CommitMessageValidation` repeats `IsCommitMessageValid` and
`FormatAllText` with upstream's texts and its `WordWrapper` (linked as is); `ConventionalCommits` repeats the type and
footer insertion with upstream's cases as tests.

**EditNetSpell (auto-completion)** (`partial`). 2026-10-11: upstream's providers are linked unchanged
(`CommitAutoCompleteProvider`: the changed files' names and the identifiers its regexes find in their diffs, from
`AutoCompleteRegexes.txt` or upstream's defaults; `CommitMessageMetadataProvider`: the trailer keywords), with upstream's
word-at-cursor rules. The list follows typing after upstream's 200 ms pause (a delayed post rather than a dispatcher timer),
Ctrl+Space asks for it and takes the only match, Tab or Enter takes the selection, Escape closes it; Up and Down move in it,
which upstream's list leaves to the text box. The message box menu has upstream's "Provide auto completion" toggle. Not
ported: the spelling checker (upstream's NetSpell is a WinForms component; PLAN.md M8), "Mark ill formed lines", and the
tooltip shown while the words are still being read.

**Avatars** (`partial`). 2026-10-11: upstream's provider chain without its System.Drawing images, which do not load off
Windows: "Default" asks GitHub, then Gravatar (with the generated style as Gravatar's fallback); "Custom" follows the
`CustomAvatarTemplate` segments (URLs with upstream's `{name}`, `{email}`, `{md5}`, `{sha1}`, `{sha256}`, `{imageSize}`
through upstream's `TemplateFormatter`, `<Default>`, `<None>`); then the fallback: the author's initials (drawn by the view
with upstream's palette and contrast rule) or Gravatar's generated style. Downloaded images are kept in upstream's cache
folder under upstream's names (`{email}.{size}px.png`, expiring after `authorimagecachedays`, never for the "None"
provider), so both apps share it; "Clear image cache" empties it. Differences: the file is kept as the provider sent it
(upstream re-encodes it as PNG); a bot's GitHub avatar (found through the GitHub API upstream) is not looked up; upstream's
last-resort user picture is the initials here; GitHub's identicon is recognised by the width in its PNG header.

**PathFormatter** (`ported`). 2026-10-11: "Truncate long filenames" for the Diff tab's and the commit window's file lists:
"Compact" keeps both ends of the path (Avalonia's path ellipsis; upstream uses Windows' `PathCompactPathEx` and shows the
whole path elsewhere), "Trim start" drops the start, "Filename only" shows the name (upstream's
`FormatTextForFileNameOnly`). The full path is the item's tooltip.

**RepositoryCurrentBranchNameProvider** (`ported`). 2026-10-11: the branch through upstream's `Commands.GetSelectedBranch`
(mostly a read of the HEAD file); none for a folder that is gone, not a repository, or bare, as upstream.

**QuickSearchProvider** (`ported`). 2026-10-10: typed characters search the loaded commits as upstream (its
`GitRevisionTester.Matches` rules: author, subject, refs and hash), the label above the grid takes the warning color when nothing matches (upstream: red),
the text clears after `revisiongridquicksearchtimeout`, and the next and previous match keys repeat the search.

**LaneInfoProvider** (`ported`). 2026-10-10: the graph's tooltip for a lane, with upstream's text (the commit or the child
it leads to, the branch upstream's `BranchFinder` finds, the message summary), on the pointer's lane.

**ArtificialCommitChangeCount / artificial rows** (`partial`). 2026-10-10: the "Working directory" and "Commit index" rows
before HEAD (on the first page without a filter), their files and diffs, and the change count beside them
(`showgitstatusforartificialcommits`). Not ported: upstream's summary of the changes by kind (changed, new, deleted,
submodules).

**BuildReportTabPageExtension** (`partial`). 2026-10-10: a "Build Report" tab while `ShowBuildResultPage` is on and the
selected commit's build has a report, following the selected build. Difference: the report is a link that opens in the
browser; upstream shows it in an embedded browser, which Avalonia does not have.

**FormBrowse toolstrip** (`partial`). 2026-10-11: the browse toolbar rebuilt in the order of upstream's `ToolStripMain`,
with upstream's images by resource name (`UpstreamImages`, which reads the linked `Images.resx`): refresh; the left panel and
commit info toggles; the submodules, worktrees, working directory and branch drop-downs; the pull split button (merge,
rebase, fetch, open dialog, fetch all, fetch and prune all, and the default pull action, shown as the button's image and
text); push with upstream's `AheadBehindData.ToDisplay` text and tooltips; "Commit (n)" with `RepoStateVisualiser`'s
image; the stash split button; the folder, terminal and settings buttons; then the filter toolstrip (see **FilterToolBar**).
The working directory caption is upstream's `RecentRepoSplitter` caption and `PathUtil.GetDisplayPath`, and the window
title is upstream's `AppTitleGenerator` (both linked GitCommands code, unchanged). The path box, Open, Init, Clone, Fetch,
Push tags, Files and Reflog buttons were removed; their actions are in the menus, as upstream. Differences: upstream's
stash button opens its stash window, which the new shell does not have, so the button stashes; toolstrip overflow is not
ported. 2026-10-13: the layout toggles are checked buttons (`ToolToggleButton`) as upstream's, and the split view toggle
is kept in upstream's `ShowSplitViewLayout`; the commit info position split button (`CommitInfoPosition`: below the grid
as the first tab, or left or right of it at upstream's width, the button cycling through them and showing the chosen
image and text, as `LayoutRevisionInfo` and `RefreshLayoutToggleButtonStates`); the terminal split button (see
**Terminals**).

**FilterToolBar** (`partial`). 2026-10-13: upstream's filter toolstrip in its order and with its images: the advanced
filter split button (opens the dialog, or with a filter on offers "Reset path filter", "Reset revision filters" and
"Advanced filter"; `FunnelExclamation` and the filter summary as tooltip while a filter is on), the reflog toggle, the
branches split button (all, current only, filtered; the button shows the chosen item's image and text), the "Branches:"
box (Enter applies it; a word that is no revision is reported with upstream's text and left out; the list offers local
branches, remote branches and tags as the "Branch type" menu says), and the "Filter:" box (Enter applies the text to the
kinds the "Filter type" menu checks, a changed kind applies it again; applied texts are remembered in upstream's
`RevisionFilterDropdowns`, then upstream's three presets), and the first parent toggle. Ctrl+E moves between the two
boxes as upstream's `SetFocus`. The new shell's own "filter loaded commits" and "search history" boxes were removed with
it (upstream has neither; the grid's quick search stays). Not ported: the shortcut texts in the tooltips and menus, the
"No results found" item, and the drop-down width that fits the longest branch.

**Terminals** (`partial`). 2026-10-13: upstream's terminal split button (`userShell`): on Windows the installed shells in
upstream's order and with its images (bash as git-bash.exe, else bash.exe or sh.exe; cmd; pwsh; powershell), found with
upstream's `PathUtil.TryFindShellPath`; the button opens bash, or the first one installed, in the working directory. On
Linux and macOS the list has the system terminal (the `TERMINAL` variable or `x-terminal-emulator`; Terminal on macOS).
Not ported: the embedded console tab (ConEmu), which is Windows-only.

**Revision grid rows and View menu** (`partial`). 2026-10-13: no column header row, as upstream's grid. The labels as
`MessageColumnProvider` lays them out (`GridLabels` in core): no HEAD label, sorted as `SortRefs` (checked-out branch,
local, remote, tags), the checked-out branch bold after upstream's filled arrow, a local branch and the remote branch it
tracks on the same commit as one pair (the remote shown by its remote's name, notched against the branch's point), the
ahead and behind arrows of a branch tracking or tracked elsewhere as a dashed virtual label, tags pointing left, a remote
branch named as the single local branch shown by its remote, bisect marks as upstream's images; drawn as
`RevisionGridRefRenderer` draws them (`RefLabelView`: its shapes, the colors and their light and dark shades, the window
color on a selected row, `FillRefLabels`). The labels read the tracked branch (`%(upstream)`) and upstream's
`AheadBehindDataProvider` with the refs. The checked-out commit's subject is bold; fixup, squash and amend commits get
upstream's marker; the commit body follows the subject in the gray text color (`ShowCommitBodyInRevisionGrid`). An
artificial row is a label with upstream's change icons and counts (changed, new, deleted) or the clean image. Row colors as
`RevisionDataGridView`: the system highlight and highlight text for the selection, `InactiveSelectionHighlight` while the
grid has no focus, `AuthoredHighlight` for the commits of the selected commit's author or, without a selection, the user's
(`AuthorRevisionHighlighting`, `HighlightAuthoredRevisions`), every other row a shade darker
(`RevisionGraphDrawAlternateBackColor`), no hover color. The View menu is upstream's `GetViewMenuCommands` in its groups
and order with the items the new shell has (branch choices and reflog, advanced filter, graph styles, artificial commits,
remote branch and tag labels, build status, commit body, dates, the graph, avatar, author, date and SHA-1 columns, author
date and topo order), the group captions in italics as `MenuUtil.SetAsCaptionMenuItem`. Not ported: stashes, git notes and
session refs in the grid, the superproject labels and refs, the notes column, per-remote label colors, the label hover
frame and tooltip per label, the multi-line indicator, non-relatives drawn in gray text (their lanes are gray), the
"unknown" artificial state and submodule counts, the merge source arrow, and "Save current view settings as default".

**CommitInfo and the detail tabs** (`partial`). 2026-10-13: the detail tabs carry upstream's images (Commit, Diff, File tree,
GPG). The commit info is upstream's layout on the window color: the avatar beside the header, whose rows come from
upstream's `CommitDataHeaderRenderer` (author, dates, committer, hash, children and parents; the children are the loaded
commits whose parent it is), the message from upstream's `CommitDataBodyRenderer` on a panel 4% darker, then what
`CommitInfo.UpdateRevisionInfo` lists: the annotated tags' messages, the related links, the branches containing the commit
(ordered as `BranchComparer`, limited as `RefsFormatter` with its "Show all" link), the tags containing it (newest first)
and the tag it derives from (`GitDescribeProvider`). Upstream renders XHTML into rich text boxes; the new shell renders
the same markup with upstream's renderers and `LinkFactory` and reads it back (`CommitInfoText.Parse`) into selectable
text with links (`InfoTextBlock`). Links to commits, branches and tags select the commit in the grid, or show upstream's
"not visible in the revision grid" error; other links open in the browser. The context menu has upstream's Copy link,
Copy commit info (`CommitDataHeaderRenderer.GetPlainText` and the message) and the six "Show ..." choices, kept in
upstream's settings. Copy hash and Copy message are only in the grid's menu, as upstream. The GPG tab is
`RevisionGpgInfoControl` over upstream's `GpgInfoProvider` and `GitGpgController`: shown for real commits while
`ShowGpgInformation` is on, read when the tab is selected. Not ported: the Console tab (ConEmu) and the Output history tab,
"Add notes", git notes in the message, the back and forward mouse buttons, the settings checkbox for GPG information, and
translations of the commit info texts that upstream keeps in `CommitInfo` and GitUI's `TranslatedStrings`.
