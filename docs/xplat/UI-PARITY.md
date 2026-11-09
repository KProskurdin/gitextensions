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
   macOS menu bar later; see `MACOS.md`, MAC3 and MAC4).
4. **Behavior first where they conflict.** A feature the Avalonia app has that upstream shows elsewhere moves to where
   upstream shows it (usually a context menu), rather than being removed.
5. **Each step is checked by screenshot against the original** with the capture script, and recorded in `PORTING-MAP.md`.
   On a Mac the original cannot run: use the designer files, then reference pictures, then a Windows machine (`MACOS.md`,
   section 6). Say in the Progress entry which of these the step was checked against.

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
- 2026-10-31, Repository > Manage submodules... done: FormSubmodules (`SubmodulesWindow`) and FormAddSubmodule
  (`AddSubmoduleWindow`), with the submodule's pull; screenshots `submodules` and `addsubmodule`. See the
  "FormSubmodules and FormAddSubmodule" note in `PORTING-MAP.md`. Left: Sparse, Repository settings, PuTTY (Windows),
  Changelog/Donate/telemetry/updates.
- 2026-10-31, Repository > Sparse Working Copy done (FormSparseWorkingCopy as `SparseWorkingCopyWindow`, screenshot
  `sparse`). See its note in `PORTING-MAP.md`. Left: Repository settings, PuTTY (Windows),
  Changelog/Donate/telemetry/updates.
- 2026-10-31, Repository > Repository settings... done: the settings on the Git Config page, as upstream. The
  Repository menu now has every upstream item. Left: PuTTY (Windows), Changelog/Donate/telemetry/updates.
- 2026-10-31, Help > Changelog (FormChangeLog) and Donate (FormDonate) done; screenshots `changelog` and `donate`. See
  their note in `PORTING-MAP.md`. Left: PuTTY (Windows), the telemetry choice and Check for updates.
- 2026-10-31, Tools > PuTTY done (Windows only, as upstream). See its note in `PORTING-MAP.md`. Left: the telemetry
  choice and Check for updates.
- 2026-10-31, the telemetry choice and Check for updates left out on purpose (the fork sends no telemetry and has no
  releases of its own); see their note in `PORTING-MAP.md`. Every upstream browse menu item is now ported or decided.
- 2026-10-31, the dialogs that were prompts are next: FormCreateTag done (`CreateTagWindow`, screenshot `createtag`;
  see its note in `PORTING-MAP.md`). Left: FormRenameBranch, FormRevertCommit and the other partial rows of
  `PORTING-MAP.md`.
- 2026-10-31, FormRenameBranch done (`RenameBranchWindow`, from the left panel). Found on the way: the grid's context
  menu lacks upstream's branch submenus (checkout, merge, rebase, rename, delete branch and tag). Left: FormRevertCommit,
  the grid's branch menus and the other partial rows of `PORTING-MAP.md`.
- 2026-10-31, FormRevertCommit done (`RevertCommitWindow` from the grid, screenshot `revertcommit`). Left: the grid's
  context menu as upstream's (branch, stash, compare, navigate and view submenus) and the other partial rows of
  `PORTING-MAP.md`.
- 2026-10-31, the grid's context menu rebuilt as upstream's, part 1: order, texts and icons from its designer (checked
  by `GridMenuTests`), the copy submenu and the branch and tag submenus. See the "Revision grid context menu" note in
  `PORTING-MAP.md`. Left for part 2: Reset another branch, Compare, Advanced, View and the right-clicked label's
  focused menu.
- 2026-11-02, the grid's Reset another branch to here done (FormResetAnotherBranch as `ResetAnotherBranchWindow`,
  screenshot `resetanotherbranch`; see its note in `PORTING-MAP.md`). Left: Compare, Advanced, View and the
  right-clicked label's focused menu.
- 2026-11-02, the grid's context menu part 2 done: View, Advanced (edit and reword through an interactive rebase;
  fixup, squash and amend commits in the commit window, upstream's `CommitKind`), Compare (FormDiff as
  `RevisionDiffWindow` and FormCompareToBranch as `CompareToBranchWindow`, screenshots `revisiondiff` and
  `comparetobranch`), and the focused menu of a right-clicked ref label with "Other actions". Found on the way: the menu's
  order at run time is upstream's `_contextMenuItems`, not its designer's; the menu and `GridMenuTests` now follow it.
  See the "Revision grid context menu", FormDiff, FormCompareToBranch and FormCommit notes in `PORTING-MAP.md`. Left:
  the stash items (the grid shows no stashes) and the two items that need several selected commits (the grid selects
  one), then the other partial rows of `PORTING-MAP.md`.
- 2026-11-02, the grid's multi-selection done (upstream's `MultiSelect`): the latest selected commit drives the commit
  info and the menu; the Diff tab shows the files between the earliest and the latest selected; Compare selected
  commits and Open selected commits with difftool work (also for one commit against its parent, as upstream's, which
  had wrongly been disabled); the copy submenu, cherry pick (a window per commit), archive (the files between two
  commits) and the bisect start range use the selection. See the RevisionGridControl, RevisionDiffControl, FormCherryPick,
  FormArchive and FormBisect notes in `PORTING-MAP.md`. Left: the stash items in the grid, then the other partial rows.
