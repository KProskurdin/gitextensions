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

## Risks

- Fluent fights a dense WinForms look; restyling may be cheaper with Avalonia's Simple theme as a base. Decide in U1 with a
  prototype of one toolstrip and the grid.
- Icons are bitmaps sized for 96 dpi; at high scaling they look soft, as in upstream. SVG versions would be new assets.
- Moving actions into context menus changes what the headless tests click; the tests move with them.
