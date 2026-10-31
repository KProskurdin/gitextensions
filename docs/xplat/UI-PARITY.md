# UI and UX parity with the WinForms app

Written 2026-10-11. Goal: make the Avalonia app look and behave as close as possible to Git Extensions for Windows, so a user
of either app feels at home in the other (PLAN.md section 9, question 1: "stay as close as possible to the WinForms app").

## How the comparison was made

Both apps were started on this repository on Windows and their windows captured by handle (`PrintWindow`): the installed Git
Extensions (browse window, commit dialog, settings) and the Avalonia build (browse window, commit window). The Avalonia
settings window was compared from its XAML, because the capture script could not open it without typing into the desktop.
The captures are in the session scratchpad, not in the repository.

## What already matches

- The main window's structure: menu bar with upstream's menus, toolbar on top, left panel, revision grid, detail tabs below,
  status bar. The commit window's structure: unstaged list above staged list, diff on the right, message below it.
- Texts: menus, labels, settings and questions use upstream's English texts, and translations by those texts.
- Data: graph model (upstream's own), labels per ref kind, relative dates, artificial rows, avatars with initials, the commit
  details' related links.
- Settings: every key is upstream's, so both apps share their state.

## Differences found

Severity: **A** visible at first glance, **B** noticed in daily use, **C** detail.

### Everywhere

| # | Original | Avalonia app | Sev. |
|---|---|---|---|
| G1 | Small icons on every toolbar button, menu item, tab, tree node and file list entry (upstream's `Images.resx`) | Text only; the icons are already linked into the app (used for scripts only) | A |
| G2 | WinForms toolstrips: flat icon buttons, split buttons with a drop-down arrow, separators, dense | Fluent buttons: gray filled rectangles with text, more padding, wrapping onto a second row | A |
| G3 | Segoe UI 9 pt, rows about 20 px | Inter or the user's font, taller controls even with `DensityStyle="Compact"` | B |
| G4 | Actions live in context menus (grid, left panel, file lists, commit details) | Many actions are buttons beside lists (Checkout, Merge, Delete, Create, Copy hash, Stage, Use ours, ...) | A |
| G5 | Selected row: full-width system blue with white text; inactive selection gray | Fluent's light accent background, text keeps its color | B |
| G6 | Every window has the Git Extensions icon | Secondary windows show Avalonia's default icon (commit window) | B |
| G7 | Menus and toolstrips are accessible (UI Automation can open them) | Avalonia menu items expose no Invoke or ExpandCollapse pattern | C (A for accessibility) |

### Browse window (FormBrowse)

| # | Original | Avalonia app | Sev. |
|---|---|---|---|
| B1 | Toolbar: refresh, layout toggles (left panel, split view, commit info position), repository drop-down with the path, submodules, worktrees, current branch drop-down ("change current branch"), pull split button, push with the ahead/behind count ("1↑"), "Commit (n)" with an icon, stash split button, file explorer, terminal drop-down ("Git bash"), settings; a second toolstrip with the branch filter ("All branches", "Branches:" box) and the revision filter ("Filter:" box) with their funnel buttons | A path text box with Open, then text buttons (Refresh, Commit, Pull + Rebase check box, Push, Push tags, Fetch + Prune check box, Clone, Init, Folder, Terminal, Files, Reflog), two filter boxes, a branch combo, Filter..., and "Load more" on a second row | A |
| B2 | Left panel: one tree with icons (Branches, Remotes with the hosts' icons, Worktrees, Tags, Submodules with their status icons, Stashes), a search box and a toolbar of section toggles; current branch bold, ahead/behind counts on branches; actions in context menus | Expanders per section, each with action buttons and a "New branch name" box; no icons, no search, no counts, no Worktrees section | A |
| B3 | Grid without a header row by default; columns chosen in the grid's context menu | A header row (Message, Author, Date, Commit, Build status) always shown | B |
| B4 | Labels: the current branch with a ▶ marker and bold, remote branches outlined with ▷, ahead/behind arrows inside the label; no separate HEAD label | HEAD as its own label, plain colored rectangles | B |
| B5 | Artificial rows: "Working directory" and "Commit index" drawn as labels with a status icon (green check when clean) | Plain text with "(0 changes)" | B |
| B6 | The message column shows the subject and then the body in gray on the same line | Subject only | B |
| B7 | Rows by the current user tinted (`HighlightAuthoredRevisions`, on by default, theme color `AuthoredHighlight`) | No tint | B |
| B8 | Lane colors: upstream picks `seed % (number of distinct, non-empty GraphBranch colors)` | The stand-in uses `seed % 8` (`UpstreamGraphLaneColor.cs`), so lanes get other colors than upstream (green there, purple here). A bug, not a style choice | A |
| B9 | Detail tabs with icons: Commit, Diff, File tree, GPG, Console, Output | Fluent underline tabs without icons: Commit, Diff, File tree (Build Report when shown) | B |
| B10 | Commit info: avatar beside a label grid (Author, Date, Commit hash, Child, Parent) with links, the message, related links, "Contained in branches/tags" | Plain lines (hash, author, dates, parents), message, related links; Copy hash and Copy message buttons on the right | B |
| B11 | Dashboard: recent and favourite repositories with categories, and Open, Clone, Create tiles | A list of recent repositories with a Remove button | B |
| B12 | Status bar: branch and counts on the toolbar's push button | Status bar text "Branch: ... [ahead 1, behind 0]" and "500 commits, more available" | C |

### Commit window (FormCommit)

| # | Original | Avalonia app | Sev. |
|---|---|---|---|
| C1 | Each file list has a toolstrip (refresh, view options, open folder, open file, edit, settings) and a placeholder ("There are no unstaged changes") | A bold heading "Unstaged changes (0)" and an empty box | A |
| C2 | Between the lists, a bar with Unstage and Stage buttons with arrow icons (and "all" variants) | Rows of buttons under each list: Stage, Stage all, Discard..., Delete untracked..., Use ours, Use theirs, Stash selected, Ignore... (upstream has these in the lists' context menus) | A |
| C3 | Bottom left: a column of buttons (Commit with icon, Push, Amend commit, Stash staged changes, Reset all changes, Reset unstaged changes); bottom right: the message box with a toolstrip (Commit message ▾, Commit templates ▾, Create branch, Options ▾) and the "Enter commit message" watermark | Message box above a wrapping row of buttons and check boxes (Commit, Commit and push, Reset..., Amend, Sign-off, Commit templates, Commit message, Author box) | A |
| C4 | Sign-off, author, skipping hooks, GPG are in the Options menu | Sign-off check box and Author box in the main view | B |
| C5 | Status bar: committer, branch → tracked remote branch, Staged n/m, Ln, Col | None | B |
| C6 | The diff viewer has its own small toolstrip of icons; stage and unstage of lines are in its context menu | A wide toolbar (whitespace combo, −, 3, +, Entire file, ¶, Syntax) and three buttons under the diff | B |

### Settings window (FormSettings)

| # | Original | Avalonia app | Sev. |
|---|---|---|---|
| S1 | A tree of pages with icons and sub-pages (Git Extensions > General, Appearance > Colors, Fonts, ...; Git > Config; Plugins > each plugin), a search box above it | One row of 19 tabs | A |
| S2 | A "Settings source" header on repository pages (effective, local, distributed, global) | A level box on the Plugins and Revision links tabs only | B |
| S3 | The Checklist page opens first and checks git, user name, tools, SSH, language | No checklist | B |
| S4 | OK, Cancel, Apply and the note that changes are saved instantly | OK and Cancel | C |

## Principles

1. **Upstream's designer files are the specification.** Item order, texts, icons, grouping and default sizes come from the
   `*.Designer.cs` of the form being matched; where possible a test reads them, as the settings label tests already do, so
   an upstream change fails a test instead of drifting silently.
2. **Reuse upstream's assets, never copy them.** Icons from `Images.resx` and `Resources/Icons` (already linked), texts from
   upstream (and so its translations), colors from the theme files.
3. **Same layout on every OS.** Keep the Windows layout on Linux and macOS; adapt only what the OS dictates (Cmd keys, the
   macOS menu bar later).
4. **Behavior first where they conflict.** A feature the Avalonia app has that upstream shows elsewhere moves to where
   upstream shows it (usually a context menu), rather than being removed.
5. **Each step is checked by screenshot against the original** with the capture script, and recorded in `PORTING-MAP.md`.

## Plan

Estimates assume one person and include tests. Order: shared groundwork first, then windows by how often they are used.

### U1. Groundwork (1 to 1.5 weeks)

1. Fix the lane colors (B8): count the theme's distinct, non-empty GraphBranch colors as upstream does.
2. Icon pipeline (G1): one `UpstreamImage` markup extension or converter that loads an `Images.resx` entry by name (the
   loader exists in `ScriptIcons.cs`), cached, with the theme's adaptation for dark themes where upstream adapts icons.
3. Toolstrip look (G2): a `ToolStrip` style class for a horizontal panel: flat icon buttons (border on hover only), split
   buttons (button plus drop-down arrow, Avalonia `SplitButton` restyled), separators, 16 px icons, text optional,
   overflow instead of wrapping.
4. Density and font (G3): Segoe UI 9 pt as the default application font on Windows when the user set none (upstream's
   default), row heights for lists and grid matching upstream's (about 20 px at 100 %), tighter Fluent paddings in the
   app's styles.
5. Selection colors (G5): the theme's selection colors for selected rows and text, active and inactive.
6. Window icons on every window (G6); automation peers for menu items so they can be opened by UI Automation (G7).
7. A capture script in `tests/xplat` (the one used for this comparison) and a short checklist per window.

### U2. Browse window (2 to 3 weeks)

1. Toolbars per `FormBrowse.Designer.cs` (B1): the main toolstrip with upstream's items and split buttons (pull actions,
   stash actions, fetch variants, terminal choice), the repository and branch drop-downs, the push ahead/behind text; the
   filter toolstrip ("All branches", "Branches:", "Filter:", the funnel buttons). The path box and Open move to the Start
   menu and the dashboard, as upstream; Clone, Init, Folder, Reflog, Files are reached through the menus as upstream.
2. Left panel as one tree with icons (B2): sections as root nodes, a search box and the section toggles; current branch bold,
   ahead/behind counts, the Worktrees section; every action in upstream's context menus (`RepoObjectsTree.ContextActions`),
   the action buttons removed.
3. Grid (B3 to B7): no header row by default and the column choice in the context menu; upstream's label drawing (▶ for the
   current branch, outlines for remote branches, arrows for ahead/behind, HEAD folded into the branch label); artificial rows
   with their status icons; the body in gray after the subject; the authored-revision tint.
4. Detail tabs with icons (B9) and the commit info layout (B10): avatar and label grid with links (child and parent
   commits select them), "Contained in branches/tags"; Copy hash and message move to the context menu as upstream.
5. Dashboard (B11): recent and favourite repositories with categories and the Open, Clone, Create entries.

### U3. Commit window (1.5 to 2 weeks)

1. File lists (C1, C2): toolstrips above the lists with upstream's items, the placeholders, the Stage/Unstage bar with
   arrows between the lists; the other actions in the lists' context menus (upstream's `FileStatusList` menu).
2. Bottom area (C3, C4): the button column on the left, the message toolstrip with Commit message, Commit templates,
   Create branch and Options (sign-off, author, no-verify, GPG) on the right.
3. Status bar (C5) with the committer, branch and tracked branch, staged count, line and column.
4. Diff viewer toolstrip (C6): upstream's compact icon strip, stage and unstage of lines in the diff's context menu.

### U4. Settings window (1 to 1.5 weeks)

1. Tree navigation with icons and sub-pages and a search box (S1), pages in upstream's grouping.
2. The "Settings source" header on every repository page (S2), sharing the plugin page's level logic.
3. The Checklist page (S3), with the checks that apply per OS, and Apply (S4).

### U5. Other windows (2 weeks, can run per window)

Pull, push, clone, checkout, create branch, stash, merge, rebase, reflog, file history, blame, conflicts, worktrees,
remotes: each against its form's designer file, with the same rules (toolstrips, icons, context menus, button order and
texts, default sizes). Dialog button order stays upstream's (OK left of Cancel) on every OS.