- 2026-11-02, stash rows in the grid done (upstream's `ShowStashes`, which is on by default; earlier entries here said the
  grid shows no stashes as upstream's default, which was wrong): View > Show stashes, the stashes before the commits they
  were made on with their selector labels and "untracked files" commits, and the grid menu's Apply, Pop and Drop stash.
  A merge's (and a stash's) Diff tab list now uses the first parent, as its diff does. See the RevisionGridControl,
  RevisionDiffControl and "Revision grid context menu" notes in `PORTING-MAP.md`. Left: the other partial rows of
  `PORTING-MAP.md`.
- 2026-11-02, the Diff tab's and FormDiff's file lists are upstream's FileStatusList (status images, group by, filter,
  "No changes"; `RevisionFileList`), and the commit picker got upstream's choose commit button (create branch, checkout
  revision). Stale "Not ported" notes fixed (the commit picker, the merge window's grid submenu). Left: the Diff tab's
  item menu (upstream's RevisionDiffController), then the other partial rows of `PORTING-MAP.md`.
- 2026-11-02, the Diff tab's item menu done: upstream FileStatusList's item menu in its order, with the items a selection
  allows decided by upstream's own `RevisionDiffController` (linked into the core unchanged, core `DiffFileMenu`): stage,
  unstage, reset to the second or first revision with upstream's question, the difftool submenu (A to B, B or A to the
  working directory), the working directory file, this revision as a temporary file, save as, delete, copy paths, show in
  folder, file tree, filter in grid, history, blame, ignore and stop tracking. The copy paths submenu is now shared with the
  commit window (`CopyPathsMenu`). See the RevisionDiffControl note in `PORTING-MAP.md`, which lists the items not
  ported. Left: the other partial rows of `PORTING-MAP.md`.
- 2026-11-02, FormDiff's file list menu done: the Diff tab's item menu is now one control (`FileStatusListMenu`, upstream
  FileStatusList's `ItemContextMenu`) that each window binds as upstream's `BindContextMenu`; FormDiff's window has it as
  upstream's does (no file tree or grid filter items, revisions by short hash). Both lists now select several files.
  Found on the way: the browse window's status refresh on activation held git's index lock, so a reset started from a
  dialog could fail with "index.lock: File exists"; the status is now read with `--no-optional-locks`, as upstream's
  `GitStatusMonitor` does. See the RevisionDiffControl, FormDiff and FormBrowse notes in `PORTING-MAP.md`. Left: the other
  partial rows of `PORTING-MAP.md`.
- 2026-11-02, FormStash's file list menu done: the stash window's files have `FileStatusListMenu` as FormStash binds it,
  each file between its own revisions as upstream's `LoadGitItemStatuses` gives them (staged files against HEAD, other
  working directory files against the index, a stash's files against its commit; core `DiffMenuRevisions`, with
  upstream's "<multiple>" captions and a reset per revision). FormStash's "branching from a stash" was a stale note:
  upstream has no such feature. See the FormStash note in `PORTING-MAP.md`. Left: the other partial rows of
  `PORTING-MAP.md`.
- 2026-11-02, the left panel's node selection and two menu items done: selecting a branch, tag, stash or worktree
  selects its commit in the grid (upstream's `OnSelected`/`GoToRef`, Ctrl adds to the selection), and the menu has
  upstream's "Copy to clipboard" and "Run script". The FormDeleteTag note's heading said `partial` though its row and a
  later note had it ported; fixed. Found on the way: a refresh (after every action) clears the grid's selection, where
  upstream keeps the selected commits, else selects HEAD, else the nearest loaded parent (`GetToBeSelectedRevisions`,
  `SetToBeSelectedFromParents`). Left: that, the left panel's merged-branch images, then the other partial rows of
  `PORTING-MAP.md`.
- 2026-11-02, the grid's selection after its rows are read again done (upstream's `GetToBeSelectedRevisions` and
  `SetToBeSelectedFromParents`, core `GridSelection.ToBeSelectedAsync`): a refresh keeps the selected commits, the grid
  opens on HEAD, and a commit no longer shown hands over to its nearest shown ancestor. See the RevisionGridControl note in
  `PORTING-MAP.md`. Also fixed: the app tests' `TestRepository.ReadFile` now treats a file git is replacing on Windows
  (access denied) as not there yet, which made a stash test flaky. Left: the left panel's merged-branch images, then the
  other partial rows of `PORTING-MAP.md`.
- 2026-11-03, the left panel's merged-branch images done (upstream's `RepoObjectsTree.SelectionChanged`): the branches
  that the grid's selected commit contains have upstream's merged image and "contained in the currently selected commit"
  tooltip. See the RepoObjectsTree note in `PORTING-MAP.md`. Left: the other partial rows of `PORTING-MAP.md`.
- 2026-11-03, the left panel's hidden commits done (upstream's `UpdateVisibility`): branches, tags and stashes whose commit
  the grid does not show have the closed eye and gray text, a branch upstream's "not currently visible" tooltip, and no
  copy or script items. See the RepoObjectsTree note in `PORTING-MAP.md`. Left in the left panel: multi-selection, the
  submodule tree's super- and subprojects, the search's highlighting; then the other partial rows of `PORTING-MAP.md`.
