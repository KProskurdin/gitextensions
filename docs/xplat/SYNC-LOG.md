# Upstream sync log

Record of every check against upstream (`gitextensions/gitextensions`, `upstream/master`) and every
merge of upstream into the fork. Procedure and targets: `docs/xplat/PLAN.md` section 11.

Add one entry per check or merge, newest first. Do not rewrite old entries; correct them with a new entry.

## Current state

| Item | Value |
|---|---|
| Fork base (last upstream commit the fork contains) | `0174ba1cc` fix(diff): do not run git after repo closed |
| Upstream `master` at last check | `c584b9740` fix(RevisionGrid): draw bisect ref labels (2026-11-08) |
| Upstream commits not yet in the fork | 55 |
| Upstream files changed since fork base | 157 |
| Fork changes in upstream folders (`src/app`, `src/plugins`, `tests/app`, `setup`, `eng`) | 0 |
| Seams (PLAN.md section 5) | 8 |
| Next sync due | 2026-10-17 (every 2 weeks while development is active) |
| Maintainer | not yet named (PLAN.md section 9, question 9) |

## Entries

### 2026-10-06: porting map review for the new rows, no merge

- Upstream checked: `52d08e996` (unchanged since the last entry; `git fetch upstream` brought nothing new).
- New rows written against the fork base and reviewed against `52d08e996`: `FormResolveConflicts` (`a4d51838f`, conflict
  description texts: skipped, the port shows none), `FilterInfo` (`c16194306`, agent session refs: ported, the all-branches
  view excludes them), `FormRevisionFilter` (`72373e530`, simplify merges only with full history: skipped, neither option is
  ported). The other new rows (`FormGitCommandLog`, `FormRemoteProcess`/`FormProcess`, `FormSettings`, `FormAbout`,
  `Dashboard`, `RepoObjectsTree`, `HotkeySettingsManager`, `FormGitIgnore`/`FormAddToGitIgnore`) had no upstream changes.
- `port-drift.sh`: 34 rows, 0 with upstream changes since their base, 0 stale.
- `xplat-drift.sh`: the two items decided on 2026-10-04 (`PathUtilTest`, `GitModuleWorktreeTests`); no new ones.
- Measures (PLAN.md 11.2): upstream files modified 0 (`git status` of `src/app`, `src/plugins`, `tests/app`, `setup`, `eng`
  is empty); seam count 7; no new upstream file read through reflection or copied. New reuse of upstream code as is:
  `RepositoryHistoryManager`, `CommandLog`, `PatchManager.GetSelectedLinesAsPatch`, `Commands.Push`/`PushTag`,
  `GitModule.PullCmd`/`FetchCmd`, and the upstream setting keys listed in `PORTING-MAP.md` (FormSettings row).
- Local `master` not moved; nothing committed.

### 2026-10-04: mirrored test review, no merge

- `xplat-drift.sh` flagged two mirrored classes against `upstream/master`, both changed after the fork base `0174ba1cc`.
- `648dd4cc7` (`PathUtilTest`, "prefer configured LinuxToolsDir"): adds a Windows-only test of `bash.exe` lookup under the
  Git for Windows install. Decision: no Linux counterpart; when this commit is merged, add the new method to
  `XplatPlatformSkips.cs` with reason `WindowsOnlyFeature`. Its production change is in `PathUtil.cs`, a shared file.
- `065da3680` (`GitModuleWorktreeTests`, "never offer to delete main worktree"): adds a platform-neutral test that the first
  worktree is the main one. Decision: no skip needed; the behavior it protects is in the WinForms left panel, which the
  shadow projects do not compile. Run it on Linux at the merge to confirm.
- The drift report stays red until the merge brings these commits into the fork. Nothing in the fork changes for them now.

### 2026-10-03: porting map review against upstream, no merge

- Upstream checked: `52d08e996`. `port-drift.sh` reported 4 of the 7 write-operation rows and the two read rows
  with upstream changes since `0174ba1cc`: GPG display, grid tooltip, `FormCommit` layout, `FormClone` submodule option.
- Each change was read and skipped with a reason in `PORTING-MAP.md`. All nine rows now use `52d08e996` as their base.
- `xplat-drift.sh` (mirrored tests) was not re-run in this entry; it still reports `PathUtilTest` and
  `GitModuleWorktreeTests`, which are open items from the earlier check.
