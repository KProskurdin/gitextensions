# Cross-platform Git Extensions: implementation plan

Status: revised 2026-10-03 after the goal update. Owner: KProskurdin. Working branch: `feature/cross-os-ui`.

Goals this plan serves (both are hard requirements):
1. **A cross-platform application**, not only passing tests: Git Extensions runs as a usable app on
   Windows, Linux and macOS.
2. **Cheap upstream merges for the life of the fork**, including the case where the fork is never
   merged upstream (section 11).

## 1. Goal

Make Git Extensions run on Windows, Linux and macOS, while the fork keeps merging upstream
(`gitextensions/gitextensions`) without permanent conflicts.

"Cross-platform" is not yet defined to a level of scope. Section 9 lists the decisions that
set the scope. Until they are made, this plan is a sequence of milestones, each with an exit
criterion, and the milestones beyond M4 are estimates only.

## 2. Where we are

| Area | State | Evidence |
|---|---|---|
| Shared core (`GitCommands`, `GitExtUtils`, `ResourceManager`, `GitExtensions.Extensibility`, `GitUIPluginInterfaces`) compiles as `net10.0` | Done | `src/xplat/*` shadow projects, 0 errors on Windows and Linux |
| Shared core tests | Done with a gap | Windows 3482 pass (1 known failure, `AsyncLoaderTests`). Linux 3369 pass, 0 fail, 144 ignored with reasons (verify run 2026-10-03) |
| Upstream files | Untouched | 0 fork changes in upstream folders (checked 2026-10-03); all changes are additive or carried as seams (section 5) |
| Seams | 7 | Section 5 table: `XplatPatch` items in `src/xplat/GitCommands/GitCommands.csproj`, plus `XplatFriends.cs`, `SupportedOSPlatforms.cs`, and the test-side replacements |
| Settings persistence | Linux done 2026-10-03; macOS by CI only | Off Windows, seam S1 now writes machine-level values to `GitExtensions.registry.json` in the user config directory. The settings XML file was already written there |
| Avalonia app | Read-only commit list | `src/xplat/GitExtensions.Xplat.App`. Runs on Windows and Linux (WSLg), checked by screenshot. Not run on macOS |
| Fork workflow | Partly set up | `upstream` remote added and fetched (upstream/master 52d08e996). Local `master` NOT moved (user decision). Verify script `tests/xplat/verify-upstream.sh` passed on that upstream |
| CI | Written, not run on GitHub | `.github/workflows/xplat.yml`: shadow tests on Windows and Ubuntu, nightly upstream check. No macOS job. The app is not built in CI |
| Git history | User commits | `7e1abfec4 begin of implementation`, `2229a18f add linux tests` on `feature/cross-os-ui` |

Upstream size, for scale (measured 2026-10-03):

- `src/app/GitUI`: about 99k lines of non-designer code, 154 designers, 125 control classes, 87 form classes.
- `src/app/GitCommands`: about 25k lines, with only 7 files that touch WinForms types.
- Upstream churn in the last 12 months: 418 commits, 275 of them in `GitUI`. The fork is 17 commits behind upstream.

The shared core is close to portable. The UI is the expensive part, and it is also where upstream
changes most often. That is why the plan keeps the WinForms UI untouched and builds a second UI beside it.

## 3. Principles

0. **Mergeable for the whole life of the fork.** Upstream merges are a routine that must stay
   cheap, not only while cross-platform work is active. The fork may never be accepted upstream,
   and it may outlive the people who started it, so every decision is checked against the
   question "can someone who did not write this still merge upstream into it in an afternoon?"
   Section 11 defines the rules and the measures.
1. **Additive fork.** Never edit an upstream file to make the fork work. New code lives under
   `src/xplat`, `tests/xplat`, `docs/xplat` and `GitExtensions.xplat.slnx`. Exceptions are
   listed as seams (section 5).
2. **One engine, two shells.** The git engine stays shared and is used as-is. The WinForms shell and
   the Avalonia shell both call it. Logic that is currently inside WinForms forms is
   **reimplemented** in the new shell, never moved out of the upstream form: moving it would edit an
   upstream file. Each reimplementation is traceable (section 6), and upstream changes to the
   original are tracked, so the copy is not silently left behind.