- 2026-11-03, the left panel's search as upstream's done: every match highlighted, refs matched by their full path, each
  search on to the next match of its list, upstream's drop-down candidates; plus upstream's LeftPanel hotkeys F2 (rename)
  and F3 (search). See the RepoObjectsTree and HotkeySettingsManager notes in `PORTING-MAP.md`. Left in the left panel:
  multi-selection (with its Ctrl+Space hotkeys) and the submodule tree's super- and subprojects; then the other partial
  rows of `PORTING-MAP.md`.
- 2026-11-03, the left panel's multi-selection done: click, Ctrl+click and Shift as upstream's, Ctrl+Space and
  Ctrl+Shift+Space, underlined marked nodes kept across a refresh, and with several marked nodes the menu's "Filter for
  selected" for all their refs and Expand and Collapse. See the RepoObjectsTree note in `PORTING-MAP.md`. Left in the
  left panel: the submodule tree's super- and subprojects; then the other partial rows of `PORTING-MAP.md`.
- 2026-11-03, the left panel's submodule tree done: upstream's `SubmoduleStatusProvider` structure from the top project
  down (also from inside a submodule), folders compacted as upstream's, the status images, counts and tooltips with
  Settings > General's "Show submodule status in browse menu", and the menu's Open, Open in a new instance, Manage,
  Update, Synchronize, Reset, Stash and Commit with upstream's rules. See the RepoObjectsTree note in `PORTING-MAP.md`.
  Left in the left panel: Alt+click to a branch's tracking branch; next the other partial rows of `PORTING-MAP.md`.
- 2026-11-03, the toolbar's submodules button done: upstream's split button and `PopulateToolbar` drop-down from the same
  structure (superproject, top project, status images, "Update current submodule"; the button goes up to the
  superproject). See the FormBrowse toolstrip note in `PORTING-MAP.md`. Next: Alt+click in the left panel, then the
  other partial rows of `PORTING-MAP.md`.
- 2026-11-03, the left panel's Alt+click to a branch's related branch done (upstream's `RelatedBranch`); the RepoObjectsTree
  row of `PORTING-MAP.md` is now `ported`. Next: the other partial rows of `PORTING-MAP.md`.
- 2026-11-03, the Diff tab's diff groups done: upstream's `FileStatusDiffCalculator` linked unchanged; a merge against
  each parent and its combined diff, two to four commits in upstream's groups (BASE for separate branches), upstream's
  group nodes in the file list, and Settings > Diff viewer's "Show file differences for all parents". See the
  RevisionDiffControl note in `PORTING-MAP.md`. Next: the other partial rows of `PORTING-MAP.md`.
- 2026-11-03, upstream's file tree in the Diff tab and FormDiff done: folders (folders first, single-folder chains as
  one), extension and status group nodes with a tree below each, the flat variants, collapsible rows, and the toolbar's
  collapse and tree buttons with dense tree and group nodes in flat list. See the RevisionDiffControl note in
  `PORTING-MAP.md`. Next: the same tree in the commit window's and the stash window's lists, then the other partial rows.
- 2026-11-03, the file tree in the commit window's two lists (with the toolbar's collapse and tree buttons) and in the
  stash window's list done; the tree's rows are shared code (`FileTreeRows`). See the FormCommit and FormStash notes in
  `PORTING-MAP.md`. Left: the stash window list's toolbar and filter box; then the other partial rows of `PORTING-MAP.md`.
- 2026-11-03, the stash window list's toolbar and filter box done (collapse, tree and group-by buttons, the regex filter).
  See the FormStash note in `PORTING-MAP.md`. Next: the other partial rows of `PORTING-MAP.md`.
- 2026-11-03, the file lists' settings menus done: the commit window's working directory list (show ignored,
  skip-worktree and assumed-unchanged files read with git's flags, untracked as git's setting, the ignore editors,
  refresh on form focus), the Diff tab's (untracked for the working directory, the ignore editors, refresh on form
  focus, all parents), and upstream's toolbar enable rules (collapse only with diff groups, dense tree, group nodes).
  See the FormCommit and RevisionDiffControl notes in `PORTING-MAP.md`. Next: the A/B diff status filter buttons and
  images of the BASE groups, then the other partial rows.
- 2026-11-03, the BASE groups' diff status done: upstream's A/B/same/unequal images and the "!", "B", "A", "=" filter.
  See the RevisionDiffControl note in `PORTING-MAP.md`. Next: the other partial rows of `PORTING-MAP.md`.
- 2026-11-03, FormRevisionFilter rebuilt as upstream's (each filter's check box, ignore case, limit, branches, reflog,
  simplify by decoration, full history, simplify merges, upstream's enable rules and stored settings); the
  FilterInfo / FormRevisionFilter row is `ported`. FormMergeBranch is `ported` too (checked against its designer; its
  old "Not ported" line was stale). Stale "Not ported" lines found in FormCommit and FormClone are marked. Next: the
  other partial rows of `PORTING-MAP.md`.