- No merge. Local `master` is still at `0174ba1cc`.

### 2026-10-03: verification against upstream, no merge

- Upstream checked: `52d08e996`, 17 commits ahead of the fork base.
- Command: `bash tests/xplat/verify-upstream.sh` (builds and tests the shadow projects against
  upstream in a temporary worktree; the working tree and local branches are not touched).
- Result: pass on Linux (WSL openSUSE): 3369 passed, 0 failed, 144 skipped (Windows-only, with
  reasons), 3513 total. Windows was not run for this check.
- Seams: all 7 anchors present in `52d08e996`; the upstream change to `AppSettings.cs` is a new
  property with no overlap with the seams.
- Measures (PLAN.md 11.2): upstream files modified 0; seam count 7 (target at most 10); anchors
  intact yes; shadow build and tests pass yes; time to sync: not measured (no merge yet);
  ported forms out of date: none (no forms ported yet).
- Notes: this was a dry run. The fork is not merged with upstream yet. Local `master` is still at
  `0174ba1cc`; the user has not asked for it to be moved, and automation must not move it.
- Next: merge `upstream/master` into the fork branch when the user decides to do so, and record
  that merge as a new entry.

### 2026-10-11: verification against upstream, no merge (end of M0 to M6)

- Upstream checked: `afc512395`, 18 commits ahead of the fork base (`0174ba1cc`).
- Command: the steps of `tests/xplat/verify-upstream.sh`, run by hand on Windows. The script's own `git fetch` failed: the
  repository's `.git/config` had `http.proxy = :8080`, left there by a failed app test run of the same day (the ProxySwitcher
  plugin test ran against the process's working directory after a repository failed to open; the same run fetched a branch
  `pr/n7_ann/feature` into the repository). The fetch ran with `-c http.proxy=`, and the worktree was checked out with
  `core.longpaths` because upstream's snapshot file names are too long for Windows' default limit. Since then the app tests
  run from an empty temporary folder with `GIT_CEILING_DIRECTORIES` set, so a git command without a repository cannot reach
  the repository the tests start from. The proxy line and the branch are still in the repository: removing them was left to
  the user.
- Result on Windows: 3506 passed, 1 failed (`Load_performed_on_thread_pool_and_result_handled_via_callers_context`, the
  known Windows failure in `AsyncLoaderTests`), 1 skipped, 3508 total. Linux was not run for this check.
- `port-drift.sh`: 93 rows, 0 with upstream changes since their base, 0 stale.
- Measures (PLAN.md 11.2): upstream files modified 0; seam count 7; anchors intact yes (the shadow build passes on
  `afc512395`); shadow build and tests pass yes (with the known failure); ported forms out of date: none.
- Fork tests the same day: 384 core and 276 app on Windows; 384 core on Linux (WSL). The Linux app run was stopped by the
  host for lack of memory and has to be repeated.
- No merge. Local `master` is not moved.

### 2026-11-08: first full test run on macOS, no merge

- Machine: MacBook Air, Apple Silicon, macOS 27.2, Apple git 2.54, Homebrew .NET SDK 10.0.401.
- Results on macOS, all from one tree: core 900 of 900; app 704 of 704; shadow projects 3821 pass, 0 fail, 201 ignored with
  reasons. Windows and Linux were not run for this: the first CI run will be their check. Details in `MACOS.md`, MAC2.
- **Found: `run-tests.sh` deleted the user's real settings file on every run off Windows** (the test attribute, and the shim's
  data folder). The user's settings on this Mac were lost on 2026-11-08, and the scratch repository of a smoke run was then
  written into the recent list, which was corrected by hand to this repository. Fixed, and checked by hash on every suite.
- Found: the open worktree was not recognized when the repository's path goes through a link, so it could be deleted. Fixed.
- Upstream checked: `c584b9740`, 55 commits ahead of the fork base. `tests/xplat/verify-upstream.sh` on macOS: 3902 passed,
  0 failed, 222 ignored. That is after adding the 8 new `RecentRepoSplitterTests` methods (21 cases, Windows path inputs only)
  to the skip list, and fixing the script for macOS's linked temporary folder. Windows and Linux were not run for this check.
- `port-drift.sh`: 132 rows, 12 with upstream changes since their base, 0 stale; none reviewed yet. `xplat-drift.sh` not run:
  it needs bash 4 or newer.