3. **Platform differences are explicit.** Each Windows-only expectation is marked as ignored with
   a reason (`XplatPlatformSkips.cs`), and the Linux expectation is written as its own test
   (`XplatLinux*.cs`). Nothing is silently excluded.
4. **No commits from automation.** The user commits. Work stays in the working tree until then.
5. **Measure before porting.** Each milestone has an exit criterion that can be checked by a
   build, a test, or a screenshot.

## 4. Repository layout (target)

```
src/
  app/                      upstream, untouched
  plugins/                  upstream, untouched
  xplat/
    Directory.Build.props   common settings for all shadow and new projects
    Directory.Packages.props central package versions for xplat
    eng/                    SharedSources.targets, XplatPatch.targets, SupportedOSPlatforms.cs, XplatFriends.cs, Generate-KeysShim.ps1
    WinFormsShim/           stand-in WinForms types with host hooks
    GitCommands/ ...        shadow projects (same names as upstream, sources linked)
    GitExtensions.Xplat.App/   Avalonia app (M1 onward)
    GitExtensions.Xplat.Core/  NEW in M2: UI-agnostic services and view models
tests/
  app/                      upstream, untouched
  xplat/
    CommonTestUtils/        shadow of upstream test helpers, plus TestAppSettingsAttribute replacement
    GitCommands.Tests/      shadow of upstream tests, XplatPlatformSkips.cs, XplatLinux*.cs
    GitExtensions.Xplat.App.Tests/  NEW in M1: Avalonia headless tests
tests/xplat/
  run-tests.sh             builds and tests the shadow projects (Windows via cmd.exe; Linux/macOS directly)
  verify-upstream.sh       runs the same against upstream/master in a temporary worktree
  port-drift.sh            NEW in M1: lists upstream commits that touched ported files since the recorded commit
  xplat-drift.sh           NEW in M1: flags mirrored upstream test classes (from each XplatLinux* file's
                           "// Mirrors:" line and the XplatPlatformSkips.cs entries) that changed or disappeared
docs/xplat/
  PLAN.md                  this file
  SYNC-LOG.md              one entry per upstream check or merge
  PORTING-MAP.md           NEW in M1: one row per ported upstream file
GitExtensions.xplat.slnx    solution for everything above
.github/workflows/xplat.yml CI: shadow tests on Windows, Linux and macOS (macOS added in M1), nightly upstream check
```

## 5. Seams: the complete list

A seam is an upstream line that must change for the shared core to run cross-platform. Seams are
applied at build time by `XplatPatch` (the build fails if an upstream merge moves the anchor), or
by an additive file.

| # | Where | Change | Why | Upstream fix candidate |
|---|---|---|---|---|
| S1 | `AppSettings.cs` | Registry key replaced by `XplatRegistryKey` (real registry on Windows, `EmulatedRegistryStore` JSON file elsewhere) | Settings are read from the registry in the static constructor | Yes, a cross-platform settings store would be an upstream discussion |
| S2 | `AppSettings.SaveSettings` | Named mutex name avoids `/` on non-Windows | `/` makes `Mutex` throw, upstream swallows the error, so settings were never saved | Yes, real bug on Linux |
| S3 | `DebugHelpers.cs` | `IsTestRunning` also detects NUnit | Only `testhost.exe` was recognized | Yes, real bug on Linux |
| S4 | `PathEqualityComparer.cs` | Trims `Path.DirectorySeparatorChar` instead of `'\'` | `/repo/` and `/repo` compared unequal on Linux | Yes, real bug on Linux |
| S5 | `GitExtUtils` | `InternalsVisibleTo("GitExtensions")` in `XplatFriends.cs` | `ThreadHelper.JoinableTaskContext` setter is internal | Maybe, a public initializer would be cleaner |
| S6 | `CommonAssemblyInfo.cs` | `SupportedOSPlatform` for linux and macos added alongside `windows7.0` | NUnit 4.3 refuses an assembly whose platform list excludes the running OS | Maybe |
| S7 | `TestAppSettingsAttribute` (test side) | Replaced with a file-lock version | Named `Semaphore` is unsupported on Unix | No, test-only |

Rule: a new seam needs a line in this table, a reason, and a note on whether it is an upstream
candidate. Seams S2, S3 and S4 are bug fixes that would help upstream users on Linux too.

## 6. Mapping WinForms to the new shell

