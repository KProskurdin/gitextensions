# Upstream sync log

Record of every check against upstream (`gitextensions/gitextensions`, `upstream/master`) and every
merge of upstream into the fork. Procedure and targets: `docs/xplat/PLAN.md` section 11.

Add one entry per check or merge, newest first. Do not rewrite old entries; correct them with a new entry.

## Current state

| Item | Value |
|---|---|
| Fork base (last upstream commit the fork contains) | `0174ba1cc` fix(diff): do not run git after repo closed |
| Upstream `master` at last check | `52d08e996` fix: do not offer to delete new files when nothing can be deleted |
| Upstream commits not yet in the fork | 17 |
| Upstream files changed since fork base | 38 |
| Fork changes in upstream folders (`src/app`, `src/plugins`, `tests/app`, `setup`, `eng`) | 0 |
| Seams (PLAN.md section 5) | 7 |
| Next sync due | 2026-10-17 (every 2 weeks while development is active) |
| Maintainer | not yet named (PLAN.md section 9, question 9) |

## Entries

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