### U6. UX details (1 week, alongside)

Access keys (Alt+letter) as upstream's `&` texts, tab order as the designer's `TabIndex`, double-click and Enter behavior
of each list as upstream, splitter positions and column widths kept (upstream's `SplitterManager` keys), tooltips on
toolbar icons with upstream's `ToolTipText`.

### Progress

- 2026-10-11, U1.1 done: lane colors count the theme's distinct GraphBranch colors (`UpstreamGraphLaneColor.LaneColorCount`).
- 2026-10-11, U1.2 done: `UpstreamImages` loads any `Images.resx` entry by name, cached; `{local:UpstreamImage Name}` and
  `{local:UpstreamIcon Name}` for markup. The dark-theme adaptation of icons is not done yet.
- 2026-10-11, U1.3 mostly done: `ToolButton`, `ToolDropDownButton` and `ToolSplitButton` (class `tool`: flat, border on
  hover, 16 px image plus optional text), `toolStrip` panels and `toolSeparator`s, on Fluent. Overflow is not done. Fluent
  restyled was enough for the toolstrip, so the Simple theme question stays open for the grid (U1.4).
- 2026-10-11, U2.1 mostly done: the main toolstrip in upstream's order and with its images, split buttons, default pull
  action, push ahead/behind text and "Commit (n)" state image; the working directory caption and the window title as
  upstream. Left: the terminal choice drop-down, the third layout toggle, the filter toolstrip's "Branches:" box and funnel
  buttons. See the "FormBrowse toolstrip" note in `PORTING-MAP.md`.
- 2026-10-13, U2.1 done except toolstrip overflow: checked layout toggles (`ToolToggleButton`) with the split view kept in
  upstream's `ShowSplitViewLayout`; the commit info position split button (below, left or right of the grid, upstream's
  `CommitInfoPosition`); the terminal split button with upstream's shells (`TerminalChoices`); upstream's filter toolstrip
  (advanced filter with its resets and summary, reflog, branch choice, "Branches:" and "Filter:" boxes with their type
  menus, first parent). The new shell's "filter loaded commits" and "search history" boxes are gone, as upstream has
  neither. See the "FilterToolBar" and "Terminals" notes in `PORTING-MAP.md`. Checked by screenshot (`XPLAT_SCREENSHOT`).
