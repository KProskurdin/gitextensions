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
| `src/app/GitUI/CommandsDialogs/FormPush.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (push), `RepositoryOperationsViewModel.cs`; `MainWindow.axaml` (Push) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormPull.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (pull), `RepositoryOperationsViewModel.cs`; `MainWindow.axaml` (Pull) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormClone.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (clone), `RepositoryOperationsViewModel.cs`; `src/xplat/GitExtensions.Xplat.App/CloneWindow.axaml(.cs)` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormCreateBranch.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (create), `RepositoryOperationsViewModel.cs`; `MainWindow.axaml` (new branch box) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormCheckoutBranch.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (checkout, remote checkout), `RepositoryOperationsViewModel.cs`; `MainWindow.axaml` (Checkout) | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormDeleteBranch.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (delete), `RepositoryOperationsViewModel.cs`; `MainWindow.axaml` (Delete) | `52d08e996` | partial |
| `src/app/GitUI/Editor/FileViewer.cs` | `src/xplat/GitExtensions.Xplat.Core/Diff/DiffParser.cs`, `DiffService.cs`, `DiffViewModel.cs`; `src/xplat/GitExtensions.Xplat.App/DiffWindow.axaml(.cs)` | `52d08e996` | partial |
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
| `src/app/GitUI/CommandsDialogs/BrowseDialog/DashboardControl/Dashboard.cs` | `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (`DashboardPanel`, Start > Recent repositories); `src/xplat/GitExtensions.Xplat.Core/Repository/RecentRepositories.cs`, `RecentRepositoriesViewModel.cs` | `52d08e996` | partial |
| `src/app/GitUI/LeftPanel/RepoObjectsTree.cs` | `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (`LeftPanel`: Branches, Tags, Stashes, Remotes sections) | `52d08e996` | partial |
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

## Notes per row

**FormBrowse** (`partial`). Layout reworked 2026-10-06 to follow the upstream form. Ported: the menu bar (Start,
Repository, Commands, Tools, Help) with upstream's default hotkeys; a toolbar (path, Open, Refresh, Commit with the number of
changes, Pull, Push, Fetch, Clone, folder, terminal, filter, search); the left panel; the revision grid; a Commit tab
(details, copy hash and message) and a Diff tab (changed files with the selected file's diff inline); a status bar; a
warning bar while a merge or rebase is stopped or conflicts exist; the dashboard of recent repositories while none is open;
Close repository; the repository refresh after every operation; and, as upstream, a status refresh when the window is
activated again (at most every 2 seconds), so files edited in other programs show up. Not ported: the GPG, Console and
build-server tabs, the branch and repository dropdowns of the toolbar, and the Navigate and View menus (Plugins: see the PluginRegistry row; user
scripts: see the ScriptsManager row). Window size, position and maximized state are kept (upstream's `WindowPositions.xml` format, under the new shell's own
window names, `WindowPlacementTracker`). The File tree tab is ported (the folder tree at the selected commit, and the selected
file's content).

**RevisionGridControl** (`partial`). Added 2026-10-06: columns (graph and message, author, date, commit); HEAD, branch,
remote-branch and tag labels colored by kind; a context menu (copy hash or message, create a branch or tag here, browse
files, cherry-pick, revert, reset soft, mixed or hard); lanes that keep one color for their whole length; 5000 commits
loaded and scrolled at 60 fps (PLAN.md M3.5). Earlier: a read-only list of the first 500 commits from `HEAD`,
with short hash, subject, author and date, using the shared `RevisionReader.GetLog`. Selecting a row
loads its details through `RevisionReader.GetRevision`. Also ported: search of the whole history by message, ignoring case (the Search history box, Enter; git log --grep); a lane graph beside each commit (since 2026-10-07 upstream's `RevisionGraph` with the ported renderer, see
GraphRenderer below). Not ported: column layout
and sorting, paging past 500 commits, the upstream filter bar and search (the box in the window filters only the loaded commits, by hash, subject or author), refs labels, and the changed-file list.

**FormStash** (`partial`). Ported: save with an optional message, a stash list, and apply, pop and drop of the selected
stash by its name. Also ported: including untracked files. Also ported: stashing only the selected files (Stash selected). Also ported: keeping the index (Keep index). Not ported: branching from a
stash. Also ported: the whole stash as a diff (Show diff, first parent only).

**FormCreateTag** (`partial`). Ported: a lightweight tag at the selected commit (or HEAD), listed in the window. Also ported: annotated tags when a message is entered. Not ported: signing and the tag-on-remote option.

**FormDeleteTag** (`partial`). Ported: delete a local tag selected in the list, and (2026-10-06) delete it on the remote the
branch tracks after a confirmation, with upstream's `push <remote> :refs/tags/<tag>`; the local tag is kept. Not ported: the
remote choice in the dialog and deleting both at once.

**FormCherryPick** (`partial`). Ported: cherry-pick the selected commit onto the current branch. Not ported: the
multi-commit selection, the no-commit option, the append-to-message option, and the commit-message edit step.

**FormResetChanges** (`partial`). Ported: reset the current branch to the selected commit with the soft and mixed
modes. Not ported: deleting whole untracked directories. Also ported: deleting selected untracked files after a confirmation (Delete untracked), the hard mode, behind a confirmation (Reset hard here), and
discarding the unstaged edits of selected tracked files, after a confirmation.

**FormRebase** (`partial`). Ported: rebase the current branch onto a selected local or remote branch, abort a stopped
rebase, and continue one after the conflicts are staged. Added 2026-10-06: the grid menu "Rebase current branch on" >
"Selected commit" and "Selected commit interactively..." (with upstream's confirmation, skipped when upstream's
`DontConfirmRebase` is set), and Skip and Edit todo beside Continue while a rebase is stopped, as the upstream form shows them
for any rebase in progress. The interactive rebase uses upstream's `Commands.Rebase` (autosquash from git's
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
merge starts from git's merge message and both apps share the draft). Not ported: GPG signing, skipping hooks,
commit templates, commit scripts, and the message history.

**FormPush** (`partial`). Ported: push of the current branch to the remote it tracks (origin when it tracks none), with upstream tracking. Not ported:
recursive submodules, pushing several branches at once, and the tags tab. Added 2026-10-06: the progress dialog (see
FormRemoteProcess); Push tags uses the upstream `Commands.PushTag` builder; a push dialog (`PushWindow`, Commands > Push...,
Ctrl+Up as upstream) with the remote, the local and remote branch, force or force with lease, and tracking, built with
upstream's `Commands.Push`. The toolbar's Push stays a quick push of the current branch (upstream's QuickPush,
Ctrl+Shift+Up).

**FormPull** (`partial`). Ported: pull of the current branch from the remote it tracks (origin when it tracks none), merge or rebase. Also ported: prune of remote-tracking branches on fetch (the Prune checkbox, next to Fetch). Not ported:
fetch tags, unshallow, fetch all remotes, and the merge-commit message options. Added 2026-10-06: the progress dialog
(see FormRemoteProcess), and a pull dialog (`PullWindow`, Commands > Pull/Fetch..., Ctrl+Down as upstream) with the remote and
branch, merge, rebase or fetch only, prune, and auto stash. Upstream's FormPull stashes and pops itself; the port passes
git's `rebase.autoStash` and `merge.autoStash` for that one command instead.

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
staged or commit file (`git difftool`, with `diff.guitool` or `diff.tool` required, as for the merge tool). Not ported: syntax
highlighting, line staging of new files, line reset, the other patch-line actions (cherry-pick or revert of lines), and
binary-file handling (the content view says a binary file is not shown).

**RevisionDiffControl** (`partial`). Ported: the files a selected commit changed, with status letters, the selected
file's diff beside the list (the Diff tab), and an action that opens it in its own window. Not ported: the per-file tree, filtering, the file history view, and the
diff of merge commits against each parent.

**FormFileHistory** (`partial`). Ported: the files at a commit (or HEAD), the commits that changed a selected file,
and a diff of that file in a chosen commit. Not ported: renames followed with `--follow`, blame, the tree view with
folders (the list is flat), the file's content view, and the branch and revision filters of the upstream form.

**FormBlame** (`partial`). Ported: per-line blame of a file as of a commit (line number, short hash, author, date, text),
read from `git blame --line-porcelain`; since 2026-10-06 a double-click or Enter on a line shows that commit's change to the
file. Not ported: the author margin and its coloring, selecting the commit in the grid, the blame-ignore-whitespace and
detect-moves options, and blame of a diff against the parent.

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
command to git's built-in tool definitions (its launches read the `guitool` keys first). Not ported: the other pages, custom
CSS themes, the tool path and command fields, and plugin settings.

**FormAbout** (`partial`). Ported: name, version, operating system, runtime and git version. Not ported: the contributor list
and the links.

**Dashboard** (`partial`). Ported: the recent repositories, newest first, from upstream's own history
(`RepositoryHistoryManager`, setting key `history`), shown while no repository is open and in Start > Recent repositories;
open with Enter or a double-click; remove an entry. Not ported: favourites and categories, the create, open and clone tiles,
and the path shortening options.

**RepoObjectsTree** (`partial`). Ported: a branch tree (`BranchTree` in core) with local branches under Branches and remote
branches under Remotes and their remote, both grouped in folders at '/', with branch counts and the checked-out branch
marked and kept visible; tags, stashes, remotes and submodules as sections, each with its actions. Not ported: the
worktree node (worktrees have their own window), filtering the grid by a branch, the tag folders, and the context menus.

**HotkeySettingsManager** (`partial`). Ported: the FormBrowse defaults for open, close, commit, refresh, pull and push dialogs,
quick pull, push and fetch, terminal (upstream's Git bash), settings, focus the filter, the grid and the left panel, create a
branch or tag, merge, rebase, stash and stash pop, with Cmd in place of Ctrl on macOS; from the FormCommit defaults, Ctrl+S
(stage all), Ctrl+Enter and F5. Since 2026-10-06 the FormBrowse hotkeys are read from and written to upstream's
`SerializedHotkeys` setting (the `HotkeySettings[]` XML, by upstream command code and name; other forms' entries are kept
as they are), and can be changed in Settings > Hotkeys, so a hotkey changed in either app applies in both. Not ported:
the commands of the RevisionGrid, FileViewer, LeftPanel and FormMergeConflicts sections that the new shell has no feature for, the Stash and BrowseDiff sections, the alternative next and previous keys of FormCommit, and the reset-to-defaults button. Added 2026-10-06 (later): the FormCommit section (focus panes, stage all, diff tool, refresh, next and previous file), editable in Settings. Fixed then: the sections are stored under upstream's names ("Browse", "Commit"); the fork had used "FormBrowse", so hotkeys were not actually shared before. Added after that: the RevisionGrid (parent, child, HEAD, filter, branch scope, merges, first parent), FileViewer (next and previous change, ignore whitespace, stage and unstage lines), LeftPanel (Delete) and FormMergeConflicts (merge tool, ours, theirs, rescan) commands the new shell has. Added 2026-10-07: the Scripts section (see ScriptsManager). Changed then: stored hotkeys are matched by section and command code, as upstream's `MergeIntoDefaultSettings` does, and written in place of the entry with the same code, so a renamed script keeps its hotkey; the fork had matched by name.

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
that name, ignoring case, from the browse and commit windows. Not ported: `ShowInFileList` scripts
and the selected file options from a file list, the script icons in menus (kept in the XML, not shown).

**ScriptsSettingsPage** (`partial`). Ported 2026-10-07: Settings > Scripts lists the scripts in run order (enabled check
box, name, event, command line), with Add (upstream's next identifier and "<New Script>" name), Delete, Up, Down and the
arguments help (upstream's text, kept equal by a test that reads upstream's source); the selected script's name, command,
arguments, event and options are edited below the list. Stored on OK, dropped on Cancel; upstream stores the page on every
save, the port only when the scripts changed. Not ported: the icon choice (upstream's embedded images; the icon is kept as
stored), the property grid itself (plain fields instead).

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
local and remote branches at the commit, each address once; a link opens in the browser. Not ported: the template icons, the
other commit info parts that upstream renders as links (branches, tags), and choosing a settings level on the page.

**AppearanceFontsSettingsPage** (`partial`). Ported 2026-10-07: Settings > Fonts edits upstream's application, commit, code
and monospace fonts (`font`, `commitfont`, `difffont`, `monospacefont`) in upstream's stored format. Upstream reads them
through `System.Drawing.Font`, which works only on Windows, so `FontSetting` parses and writes the same text. Avalonia has no
font dialog: the family is chosen from the installed fonts (a stored one that is not installed is listed too) and the size
typed in points. "(default)" removes the key, so the app's own font applies (upstream cannot unset a font). Only a font the
user changed is written. The fonts apply at once, without upstream's restart: the application font to the whole UI through
Fluent's font resources, the commit font to the commit message box and the commit details, the code font to the diff,
editors, blame, git output, command log and help, the monospace font to commit hashes and the reflog. Not ported: bold and
italic (kept as stored, not shown), "Show end-of-line markers as glyph" (the diff view shows no end-of-line markers).

**FormBrowseRepoSettingsPage / revision grid tooltips** (`partial`). Ported 2026-10-07: "Show revision tooltips"
(`ShowRevisionGridTooltips`, on by default) and the tooltips it controls, with upstream's texts: the message column shows
the message summary (upstream's `GitRevisionSummaryBuilder`) when it has more lines or the commit has refs, then the refs in
brackets (local branches, remote branches, tags); the author column the author and committer; the date column both dates;
the hash column the full hash. Upstream needs a restart; here the setting applies when the settings are saved. Not ported:
ahead/behind counts in the ref lines and the tooltips of a hovered ref label (the shell has no ahead/behind data per ref),
the artificial commits' change counts, and the page's other settings: the Console tab and its shell, GPG information, find
in commit files with git grep, output history (the shell has none of them); "Show file history in the main window" and
"Show blame in diff viewer" (the shell shows file history and blame in their own windows only).

**BlameViewerSettingsPage / blame gutter and menu** (`partial`). Ported 2026-10-07: the blame window blames with upstream's
flags (`-M`, `-C`, `-w` from `DetectCopyInFileOnBlame`, `DetectCopyInAllOnBlame`, `IgnoreWhitespaceOnBlame`) and shows
upstream's gutter line (author and date in the chosen order, the time, the original file path) on the first line of each run
of lines from one commit, with line numbers when chosen. Its context menu has the blame settings of upstream's file history
window, with upstream's rules (the author or the date always shows; the time only with the date); Settings > Blame viewer has
the page with upstream's labels and warning tooltip. Differences: the short hash stays in its own column (a column the new
shell had before), shown once per run like the gutter line. Not ported: the author avatar, the age colors of upstream's
gutter, and blaming the line's commit or its parent from the menu.

**GraphRenderer / SegmentRenderer / DetailedSettingsPage** (`partial`). Ported 2026-10-07: upstream's graph model is linked
unchanged; its renderer is ported line by line from WinForms' Graphics to shapes (`GraphPainter`): lanes, shared lanes,
diagonals or curves, perpendicular junctions and bows, square nodes for commits with refs, the outline of HEAD, upstream's
sizes. Differences: upstream's 1/8-pixel anti-aliasing shift for GDI+ is left out; the two lane states upstream throws on
are skipped instead; colors come from the theme's lane colors by index (stand-in `RevisionGraphLaneColor`). Not ported:
the gray non-relative and highlight draw styles, hover highlighting, the cached row bitmaps (`GraphCache`), lane tooltips
(`LaneInfoProvider`, `BranchFinder`), artificial working-tree rows, and the Detailed page's push and merge window options.

**GeneralSettingsPage / AppearanceSettingsPage / DateColumnProvider** (`partial`). Ported 2026-10-07: relative or full date
of the author or the committer in the grid (also in search results), with upstream's View menu toggles; open the last
working directory on startup (upstream's `Program` rule, only for a valid repository); the changed-file count on the
Commit button; the default clone destination. Not ported: the other settings of both pages (see PLAN.md M5).

**DiffViewerSettingsPage / FileViewer options** (`partial`). Ported 2026-10-07: the four whitespace modes (one box instead
of upstream's three toggle buttons, same states), fewer and more lines of context (down to none, buttons off while the
entire file is shown) and the entire file, with upstream's diff arguments and hotkeys (IgnoreAllWhitespace, Increase and
DecreaseNumberOfVisibleLines, ShowEntireFile), kept for the next view in this run as upstream's runtime settings; the three
"Remember" settings and "Save current view settings as default". Not ported: the page's other settings (non-printing
characters, syntax highlighting, diff appearance, combined diff, continuous scroll, submodule diff, all parents, difftool
list, vertical ruler, Git coloring) and the FileViewer features they configure.

**CommitDialogSettingsPage** (`partial`). Ported 2026-10-07: the second-line setting, the remembered Amend check box and
the Commit & Push button, under upstream's keys and labels. In the commit window (FormCommit row): the message goes through
upstream's `FormatCommitMessage` before the commit (it was given to git as typed before), Amend is put back checked when it
was left checked (not during a merge, and keeping the draft, as upstream), and Commit and push hides when turned off. Not
ported: show errors when staging, compose in the commit dialog, the number of previous messages, the reset buttons,
auto-completion.

**ConfirmationsSettingsPage / ConfirmSuppressible** (`partial`). Ported 2026-10-07: the confirmations whose action the
new shell has (amend, commit with no branch, rebase, fetch with prune, push of a new branch, force delete of an unmerged
branch, left panel checkout, stash drop, worktree switch), under upstream's keys and labels, asked with upstream's texts
and "Don't show me this message again" (not for the left panel checkout, as upstream). Where they are asked: the commit
window (amend; no branch unless a rebase is under way), the grid's rebase, quick fetch with Prune and the pull dialog's
fetch with prune, quick push and the push dialog when the remote branch is not among the fetched remote branches, Force
delete, the left panel's checkout, Drop stash, and opening a worktree. Not ported: undo last commit, the tracking reference
question, the automatic stash pop after pull and checkout, the conflict resolution questions, the second abort question,
submodule update on checkout, the branch buttons of the "not on a branch" question, and upstream's Yes/No buttons (the
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
in `src/xplat/plugins` (only their bitmap resources are replaced, see PLAN.md M6). The other upstream plugins use WinForms
forms and stay Windows-only.

**GitUICommands (plugin side)** (`partial`). `PluginHost` is the `IGitUICommands` the plugins get: the current module,
`RepoChangedNotifier` (from any thread; it refreshes the window on the UI thread through `PostRepositoryChanged`, as
upstream), the events `PreCommit` and `PostCommit` (around the commit window, as `StartCommitDialog`), `PreCheckoutBranch`,
`PostCheckoutBranch`, `PreCheckoutRevision`, `PostCheckoutRevision` (around a checkout; a "pre" handler can cancel it),
`PostSettings`, `PostUpdateSubmodules`, `PostEditGitIgnore`, `PostRegisterPlugin`, `PostBrowseInitialize`; settings
(`StartSettingsDialog` for a plugin opens its page), the commit window, and command and git process windows. Unlike
upstream, `StartCommandLineProcessDialog` returns once the command started, not when it ended. Every other dialog throws
`NotSupportedException`, which the Plugins menu shows as an error. Not ported: commit templates
(`AddCommitTemplate` is accepted and ignored), `RunCommand`, services through `GetService`, and `BrowseRepo`.

**PluginSettingsPage / SettingControlBindings** (`partial`). Ported 2026-10-07: Settings > Plugins lists the plugins and
shows the selected plugin's settings with the editor of upstream's binding for its type: three-state check box, text box
(with upstream's "<empty string>" marker and placeholder), password box, number box (red while not a number of the
setting's type; an invalid number is stored as no value, as upstream), drop-down list, read-only note, and "There are no
settings available for this plugin." The values are the repository's effective settings when one is open (a value the
repository sets is changed there, any other in the user's settings, as upstream's `DistributedSettings` does) and the user's
settings otherwise; unchanged values are not written. Not ported: the settings level choice (local, distributed, global),
credentials settings and settings with their own WinForms control (shown as not editable).

**FormGitIgnore / FormAddToGitIgnore** (`partial`). Ported: edit the repository's top-level `.gitignore` as text (Repository >
Edit .gitignore...), and add the selected untracked files from the commit window (Ignore...), each as a root-anchored pattern
on a line of its own, skipping patterns already there and keeping the file's line ending. Not ported: the default ignore
templates, the preview of matched files, the "info/exclude" choice, and patterns for whole folders or extensions.

**FilterInfo / FormRevisionFilter** (`partial`). Ported: all branches (the upstream default) or the current branch only, kept
in upstream's `ShowCurrentBranchOnly` setting; the advanced filter for author, committer, message (ignoring case), since,
until, path, hide merges and first parent only, built in upstream's argument order; the all-branches view leaves out notes,
the stash and agent session refs as upstream does with its default settings (including `c16194306`). The status line says
when the list is filtered. Not ported: the custom branch filter (wildcards), reflog and simplify-by-decoration, the diff
content search (`-G`), full history and simplify merges, the commit count limit, and applying the filter to the history
search box (which still searches from HEAD).

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
appearance choice lists the same themes and the colorblind variation. Not ported: applying the panel, editor and selection
colors and the system color overrides to the controls, the theme editor, `UseSystemVisualStyle`, and the restart prompt
(the new shell applies a theme at once).
