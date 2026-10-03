# Cross-platform Git Extensions: implementation plan

Status: draft, 2026-10-03. Owner: KProskurdin. Working branch: `feature/cross-os-ui`.

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
| Shared core tests | Done with a gap | Windows 3482 pass (1 known failure, `AsyncLoaderTests`). Linux 3365 pass, 0 fail, 144 ignored with reasons |
| Upstream files | Untouched | All changes are additive or carried as seams (section 5) |
| Seams | 5 | `XplatPatch` items in `src/xplat/GitCommands/GitCommands.csproj`, plus `XplatFriends.cs` and `SupportedOSPlatforms.cs` |
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
2. **One engine, two shells.** The git engine and the domain logic stay shared. The WinForms
   shell and the Avalonia shell both call them. Logic that is currently inside WinForms forms is
   reimplemented in the new shell, and the reimplementation is traceable (section 6).
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
docs/xplat/PLAN.md          this file
GitExtensions.xplat.slnx    solution for everything above
```

## 5. Seams: the complete list

A seam is an upstream line that must change for the shared core to run cross-platform. Seams are
applied at build time by `XplatPatch` (the build fails if an upstream merge moves the anchor), or
by an additive file.

| # | Where | Change | Why | Upstream fix candidate |
|---|---|---|---|---|
| S1 | `AppSettings.cs` | Registry key replaced by `XplatRegistryKey` (real registry on Windows, in-memory elsewhere) | Settings are read from the registry in the static constructor | Yes, a cross-platform settings store would be an upstream discussion |
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
`docs/xplat/PORTING-MAP.md` (to be created in M2) with four columns: upstream file, new file,
upstream commit the port was based on, and status. This table is what makes upstream merges
manageable. A weekly script lists upstream commits that touched mapped files since the recorded
commit, so each one can be ported or consciously skipped.

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

Exit: the app opens a repository on Windows, Linux and macOS, lists commits, and shows the
selected commit's details.

Done: window, path box, commit list (read-only), developer screenshot aid.

Remaining:
1. Commit selection shows the message, author, date and parents (uses `RevisionReader.GetRevision`).
2. Virtualized commit list that does not load the whole history; paging through `GetLog` with a limit.
3. Headless UI tests (Avalonia.Headless) for the list and the selection.
4. Application icon, window title from the repository name, error dialog instead of status text.
5. CI job for the app on the three OSes, with screenshot artifacts.
6. macOS verification: build, run, and a screenshot from a Mac. This needs a Mac; see section 10.

Estimate: 2 to 3 weeks.

### M2. Engine services for UI (the boundary)

Exit: every operation the UI needs is available as a service with no WinForms dependency, and the
WinForms shell calls the same services where practical.

- Create `GitExtensions.Xplat.Core` with interfaces for: repository open, history, refs, status,
  stage/unstage, commit, branch, checkout, fetch/pull/push, stash.
- Each service delegates to `GitCommands`. Where the logic lives in a WinForms form today, the
  logic is moved to the core and the form calls it. That move is an upstream change, so it
  happens only where the shared logic is small and self-contained; otherwise the core
  reimplements it and the port is recorded in `PORTING-MAP.md`.
- View models (`CommitListViewModel`, `CommitDetailsViewModel`, ...) in the core, with no UI types.
- Threading: one UI-thread abstraction, built on the existing `ThreadHelper` and `AsyncLoader`
  (both in the shared core). Replace the Avalonia dispatcher calls with it.

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

Exit: a user can stage and unstage files, commit (with amend), create, switch and delete branches,
fetch, pull and push, stash and apply stashes, and see conflicts.

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

- Settings: keep the existing `GitExtensions.settings` format on Windows. On other OSes, use the
  XDG (Linux) or Application Support (macOS) directory. `XplatRegistryKey` (S1) gets a real store
  here.
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
| App visual check | Screenshot via `XPLAT_SCREENSHOT` | Windows, Linux | Manual review, and an artifact in CI from M1 |
| Git behavior | Real temporary repositories (`ReferenceRepository`) | Windows, Linux | As upstream does |

Principle: no test is excluded silently. A Windows-only test is an `XplatPlatformSkips.cs` entry
with a reason. A Linux-only expectation is an `XplatLinux*.cs` test.

## 9. Open decisions (need the user)

These change the plan. Each has a recommendation, but the choice is the user's.

1. **Scope:** full parity with the WinForms app (M8), or a lighter client covering the daily
   workflow (M1 to M5, with M6 and M7 as needed)?
   *Recommendation:* lighter client first. It produces a usable app sooner and tells us which
   WinForms features matter.
2. **First target OS:** Linux, macOS, or both at once?
   *Recommendation:* Linux first, because it is testable here; macOS once a Mac is available.
3. **Upstream strategy:** shadow projects only, or also send seam PRs upstream (S2, S3, S4 first)?
   *Recommendation:* send S2, S3 and S4 upstream. They are real bugs, and upstream acceptance
   shrinks the seam list.
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

### 11.2 Measures (checked on every sync)

| Measure | Target | How it is measured |
|---|---|---|
| Upstream files modified | 0 | `git diff --name-only <last-sync> HEAD -- src/app src/plugins tests/app setup eng` must be empty, except seam files listed in section 5 |
| Seam count | at most 10 | Rows in section 5 |
| Seam anchors intact after upstream merge | 100% | Build fails if an anchor is missing; `verify-upstream.sh` |
| Shadow build and tests on current upstream | pass | `tests/xplat/verify-upstream.sh` |
| Time to sync one upstream batch | under 1 day for a batch of 50 commits | Recorded in the sync log |
| Ported forms out of date | listed | `PORTING-MAP.md` rows whose upstream file changed since the recorded commit |

### 11.3 Sync cadence and procedure

- **Cadence:** at least every 2 weeks while development is active; at least every 3 months after
  that. A sync that has not happened for 3 months is recorded as overdue in `docs/xplat/SYNC-LOG.md`.
- **Procedure:**
  1. Fetch `upstream/master`.
  2. Run `tests/xplat/verify-upstream.sh`. Read the failures before merging anything.
  3. If the shadow build or a seam fails, fix it in `src/xplat` on the fork branch first.
  4. Merge `upstream/master` into the fork branch (merge, not rebase, so history is kept).
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

1. Answer the decisions in section 9, at least 1, 2 and 3.
2. Commit the current work (user action).
3. Start M1 item 1: commit details in the Avalonia app, with a headless test.
4. Create `docs/xplat/PORTING-MAP.md` with the rows for the forms already used by the app
   (the commit list maps to `FormBrowse`'s revision grid).
5. Get access to a Mac, or decide to defer macOS, so M1 item 6 has an owner.