- 2026-11-04, FormGitIgnore and FormAddToGitIgnore rebuilt as upstream's two windows: the editor with the default
  ignores, "Add pattern", the help text and links and the unsaved-changes question; the add window with the live preview
  of the matched files, now opened by the file lists' "Add to .gitignore" and "Add to .git/info/exclude". The
  FormGitIgnore row is `ported`; the FormSubmodules row too (its last gap, the submodule tree, was done 2026-11-03).
  See the FormGitIgnore note in `PORTING-MAP.md`. Next: the other partial rows of `PORTING-MAP.md`.
- 2026-11-04, FormGitCommandLog rebuilt as upstream's: the command log and the command cache tabs, the log oldest first
  following the newest command, the menus with Save to file, Copy full command line and Clear, and the "Always on top",
  "Word wrap" and "Capture call stacks" check boxes. The row is `ported`. See its note in `PORTING-MAP.md`. Next: the
  other partial rows of `PORTING-MAP.md`.
- 2026-11-04, FormAbout rebuilt as upstream's: the logo, donate badge and links, the contributor thanked every second
  with FormContributors behind it, and upstream's environment information with its copy button (now also copied by
  Help > Report an issue and the dashboard's Issues link). The row is `ported`. See its note in `PORTING-MAP.md`. Next:
  the other partial rows of `PORTING-MAP.md`.
- 2026-11-04, FormRebase's remaining buttons: "Add files" during a rebase, the From revision picker (FormChooseCommit on
  the current branch down to the merge base), the range from two selected commits in the grid, and the commit
  list's double-click (also in the apply patch window). See the FormRebase
  note in `PORTING-MAP.md`. Next: the other partial rows of `PORTING-MAP.md`.
- 2026-11-04, FormPull: "Checkout branch" in the detached HEAD question (Cancel closes the window, as upstream), the
  remote URL history (in FormPush too), and the submodule update after every kind of pull. See the FormPull and FormPush
  notes in `PORTING-MAP.md`. Next:
  the other partial rows of `PORTING-MAP.md`.
- 2026-11-04, FormFileHistory's menu: upstream's copy submenu (branches and tags too, shared with the browse grid) and
  "Difftool selected < - > local". See the FormFileHistory note in `PORTING-MAP.md`. Next: the other partial rows of
  `PORTING-MAP.md`.
- 2026-11-04, rows checked against upstream and marked `ported` (their "Not ported" lines were stale): FormCreateBranch,
  FormCheckoutBranch, FormCheckoutRevision, FormResetChanges; earlier today FormGitIgnore, FormSubmodules,
  FormGitCommandLog and FormAbout. 69 rows are still `partial`.
- 2026-11-04, FormRemotes' PuTTY buttons, "Load SSH key" and "Test connection" (core `Putty`). See the FormRemotes note
  in `PORTING-MAP.md`.
- 2026-11-04, PuTTY keys loaded into Pageant before pulls, fetches and pushes (dialogs and quick commands), and FormPush's
  "Load SSH key". See the FormPush note in `PORTING-MAP.md`.
- 2026-11-04, FormPull's prune offer after a pull rejected for a removed remote branch, and FormPush's rejection handling
  for "Push multiple branches". See the FormPull and FormPush notes in `PORTING-MAP.md`.
- 2026-11-04, the Detailed settings page's push and merge window groups, and the push window's "Get remote branches
  directly from the remote" (`ls-remote`). See the GraphRenderer / DetailedSettingsPage and FormPush notes in
  `PORTING-MAP.md`.
- 2026-11-04, upstream's title bar help button, as decided with the user: F1 and a "?" button in the seven dialogs that
  have it; FormResetCurrentBranch is `ported`. See "Title bar help button" in `PORTING-MAP.md`.
- 2026-11-04, the mouse's back and forward buttons navigate the grid's history (over the grid and the commit's
  details); the Navigate menu row is `ported`.
- 2026-11-04, FormCommitDiff's file list shows upstream's status images.
- 2026-11-04, FormFileHistory's remaining window parts: selecting several commits (the Diff tab and "Open with
  difftool" between two), the custom diff tools under the difftool items, upstream's filter toolstrip (now a shared
  `FilterToolBar` control the browse window uses too), the grid's Navigate and View menus with its hotkeys and quick
  search, and the build status column with the Build Report tab. `UseBrowseForFileHistory` is not ported (decided with
  the user: the file history window always opens), so FormFileHistory is `ported`. See FormFileHistory and FilterToolBar
  in `PORTING-MAP.md`.
- 2026-11-06, the file list menu (Diff tab, FormDiff, FormStash): "Diff the selected files", "Remember Second / First for
  diff" and "Diff with" the remembered file, the custom difftool list under the difftool items, and "Rename / move" with
  its F2 hotkey in the Diff tab. Fixed: a refilled list could hold the selected file twice, so the menu counted two
  files. The app tests build again: `DashboardDropTests` (from the 2026-11-04 commit) implemented Avalonia's
  `IStorageFolder`, which Avalonia 12 forbids, and now uses Avalonia's own folder item. See RevisionDiffControl in
  `PORTING-MAP.md`.