- 2026-10-13, U2.2 done: the left panel is upstream's one tree with its six trees, images, bold current branch and
  worktree, ahead/behind counts, the toolstrip of tree toggles, the search box, and upstream's context menu per node kind
  (with stand-ins where upstream opens a window the new shell lacks: remotes, reset, stashes). The per-section buttons and
  boxes are gone. See the "RepoObjectsTree" note in `PORTING-MAP.md`. Checked by screenshot.
- 2026-10-13, U2.3 done (B3 to B7, and G5 for the grid): no header row; upstream's View menu with its Columns, Grid labels
  and Grid info groups; upstream's ref labels (shapes, filled arrow, tracked remote pair, ahead/behind arrows); artificial
  rows as labels with change icons; the gray body after the subject; the authored tint, alternating rows and upstream's
  selection colors. See the "Revision grid rows and View menu" note in `PORTING-MAP.md`. Checked by screenshot.
- 2026-10-13, U2.4 done (B9, B10): tab images; the GPG tab; the commit info as upstream's header (avatar, labels, links to
  the author, children and parents), the message on its darker panel with commit hashes as links, and the annotated tag
  messages, related links, branches and tags containing the commit and the tag it derives from. Links select commits;
  the commit info menu has upstream's items, and the Copy hash and Copy message buttons are gone. Not ported: the Console
  and Output tabs. See the "CommitInfo and the detail tabs" note in `PORTING-MAP.md`. Checked by screenshot.
