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
| `src/app/GitUI/CommandsDialogs/FormBrowse.cs` | `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml`, `MainWindow.axaml.cs` (view); `src/xplat/GitExtensions.Xplat.Core/CommitHistory/CommitListViewModel.cs` (state) | `52d08e996` | partial |
| `src/app/GitUI/UserControls/RevisionGrid/RevisionGridControl.cs` | `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (`CommitList`); `src/xplat/GitExtensions.Xplat.Core/CommitHistory/GitCommitHistory.cs` (reads); `CommitListViewModel.cs` | `52d08e996` | partial |
| `src/app/GitUI/CommandsDialogs/FormCommit.cs` | `src/xplat/GitExtensions.Xplat.Core/Operations/GitOperations.cs` (commit), `RepositoryOperationsViewModel.cs`; `src/xplat/GitExtensions.Xplat.App/MainWindow.axaml` (commit panel) | `52d08e996` | partial |
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

## Notes per row

**FormBrowse** (`partial`). Ported: repository open from a path, the commit list, the status line, and
a commit details panel (full message, author, dates, parents). Not ported: menus and toolbars, the file
tree, diff, the branch and remote trees, all dialogs launched from the form, and repository-changed refresh.

**RevisionGridControl** (`partial`). Ported: a read-only list of the first 500 commits from `HEAD`,
with short hash, subject, author and date, using the shared `RevisionReader.GetLog`. Selecting a row
loads its details through `RevisionReader.GetRevision`. Not ported: the revision graph, column layout
and sorting, paging past 500 commits, filters and search, refs labels, and the changed-file list.

## Not in this map

- The repository path box and the git discovery at startup have no upstream form to port; they are
  new code in `App.axaml.cs` and `XplatGitDiscovery.cs`.
- The `RevisionReader` and `GitModule` engine code is used as-is from the shadow projects, not
  reimplemented, so it has no row.

**FormCommit** (`partial`). Ported: stage and unstage of selected changes, commit with a message, amend,
and the repository refresh after the commit. Not ported: the file diff preview, author override, sign-off,
GPG signing, skipping hooks, commit templates, commit scripts, and the amend-from-HEAD message load.

**FormPush** (`partial`). Ported: push of the current branch to `origin`, with upstream tracking. Not ported:
destination branch and remote choice, force push, pushing tags, recursive submodules, and the push progress dialog.

**FormPull** (`partial`). Ported: pull of the current branch from `origin`, merge or rebase. Not ported: remote
and branch choice, prune, fetch tags, unshallow, and the progress dialog.

**FormClone** (`partial`). Ported: clone from a URL or local path into a new folder, then open it. Not ported:
branch choice, depth, recursive submodules, and the folder browser.

**FormCreateBranch** (`partial`). Ported: create a branch at HEAD and check it out. Not ported: creating from a
specific revision, the branch name rules shown in the form, and the no-checkout option.

**FormCheckoutBranch** (`partial`). Ported: check out a local branch, and a remote branch as a tracking branch.
Not ported: local-change handling (merge or reset), and checkout of tags or revisions.

**FormDeleteBranch** (`partial`). Ported: delete a local branch that is not checked out, with an optional force
flag that the window leaves off. Not ported: deleting remote branches, the merged-branches list, and the
confirmation dialog.

## Review against upstream 52d08e996 (2026-10-03)

Upstream changes to the mapped files between `0174ba1cc` and `52d08e996` were read and found to be outside the
reimplemented behavior, so the rows now use `52d08e996` as their base:

- `f425acf4a`, `35ae6a77f` (GPG verification display in `FormBrowse` and `RevisionGpgInfoControl`): skipped,
  the commit details panel does not show signatures.
- `1c98cad37` (grid tooltip in `RevisionGridControl`): skipped, the commit list has no tooltips.
- `36d8b34f6`, `d2b1e3528` (`FormCommit` focus scrolling and status bar height): skipped, layout of the WinForms file lists.
- `af375e1be` (`FormClone` option to init submodules): skipped, the clone form does not expose submodule options.

**FileViewer** (`partial`). Ported: the diff of one file, for a commit or the working tree (staged or unstaged), with
added, removed, hunk and header lines colored. Not ported: syntax highlighting, line numbers, the editor's
selection and patch-line actions, and the external diff tool.

**RevisionDiffControl** (`partial`). Ported: the files a selected commit changed, with status letters, and a
Show diff action for one file. Not ported: the per-file tree, filtering, the file history view, and the
diff of merge commits against each parent.

**FormFileHistory** (`partial`). Ported: the files at a commit (or HEAD), the commits that changed a selected file,
and a diff of that file in a chosen commit. Not ported: renames followed with `--follow`, blame, the tree view with
folders (the list is flat), the file's content view, and the branch and revision filters of the upstream form.

**FormBlame** (`partial`). Ported: per-line blame of a file as of a commit (line number, short hash, author, date, text),
read from `git blame --line-porcelain`. Not ported: the author margin and its coloring, jumping to the commit a line came
from, the blame-ignore-whitespace and detect-moves options, and blame of a diff against the parent.

**FormMergeBranch** (`partial`). Ported: merge the selected branch into the current one with git's fast-forward
default, abort a stopped merge, and show "merge in progress" while MERGE_HEAD exists. Conflicting files appear in the
Changes list with the Conflict kind. Not ported: the strategy and strategy options, squash, no-commit, the
allow-unrelated-histories option, the merge message editor, and the conflict resolution dialog (conflicts are
resolved outside the app for now).