- 2026-11-06, upstream's "Find file" search window: the file list menu's "Find file..." and the browse window's
  Ctrl+Shift+F (find a file of the selected commit in the File tree tab). See SearchWindow / Find file in
  `PORTING-MAP.md`. The File tree tab still lacked upstream's file list menu then (added the same day, below).
- 2026-11-06, the File tree tab's item menu: upstream's file list menu in file tree mode (the commit's files, a selected
  folder renamed, copied, ignored, filtered or shown in history as upstream's `SelectedFolder`), the tree items (select
  all, collapse and expand all, collapse root folders), several selected nodes, the BrowseDiff hotkeys, and Blame in place
  of the file's content. The Diff tab's Blame now opens the file there, as upstream's. See RevisionDiffControl in
  `PORTING-MAP.md`. Left: git-grep, file images and the folder summary in the tree; tree items and folder selection in the
  Diff tab's tree.
- 2026-11-06, folder selection in the file lists' trees (the Diff tab, FormDiff, FormStash): a click selects a folder or
  group node, its glyph or a double-click toggles it, as upstream's tree; a selected node stands for its files in the menu
  (upstream's `SelectedFolder`), and the menus have the tree items. See RevisionDiffControl in `PORTING-MAP.md`.
- 2026-11-06, "Find in commit files using git-grep" in the Diff tab: upstream's box, toolbar button and window, the
  "grep" group of matching files and their matches in the viewer, the menu items and the Ctrl+Shift+F hotkey. See
  RevisionDiffControl in `PORTING-MAP.md`. The File tree tab's git-grep was added the same day: its box and the menu's
  items, the tree listing only the matching files, and their matches in the viewer. Also the File tree tab's filter box.
- 2026-11-06, upstream's file filter combo box in every file list (`FileFilterBox`): the 250 ms delay, the sample and
  recent filters, the delete button, the colors, working directory paths cut and old names matched. See RevisionDiffControl
  in `PORTING-MAP.md`.
- 2026-11-06, a selected folder's description in the Diff and File tree tabs' viewer, and the File tree tab's folder and
  file images.
- 2026-11-06, upstream's line patching: "Reset selected line(s)" (R) in the commit window, and in the Diff tab, FormDiff
  and FormStash the viewer's stage, unstage and reset of lines, a commit's lines applied or undone, and the file menu's
  "Cherry pick changes". See RevisionDiffControl in `PORTING-MAP.md`.
- 2026-11-06, upstream's continuous scroll in the commit window, FormDiff and FormStash (Alt + wheel past a diff's end, or
  always with the setting, which the viewer's menu and the Diff viewer settings page have). See FileViewer in
  `PORTING-MAP.md`. Also: the browse window stays busy until opening a repository has finished (recent list, setting,
  plugins), which two plugin tests and a settings test raced.
- 2026-11-06, the commit window's "Skip worktree" and "Assume unchanged" items. See FormCommit in `PORTING-MAP.md`.
- 2026-11-06, the Diff tab menu's submodule items (update, reset, stash, commit, open with Git Extensions), also in the
  commit window. See RevisionDiffControl in `PORTING-MAP.md`.
- 2026-11-06, the file menus' "Open ... with..." items (Windows' "Open with" dialog, on Windows only) and the remaining
  BrowseDiff hotkeys of the menu (open revision, difftool to the working directory, filter in grid, add to .gitignore).
- 2026-11-07, the BrowseDiff hotkeys in FormDiff's and FormStash's file lists (upstream loads them in every
  FileStatusList), the `SelectFirstGroupChanges` hotkey (Ctrl+A: the first diff group's files) in every list with the
  menu, and the submodule items in FormDiff and FormStash. See RevisionDiffControl in `PORTING-MAP.md`.
- 2026-11-07, the file menu's "Reset chunk of file..." and "Interactive add..." (git's interactive patch in a console
  window on Windows, in the terminal emulator elsewhere, not on macOS). See RevisionDiffControl in `PORTING-MAP.md`.
- 2026-11-07, the file menu's "Open in Visual Studio" and its Ctrl+Shift+S hotkey (Windows only). See RevisionDiffControl
  in `PORTING-MAP.md`.
- 2026-11-07, the commit window's lists use the shared file list menu, as upstream's FormCommit binds FileStatusList's
  (its own two menus are gone), so they have the whole menu and the BrowseDiff hotkeys; "Skip worktree" and "Assume
  unchanged" moved into the shared menu. See FormCommit in `PORTING-MAP.md`. Fixed two flaky tests at their root: a
  submodule stash raced the viewer's diff of the submodule (git's index lock in it), and a test read a file git was
  still writing. Upstream FileStatusList's item menu is now ported in every list that has it.
- 2026-11-07, the viewer's find (Ctrl+F, F3, Shift+F3; upstream's find window, highlight all, the Diff tab's search going
  on in the next file) and go to line (Ctrl+G), and the syntax highlighting hotkey (X). The browse and commit windows let
  the focused viewer take its hotkeys first. See FileViewer in `PORTING-MAP.md`.
- 2026-11-07, the viewer's "Treat all files as text" (a binary file's changes as text). Next and previous occurrence are
  not ported: they need text selected in a line, which the shell's viewer does not have. See FileViewer in
  `PORTING-MAP.md`.