- Measures (PLAN.md 11.2): upstream files modified 0; seam count 8, all in the section 5 table, no new one; anchors intact yes;
  shadow build and tests pass yes; ported forms out of date: the 12 rows above.
- Local changes only; `master` is not moved. The `upstream` remote was added, as `MACOS.md` section 3 says.

### 2026-11-08: after MAC3 to MAC5, no merge

- `tests/xplat/verify-upstream.sh` on `c584b9740` on macOS: 3902 passed, 0 failed, 222 ignored. Upstream files modified 0, seam count 8.
- On macOS: core 912, shadow projects 3821 pass and 201 ignored, then app 718 of 718 after the last change, which touched only the
  commit window's completion and app tests. The settings folder and `~/.gitconfig` were unchanged by hash in each run. Windows and
  Linux were not run.

### 2026-11-08: after the Settings, editor, git-grep, toolbar and blame slices of `UI-PARITY.md`'s Mac list, no merge

- On macOS, from one tree: core 929 of 929; app 738 of 738 (one more is Windows only); shadow projects 3821 pass, 201 ignored. The settings
  folder and `~/.gitconfig` were unchanged by hash through each suite.
- `verify-upstream.sh` on `c584b9740` last ran before the toolbar and blame slices, which changed only `GitExtensions.Xplat.App` and
  `GitExtensions.Xplat.Core` and their tests, none of which that script builds: 3902 passed, 0 failed, 222 ignored. `port-drift.sh`: 133 rows, 12
  with upstream changes. Windows and Linux were not run.

### 2026-11-08: after the dialogs of `UI-PARITY.md`'s Mac list and the first CI results, no merge

- On macOS, from one tree: core 956 of 956; app 752 of 752 (one more is Windows only). The settings folder and `~/.gitconfig` were unchanged by hash
  through the app run. Shadow projects were not run again: nothing they build changed since 3821 passed, 201 ignored. All 12 also pass on a clone with
  Windows line endings of `5ea555567`, on macOS. `verify-upstream.sh` was not run again, for the same reason. `port-drift.sh`: 134 rows, 12 with upstream
  changes. Windows and Linux were not run locally; CI's first run is in `MACOS.md`, MAC1.

### 2026-11-09: after the revision grid slice of `UI-PARITY.md`'s Mac list, no merge

- The grid's session refs ("Show session checkpoints") and the superproject's labels and refs in a submodule; the "Revision grid rows and
  View menu" row of `PORTING-MAP.md` is now `ported`.
- On macOS, from one tree: core 961 of 961; app 755 of 755 (one more is Windows only), then 22 grid tests again after a last cleanup in
  `CommitListViewModel`. The settings folder and `~/.gitconfig` were unchanged by hash through the app run. Shadow projects and
  `verify-upstream.sh` were not run again: only `GitExtensions.Xplat.App` and `GitExtensions.Xplat.Core` and their tests changed. Upstream files
  modified 0, seam count 8. Windows and Linux were not run.

### 2026-11-09: after most of item 5 of `UI-PARITY.md`'s Mac list, no merge

- Git's `commit.template`, the plugin templates' icons, the action bars' icon, the pull request files' icons, the filter boxes'
  drop-down width, the plugin load error's copy, `GetService` and `BrowseRepo` for plugins, and the commit info translations. Rows
  now `ported`: Commit templates, FormBisect, ViewPullRequestsForm, FilterToolBar, PluginRegistry. Left of item 5: `RunCommand` and
  the command line verbs, to be decided.
- On macOS, from one tree: core 972 of 972; app 763 of 763 (one more is Windows only). The settings folder and `~/.gitconfig` were
  unchanged by hash through the app run. Shadow projects and `verify-upstream.sh` were not run again: only `GitExtensions.Xplat.App`
  and `GitExtensions.Xplat.Core` and their tests changed. Upstream files modified 0, seam count 8. Windows and Linux were not run.

### 2026-11-09 and 10: after item 6 of `UI-PARITY.md`'s Mac list, no merge

- The command line verbs and `RunCommand` are recorded as not ported for now (decided 2026-11-09). Item 6: the Colors page's "Open theme
  folder" (upstream has no theme editor since 2019), the spelling checker with "Mark ill formed lines" and the dictionary setting, and the
  output history. New shadow project: `src/xplat/NetSpell.SpellChecker` compiles `externals/NetSpell.SpellChecker` unchanged; upstream's
  `.dic` files are copied next to the app. Rows now `ported`: ThemeModule, OutputHistory (new).