- 2026-10-13, U2.5 done (B11): upstream's dashboard with its logo, Start and Contribute links, background, and the
  repository tiles grouped as recent repositories and favourites by category, with the search box, the tile and category
  menus, and upstream's light and dark colors. See the "Dashboard" note in `PORTING-MAP.md`. Checked by screenshot (the
  screenshot aid now also captures the dashboard when no repository opens).
- 2026-10-13, U3.1 done (C1, C2): the commit window's lists as upstream's FileStatusList (toolbar with group-by and
  settings, regex filter box, status images, NoFiles texts) without the tree view, `toolbarStaged` between them, and
  upstream's item menus in place of the rows of buttons; the "unresolved merge conflicts" button over the diff. See the
  FormCommit note in `PORTING-MAP.md`. Checked by screenshot (`XPLAT_SCREENSHOT_WINDOW=commit` saves the commit window).
- 2026-10-13, U3.2 done (C3, C4): the commit window's button column (Commit, Commit & push, Amend with Reset author and
  Reset soft, Stash staged changes, the reset buttons) and the message toolstrip with upstream's Options menu (close
  choices, refresh on focus, select staged, sign-off, author, no verify, GPG). See the FormCommit note in
  `PORTING-MAP.md`. Checked by screenshot.
- 2026-10-13, U3.3 done (C5): the commit window's status strip (committer and author, branch and where it pushes, staged
  count, line and column) and upstream's window title. See the FormCommit note in `PORTING-MAP.md`. Checked by screenshot.
- 2026-10-13, U3.4 done (C6), and with it U3: the diff viewer's toolbar as upstream's icon strip over the lines, shown on
  hover, and upstream's diff menu with line staging, the copy items and the view options; the commit window's buttons
  under the diff are gone. See the FileViewer note in `PORTING-MAP.md`. Checked by screenshot.
- 2026-10-14, U4.1 done (S1, and Apply of S4): the settings window is upstream's page tree with its groups, images,
  nesting and the plugins as nodes, the "Type to find" box, the page in the window title, the page shown last opened
  again, OK, Cancel and Apply in upstream's order, and the theme on upstream's Colors page. A test compares the tree with
  `FormSettings.OnRuntimeLoad`. See the "FormSettings page tree" note in `PORTING-MAP.md`. Checked by screenshot
  (`XPLAT_SCREENSHOT_WINDOW=settings`, or `settings:<text>` to type a search first).
- 2026-10-14, U4.2 done (S2): upstream's "Settings source" header on every page; the repository pages (Revision links,
  Build server integration, Detailed, plugins) choose the level there, read-only at Effective as upstream; the Config page
  rebuilt as upstream's one column at Effective, Local, Global or System, with the tools in upstream's `guitool` keys. See
  the "SettingsPageHeader" and "GitConfigSettingsPage" notes in `PORTING-MAP.md`. Checked by screenshot.
- 2026-10-14, U4.3 done except the startup check (S3, S4): the Checklist page first, with upstream's rows, colors, Repair
  buttons, "Save and rescan" and the instant save notice; the Windows-only rows on Windows. Not ported: "Check settings at
  startup" and the startup check. See the "ChecklistSettingsPage" note in `PORTING-MAP.md`. Checked by screenshot.
  Found then: on Windows the shim's `Application.UserAppDataPath` is `%APPDATA%\GitExtensions`, while WinForms (upstream)
  uses `%APPDATA%\GitExtensions\GitExtensions`, so the two apps read different `GitExtensions.settings` files there. Not
  fixed yet.