- 2026-11-07, the viewer toolbar's encoding box (diffs and grep matches read in the chosen encoding, the repository's by
  default) and settings button. The second submodule stash test had the same race as the first (now waits for the
  viewer) and read a file git was rewriting. See FileViewer in `PORTING-MAP.md`.
- 2026-11-07, fixed: the toolbar's submodules, worktrees, working directory and branch drop-downs (and the commit
  window's "Commit message" and "Commit templates" menus and the difftool chooser) opened as a thin empty line. Their
  items were built in the flyout's `Opening` event, and an Avalonia 12 `MenuFlyout` whose items change while it opens
  keeps the size of its empty presenter. They are now `PopulatingMenuFlyout`s, built before they open (after the read,
  for the worktrees, the earlier messages and the difftools). `ToolbarDropDownTests` checks that each opens wide.
- 2026-11-07, the grid's artificial rows as upstream's `ArtificialCommitChangeCount`: the changed and dirty submodule
  counts with their icons, the unknown state until the status has been read, and the summary of the changed files as the
  row's tooltip. The toolbar's commit button got `RepoStateVisualiser`'s dirty submodules and unknown states, and the
  clean image while the setting hides the status. The ArtificialCommitChangeCount row is `ported`; see it and the
  FormBrowse toolbar note in `PORTING-MAP.md`. 56 rows are still `partial`.
- 2026-11-07, the grid's ref labels: upstream's highlight frame on the label under the pointer and its tooltip (the
  branch's tracking, ahead and behind counts, or what the ref is, then the commit's message). See the MessageColumnProvider
  row's note in `PORTING-MAP.md`. Fixed a flaky test: `DiffLinePatchingTests` read the file while git rewrote it.
- 2026-11-07, the Colors settings page's "Revision graph" group (six check boxes, applied when saved) and "Draw non
  relatives text gray" in the grid. See the ColorsSettingsPage row and note in `PORTING-MAP.md`.
- 2026-11-07, the grid's multi-line indicator (three dots at the message's right end for a commit with a body). See the
  MessageColumnProvider row's note in `PORTING-MAP.md`.
- 2026-11-07, the toolbar's branch drop-down as upstream's (checkout item, branches read from git as it opens, hidden
  branches marked). See the FormBrowse toolbar note in `PORTING-MAP.md`. Fixed the flaky `ToolbarDropDownTests` branch
  case at its root: the headless font is wide, so the working directory's path made its button about 900 pixels wide and
  the branch button lay outside the 900 pixel test window, where the click hit nothing. The tests' window is wide enough
  now, and their clicks fail at once outside it. The product side, a toolbar wider than the window cutting its last buttons off,
  is fixed by the overflow drop-down below.
- 2026-11-07, the grid's merge source arrow (an outlined arrow on the remote branch the checked-out branch tracks). See
  the MessageColumnProvider row's note in `PORTING-MAP.md`.
- 2026-11-07, "Save current view settings as default" saves all of upstream's runtime view settings (grid sort, reflog,
  branch filter, current branch only, diff appearance), not only the diff viewer's four. See DiffViewerSettingsPage in
  `PORTING-MAP.md`.
- 2026-11-07, the Browse repository window settings page's "Show 'Find in commit files using git-grep'" and "Show GPG
  information" (Tabs group). See FormBrowseRepoSettingsPage in `PORTING-MAP.md`.
- 2026-11-07, upstream's toolstrip overflow (G2) on the browse window's main toolstrip: the buttons that do not fit go into
  a "»" drop-down that hosts the buttons themselves. See the FormBrowse toolbar note in `PORTING-MAP.md`.
- 2026-11-07, git notes in the commit info (after the message) and its menu's "Add notes" with the Ctrl+Shift+N hotkey.
  See the CommitInfo note in `PORTING-MAP.md`. Then the grid's notes: the View menu's "Show git notes" and "Show Git
  notes column". See the MessageColumnProvider row's note in `PORTING-MAP.md`.
- 2026-11-07, audit of every `partial` row of `PORTING-MAP.md`: each "not ported" line was checked against the code, and
  each note ends with an "Audit 2026-11-07" line naming what is really missing and which earlier lines are stale. Ten
  rows had nothing left and are `ported` (FormRemotes, FormResolveConflicts, Dashboard, the Scripts, Revision links,
  Detailed and Plugins pages, the askpass helper, FormGitStatistics, Avatars); 46 stay `partial`. What is left, by area:
  - Revision grid: session refs; the superproject's labels and refs.
  - Viewer (FileViewer): replace.
  - Diff and File tree tabs: the settings menu's toolbar submenu, and the git-grep window's selected text, "Treat all files as text" and next/previous match.
  - Blame: the text pane is a list, not upstream's FileViewer (syntax highlighting, find, the encoding).
  - Dialogs: FormCommit's progress bar; FormPush's Azure DevOps link; the help pictures' dark theme adaptation; FormClone's
    PuTTY prompts and `StartCloneDialog`; FormDiff's choose commit artificial commits; the process dialog's PuTTY retry;
    FormStash's loading animation; the editor's `i18n.filesEncoding` and todo syntax highlighting (no editor control).
  - Settings: the tool path and command fields and rows, the files encoding, the warning without git; "Download Git",
    validating the git command, the HOME check and FormFixHome's search; the theme editor; the pages and settings of
    features the shell lacks (Console, Shell
    extension, Git > Advanced, help images, spelling dictionary); the checklist's installer rows and startup check; the
    confirmations of features the shell lacks.
  - Hotkeys: the commands of missing features.
  - Plugins and the rest: the repository hosts menu, a load error's clipboard copy, `RunCommand`, `GetService` and
    `BrowseRepo`; the `searchfile` verb; the
    bisect toolbar icons; the pull request window's status icons; `commit.template` and plugin template icons; the
    spelling checker and its "Mark ill formed lines"; the filter toolbar's drop-down width; the PuTTY menu items
    (Windows); the Console and Output history tabs (ConEmu, Windows only); the
    commit info texts' translations.