- On macOS, from one tree: core 994 of 994; app 772 of 772 (one more is Windows only); shadow projects 3821 pass, 0 fail, 201 ignored, of
  4022. The settings folder and `~/.gitconfig` were unchanged by hash through the app and shadow runs, and the tests wrote no NetSpell user
  word list. The whole `GitExtensions.xplat.slnx` builds. `verify-upstream.sh` was not run again. Upstream files modified 0, seam count 8.
  `port-drift.sh`: 135 rows, 12 with upstream changes. Windows and Linux were not run.

### 2026-11-13: review of the commits since `MACOS.md` against the plan, no merge

- Reviewed `04d868edd..34744f906`. The 2026-11-10 commits were made on Windows (staging reads only the changed files; opening reads the
  state while the commits load, without `git submodule status`; the Dock icon outside a bundle; solution items). Each is in `PORTING-MAP.md`
  or `MACOS.md`; `UI-PARITY.md`'s Progress has an entry for them now.
- **Correction to the entries of 2026-11-09 and 10:** "Upstream files modified 0" was wrong. `fc4d4ba5d` (2026-11-08) comments out the 13
  upstream workflows in `.github/workflows/`, and `.gitignore` has a fork line since `735c6ae4d`. The measure only looked at the code folders.
  `PLAN.md` section 11.2 now checks every path; the files are listed in section 5 and the choice is section 9, question 16. Seam count 8.
- CI: off since `fc4d4ba5d`, so no run since `42521621315`.
- On Windows, from `34744f906` with these doc changes only: the whole `GitExtensions.xplat.slnx` builds with 0 errors (warnings include SA1518 in
  `PushWindowTests.cs`, no final newline). Core 992 pass of 1001, 9 not run on Windows. App, the classes touched since 2026-11-08 only
  (`ApplicationMenuTests`, `MainWindowTests`, `GitOperationsTests`, `CommitTemplateFileTests`, `LeftPanelTests`, submodule tree): 187 of 187.
  The full app suite, the shadow projects and `verify-upstream.sh` were not run. Linux and macOS were not run.

### 2026-11-20: first Windows check after the macOS work of 2026-11-18 to 20, no merge

- Checked `dcc0e441d` plus uncommitted fixes, on Windows. Details: `PLAN.md` section 12.6, step 5, the note of 2026-11-20.
- Found and fixed: a click handler back in `DiffView` ("Add selection to commit message" is a view model command now); a core test
  that expected backslashes in a POSIX path on Windows (written on the Mac); analyzer warnings in the files changed on 2026-11-19
  (VSTHRD001 in `OutputReporter` muted with its reason); and the left panel losing the keyboard focus when a refresh fills the tree
  again, which made `LeftPanelTests`' multi-select test fail only in full runs (`PORTING-MAP.md`, RepoObjectsTree).
- On Windows, from one tree: the whole `GitExtensions.xplat.slnx` builds with 0 errors; core 1410 of 1410; app 813 of 813 (816, 3 are
  macOS only). Before the fixes: core 1406 of 1407, app 810 of 811, then 811 of 812 (the same multi-select test). Shadow projects and
  `verify-upstream.sh` were not run: only `GitExtensions.Xplat.App` and `GitExtensions.Xplat.Core` and their tests changed. The fixes
  touch no upstream file; seam count unchanged. CI is still off. Linux and macOS were not run.

### 2026-11-20, later: Windows-only parity items of `UI-PARITY.md`, no merge