- 2026-10-14, U5 started with the groundwork the dialogs share: upstream's dialog frame (`Border.dialogFooter`: the button
  strip with its colors and line), the group box (`HeaderedContentControl.groupBox`), the help picture (`HelpImageView`,
  upstream's help pictures linked), and `XPLAT_SCREENSHOT_WINDOW=<dialog>` to capture a dialog on the open repository.
- 2026-10-14, U5 Pull done: upstream's FormPull layout, texts, images and help picture, its options (URL, all remotes,
  local branch, tags, full history, prune tags) and its checks before pulling (`PullDialog`). Manage remotes and Stash
  changes wait for the remotes and stash windows. See the FormPull note in `PORTING-MAP.md`. Checked by screenshot
  (`XPLAT_SCREENSHOT_WINDOW=pull`).
- 2026-10-14, U5 Push done: upstream's FormPush layout, texts and images, its three tabs (branches with the options panel,
  tags, multiple branches with upstream's grid and header menu), its checks before pushing (`PushDialog`), and upstream's
  handling of a rejected push (offer a pull with the default action, rebase or merge, or a force push with lease, then push
  again; `PushRejection`), also for the quick push and the commit window's Commit & push. The task dialog gained command
  links and "Don't show me this message again". Fixed then: tag, all-tags and multiple-branch pushes from the dialog were
  refused as "no branch". Manage remotes waits for the remotes window. See the FormPush note in `PORTING-MAP.md`. Checked
  by screenshot (`XPLAT_SCREENSHOT_WINDOW=push`).
- 2026-10-14 to 2026-10-16, U5 Remotes done: upstream's FormRemotes (the active and inactive remotes with their details,
  New, Delete and Activate/Deactivate, the URL history and proposals, prefix, color, push URL and PuTTY key, and the
  default pull behavior tab), opened from the menus, the left panel, the pull and push windows' Manage remotes and the
  commit window's remote link. The left panel gained upstream's "[ Inactive ]" folder with Activate, Activate and fetch
  and Deactivate, and double-click on a remote. Fixed then: the window threw "Visual was invalidated during the render
  pass" when it opened (labels nested as `AccessText` in a `TextBlock`). The grid's labels take the remotes' colors, as
  upstream's. Not ported: Test connection and Load SSH key. See the FormRemotes note in `PORTING-MAP.md`. Checked by
  screenshot (`XPLAT_SCREENSHOT_WINDOW=remotes`).
- 2026-10-16, U5 Stash done: upstream's FormStash (the working directory's changes and the stashes, their files in
  upstream's groups and diffs, the message, Keep index and Include untracked files, stash all or selected changes, drop
  after the confirmation, apply), opened where upstream opens it: the toolbar's stash button and its "Manage stashes..."
  and "Create a stash...", Commands, the left panel (also "Open stash" and double-click on a stash) and the pull window's
  Stash changes. The commit window's "Stash selected" item left, as upstream has it only here. Errors of operations now
  open over the active window, so they are not hidden behind a modal one. See the FormStash note in `PORTING-MAP.md`.
  Checked by screenshot (`XPLAT_SCREENSHOT_WINDOW=stash`).
- 2026-10-16, U5 Create branch done: upstream's FormCreateBranch (name proposal and normalising, the commit with its count
  against HEAD and upstream's commit summary, checkout after create, the orphan options, upstream's checks and commands),
  in place of the name prompt everywhere a branch is created; Commands > Create branch now starts at the selected commit,
  as upstream. Not ported: the commit picker's button (FormChooseCommit). See the FormCreateBranch note in
  `PORTING-MAP.md`. Checked by screenshot (`XPLAT_SCREENSHOT_WINDOW=createbranch`).
- 2026-10-16, U5 Checkout branch done: upstream's FormCheckoutBranch (local and remote branches with the count against
  HEAD, reset or create a local branch or detach for a remote branch, the local changes group with "Set as default") and
  its flow (checkout at once when the window is not needed, the reset warning, stash and the "apply stashed items again"
  question, scripts, submodules); Commands > Checkout branch... with upstream's Ctrl+. hotkey. Tags and commits keep the
  old local-changes window until FormCheckoutRevision is ported. See the FormCheckoutBranch note in `PORTING-MAP.md`.
  Checked by screenshot (`XPLAT_SCREENSHOT_WINDOW=checkout`).
- 2026-10-16, U5 Checkout revision done: upstream's FormCheckoutRevision (the commit picker and "Force (reset local
  changes)"), for Commands > Checkout revision... (new), the grid's "Checkout this commit..." and a tag's checkout; the
  commit picker became a shared control (`CommitPickerView`) used by the create branch window too, and the old
  local-changes window is gone. See the FormCheckoutRevision note in `PORTING-MAP.md`. Checked by screenshot
  (`XPLAT_SCREENSHOT_WINDOW=checkoutrevision`).