- 2026-11-08, the audit's quick wins, each struck from the list above. The blame view is upstream's whole BlameControl but
  its text pane: the menu's commit items, the line kept and followed between commits, the commit information above, the
  gutter's tooltip and the hovered commit's highlight. This also fixed a bug: a blame line carried only a short hash, so
  going to its commit in the file history failed and the repository host's items never showed. FormRebase's Continue reads
  git's output, so recorded resolutions (rerere) go on by themselves. The action bar's Abort runs at once. Continuous
  scroll in the Diff tab, the File tree tab and FormCommitDiff. Gource's author pictures through `$(AVATARS)`. See the
  FormBlame, FormRebase, InteractiveGitActionControl, RevisionDiffControl, FormCommitDiff and GourceStart notes in
  `PORTING-MAP.md`.
- 2026-11-08, more of the list above, each struck from it. The Diff tab's range diff item and "Show skip-worktree files".
  FormCommit's "Stage in Superproject" and no "Stash staged" for an old git. The process dialog's "Keep dialog open". The
  hotkeys' alternative next and previous keys, the path filter's reset and "Reset all Hotkeys to defaults". The sorting
  page's help links. "Show end-of-line markers as glyph", with upstream's text marks as the default. The filter toolbar's
  shortcut texts and "No results found". FormClone giving its key to Pageant. The audit's "authors of several selected
  commits" was a misreading: upstream keeps the highlight, and now so does the shell. See the RevisionGridControl,
  RevisionDiffControl, FormCommit, FormRemoteProcess, HotkeySettingsManager, SortingSettingsPage,
  AppearanceFontsSettingsPage, FilterToolBar and FormClone notes in `PORTING-MAP.md`; 39 rows stay `partial`.
- 2026-11-08, the viewer's images and binary content: every window with a viewer shows an image of the file's revision as a
  picture instead of its diff, a new binary file (and any binary file in the File tree tab and the file history's View tab)
  as upstream's hex dump, and asks before showing a file over 5 MB. See the FileViewer note in `PORTING-MAP.md`.
- 2026-11-08, git's coloring and the diff appearances: the viewer reads git's colored output with upstream's parser (linked),
  which is upstream's default, so real diffs now show git's colors, moved code included; "Git coloring" and "Reverse
  background color" on the Diff viewer page; "Diff appearance" with Patch, Git word diff and Difftastic, with their hotkeys;
  the combined diff of merges and the range diff in git's colors too. See the FileViewer note in `PORTING-MAP.md`.
- 2026-11-08, the viewer's inline differences: what a changed line shares with the one it replaces is dimmed, as upstream. Also
  fixed upstream's color helpers reading the light default theme whatever the theme was. See the FileViewer note in
  `PORTING-MAP.md`.
- 2026-11-08, submodules in the viewer (upstream's status text, "Open Submodule Diff in separate window"), "Show blame in diff viewer" and the File tree tab's submodule items. See the FileViewer and RevisionDiffControl notes in `PORTING-MAP.md`.
- 2026-11-08, the app icon on every window: only the browse window set one, so all others had a blank taskbar button. It now comes from the application's `Window` style, which plugin windows get too.
- 2026-11-08, plan for moving development to macOS: `MACOS.md`, and the section below. No code changed.
- 2026-11-08, first real results on a Mac, then the Cmd keys and macOS's own defaults, and the fit into macOS: `MACOS.md`, MAC2 to MAC5.
- 2026-11-08, on macOS: the file lists' toolbar submenu (upstream's `tsmiToolbar`, its settings shared with the WinForms app) in the
  browse and commit windows, and the choice applied in the stash and diff windows. The blame text's find, go to line, syntax colors
  and encoding. See the notes in `PORTING-MAP.md`.
- 2026-11-08, on macOS: find, replace and go to line in the editor window (upstream's FormEditor is an editable viewer; here it was
  a text box without them), with upstream's FindAndReplaceForm in replace mode. Next and previous match in git-grep's view, whose
  buttons showed but did nothing. See the notes in `PORTING-MAP.md`.
- 2026-11-08, on macOS: the Git config page's tool path and command rows with Browse and Suggest, the files encoding with upstream's
  FormAvailableEncodings, the warning without git and the check of what is typed as the git command. Fixed on the way: upstream's
  `AppSettings.LoadSettings` was never called, so the viewer's encoding box was empty in the real app; and a git failure while the
  page loaded left the window's Save disabled, so the git path could not be corrected.

### Continuing on macOS

Written 2026-11-08. Parity work resumes after phases MAC1 and MAC2 of `MACOS.md`, so that the first macOS test results are
not mixed with new changes. This sorts the audit's list above (2026-11-07) by where the work can be done and checked. All
results so far come from Windows: nothing in this list or the Progress above has been seen on a Mac.

**Do on the Mac, fully checkable there.** In the proposed order, starting with what affects first use:

1. ~~Settings: the tool path and command fields, the files encoding, the warning without git, validating the git command.~~
   Done 2026-11-08 on macOS (see the GitConfigSettingsPage note in `PORTING-MAP.md`).
2. ~~Viewer and tabs: replace; the settings menu's toolbar submenu; the git-grep window's "Treat all files as text" and
   next/previous match; the blame text pane's find, syntax colors and encoding.~~ Done 2026-11-08 on macOS. The
   git-grep window's selected text is **not doable as it stands**: the viewer selects whole lines and has no text selection, so upstream's
   `getSelectedText` has nothing to read. The same holds for "next and previous occurrence" (Alt+Right, Alt+Left). Both wait for
   a viewer with real text selection, which is a larger change than either.
3. ~~Dialogs: FormCommit's progress bar, FormPush's Azure DevOps link, the help pictures in dark themes,
   `StartCloneDialog`, FormDiff's choose commit artificial commits, FormStash's loading animation, the editor's
   `i18n.filesEncoding`.~~ Done 2026-11-08 on macOS. The editor's todo highlighting is **not doable as it stands**: a `TextBox` has one
   color, so it waits for an editor control, as its line numbers do. Found on the way: staging was not split by command line length,
   unstaging a rename left its old name staged, and dark themes used light system colors (see `PORTING-MAP.md`). New from the dark
   render: most icons are not adapted to a dark theme yet, which upstream does with `AdaptLightness`; that belongs to item 6.
4. ~~Revision grid: session refs, the superproject's labels and refs.~~ Done 2026-11-09 on macOS (see the Revision grid
   rows and View menu note in `PORTING-MAP.md`). Only the browse window's grid shows the superproject labels.
