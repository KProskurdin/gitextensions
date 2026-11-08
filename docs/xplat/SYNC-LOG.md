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