- 2026-10-16, U5 Merge done: upstream's FormMergeBranch (help pictures, fast forward or merge commit, no commit, and the
  advanced options: strategy, squash, unrelated histories, log messages, merge message), its kept settings, and upstream's
  "solve conflicts now?" after a merge that stops on conflicts (a new "Resolve conflicts" confirmation); Commands > Merge
  branches... and the left panel's merge open it instead of merging at once. See the FormMergeBranch note in
  `PORTING-MAP.md`. Checked by screenshot (`XPLAT_SCREENSHOT_WINDOW=merge`).
- 2026-10-16, U5 Clone done: upstream's FormClone (repository to clone with the remote history, destination with the
  recent repositories' folders, subdirectory, branch read from the remote when its list opens, the location line in
  upstream's Info colors, repository type, "Initialize all submodules", "Download full history", Load SSH key for plink),
  its start from the clipboard or the open repository's remote, and its "open the new repository now?" question; Commands >
  Clone, the toolbar and the dashboard open it. See the FormClone note in `PORTING-MAP.md`. Checked by screenshot
  (`XPLAT_SCREENSHOT_WINDOW=clone`).
- 2026-10-16, U5 Rebase done: upstream's FormRebase (help picture, rebase on a branch, tag or commit, the options with
  their exclusions and the specific range, the commits to re-apply while a rebase is in progress, and Commit, Edit todo,
  Skip, Solve conflicts, Continue and Abort); Commands > Rebase..., the grid's "Rebase current branch on" (now with
  "Selected commit with advanced options..."), the left panel's rebase and the action bar's More... open it. See the
  FormRebase note in `PORTING-MAP.md`. Checked by screenshot (`XPLAT_SCREENSHOT_WINDOW=rebase`).
- 2026-10-16, U5 Reflog done: upstream's FormReflog (the local changes warning, the reference with its current branch and
  HEAD links, the SHA-1, Ref and Action columns, and the menu a click opens: copy SHA-1, create a branch, reset the
  current branch), with upstream's FormResetCurrentBranch ported for it (the commit summary, the five reset types on their
  colors, the hard reset question), which the grid's and the left panel's "Reset current branch to here..." open too. See
  the FormReflog and FormResetCurrentBranch notes in `PORTING-MAP.md`. Checked by screenshot
  (`XPLAT_SCREENSHOT_WINDOW=reflog`, `reset`).
- 2026-10-16, U5 File history and Blame done: upstream's FormFileHistory (its toolstrip, the file's revision grid with the
  browse window's rows and its menu, and the Commit, Diff, View and Blame tabs as upstream shows them per commit) and
  upstream's BlameControl as a view shared with the blame window; the grid's rows moved to a shared template
  (`RevisionGrid.axaml`), and a path filter follows renames as upstream's `BuildPathFilter` does. File lists' "File history"
  and "Blame" open it. See the FormFileHistory, FormBlame and RevisionGridControl notes in `PORTING-MAP.md`. Checked by
  screenshot (`XPLAT_SCREENSHOT_WINDOW=filehistory` with `XPLAT_SCREENSHOT_FILE=<file>`).
