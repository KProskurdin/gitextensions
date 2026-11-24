# Upstream sync log

Record of every check against upstream (`gitextensions/gitextensions`, `upstream/master`) and every merge of upstream into
the fork. Procedure and targets: `docs/xplat/PLAN.md` section 11.

Add one row per check or merge to the table below, newest last: what was checked, on which OS, the result, and what changed
in the fork because of it. Work on the fork itself is recorded in git history, not here. Trimmed on 2026-11-24 to the checks
and the items still open; the full entries before that are in git history.

## Current state

| Item | Value |
|---|---|
| Fork base (last upstream commit the fork contains) | `bd69886b7` fix(merge): offer commit dialog after a squash merge (merged 2026-11-24) |
| Upstream `master` at last check | `bd69886b7` (2026-11-24, the fork base) |
| Fork changes in upstream folders (`src/app`, `src/plugins`, `tests/app`, `setup`, `eng`) | 0 |
| Changes at other upstream paths | `.gitignore` (one line) and `CLAUDE.md`; `PLAN.md` section 5 |
| Seams (PLAN.md section 5) | 9 |
| Next sync | When the maintainer chooses (PLAN.md section 9, question 9); overdue after 3 months without one |
| Maintainer | KProskurdin (PLAN.md section 9, question 9) |

## Open from the last merge

Kept in `PLAN.md` section 12.2 (items 3, 4, 6 and 7): the suites on macOS and Linux, the reference pictures, and the
ported features at the screen.

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
| 2026-11-24 | `bd69886b7` merged into `feature/cross-os-ui` (no conflicts) | Windows; Linux (WSL) | Windows: core 1530, app 890, shadow 12 projects build and 11 pass, `GitCommands.Tests` 3565 of 3567 (the known `AsyncLoaderTests` failure). Linux: `GitCommands.Tests` 3424 passed, 174 skipped; the new upstream tests (`IsGitFsMonitorDaemon`, `TryFindShellInGitDir` order, main worktree) pass there without skips. `port-drift.sh`: 147 rows, 0 with upstream changes, 0 stale | Fork code follows upstream's API changes: `Setting<T>.Value` for 5 settings, `ObjectId.Parse` as a lambda (new span overload), `LoadGpgInfoAsync` token, `HandleConflictSelectSide`'s error text. The 20 upstream commits on the 16 ported files decided, every row rebased to `bd69886b7` (`PORTING-MAP.md` notes): ported `656a626c1`, `8622a8464`, `af8e3a7e5`, `939de0a12`, `68a9100a9`, `a8cb9e786`, `bd69886b7`, `2dc467adf`, `a816fb0cd`, `1586a6db1`, `c422c7771`; already so `54f96efab`, `84bbeb91e`, `5c18f5bce`, `e2a7dc3ab` (but `.avif`); refactors only `c249cc986`, `813f48be0`; not applicable `71b8548e8` (no index watcher), `cfdef343c`, `c175c97ad` (WinForms fixed heights), `45843dd71` (no embedded browser). POSIX `TryRemoveWorktree` tests in `XplatLinuxCommandsTests` |