For every upstream form or control that gets ported, the port is recorded in
`docs/xplat/PORTING-MAP.md` (created in M1) with four columns: upstream file, new file,
upstream commit the port was based on, and status (`ported`, `partial`, `skipped: <reason>`,
`windows-only`). This table is what makes upstream merges manageable.

`tests/xplat/port-drift.sh` (built in M1) reads the table and lists upstream commits that touched
a mapped file since its recorded commit. Each listed commit must end as a row update: ported,
skipped with a reason, or the row is marked `stale`. It runs with the nightly verify job, so
drift is visible without anyone remembering to look.

## 7. Milestones

Estimates are for one developer who is familiar with the codebase. They are rough, and they are
not a commitment. The first three rows are the ones the team can plan against.

### M0. Foundations (done, with one open item)

Exit: shared core builds and tests on Windows and Linux; fork has an upstream remote; CI file exists.

- Done: shadow projects, stand-in shim, seams S1 to S7, Windows and Linux test runs, upstream
  verify script, solution file `GitExtensions.xplat.slnx`, CI workflow.
- Open: move local `master` to `upstream/master` (user action, not done by automation);
  commit the current work (user action); confirm the CI workflow runs on GitHub.

### M1. Application shell and first read-only view (started)

Exit: the app opens a repository on Windows, Linux and macOS, lists commits, shows the selected
commit's details, and remembers its state across restarts. On macOS the exit is verified by CI
(build, headless tests, screenshot artifact); a run on real hardware follows when a Mac is available.

Done: window, path box, commit list (read-only), developer screenshot aid.

Remaining:
1. Done 2026-10-03 (Windows and Linux): commit selection shows the message, author, dates and parents
   (`RevisionReader.GetRevision`, off the UI thread).
2. Done 2026-10-03 (Windows and Linux): the list reads one page of 500 commits at a time. Reading stops once the page is full (`CommitHistory.LoadPage`), "Load more" re-reads with a larger limit, and the list is virtualized by Avalonia. Not done: a skip-based page read, which needs `GetLog` to take a count or offset.
3. Done 2026-10-03 (Windows and Linux): headless UI tests in `tests/xplat/GitExtensions.Xplat.App.Tests`
   (Avalonia.Headless.NUnit, 5 tests: git missing, git too old, open a repository, select a commit,
   root commit). CI runs them on all three OSes; macOS pending the first CI run.
4. **Settings persistence off Windows (done on Linux 2026-10-03; macOS pending CI).** Replace the in-memory store behind seam S1 with a file in
   the platform's user-config directory (XDG on Linux, Application Support on macOS). Windows keeps
   the registry-plus-settings-file behavior it has upstream. Recent repositories must survive a restart.
5. **Git discovery (done on Linux 2026-10-03; macOS rules unit-tested, not yet run on a Mac).** Find the `git` executable per OS (PATH, then known locations; macOS Command Line
   Tools), validate its version against the minimum the shared core expects, and show a clear message
   when it is missing. Upstream has a `gitcommand` setting; the new shell reads it and does not
   invent a second one.
6. Done 2026-10-03 (Windows and Linux): window title from the repository folder, icon from `setup/assets/Logo` (linked, not copied), and an error dialog (`ErrorWindow`) for failed opens. The icon is set in code and not seen in a screenshot, because the screenshot aid renders the content only.
7. **macOS in CI (workflow changed 2026-10-03; first green run pending a push).** `macos-latest` is in
   `.github/workflows/xplat.yml`: shadow tests and app build. Headless tests (item 3) and the screenshot
   artifact (item 8) are still to come. Until a Mac is available this is the macOS evidence,
   and every status report says so.