- 2026-10-17, U5 Worktrees done: upstream's FormManageWorktree (the Path, Type, Branch and SHA-1 columns with deleted
  worktrees struck out, Open selected, Delete selected, Prune deleted worktrees and Create... with upstream's rules) and
  FormCreateWorktree (an existing branch or a new one, the proposed directory, Browse..., upstream's checks and its `git
  worktree add` command), with upstream's switch, create and delete flows (`WorktreeFlow`: a delete removes the folder and
  prunes, as upstream's). The toolbar's worktree drop-down and the left panel's worktree items now behave as upstream's.
  See the FormManageWorktree note in `PORTING-MAP.md`. Checked by screenshot (`XPLAT_SCREENSHOT_WINDOW=worktrees`,
  `createworktree`).
- 2026-10-16 to 2026-10-17, U5 Conflicts done, and with it U5: upstream's FormResolveConflicts (the files with
  upstream's menu and hotkeys, the description and the base, local and remote names, ours and theirs swapped in a rebase,
  the merge tool run with its command and the file staged as upstream decides, the "Solve merge conflict" choices for a
  file deleted on one side, created on both or binary, Reset's two questions, and the offer to commit when done), the list
  of git's merge tools under "Open in mergetool", and upstream's FormMergeSubmodule for a conflicted submodule, whose
  Checkout Branch offers only the branches containing both commits. Not ported: the custom merge scripts for Word and
  OpenOffice files (Windows' `wscript`). See the FormResolveConflicts note in `PORTING-MAP.md`. Checked by screenshot
  (`XPLAT_SCREENSHOT_WINDOW=conflicts`, `mergesubmodule` with `XPLAT_SCREENSHOT_FILE=<submodule>`).
- 2026-10-17, U6 started with the splitter positions: upstream's `SplitterManager` keys in GitExtensions.settings, shared
  with the WinForms app, for the browse window's left panel (with whether it is hidden), the grid above the tabs and the
  Diff and File tree tabs' file lists, and the commit window's three splitters; restored as upstream restores them for
  another window size or DPI. See the SplitterManager note in `PORTING-MAP.md`. Still open in U6: column widths, access
  keys and tab order checked against the designers, the double-click and Enter behavior of each list, and tooltips.
- 2026-10-28, U6 column widths and tooltips done: upstream keeps no column widths (its grid has no header row and its
  dialogs' grids size themselves), so the grid's author, date and SHA-1 columns now start at upstream's providers'
  widths (`RevisionGridColumns`). Every `ToolTipText` and `SetToolTip` of the ported forms' designers was compared
  with the new shell; the missing ones were added (stash, pull, push, create branch, the Diff viewer settings page),
  and with them two behaviors the tooltips name: the commit window's committer is upstream's link to the Config
  settings page, and Stage all and Unstage all act on a filtered list's files as upstream's "Stage filtered". Left:
  the tooltips of features not ported (the file list's tree view, A/B filter and git-grep toggle, the left panel's
  submodule actions, the console tab). See the "Revision grid rows and View menu" and FormCommit notes in
  `PORTING-MAP.md`. Still open in U6: access keys and tab order checked against the designers, and the double-click
  and Enter behavior of each list.
- 2026-10-28, U6 double-click partly done: every `DoubleClick` and Enter handler of the ported forms was compared.
  Added: a double-click in the commit window's lists stages or unstages the file (`Unstaged_DoubleClick`,
  `Staged_DoubleClick`), and one in the browse window's Diff tab opens the file's history (`DiffFiles_DoubleClick`).
  The left panel, conflicts, worktrees, blame, the dashboard, the filter boxes and the settings search already
  behaved as upstream. Left: a double-click on the grid (browse window and file history) opens upstream's
  FormCommitDiff, which is not ported, so the new shell still shows the Diff tab; upstream's FileStatusList default
  (the file's history) in the stash window. Still open in U6: access keys and tab order against the designers.
- 2026-10-28, U6 access keys and tab order done, and with them U6: every `&` text of the ported forms' designers was
  compared with the new shell's `_` texts. The dialogs matched; the browse window's menus did not, so they were rebuilt
  from upstream's designers (Start with upstream's numbered favourite and recent repositories, the Dashboard menu,
  Repository, Commands, Tools and Help in upstream's order, texts, keys and images), checked by a test that reads the
  designers. Tab order: the order of every ported dialog's buttons, check boxes and radio buttons was compared with the
  designer's `TabIndex` order as a cycle (a window's Tab order wraps, and the docked button strip only rotates it). The
  differences left are upstream's own accidents and are not copied: buttons in right-to-left flow panels that Tab visits
  from right to left (reset changes, worktrees), and `TabIndex` numbers against the layout (merge's
  hidden advanced options first, checkout branch's local changes group first, stash's "Stash selected changes" last).
  Found then: the dialogs opened with nothing focused, so the first Tab reached their buttons; they now open on
  upstream's control (`InitialFocus`). See the "FormBrowse menus" and "Initial focus" notes in `PORTING-MAP.md`. Left
  from U6: the grid's double-click (FormCommitDiff), the Navigate menu and the menus' unported items.
- 2026-10-28, U6 leftover FormCommitDiff done: upstream's CommitInfo is a shared control (`CommitInfoView`), upstream's
  CommitDiff (`CommitDiffView`) shows it with the commit's files and diff, and upstream's FormCommitDiff
  (`CommitDiffWindow`) opens on a double-click on a commit of the browse window's or the file history's grid; the file
  history's Commit tab is the same view. See the "FormCommitDiff and CommitDiff" note in `PORTING-MAP.md`. Checked by
  screenshot (`XPLAT_SCREENSHOT_WINDOW=commitdiff`). Left: the Navigate menu and the menus' unported items.
- 2026-10-28, U6 leftover Navigate menu done: upstream's Navigate menu with the grid's commands, the new ones (toggle
  between artificial and HEAD, first and last parent, navigate backward and forward) as grid hotkeys with upstream's ids
  and keys. See the "Navigate menu" note in `PORTING-MAP.md`. Left: the menus' unported items (their windows are not
  ported) and the working directory drop-down's search box and settings.
- 2026-10-28, U6 leftover working directory drop-down done: upstream's drop-down (search box, favourites, recent
  repositories, Open, Close, "Configure this menu..."), its right click and tooltip, Ctrl+click for a new instance, and
  FormRecentReposSettings as `RecentRepositoriesSettingsWindow`, with a screenshot
  (`XPLAT_SCREENSHOT_WINDOW=recentrepos`). See the "Working directory drop-down and FormRecentReposSettings" note in
  `PORTING-MAP.md`. Left: the menus' unported items, whose windows are not ported.
- 2026-10-28, Navigate menu finished: Go to commit... (FormGoToCommit as `GoToCommitWindow`, screenshot
  `XPLAT_SCREENSHOT_WINDOW=gotocommit`) and Go to common ancestor (merge base), with upstream's hotkeys. See the
  "FormGoToCommit and Go to common ancestor" note in `PORTING-MAP.md`. Left: the other menus' unported items (Delete
  branch/tag, Cherry pick, Archive, patches, Git maintenance, submodules, .gitattributes/.mailmap, Sparse, Repository
  settings, Undo last commit, Clean, Git GUI/GitK/PuTTY, Changelog/Donate/telemetry/updates).
- 2026-10-28, Commands > Delete branch... and Delete tag... done: FormDeleteBranch (with BranchComboBox,
  FormSelectMultipleBranches and the worktree handling) and FormDeleteTag, screenshots `deletebranch` and `deletetag`.
  Fixed on the way: the documentation address was never set, so the user manual's links threw. See the "Delete branch
  and Delete tag windows" note in `PORTING-MAP.md`. Left: Cherry pick, Archive, the patch items, Git maintenance,
  submodules, .gitattributes/.mailmap, Sparse, Repository settings, Undo last commit, Clean, Git GUI/GitK/PuTTY,
  Changelog/Donate/telemetry/updates; the left panel and grid opening the delete windows.
- 2026-10-28, more menu items done: the left panel opens the delete windows (local branch, Delete All, tag), Commands >
  Undo last commit... (with upstream's confirmation setting), Tools > Git GUI and GitK, Repository > Edit
  .gitattributes and Edit .mailmap (`RepositoryTextFileWindow`, screenshot `mailmap`). See the "Undo last commit, Git
  GUI, GitK, .gitattributes and .mailmap" note in `PORTING-MAP.md`. Left: Cherry pick, Archive, the patch items, Git
  maintenance, Manage submodules, Sparse, Repository settings, Clean, Delete remote branch window, PuTTY (Windows),
  Changelog/Donate/telemetry/updates.
- 2026-10-28, Commands > Clean working directory... (FormCleanupRepository) and Archive revision... (FormArchive, with
  FormChooseCommit) done, also from the grid's menu; screenshots `cleanup`, `archive`, `choosecommit`. See the "Clean
  working directory, Archive and FormChooseCommit" note in `PORTING-MAP.md`. Left: Cherry pick (FormCherryPick), the
  patch items, Git maintenance, Manage submodules, Sparse, Repository settings, the delete remote branch window, PuTTY
  (Windows), Changelog/Donate/telemetry/updates.
- 2026-10-28, Commands > Cherry pick... and the grid's "Cherry pick this commit..." open upstream's FormCherryPick
  (`CherryPickWindow`, screenshot `cherrypick`) with the parent of a merge, the commit and reference options and the
  conflict question. See the FormCherryPick note in `PORTING-MAP.md`. Left: the patch items, Git maintenance, Manage
  submodules, Sparse, Repository settings, the delete remote branch window, PuTTY (Windows),
  Changelog/Donate/telemetry/updates.
- 2026-10-28, upstream's FormDeleteRemoteBranch done (`DeleteRemoteBranchWindow`), opened by the left panel's delete of
  a remote branch. See its note in `PORTING-MAP.md`. Left: the patch items, Git maintenance, Manage submodules, Sparse,
  Repository settings, PuTTY (Windows), Changelog/Donate/telemetry/updates.
- 2026-10-28, the patch items done: Format patch (FormFormatPatch), Apply patch (FormApplyPatch, with FormAddFiles) and
  View patch file (FormViewPatch); screenshots `formatpatch`, `applypatch`, `viewpatch`. See the "Patch windows" note
  in `PORTING-MAP.md`. Left: Git maintenance, Manage submodules, Sparse, Repository settings, PuTTY (Windows),
  Changelog/Donate/telemetry/updates.
- 2026-10-31, Repository > Git maintenance done: Compress git database (`git gc` with its output), Recover lost
  objects... (FormVerify as `VerifyDatabaseWindow`, with FormEdit as `ViewTextWindow`; screenshot
  `XPLAT_SCREENSHOT_WINDOW=verifydatabase`), Delete index.lock and Edit .git/config (the editor window with upstream's
  warning panel). See the "Git maintenance menu and FormVerify" note in `PORTING-MAP.md`. Left: Manage submodules,
  Sparse, Repository settings, PuTTY (Windows), Changelog/Donate/telemetry/updates.

## Risks

- Fluent fights a dense WinForms look; restyling may be cheaper with Avalonia's Simple theme as a base. Decide in U1 with a
  prototype of one toolstrip and the grid.
- Icons are bitmaps sized for 96 dpi; at high scaling they look soft, as in upstream. SVG versions would be new assets.
- Moving actions into context menus changes what the headless tests click; the tests move with them.