- Done on Windows: the Git page's "Download Git" link and upstream's HOME check after a save (FormFixHome's search); "Show
  console window when executing git process"; the PuTTY prompts of the output window and the clone window, with upstream's
  FormPuttyError. Decided with the user: the Console tab (ConEmu, Mintty) stays `windows-only`. `PORTING-MAP.md` has 3
  `partial` rows left (the Shell extension page, and `RunCommand` with the command line verbs, set aside 2026-11-09).
- On Windows, from one tree: the whole `GitExtensions.xplat.slnx` builds with 0 errors; core 1434 of 1434; app 817 of 817
  (820, 3 are macOS only). `port-drift.sh`: 141 rows, 0 with upstream changes since their base. Shadow projects and
  `verify-upstream.sh` were not run: only `GitExtensions.Xplat.App` and `GitExtensions.Xplat.Core` and their tests changed,
  and no upstream file. The PuTTY prompts were not tried against a real PuTTY server. Linux and macOS were not run.

### 2026-11-20, evening: the commit window's next file, two flaky tests and the test host's memory, no merge

- Fixed on Windows (found by the user): after a stage or unstage the commit window selected nothing, so the diff kept
  showing the file that had moved; it now selects the next file as upstream does (`PORTING-MAP.md`, FormCommit).
- Full `GitExtensions.xplat.slnx` run on Windows, on `d82a23c8b`: 6236 passed, 3 failed, 2 skipped of 6241. The known
  `AsyncLoaderTests` failure in `GitCommands.Tests`, and two headless tests that failed only in that run, with all 14
  test projects at once: `LeftPanelTests`' search test (fixed: it held a tree node a refresh had replaced, and the window
  could select such a node too; `PORTING-MAP.md`, RepoObjectsTree) and `AdvancedSettingsTests`' new branch name test
  (the Create Branch window did not finish loading in 20 seconds; not reproduced in 8 runs, two of them under load, and
  not changed).
- A second full run was stopped by Claude Code for lack of memory: the app test host had grown to 7.4 GB. The cause was
  in the tests: most end with their window open, an open window stays in Avalonia's render loop, and each main window
  joins the app's shared output history, so every window kept the ones before it (about 18 MB per test). The recent
  repositories list, shared by all tests, also grew with each opened repository, and a late test's Start menu and
  dashboard listed hundreds of entries. New `tests/xplat/GitExtensions.Xplat.App.Tests/TestIsolation.cs`, an assembly test
  action: it closes the windows a test left open, and gives each test its own output history and recent list.
  `A_large_file_waits_for_a_click` wrote its 5 MB as one line, which the headless view took about 4 minutes to lay out; it
  now writes lines of a kilobyte. No product code changed for this.