8. Done 2026-10-03, not yet run on GitHub: `app-screenshot` job in `.github/workflows/xplat.yml` builds, launches the app on the checkout (`xvfb-run` on Linux), and uploads a screenshot on all three OSes. The launch command was run locally in WSL; the workflow itself has not run.
9. `docs/xplat/PORTING-MAP.md` created 2026-10-03 with rows for the forms the app already replaces (`FormBrowse`, `RevisionGridControl`, both `partial`)
   (the commit list replaces `FormBrowse`'s revision grid, the window replaces `FormBrowse`).
10. `tests/xplat/port-drift.sh` and `tests/xplat/xplat-drift.sh` (section 4), run by the nightly job.
    Done 2026-10-03. Against `upstream/master` today: port-drift lists 2 upstream commits touching the
    two ported forms; xplat-drift flags `PathUtilTest` (648dd4cc7) and `GitModuleWorktreeTests` (065da3680).
    Both need a decision before the next sync.

Estimate: 3 to 4 weeks (the settings and git-discovery items were not in the earlier estimate).

### M2. Engine services for UI (the boundary)

Exit: every operation the UI needs is available as a service with no WinForms dependency, and the
WinForms shell calls the same services where practical.

Started 2026-10-03: `src/xplat/GitExtensions.Xplat.Core` holds `ICommitHistory` with `GitCommitHistory`
(the history reads, moved out of the app), `CommitListViewModel` (commit list, paging, selection,
details, errors, with no UI types) and `ObservableObject`. The app's window is a view over the view model.
Continued 2026-10-03: `Repository/IRepositoryService` with `GitRepositoryService` (snapshot: current branch,
local and remote branches, changed files with kind and staged state) and `RepositoryViewModel`. The window
shows the branch and the changes in a left panel. Open stays disabled while either panel is loading.
Tests: `tests/xplat/GitExtensions.Xplat.Core.Tests` (22, against fakes, including the status mapping) and the
app's headless tests. `GitCommands` grants friend access to the core in
`src/xplat/GitCommands/XplatFriends.cs`.

Write services, 2026-10-03: `Operations/IGitOperations` with `GitOperations`: stage, unstage, commit (with
amend), create and check out branches, delete branches, fetch, pull, push and clone. Each builds its command
with the upstream builders (`Commands.*`, `GitModule.FetchCmd/PullCmd`) and runs it through the same
executor. A failed exit raises `GitOperationException` with git's own message. Tested against real
repositories and a bare remote.

Operations view model and window, 2026-10-03: `Operations/RepositoryOperationsViewModel` runs each operation,
reports its status and error, and raises `RepositoryChanged` with the repository path so the window reloads it
(for clone, the new folder). The window has Fetch, Pull and Push (origin, current branch), Clone (a dialog for
URL and folder), stage and unstage of selected changes, commit with amend, create, check out and delete of
branches. Buttons are enabled only when their action can run. Tests: 34 core tests (fake operations) and 27 app
tests, including an end-to-end stage-and-commit through the window.

Added 2026-10-03: checkout of remote branches as tracking branches; headless tests for the clone dialog (URL and
folder, cancel, and staying open when the folder is missing); `PORTING-MAP.md` rows for the seven upstream
write-operation forms, reviewed against upstream `52d08e996` in `SYNC-LOG.md`.

Platform services, 2026-10-03: `Platform/` in the core holds `HostPlatform`, `IProcessLauncher` (no shell, does not
wait), `IFileManager` (Explorer, Finder via `open`, `xdg-open`) and `ITerminalLauncher` with a per-OS default
terminal (`cmd.exe`; Terminal on macOS; `$TERMINAL` or `x-terminal-emulator` on Linux). The window has Open folder
and Terminal buttons. The clone dialog has a folder picker (Avalonia storage provider) that names the new folder
after the repository. Tests: 44 core tests (recording fake launcher, one case per OS) and 32 app tests.
Not tested: the Avalonia folder picker and the real launches (they need a desktop session and would open windows).

Stash and clipboard, 2026-10-03: stash save (with an optional message) and pop of the top stash, through the same
operations layer; the window has the stash controls and a Copy hash button for the selected commit (Avalonia
clipboard). Tests: 46 core tests, 35 app tests, including a stash round trip and a clipboard check in headless.

Credentials: no new service yet. Upstream's `EnvironmentConfiguration` runs before every git start, so `SSH_ASKPASS`
and `DISPLAY` are already set for the shadow process. HTTPS and SSH authentication therefore rely on git's own
credential helpers (decision 6). Without a helper, a GUI push to an HTTPS remote fails with git's message instead
of prompting. This is untested against a real remote.

Still open in M2: tags; notifications; the UI-thread abstraction (the view models do not marshal, so it is not
needed yet); a credential store for Linux and macOS, if git's helpers are not enough.

- Create `GitExtensions.Xplat.Core` with interfaces for: repository open, history, refs, status,
  stage/unstage, commit, branch, checkout, fetch/pull/push, stash.
- Each service delegates to `GitCommands`. Where the logic lives in a WinForms form today, the
  core **reimplements** it against `GitCommands`. The upstream form is not changed. The port is
  recorded in `PORTING-MAP.md` (section 6), so upstream changes to the form show up in the drift check.
- View models (`CommitListViewModel`, `CommitDetailsViewModel`, ...) in the core, with no UI types.
- Threading: one UI-thread abstraction, built on the existing `ThreadHelper` and `AsyncLoader`
  (both in the shared core). Replace the Avalonia dispatcher calls with it.
- **Platform services**, each with a Windows implementation and a POSIX one, behind interfaces in
  the core:
  - file and folder pickers (Avalonia storage provider);
  - open a folder in the file manager (Explorer, Finder, `xdg-open`);
  - open a terminal in a folder (per OS, configurable; section 9, question 10);
  - clipboard;
  - credential store (section 9, question 6);
  - notifications and message boxes, replacing the `MessageBox` stand-in's host hook.
- Each interface gets a unit test with a fake implementation, so the core does not depend on any OS.

Estimate: 3 to 4 weeks.

### M3. Read-only browsing parity

Exit: a user can browse history, see a diff for any commit, browse the file tree at a commit,
and view blame and file history.

Components, in suggested order:
1. Diff viewer. Editor control: AvaloniaEdit (not yet checked: current version and maintenance;
   the NuGet index lists 0.10.12). Syntax highlighting and line numbers. Patch parsing reuses
   the shared `Patches` code where it fits.
2. Changed-file list for a commit, with per-file diff.
3. File tree at a commit, and file history.
4. Blame.
5. Revision graph. This is the riskiest component: upstream `RevisionGrid` is about 13.5k lines,
   and the graph layout alone is about 2.7k. Plan a spike (M3.5) before committing to it.

Estimate: 6 to 8 weeks, plus the graph spike.

### M3.5. Revision graph spike

Exit: a graph of the last 5000 commits renders at 60 fps on the three OSes, with labels, and the
layout code is either reused from upstream (if it is not WinForms-bound) or reimplemented.

Estimate: 1 to 2 weeks.

### M4. Write operations

Exit: a user can clone a repository, stage and unstage files, commit (with amend), create, switch and
delete branches, fetch, pull and push, stash and apply stashes, and see conflicts. Clone is in
scope from 2026-10-03 (section 9, question 1).

Notes:
- Each command goes through the shared `Commands`/`IGitCommand` path, which already declares
  `AccessesRemote` and `ChangesRepoState`. The new shell reuses that declaration to decide
  whether to show a progress indicator and whether to refresh.
- Credential prompts: the Windows Credential Manager is used on Windows. Linux and macOS need the
  equivalent (libsecret / Keychain). Upstream `AdysTech.CredentialManager` is Windows-only; this
  needs its own decision and probably a seam (see section 9).
- Conflict resolution is a dedicated milestone if the upstream flow is complex (its
  `FormResolveConflicts` is large).

Estimate: 6 to 8 weeks.

### M5. Settings, themes and hotkeys

Exit: user settings are editable in the new shell; themes apply; hotkeys are configurable.

- Settings: the file store itself is M1 item 4. M5 adds the settings editor UI and migrates any
  values that upstream keeps in the Windows registry (through S1, read-only on Windows).
- External tools: diff and merge tools are configured per OS. Upstream's presets are Windows
  programs (WinMerge, Beyond Compare, P4Merge, ...); the new shell detects the ones that exist
  on the current OS (Meld and Kdiff3 on Linux, FileMerge and Beyond Compare on macOS) and lets
  the user add others. The `DiffMergeTools` code in the shared core stays as it is.
- Shell integration: the Windows terminal and shell launch (`CmdShell`, `PowerShellShell`,
  `BashShell`, ConEmu) has POSIX equivalents (`$SHELL`, the configured terminal). Only the launch
  is ported; ConEmu and Mintty embedding stay Windows-only.
- Themes: map `AppColor` to Avalonia resources. The upstream theme files are CSS-like and are
  loaded by `ThemeLoader`, so the same files can be read.
- Hotkeys: `HotkeyCommand` model reused; key names differ per OS (Cmd on macOS).

Estimate: 3 to 4 weeks.

### M6. Plugins and scripts

Exit: scripts (user commands on lifecycle events) run on all OSes; plugins load on all OSes.

- Scripts engine: shared logic in `ScriptRunner`; the UI for editing scripts is new.
- Plugins: MEF-based hosting in `GitUIPluginInterfaces`. Non-UI plugins work once the core loads
  on each OS. UI plugins need a new contract; the upstream plugin API is versioned
  (`src/app/GitExtensions.Extensibility`), so changes there must be additive and noted.
- Upstream plugins that use WinForms (28 forms) are not ported in this milestone. They stay
  Windows-only until someone needs them on other OSes.

Estimate: 3 to 4 weeks.

### M7. Packaging and distribution

Exit: installable builds for Windows (existing installer), Linux (AppImage, and a `.deb` or
`.rpm`), and macOS (`.app` in a `.dmg`), with signing as far as the platform needs.

- Windows: upstream WiX installer keeps working for the WinForms app. The Avalonia app ships as a
  separate artifact at first.
- Linux: AppImage first, since it needs no distro packaging.
- macOS: code signing and notarization need an Apple developer account. Decide whether the project
  has one (section 9).
- Shell extension and ConEmu terminal are Windows-only and stay out of the Linux/macOS builds.

Estimate: 2 to 3 weeks per platform, after the app works.

### M8. Feature parity and retirement (optional)

Exit: the Avalonia app covers the WinForms app's features, or the decision is made to keep both.

Not planned in detail. Full parity is the largest unknown and depends on section 9, question 1.

## 8. Testing strategy

| Layer | Tool | Runs on | Notes |
|---|---|---|---|
| Shared core unit tests | NUnit, the shadow `GitCommands.Tests` | Windows, Linux | Upstream tests, plus `XplatLinux*.cs` counterparts. 144 ignored on Linux, each with a reason |
| Upstream merge check | `tests/xplat/verify-upstream.sh` | Linux (CI nightly) | Builds and tests shadow projects against `upstream/master` in a temporary worktree |
| App unit tests (view models) | NUnit | Windows, Linux, macOS | Start in M2 |
| App UI tests | Avalonia.Headless | Windows, Linux, macOS | Start in M1 |
| App visual check | Screenshot via `XPLAT_SCREENSHOT` | Windows, Linux, macOS (CI) | Manual review, and an artifact in CI from M1. The macOS screenshot comes from a CI runner until a Mac is available |
| Git behavior | Real temporary repositories (`ReferenceRepository`) | Windows, Linux, macOS | As upstream does. macOS needs the Command Line Tools on the runner |
| Drift checks | `port-drift.sh`, `xplat-drift.sh`, `verify-upstream.sh` | Linux (nightly) | Upstream changes to ported files and to mirrored tests are reported, not discovered by accident |

Principle: no test is excluded silently. A Windows-only test is an `XplatPlatformSkips.cs` entry
with a reason. A Linux-only expectation is an `XplatLinux*.cs` test.

## 9. Open decisions (need the user)

These change the plan. Each has a recommendation, but the choice is the user's.

1. **Scope.** *Decided 2026-10-03:* core UI workflows first: clone, commit, push, pull and branch
   work. The new UI should stay as close as possible to the WinForms app, so later it can match
   its look and behavior. Full parity (M8) stays optional. Clone is added to M4 (see below).
   *Consequence:* M6 (plugins) and M7 (packaging) move after M4, and M8 remains optional.
2. **First target OS.** *Decided 2026-10-03:* Windows, Linux and macOS together. Every milestone
   from M1 on has to build and run on all three; the platform services in M2 and the terminal
   and credentials choices are per OS from the start.
3. **Upstream strategy.** *Decided 2026-10-03:* shadow projects only. No seam PRs are sent
   upstream. S2, S3 and S4 stay as seams in `XplatPatch`.
   *Consequence:* the seam count stays at 7 and must not grow without a table entry.
4. **macOS hardware run.** *Decided 2026-10-03:* the user will test the macOS build on their own
   laptop later. Until then, CI-only evidence (question 11) is what the milestones use.
4. **Translations:** reuse the Transifex `.xlf` files, or start new?
   *Recommendation:* reuse. The strings already exist and translators already work on them.
   Needs a check of how `TranslationString` maps to the Avalonia side.
5. **Plugins off Windows:** which plugins must work, and is it acceptable that WinForms-based
   plugins stay Windows-only?
   *Recommendation:* non-UI plugins first (M6); UI plugins later, by demand.
6. **Credentials on Linux and macOS:** libsecret and Keychain, or plain git credential helpers?
   *Recommendation:* use git's own credential helpers, and add libsecret and Keychain later.
7. **Branch-name rule and commit-message line endings** (decided earlier as "keep upstream
   behavior"): confirm, or change to one fixed set on every OS.
8. **Apple developer account** for signing and notarization (M7).
9. **Who maintains the xplat track** after the first milestones, and how often it syncs with upstream.
10. **Terminal on Linux and macOS:** which terminal to launch when the user asks for one. Options:
    `$TERMINAL` or `$SHELL` with a configurable command, or a fixed list per OS.
    *Recommendation:* configurable command, with a default detected from the OS.
11. **macOS evidence until a Mac exists:** accept CI-only evidence (build, headless tests, screenshot
    artifact) as the macOS exit for M1 and M2, with a real run before any macOS release?
    *Recommendation:* yes. A macOS release must not ship without a run on hardware.

## 10. Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Upstream GitUI churn breaks ported forms faster than we port them | High | `PORTING-MAP.md` and the weekly upstream script; port the high-churn forms last |
| Revision graph is harder to port than estimated | High | M3.5 spike before M3 is committed |
| Avalonia API changes between majors | Medium | Pin versions in `Directory.Packages.props`; upgrade deliberately |
| macOS untested until a Mac is available | Medium | Add a macOS CI job as soon as one is available; until then, say so in every status report |
| Seams grow beyond what `XplatPatch` can carry | Medium | Each seam is in section 5; more than about 10 means revisiting the approach |
| Upstream test changes make Linux counterparts stale | Medium | Verify script runs nightly; `XplatLinux*.cs` tests have the upstream test name in their comment |
| WinForms-only features (shell extension, ConEmu, WiX) confuse users on other OSes | Low | Document them as Windows-only; hide the menu items on other OSes |
| Credential storage differs per OS | Medium | Section 9, question 6 |
| Settings lost on Linux and macOS until M1 item 4 | High | Fix first in M1; until then, say in every status report that nothing persists off Windows |
| `git` not found or too old on a user's machine (especially macOS without Command Line Tools) | High | M1 item 5: detection, version check, clear message |
| Reimplemented form logic drifts from upstream without anyone noticing | High | `port-drift.sh` in the nightly job; `PORTING-MAP.md` rows marked `stale` block the milestone exit |
| `XplatLinux*` counterparts go stale when the upstream test changes | Medium | `xplat-drift.sh` in the nightly job flags them (section 4) |
| `WinFormsShim` grows with every new shared use of WinForms, and its stand-ins drift from real behavior | Medium | Measure the member count (11.2); each new member needs a note on what the real type does |
| Terminal, diff-tool and shell launch behave differently on each OS | Medium | Section 9, question 10; per-OS tests where the OS allows |

## 11. Long-term mergeability

Upstream changes will keep arriving for as long as the fork exists, and the fork may never be
merged back. Mergeability is therefore a property the project keeps, not a phase it passes.

### 11.1 Rules (also in `CLAUDE.md`)

- No upstream file is edited. The seams in section 5 are the only exceptions, and each is carried
  by `XplatPatch` or an additive file.
- No renames, moves, reformatting or cleanups of upstream code. Those are the changes that conflict
  with everything else.
- No new public types or members in upstream projects. `GitExtensions.Extensibility` is not changed.
- Upstream test files are not edited. Windows-only tests are skipped with a reason; Linux
  expectations are separate tests.
- All new code is in fork-owned folders, and fork-owned folders never import upstream code by
  copying it.
- Logic in upstream forms is reimplemented, never moved out. Moving it edits an upstream file, and
  the copy in the fork then diverges silently. Every reimplementation has a `PORTING-MAP.md` row.
- Upstream settings keys, registry names and file formats are read as they are. The fork does not
  rename or migrate them in the shared core.

### 11.2 Measures (checked on every sync)

| Measure | Target | How it is measured |
|---|---|---|
| Upstream files modified | 0 | `git diff --name-only <last-sync> HEAD -- src/app src/plugins tests/app setup eng` must be empty, except seam files listed in section 5 |
| Seam count | at most 10 | Rows in section 5 |
| Seam anchors intact after upstream merge | 100% | Build fails if an anchor is missing; `verify-upstream.sh` |
| Shadow build and tests on current upstream | pass | `tests/xplat/verify-upstream.sh` |
| Time to sync one upstream batch | under 1 day for a batch of 50 commits | Recorded in the sync log |
| Ported forms out of date | listed | `PORTING-MAP.md` rows whose upstream file changed since the recorded commit |
| Mirrored tests out of date | listed | `xplat-drift.sh` output: `XplatLinux*` and `XplatPlatformSkips` entries whose upstream test changed |
| `WinFormsShim` member count | not growing faster than the ported surface | Count of public members in `Shim.cs` and the `WinFormsShim` folder, recorded in `SYNC-LOG.md` |
| Upstream WinForms references in shared projects | 0 new per sync | `git grep` of `System.Windows.Forms` in the shared upstream folders, compared to the last sync |

### 11.3 Sync cadence and procedure

- **Cadence:** at least every 2 weeks while development is active; at least every 3 months after
  that. A sync that has not happened for 3 months is recorded as overdue in `docs/xplat/SYNC-LOG.md`.
- **Procedure:**
  1. Fetch `upstream/master`.
  2. Run `tests/xplat/verify-upstream.sh`. Read the failures before merging anything.
  3. If the shadow build or a seam fails, fix it in `src/xplat` on the fork branch first.
  4. Merge `upstream/master` into the fork branch (merge, not rebase, so history is kept). Branch
     model: `master` mirrors `upstream/master` and is moved by the user only (fast-forward);
     `xplat/main` is the fork's integration branch; feature work branches from `xplat/main`. The
     sync merges `master` into `xplat/main`. Automation does not create, move or commit branches.
  5. Run the Windows and Linux test suites.
  6. For each upstream commit that touched a ported form, update `PORTING-MAP.md`: port it, or
     record that it was deliberately skipped, with the reason.
  7. Append a line to `SYNC-LOG.md`: date, upstream commit, number of commits, seam status.
- **Catch-up:** if the fork has been out of sync for a long time, catch up in batches of about
  200 upstream commits, verifying after each batch, rather than one large merge.

### 11.4 When something breaks

- A seam anchor moved: the build fails and names the file. Update the seam in `src/xplat`; do not
  copy the upstream file. If the anchor is now gone (the code was rewritten), re-evaluate the seam
  against the new code and update section 5.
- A ported form changed upstream: the `PORTING-MAP.md` row shows it. Decide per change: port it,
  or skip it with a reason.
- An upstream type used by the shadow build was removed: the shadow build fails at compile time.
  Remove the reference from the shadow project or add a stand-in in `WinFormsShim`, and record it.
- The upstream `Extensibility` interface version changed: check whether the fork's plugins still
  load; do not change the fork's copy of the interface.

### 11.5 If the fork is never merged upstream

The fork then owns its own release line. Everything above still applies, and in addition:

- Keep the fork's own CI running (`.github/workflows/xplat.yml`), including the nightly upstream
  verification. Without it, the first sign of drift is a failed merge.
- Keep the sync log current; it is the record of how far behind the fork is.
- Name the maintainer and a fallback in this file (section 9, question 9). A fork with no one
  syncing it degrades within a few months of upstream change.

### 11.6 Milestone exit criteria (added to each milestone)

Each milestone's exit criteria include a sync: `verify-upstream.sh` passes on the current
`upstream/master`, the measures in 11.2 are reported, and `SYNC-LOG.md` has an entry for the
sync that was done during the milestone.

## 12. Immediate next steps

1. Answer the decisions in section 9, at least 1, 2, 3 and 11.
2. Commit the current work and move `master` to `upstream/master` (user actions).
3. M1 item 4: settings persistence off Windows. It blocks any real use, so it comes before the
   commit details.
4. M1 item 5: git discovery per OS, with a test for the missing and too-old cases.
5. M1 item 7: add `macos-latest` to CI, so macOS is checked from the next push.
6. M1 item 9: create `docs/xplat/PORTING-MAP.md`.
7. M1 item 1: commit details in the Avalonia app, with a headless test.
8. macOS: the user runs the build on their own laptop later. Until then, CI-only evidence (section 9,
   questions 4 and 11). Record the hardware run in `SYNC-LOG.md` when it happens.
