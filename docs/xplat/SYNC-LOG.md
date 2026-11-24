# Upstream sync log

Record of every check against upstream (`gitextensions/gitextensions`, `upstream/master`) and every merge of upstream into
the fork. Procedure and targets: `docs/xplat/PLAN.md` section 11.

Add one row per check or merge to the table below, newest last: what was checked, on which OS, the result, and what changed
in the fork because of it. Work on the fork itself is recorded in git history, not here. Trimmed on 2026-11-24 to the checks
and the items still open; the full entries before that are in git history.

## Current state

| Item | Value |
|---|---|
| Fork base (last upstream commit the fork contains) | `0174ba1cc` fix(diff): do not run git after repo closed |
| Upstream `master` at last check | `bd69886b7` (2026-11-24, 76 commits past the fork base, 210 files changed) |
| Fork changes in upstream folders (`src/app`, `src/plugins`, `tests/app`, `setup`, `eng`) | 0 |
| Changes at other upstream paths | `.gitignore` (one line) and `CLAUDE.md`; `PLAN.md` section 5 |
| Seams (PLAN.md section 5) | 9 |
| Next sync | When the maintainer chooses (PLAN.md section 9, question 9); overdue after 3 months without one |
| Maintainer | KProskurdin (PLAN.md section 9, question 9) |

## Due at the next merge

Upstream tests that need fork work once the merge brings their commits in:

1. `648dd4cc7` (`PathUtilTest`, "prefer configured LinuxToolsDir"): a Windows-only test of the `bash.exe` lookup. Add the
   new method to `XplatPlatformSkips.cs` with reason `WindowsOnlyFeature`; no Linux counterpart.
2. `065da3680` (`GitModuleWorktreeTests`, "never offer to delete main worktree"): platform neutral, no skip. Run it on
   Linux to confirm.
3. `645715232` (`fix(worktree): remove using git`, `TryRemoveWorktree`): 4 tests are already in `XplatPlatformSkips.cs`
   (three pass Windows paths; one holds a file open to make a folder undeletable, which macOS and Linux allow). Write their
   POSIX counterparts once `TryRemoveWorktree` is in the fork.
4. The 8 `RecentRepoSplitterTests` methods of `c584b9740` and earlier (Windows path inputs only) are in the skip list
   already; nothing to do beyond confirming they still compile. `e2bd84b07` ("distinguish filesystems") changed the class
   again: check its new cases on Linux.
5. New `GitModuleTests` methods (`1a80a42d3`, `a816fb0cd`, `656a626c1`, `afc512395`): run them on Linux and macOS.
   `IsGitFsMonitorDaemon_should_return_expected` passes command lines with `C:\...\git.exe`: skip it off Windows with a
   reason if it parses paths with the OS's separator, or leave it if it only matches text.
6. The 16 ported files with upstream changes since their row's base (`port-drift.sh` on `bd69886b7`): port each change or
   record why it is skipped (`PLAN.md` 11.3, step 7). Most are `c249cc986` (AppSettings' `Setting<T>` refactor) and
   features such as "Edit commit by fixup commit & rebase" (`af8e3a7e5`), the diff dialog for two revisions
   (`2dc467adf`), `.webp` and `.avif` images (`e2a7dc3ab`), the conflicts window's changes (`8622a8464`, `656a626c1`) and
   two new Git > Advanced settings (`1586a6db1`, `c422c7771`).

## Checks

| Date | Upstream | OS | Result | Fork changes |
|---|---|---|---|---|
| 2026-10-03 | `52d08e996` (17 ahead) | Linux (WSL) | `verify-upstream.sh` pass: 3369 passed, 144 skipped with reasons. The 4 upstream changes to mapped files (GPG display, grid tooltip, `FormCommit` layout, `FormClone` submodule option) read and skipped with reasons in `PORTING-MAP.md` | Every row rebased to `52d08e996`; seam count 7 |
| 2026-10-06 | `52d08e996` | Windows | `port-drift.sh`: 34 rows, none with upstream changes | None |
| 2026-10-11 | `afc512395` (18 ahead) | Windows | The script's steps by hand: 3506 passed, 1 failed (the known `AsyncLoaderTests` failure on Windows); `port-drift.sh`: 93 rows, none with upstream changes | The app tests run from an empty temporary folder with `GIT_CEILING_DIRECTORIES`, after a test run wrote `http.proxy` and a branch into this repository (since removed) |
| 2026-11-08 | `c584b9740` (55 ahead) | macOS | `verify-upstream.sh` pass: 3902 passed, 222 ignored; `port-drift.sh`: 132 rows, 12 with upstream changes | 8 `RecentRepoSplitterTests` methods skipped (Windows paths); the script fixed for macOS's linked temporary folder; seam count 8 |
| 2026-11-13 | none | none | Measure 11.2 widened to every path: the earlier "upstream files modified 0" missed the commented-out workflows (since restored) and `.gitignore` | `PLAN.md` section 5, "Files outside the fork folders" |
| 2026-11-20 | `17213fe3f` (73 ahead) | macOS | `verify-upstream.sh` pass after 4 skips: 12 of 12 projects, `GitCommands.Tests` 3421 passed, 174 skipped | The 4 `TryRemoveWorktree` tests skipped (item 3 above) |
| 2026-11-24 | `afc512395` (this Windows clone; `17213fe3f` not fetched here): `ApiClient.cs` unchanged since the fork base | Windows | Azure DevOps plugin tests 60 of 60 | Seam S9 (Azure DevOps credentials off Windows); seam count 9 |
| 2026-11-24 | `bd69886b7` (76 ahead) | Windows | `verify-upstream.sh` pass: 12 projects build; 11 pass, `GitCommands.Tests` 3565 of 3567 (1 skipped, 1 failed: the known `AsyncLoaderTests` failure on Windows). Every seam anchor found; `ApiClient.cs` (S9) unchanged. `port-drift.sh`: 147 rows, 16 with upstream changes (item 6 above). `xplat-drift.sh`: 5 mirrored classes changed (items 1, 2, 4, 5 above, and `ExecutableTests`) | `XplatLinuxExecutableTests` accepts a `TaskCanceledException` as upstream's `92716105e` does. The run needed `core.longpaths` for upstream's long test file names (passed by `GIT_CONFIG_COUNT`), and left its worktree folder (a process held a file), removed by hand |