- App suite on Windows after the changes: 818 of 818 in 13 minutes (27 to 29 before); the tests' own time 796 seconds
  (1630 before); live managed memory stays between 40 and 110 MB, the test host's working set about 600 MB. Left as they
  are: `The_tool_catalog_lists_what_git_finds_without_terminal_tools` (26 seconds: git's `--tool-help` takes 13 seconds
  per list on Windows, and upstream's cache runs the two one after the other); merging tests to save the second each
  spends opening a window (not done: about 12% at most, and one behaviour per test is the rule).
- Found, not fixed: `DiffView.ShowSettings` is static and set by every new main window, so with two windows open a
  viewer's settings item opens Settings over the newer one, even after it closed; and a file that is one very long line
  (several megabytes) holds the viewer for minutes after "Show large file".
- Core 1435 of 1435 in the full run. Shadow projects passed but for the known failure. `verify-upstream.sh` was not run:
  no upstream file changed. Linux and macOS were not run.

### 2026-11-20, late: the macOS check of the Windows work, and two viewer fixes, no merge

- On macOS (MacBook Air, Apple Silicon, macOS 27.2, Apple git 2.54, .NET SDK 10.0.401), from `4587038c7` unchanged: the
  whole `GitExtensions.xplat.slnx` builds with 0 errors; core 1441 of 1441; app 819 of 820 in 14 minutes 34 seconds. The
  failure was `ThemedImagesTests.The_adapted_pictures_are_those_upstream_adapts`, because that run was built into a folder
  outside the repository and the test looks for `src/app` above its own folder; it passes from `artifacts/`.
- Fixed (`UI-PARITY.md`, "Next on the Mac", items 2 and 3; `PORTING-MAP.md`, FileViewer): the viewer's settings button
  used a static every new browse window set, and is now opened by the browse window that owns the viewer's window; and a
  line with many colored parts (syntax runs or find's marks) took time growing with their square, so past 1,000 parts a
  line keeps its own color. The slowness of a one-line 5 MB file in headless tests was Avalonia's headless text shaper:
  with Skia's shaping, as the app has it, such a plain line takes about a second. New tests: `DiffViewSettingsTests` (3)
  and `DiffTextViewLongLineTests` (2), each failing on the code before its fix.
- On macOS after the fixes, from one tree: the whole `GitExtensions.xplat.slnx` builds with 0 errors and 0 warnings;
  core 1441 of 1441; app 825 of 825 in 15 minutes 4 seconds (`caffeinate -dis`, no sleep in `pmset -g log` during the
  run), the test host's resident memory at most 836 MB (Windows: 13 minutes, about 600 MB). No upstream file changed;
  seam count unchanged. Not tried at the screen in the real app. Windows and Linux were not run.
- Shadow projects on macOS: 12 of 12 pass (`GitCommands.Tests` 3381 passed, 149 skipped of 3530).
- `verify-upstream.sh` on macOS against `upstream/master` `17213fe3f` (73 commits past the fork's base `0174ba1cc`) first
  failed: 4 tests in `GitCommands.Tests`, all new with upstream's `645715232` (`fix(worktree): remove using git`,
  `TryRemoveWorktree`). Three pass Windows paths (`C:\repos\...`, `\\wsl$\...`); one makes a folder undeletable by
  holding a file open with `FileShare.None`, which macOS and Linux do not prevent, so the deletion succeeds and the code
  goes on to `worktree prune`. Not a broken seam. Both methods are now in `XplatPlatformSkips.cs` (Windows path input;
  Windows file lock). Their POSIX counterparts cannot compile in the fork before the merge brings `TryRemoveWorktree`,
  so they are for the merge (`PLAN.md` 12.1). After: `verify-upstream.sh` passes, 12 of 12 (`GitCommands.Tests` 3419
  passed, 174 skipped of 3593), and the fork's `GitCommands.Tests` still 3381 passed, 149 skipped.

### 2026-11-20, night: the settings folder, the macOS menu bar, the command line verbs, no merge

- On macOS, chosen by the user from `UI-PARITY.md` "Next on the Mac" item 5: on Windows the shim's `UserAppDataPath` is
  WinForms' folder, so the shell and the WinForms app share `%APPDATA%\GitExtensions\GitExtensions\GitExtensions.settings`
  (macOS and Linux keep theirs; nothing is moved); the browse window's menus are in the macOS menu bar (`SystemMenuBar`,
  `PLAN.md` question 15); upstream's command line is ported, every verb and `browse`'s filters (`PORTING-MAP.md`, Command
  line). `MACOS.md` section 10 is a new checklist for the checks that need a person at the Mac (rows 1 to 19).
- On macOS, from one tree: the whole `GitExtensions.xplat.slnx` builds with 0 errors and 0 warnings (the one SA1515 of the
  run below fixed after it); core 1461 of 1461; app 865 of 865 in 17 minutes 54 seconds, the test host at most 827 MB; the
  shadow projects 12 of 12; `verify-upstream.sh` passes against `17213fe3f`. No sleep during the runs. No upstream file
  changed; seam count unchanged.
- Not tried at the screen: the menu bar itself (a rendered window of the real app had no menu row, so macOS took the menus)
  and the verbs' windows. In the real app `commit --quiet` in a clean repository exited 0 after 2 seconds with no window, and
  `about` and `stash` opened and stayed open. The settings folder's check is Windows only. Windows and Linux were not run.

### 2026-11-20, late night: the plugins' RunCommand and Start...Dialog methods, no merge

- On macOS: every `IGitUICommands` method that threw now runs the browse window's flow and returns its answer, as
  upstream's, waiting in a nested dispatcher frame (`ModalWindow.Wait`); `RunCommand` runs a command line verb on the open
  repository. Only `ShowModelessForm` (a WinForms form) still throws. `PORTING-MAP.md`, GitUICommands (plugin side), now
  `ported`. Small changes to the browse window's flows, the menus unchanged: the quick pull and push and the pull and push
  dialogs answer whether they ran, the pull dialog takes a remote, branch and action, and cherry pick, revert, archive and the
  special commits have variants taking the revision.
- On macOS, from one tree: the whole `GitExtensions.xplat.slnx` builds with 0 errors and 0 warnings; core 1461 of 1461; app
  868 of 868 in 13 minutes 22 seconds, the test host at most 862 MB; the shadow projects 12 of 12; `verify-upstream.sh`
  passes against `17213fe3f`. No sleep during the runs. No upstream file changed; seam count unchanged. Not tried with a
  third-party plugin or at the screen. Windows and Linux were not run.
