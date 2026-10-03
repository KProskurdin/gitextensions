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
| `src/app/GitUI/CommandsDialogs/FormBrowse.cs` | `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml`, `MainWindow.axaml.cs` | `0174ba1cc` | partial |
| `src/app/GitUI/UserControls/RevisionGrid/RevisionGridControl.cs` | `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (`CommitList`), `MainWindow.axaml.cs` (`LoadCommits`) | `0174ba1cc` | partial |

## Notes per row

**FormBrowse** (`partial`). Ported: repository open from a path, the commit list, the status line.
Not ported: menus and toolbars, the commit details panel, the file tree, diff, the branch and remote
trees, all dialogs launched from the form, and repository-changed refresh.

**RevisionGridControl** (`partial`). Ported: a read-only list of the first 500 commits from `HEAD`,
with short hash, subject, author and date, using the shared `RevisionReader.GetLog`. Not ported:
the revision graph, column layout and sorting, paging past 500 commits, filters and search, refs
labels, and the selection-driven details.

## Not in this map

- The repository path box and the git discovery at startup have no upstream form to port; they are
  new code in `App.axaml.cs` and `XplatGitDiscovery.cs`.
- The `RevisionReader` and `GitModule` engine code is used as-is from the shadow projects, not
  reimplemented, so it has no row.