5. Plugins and the rest: ~~the repository hosts menu, a load error's clipboard copy, `GetService`, `BrowseRepo`, bisect
   toolbar icons, the pull request window's status icons, `commit.template`, plugin template icons, the filter toolbar's
   drop-down width, the commit info translations~~ done 2026-11-09 on macOS (the hosts menu was already there; see each row
   in `PORTING-MAP.md`). **Left: `RunCommand` and the `searchfile` verb**, which turned out to be one larger piece: upstream's
   command line has about 40 verbs (`GitUICommands.RunCommand`), of which the new shell has `fileeditor`, `clone` and a
   repository path, and the plugin API's `Start...Dialog` methods mostly throw `NotSupportedException`. Both need the same
   dispatcher from a verb or call to the shell's windows, each opened on its own as upstream's are. **Decided 2026-11-09: not
   ported for now**; the work goes on with item 6. Recorded in the GitUICommands (plugin side) and SearchWindow notes of
   `PORTING-MAP.md`.
6. The larger ones: ~~the theme editor, then the spelling checker with "Mark ill formed lines" and its dictionary setting~~
   done 2026-11-09 on macOS. Upstream has had no theme editor since 2019; its Colors page's "Open theme folder" is ported
   instead. See the ThemeModule and EditNetSpell rows of `PORTING-MAP.md`.
   The hotkeys and settings pages of a feature come with that feature. ~~Output history can be done too~~ done 2026-11-09 (see
   the OutputHistory note of `PORTING-MAP.md`); only the Console tab is ConEmu.

**Windows only: do not start from the Mac** (`MACOS.md`, section 7): FormClone's PuTTY prompts, the process dialog's PuTTY
retry, the PuTTY menu items, "Download Git", the HOME check and FormFixHome's search, the checklist's installer rows and
startup check, the Shell extension page, the Console tab (ConEmu).

**Needs a decision off Windows:** what the Console tab and its settings page mean without ConEmu (leave them out, as now, or
an embedded terminal, which would be new work). Recommendation: leave out, and record `windows-only` in `PORTING-MAP.md`.

**New for macOS:** the Cmd keys and macOS's reserved keys (MAC3), and the menu bar, application menu and `PATH` (MAC4). They
have no upstream form to compare with, so a test or a recorded check is their evidence, and notes go in `PORTING-MAP.md`.

**Items that look different on a Mac** are not parity bugs: `MACOS.md`, section 6, lists them. The reverse also holds: a step
that adds a Windows-only code path while on the Mac goes on the Windows check list.

## Risks

- Fluent fights a dense WinForms look; restyling may be cheaper with Avalonia's Simple theme as a base. Decide in U1 with a
  prototype of one toolstrip and the grid.
- Icons are bitmaps sized for 96 dpi; at high scaling they look soft, as in upstream. SVG versions would be new assets.
- Moving actions into context menus changes what the headless tests click; the tests move with them.
