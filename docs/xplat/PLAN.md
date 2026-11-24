# Cross-platform Git Extensions: implementation plan

Status: cleaned up 2026-11-22. Completed milestones and plans were removed, and decided questions reduced to their outcome;
their full text is in git history (this file and `UI-PARITY.md` before 2026-11-22). Owner: KProskurdin. Working branch:
`feature/cross-os-ui`.

Goals this plan serves (both are hard requirements):
1. **A cross-platform application**, not only passing tests: Git Extensions runs as a usable app on
   Windows, Linux and macOS.
2. **Cheap upstream merges for the life of the fork**, including the case where the fork is never
   merged upstream (section 11).

## 1. Goal

Make Git Extensions run on Windows, Linux and macOS, while the fork keeps merging upstream
(`gitextensions/gitextensions`) without permanent conflicts.

## 2. Where we are

| Area | State | Evidence |
|---|---|---|
| Shared core (`GitCommands`, `GitExtUtils`, `ResourceManager`, `GitExtensions.Extensibility`, `GitUIPluginInterfaces`) as `net10.0` | Done | `src/xplat/*` shadow projects. Every non-UI upstream test project has a shadow in `GitExtensions.xplat.slnx` (section 8). Windows 2026-11-22: `GitCommands.Tests` 3500 of 3502 (1 skipped, 1 known Windows failure in `AsyncLoaderTests`). macOS 2026-11-20: 3823 pass, 201 ignored with reasons |
| Avalonia app | M1 to M6 done; UI parity with the WinForms app done | `src/xplat/GitExtensions.Xplat.App`. `PORTING-MAP.md`: every row `ported`, `windows-only` or skipped with a reason. Windows 2026-11-22: core 1527, app 885, all passing. Linux (WSL) 2026-11-22: core 1533, app 882 (one test fixed after the run), shadow projects 3823 pass, 0 fail. macOS 2026-11-20: core 1468, app 876, all passing |
| Upstream files | Clean but for the files in section 5 | The 13 upstream workflows are back to upstream's content; `.gitignore` (one line) and `CLAUDE.md` (fork file) are the changes at upstream paths |
| Seams | 9 | Section 5 |
| Fork workflow | Partly set up | `upstream` remote fetched; `verify-upstream.sh` passed against `17213fe3f` on macOS, 2026-11-20 (`SYNC-LOG.md`). Local `master` not moved (user decision) |
| CI | **Off on purpose** since 2026-11-08 | `.github/workflows/xplat.yml` is commented out until the user sets CI up by hand (question 16, decided 2026-11-24). Windows and macOS are checked by local runs, Linux by WSL on the Windows machine (question 12; last run 2026-11-22) |

The shared core is portable. The UI is where upstream changes most often, which is why the plan keeps the WinForms UI
untouched and builds a second UI beside it.

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
6. **MVVM for new and reworked UI** (decided 2026-11-13, the user). A new window, dialog, user
   control or panel keeps its state and commands in a view model in `GitExtensions.Xplat.Core`,
   without Avalonia types, and its view binds to it (compiled bindings, `x:DataType`, commands
   with `CanExecute`). An existing one that is reworked (new behavior, a layout change, or its
   logic rewritten; not a small fix) moves to MVVM in the same change. Code-behind keeps only view
   concerns. View models are tested with plain NUnit tests. They use CommunityToolkit.Mvvm
   (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`; section 9, question 17), and no
   other MVVM library. Button and menu actions are commands, and menus built in code are entries a
   view model lists (decided 2026-11-17). The pattern and its gotchas are `MVVM.md`.

## 4. Repository layout

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
    GitExtensions.Xplat.App/   Avalonia app
    GitExtensions.Xplat.Core/  UI-agnostic services and view models
    plugins/                upstream's plugins built for the new shell
tests/
  app/                      upstream, untouched
  xplat/
    CommonTestUtils/        shadow of upstream test helpers, plus TestAppSettingsAttribute replacement and
                            XplatSkipOnNonWindowsAttributeBase (the skip list base of every other shadow test project)
    GitCommands.Tests/      shadow of upstream tests, XplatPlatformSkips.cs, XplatLinux*.cs
    GitExtensions.Extensibility.Tests/, GitExtUtils.Tests/, ResourceManager.Tests/
                            shadows of the other non-UI upstream unit tests, each with its XplatPlatformSkips.cs
    plugins/                shadows of the non-UI plugin unit tests (DeleteUnusedBranches, GitUIPluginInterfaces,
                            ReleaseNotesGenerator, BuildServerIntegration/*). Not shadowed, because they test WinForms
                            forms: FindLargeFiles.Tests, BugReporter.Tests, GitUI.Tests and the integration tests
    GitExtensions.Xplat.Core.Tests/  view model and service tests
    GitExtensions.Xplat.App.Tests/   Avalonia headless tests
tests/xplat/
  XplatTestUserData.cs     moves the settings folder of every test assembly, so tests never touch the user's settings
  run-tests.sh             builds and tests every shadow test project (Windows via cmd.exe; Linux/macOS directly)
  run-with-watchdog.sh     CI: runs the app with a time limit and reports its processes and stacks if it hangs
  ci-report.py             CI: turns each suite's .trx into job annotations, readable without a sign-in
  make-bench-repo.sh       builds the 5500-commit repository of the revision graph benchmark
  verify-upstream.sh       runs the same against upstream/master in a temporary worktree
  port-drift.sh            lists upstream commits that touched ported files since the recorded commit
  xplat-drift.sh           flags mirrored upstream test classes (from each XplatLinux* file's "// Mirrors:" line and
                           the XplatPlatformSkips.cs entries) that changed or disappeared, in every shadow test project
docs/xplat/
  PLAN.md                  this file
  SYNC-LOG.md              one row per upstream check or merge, and the items due at the next merge
  PORTING-MAP.md           one row per ported upstream file
  MVVM.md                  the MVVM pattern of the app, its gotchas and the pieces to reuse
  MACOS.md                 setup, commands and the open checks for working on a Mac
  reference/windows/       proposed: pictures of the WinForms app, which cannot run on a Mac (section 9, question 14)
GitExtensions.xplat.slnx    solution for everything above
.github/workflows/xplat.yml CI: shadow tests on Windows, Linux and macOS, nightly upstream check (off, question 16)
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
| S8 | `ManagedExtensibility.CurrentDomain_AssemblyResolve` (additive, `PluginAssemblyResolve.cs`) | After `Initialise`, the private handler is unsubscribed by reflection and re-subscribed behind a guard that skips satellite (`*.resources`) requests and re-entry. Anchor-only `XplatPatch` items (Old equals New) in the shadow `GitUIPluginInterfaces.csproj` pin the handler's signature and its method-group subscription, so the build fails if upstream changes either; a missing handler also throws at runtime, and `PluginAssemblyResolveTests` pins the name | Off Windows `FileVersionInfo` parses every file as PE, and for any other file builds a `BadImageFormatException` (caught inside `FileVersionInfo`); under any UI culture that asks for a satellite (`en-NL` does, not only non-English ones) its localized message asks for one, raising the handler again until the stack overflows at startup | Yes, real bug on Linux and macOS |
| S9 | Azure DevOps plugin's `ApiClient.cs` (2 `XplatPatch` items in the shadow plugin project; logic in the additive `XplatAzureDevOpsAuth.cs`) | The password `git credential fill` returns goes as Bearer only when it is an OAuth access token (a JWT), otherwise as Basic, as git sends it; a response that is Azure DevOps's sign-in page (status 203, or HTML with a success status) throws `UnauthorizedAccessException`, so the adapter offers to open the settings | Upstream sends every stored credential as Bearer, which holds only for Git Credential Manager's OAuth token on Windows. Off Windows, git's helpers (`osxkeychain`, `libsecret`; question 6) return the personal access token the user typed, Azure DevOps answers as for an anonymous request with its sign-in page, and the build status failed with "'<' is an invalid start of a value" (seen on macOS, 2026-11-24) | Yes, probably: a PAT stored by GCM on Windows would meet the same Bearer (not checked) |

Rule: a new seam needs a line in this table, a reason, and a note on whether it is an upstream
candidate. Seams S2, S3 and S4 are bug fixes that would help upstream users on Linux too.

*Status 2026-11-20 (checked on macOS against `upstream/master` `c584b9740`, 2026-11-06):* the seams patch 4 upstream files
with 11 `XplatPatch` items (S1 4 and S2 1 in `AppSettings.cs`, S4 3 in `PathEqualityComparer.cs`, S3 1 in `DebugHelpers.cs`,
S8 2 anchor-only in `ManagedExtensibility.cs`), and every anchor is still found there. Of the 4 files only `AppSettings.cs`
changed upstream since the fork base `0174ba1cc` (159 lines, none of them an anchor), so S1 and S2 are the seams most likely to
need work after a merge. Patches never cause a git conflict; a moved anchor fails the build instead. *2026-11-24:* S9 adds
2 items in a fifth file, the Azure DevOps plugin's `ApiClient.cs`, unchanged upstream since the fork base up to
`afc512395` (not checked against `17213fe3f`), so 13 items in all.

How to have fewer, in order of effect:

1. Offer the bug fixes upstream: S4, S2, S3, S8 and S9, and the `Lazy` defect below. If they are taken, 9 of the 13 items
   and `GitOutputEncodingWarmUp` go, leaving S1.
2. S1 stays as it is unless upstream takes a settings store. Considered and not done: a fork-side `GitCommands.Registry` and
   `GitCommands.RegistryKey`, which C# would pick over `Microsoft.Win32`'s for upstream's `GitCommands` code and so remove the
   4 items. It would also capture `DiffMergeTools/VsDiffMerge.cs` (`Registry.LocalMachine`), and nothing in the code would
   show the substitution, while today a moved anchor fails the build and names the file.
3. S5 and S6 are additive files and cannot conflict; nothing to gain.

### Files outside the fork folders

Not seams, as the build does not depend on them, but each is a change at an upstream path and can conflict with an upstream
merge.

| File | Change | Merge risk | Recommendation |
|---|---|---|---|
| `.github/workflows/xplat.yml` | Fork-owned, added by the fork; commented out (`fc4d4ba5d`) | None | Stays off until the user sets CI up by hand; question 16 |
| `.gitignore` | One line appended, `/.claude` (`735c6ae4d`, 2026-10-17) | Low: only an upstream change at the file's end conflicts | Keep and accept, or move the line to each clone's `.git/info/exclude`. User's choice |
| `CLAUDE.md` | Added at the root by the fork | Add/add conflict if upstream ever adds one (upstream has none on 2026-10-09) | Keep; on such a conflict, merge the fork's rules into upstream's file |

The 13 upstream workflows, commented out by `fc4d4ba5d` (2026-11-08), are back to the fork base's content since 2026-11-20.
Workflows the fork does not want are turned off on GitHub (`gh workflow disable "<name>"`, or Settings, Actions), never by
editing their files: that state lives on GitHub and survives merges, and it is the only way that stops scheduled workflows,
which GitHub runs from `master`.

### Upstream defects worked around in fork code

Not seams either: upstream's file stays as it is and fork code avoids the defect. Each is an upstream candidate, and the
workaround goes when upstream fixes it.

| Defect | Effect | Workaround | Upstream candidate |
|---|---|---|---|
| `src/app/GitCommands/Git/ExecutableExtensions.cs`: `_defaultOutputEncoding` is `new Lazy<Encoding>(() => GitModule.SystemEncoding, false)`, not thread-safe | When a process's first two git commands run on two threads at once (the browse window reads the refs and the commits in parallel when it opens), the second throws "ValueFactory attempted to access the Value property of this instance": an error window, and the repository does not load | `Core/Platform/GitOutputEncodingWarmUp.cs`, called first in `App.OnFrameworkInitializationCompleted` (the app and the headless tests): it creates the value on the UI thread through `GetOutputAsync` with an executable that throws instead of starting, so no process runs. If upstream's method stops reading the encoding before starting the executable, the warm-up throws at start-up instead of leaving the race open again | Yes: make the lazy thread-safe (drop `false`); upstream's WinForms app hits the same race when two threads run the first git commands |

## 6. Mapping WinForms to the new shell

For every upstream form or control that gets ported, the port is recorded in
`docs/xplat/PORTING-MAP.md` with four columns: upstream file, new file, upstream commit the port was based on, and status
(`ported`, `partial`, `skipped: <reason>`, `windows-only`). This table is what makes upstream merges manageable.

`tests/xplat/port-drift.sh` reads the table and lists upstream commits that touched a mapped file since its recorded commit.
Each listed commit must end as a row update: ported, skipped with a reason, or the row is marked `stale`. It runs with the
nightly verify job, so drift is visible without anyone remembering to look.

To find upstream forms no row names (as FormOpenDirectory, EmptyRepoControl and the process dialog's password input were,
until 2026-11-22), compare the names of upstream's `*.Designer.cs` files with the map after each upstream merge; the ones
left are base classes and helpers, or ported under another name.

**Parity rules** (from the UI parity plan, completed 2026-11-22; they still hold for every port):

1. **Upstream's designer files are the specification.** Item order, texts, icons, grouping and default sizes come from the
   `*.Designer.cs` of the form being matched; where possible a test reads them (`MainMenuParityTests`,
   `DialogKeysParityTests`, the settings label tests), so an upstream change fails a test instead of drifting silently.
2. **Reuse upstream's assets, never copy them.** Icons from `Images.resx` and `Resources/Icons` (already linked), texts from
   upstream (and so its translations), colors from the theme files.
3. **Same layout on every OS.** Keep the Windows layout on Linux and macOS; adapt only what the OS dictates (Cmd keys, the
   macOS menu bar; `MACOS.md`).
4. **Behavior first where they conflict.** A feature the Avalonia app has that upstream shows elsewhere moves to where
   upstream shows it (usually a context menu), rather than being removed.
5. **Each step is checked against the original** and recorded in `PORTING-MAP.md`: by a screenshot of the running app
   (`XPLAT_SCREENSHOT`), and on a Mac, where the original cannot run, by the designer files, then reference pictures, then a
   Windows machine (`MACOS.md`, section 6). Say which of these a step was checked against.

## 7. Milestones

M0 (foundations), M1 (application shell), M2 (engine services), M3 (read-only browsing), M3.5 (revision graph), M4 (write
operations), M5 (settings, themes and hotkeys) and M6 (plugins and scripts) are done: their exits are met on Windows and
macOS by local runs, and on Linux by CI's only run (2026-11-08). The part of their exits that CI was to prove on every push
waits for CI, which the user sets up by hand later (question 16). Their history is in git and in `PORTING-MAP.md`.

### M7. Packaging and distribution

Exit: installable builds for Windows (existing installer), Linux (AppImage, and a `.deb` or
`.rpm`), and macOS (`.app` in a `.dmg`), with signing as far as the platform needs.

- Windows: upstream WiX installer keeps working for the WinForms app. The Avalonia app ships as a
  separate artifact at first.
- Linux: AppImage first, since it needs no distro packaging.
- macOS: code signing and notarization need an Apple developer account (question 8).
- Shell extension and ConEmu terminal are Windows-only and stay out of the Linux/macOS builds.

Done: `src/xplat/eng/publish-app.sh <rid>` publishes the app self-contained and packs it per OS: a zip for Windows; for
Linux a tarball with a `.desktop` entry and icon, an AppImage (`appimagetool`) and a `.deb` (`dpkg-deb`, the app in
`/usr/lib/gitextensions-xplat` and the command `gitextensions-xplat`), the last two only when run on Linux; and a
`Git Extensions.app` bundle (`.icns`, ad-hoc signed) as a zip and a `.dmg` for macOS. The app starts itself again through the
AppImage file, not its temporary mount (`AppExecutable`). What is left is section 12.3.

### M8. Feature parity and retirement (optional)

Exit: the Avalonia app covers the WinForms app's features, or the decision is made to keep both.

The parity part is done (section 2). Left: the decision whether the WinForms app is retired or both are kept; section 9
has no open question left to wait for.

## 8. Testing strategy

| Layer | Tool | Runs on | Notes |
|---|---|---|---|
| Shared core unit tests | NUnit, the shadow `GitCommands.Tests` | Windows, Linux (WSL), macOS | Upstream tests, plus `XplatLinux*.cs` counterparts, each ignored test with a reason |
| Other non-UI upstream unit tests | NUnit, 11 shadow test projects (Extensibility, GitExtUtils, ResourceManager, plugin and build server tests) | Windows, Linux, macOS | One upstream Gitlab test is skipped on every OS, as upstream |
| Upstream merge check | `tests/xplat/verify-upstream.sh` | By hand, before each merge | Builds and tests shadow projects against `upstream/master` in a temporary worktree. Not nightly: GitHub runs scheduled workflows from the default branch, which stays `master` (question 13) |
| App unit tests (view models) | NUnit, `GitExtensions.Xplat.Core.Tests` | Windows, Linux, macOS | |
| App UI tests | Avalonia.Headless, `GitExtensions.Xplat.App.Tests` | Windows, Linux, macOS | Tests that run the real `GitOutputRunner` belong here: its command log needs the threading context the app sets |
| App visual check | Screenshot via `XPLAT_SCREENSHOT` | Windows, Linux, macOS | Manual review, and an artifact in CI once it runs. The aid renders the content only, without title bar or native menu bar. Run it with `GITEXTENSIONS_XPLAT_USER_DATA` on a scratch folder, so the real settings stay untouched |
| macOS checks at the screen | The real app, by hand | macOS | `MACOS.md`, section 10 |
| Windows checks that need a person | Pageant, taskbar, Explorer and others | Windows | `MACOS.md`, section 7, and section 12.2 |
| Git behavior | Real temporary repositories | Windows, Linux, macOS | As upstream does. macOS needs the Command Line Tools |
| Drift checks | `port-drift.sh`, `xplat-drift.sh`, `verify-upstream.sh` | By hand at each sync (Linux or WSL for `xplat-drift.sh`, which needs bash 4) | Upstream changes to ported files and to mirrored tests are reported, not discovered by accident |

Principle: no test is excluded silently. A Windows-only test is an `XplatPlatformSkips.cs` entry
with a reason. A Linux-only expectation is an `XplatLinux*.cs` test, and a macOS-only one an `XplatMac*.cs` test.

## 9. Open decisions (need the user)

These change the plan. Each has a recommendation, but the choice is the user's. Decided questions keep their number and
are listed with their outcome at the end, as code and the other documents refer to them.

None is open.

Decided:

1. Scope: core workflows first, as close as possible to the WinForms app; full parity optional (2026-10-03). Since done.
2. First target OS: Windows, Linux and macOS together (2026-10-03).
3. Upstream strategy: shadow projects only, no seam pull requests upstream (2026-10-03).
4. macOS hardware runs: routine since development moved to a Mac (2026-11-08). Translations (the second question 4):
   upstream's `.xlf` files reused as they are (2026-10-09).
5. Plugins off Windows: non-UI plugins first, upstream's WinForms plugins built for the new shell, third-party WinForms
   plugins not loadable and listed with the reason (2026-10-09).
6. Credentials on Linux and macOS: git's own credential helpers (`osxkeychain`, `libsecret`), chosen in Settings > Git config
   (2026-10-09).
7. Branch-name rule and commit-message line endings: upstream's behavior on each OS, as the app has it (2026-11-22).
8. No Apple developer account for now (2026-11-22): macOS builds are ad-hoc signed, and a downloaded copy needs
   `xattr -dr com.apple.quarantine "Git Extensions.app"`, which is said wherever a build is offered. Revisit before the app
   is offered to other people.
9. Maintainer: KProskurdin, who merges upstream when needed rather than on a fixed cadence (2026-11-22; section 11.3).
10. Terminal on Linux and macOS: a terminal command setting, with the OS default when it is empty (Terminal on macOS,
    `$TERMINAL` or `x-terminal-emulator` on Linux) (2026-11-22).
11. macOS evidence: overtaken by real runs on a Mac (2026-11-08).
12. Where the work runs: the Mac and the Windows machine, and Linux through WSL on the Windows machine (2026-11-22).
13. Default branch: stays `master` (2026-11-22). So the nightly upstream check (a scheduled workflow) cannot run; syncs are
    checked by hand with `verify-upstream.sh`.
14. Reference pictures of the WinForms app: in the repository, `docs/xplat/reference/windows/<form>[-<page>].png`, taken on
    the Windows machine (2026-11-22; section 12.2).
15. macOS menu bar: the system menu bar, generated from the window's menu (2026-11-20, done: `SystemMenuBar`).
16. CI: off on purpose for now; the user sets it up by hand later (2026-11-24). Until then `xplat.yml` stays commented out,
    nobody (the user or an agent) turns it back on or "fixes" it, and every status report says which OS ran. Setting it up
    is the user's: enabling Actions on the fork, turning the unwanted upstream workflows off on GitHub
    (`gh workflow disable "<name>"` for each, or Settings, Actions), and turning `xplat.yml` back on. Notes for then: the
    nightly `schedule` cannot run while the default branch is `master` (question 13), so `workflow_dispatch` and push to
    `feature/**` are what is left; the open failures of its only run are in `MACOS.md`, MAC1.
17. The base of the view models: CommunityToolkit.Mvvm (2026-11-13); the core's own `ObservableObject` was replaced by it.

## 10. Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Upstream GitUI churn breaks ported forms faster than they are ported | High | `PORTING-MAP.md` and `port-drift.sh`; port changes at each sync |
| Avalonia API changes between majors | Medium | Pin versions in `Directory.Packages.props`; upgrade deliberately |
| Windows or Linux breaks unnoticed while CI is off | High | Accepted until the user sets CI up (question 16): local runs on Windows and Linux (WSL), and a status report that names the OS |
| The WinForms app, the parity reference, cannot run on a Mac | Medium | Designer files, then reference pictures, then a Windows machine (`MACOS.md`, section 6) |
| Seams grow beyond what `XplatPatch` can carry | Medium | Each seam is in section 5; more than about 10 means revisiting the approach |
| Upstream test changes make Linux counterparts stale | Medium | `xplat-drift.sh` in the nightly job; `XplatLinux*.cs` tests have the upstream test name in their comment |
| WinForms-only features (shell extension, ConEmu, WiX) confuse users on other OSes | Low | Documented as Windows-only; their menu items are hidden on other OSes |
| Reimplemented form logic drifts from upstream without anyone noticing | High | `port-drift.sh` in the nightly job; `PORTING-MAP.md` rows marked `stale` block the milestone exit |
| `WinFormsShim` grows with every new shared use of WinForms, and its stand-ins drift from real behavior | Medium | Measure the member count (11.2); each new member needs a note on what the real type does |
| Terminal, diff-tool and shell launch behave differently on each OS | Medium | Question 10; per-OS tests where the OS allows |
| A terminating exception on the UI thread itself is only traced: no thread is left to show it on | Low | Accepted; every other unhandled exception gets upstream's bug report |

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
| Upstream files modified | 0, apart from the files listed in section 5 | `git diff --name-only <fork-base> HEAD -- . ':!src/xplat' ':!tests/xplat' ':!docs/xplat' ':!GitExtensions.xplat.slnx' ':!.github/workflows/xplat.yml'` lists only the files in section 5's tables |
| Seam count | at most 10 | Rows in section 5 |
| Seam anchors intact after upstream merge | 100% | Build fails if an anchor is missing; `verify-upstream.sh` |
| Shadow build and tests on current upstream | pass | `tests/xplat/verify-upstream.sh` |
| Time to sync one upstream batch | under 1 day for a batch of 50 commits | Recorded in the sync log |
| Ported forms out of date | listed | `PORTING-MAP.md` rows whose upstream file changed since the recorded commit |
| Mirrored tests out of date | listed | `xplat-drift.sh` output: `XplatLinux*` and `XplatPlatformSkips` entries whose upstream test changed |
| `WinFormsShim` member count | not growing faster than the ported surface | Count of public members in `Shim.cs` and the `WinFormsShim` folder, recorded in `SYNC-LOG.md` |
| Upstream WinForms references in shared projects | 0 new per sync | `git grep` of `System.Windows.Forms` in the shared upstream folders, compared to the last sync |

### 11.3 Sync cadence and procedure

- **Cadence:** when the maintainer chooses (question 9). A sync that has not happened for 3 months is recorded as overdue in
  `docs/xplat/SYNC-LOG.md`, as the risk of a hard merge grows with the gap.
- **Procedure:**
  1. Fetch `upstream/master`.
  2. Run `tests/xplat/verify-upstream.sh`. Read the failures before merging anything.
  3. If the shadow build or a seam fails, fix it in `src/xplat` on the fork branch first.
  4. Merge `upstream/master` into the fork branch (merge, not rebase, so history is kept). Branch
     model: `master` mirrors `upstream/master` and is moved by the user only (fast-forward);
     `xplat/main` is the fork's integration branch; feature work branches from `xplat/main`. The
     sync merges `master` into `xplat/main`. Automation does not create, move or commit branches.
  5. If the merge added an upstream workflow, turn it off on GitHub (`gh workflow disable "<name>"`); a new workflow
     starts enabled, and the fork's workflow files stay as upstream has them (section 5).
  6. Run the test suites locally on the machine at hand, and confirm the other OSes in CI, or by hand (Linux in WSL).
  7. For each upstream commit that touched a ported form, update `PORTING-MAP.md`: port it, or
     record that it was deliberately skipped, with the reason. Compare upstream's designer file names with the map
     (section 6) for new forms.
  8. Append a line to `SYNC-LOG.md`: date, upstream commit, number of commits, seam status.
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

- Keep the fork's own CI running (`.github/workflows/xplat.yml`) once the user has set it up (question 16), and run
  `verify-upstream.sh` before each merge. Without them, the first sign of drift is a failed merge.
- Keep the sync log current; it is the record of how far behind the fork is.
- The maintainer is named in section 9, question 9; name a fallback there too. A fork with no one syncing it degrades
  within a few months of upstream change.

### 11.6 Milestone exit criteria (added to each milestone)

Each milestone's exit criteria include a sync: `verify-upstream.sh` passes on the current
`upstream/master`, the measures in 11.2 are reported, and `SYNC-LOG.md` has an entry for the
sync that was done during the milestone.

## 12. Remaining work

### 12.1 User actions

1. Decide whether to merge `upstream/master` (`verify-upstream.sh` passed against `17213fe3f` on 2026-11-20) and move
   `master`. Record the merge in `SYNC-LOG.md`.
2. Commit the work in the working tree.
3. Set CI up by hand, when wanted (question 16; `MACOS.md`, MAC1). It is off on purpose until then.

### 12.2 Checks not run yet

1. **CI** (question 16): off on purpose until the user sets it up. In its only run (`42521621315`, 2026-11-08) Windows'
   shadow tests failed and the screenshot job hung on every OS; the causes are not known (`MACOS.md`, MAC1).
2. **Linux at the screen:** the suites pass in WSL (2026-11-22), but the app itself has not been started on Linux since
   2026-10-11 (WSLg can show it).
3. **On a Mac, at the screen:** `MACOS.md`, section 10.
4. **Reference pictures** (question 14): the WinForms app's windows on the Windows machine, into
   `docs/xplat/reference/windows/`: at least the browse window with each tab, the commit window, every settings page, and
   the dialogs most used (clone, push, pull, the output window); a light version, and a dark one where colors matter.
5. **On Windows, at the screen:**
   - The Shell extension page with the Explorer menu: Enable and Disable (they ask for administrator rights and change the
     machine's registration), and the menu's items starting this app.
   - The Checklist's Repair buttons: git, the linux tools and PuTTY found as upstream finds them, the install folder.
   - The PuTTY windows against a real server and Pageant.
   - Narrator on the tool buttons and the lists' rows.
   - Both apps showing the same settings (the settings folder is shared since 2026-11-20).
   - The new windows of 2026-11-22: "Open local repository", the empty repository panel, and the output window's password
     box with a real question on git's input (plink's host key question, with PuTTY as the SSH client).

### 12.3 M7, packaging and distribution

1. Real signing and notarization, once an Apple developer account exists (question 8: not for now).
2. Trying a downloaded, quarantined macOS package (`MACOS.md`, MAC5).
3. The AppImage and the `.deb` on Linux (written 2026-11-22 on macOS; only their folder trees were checked there, with
   stand-ins for the two tools): run `publish-app.sh linux-x64 ~/xplat-publish` in WSL with `appimagetool` and `dpkg-deb`
   installed, then start the AppImage (WSLg), install the `.deb` with `apt install ./<file>.deb` on a Debian or Ubuntu, and
   try "New instance" and Settings, Git config's own editor from each. `lintian` on the `.deb` is optional. An `.rpm` is not
   made: openSUSE and Fedora users take the AppImage or the tarball.
4. An installer for the Avalonia app on Windows, with the Explorer shell extension's files (built by `src/native`, which
   needs VC++ and ATL); until then its Shell extension page works with an installed Git Extensions' files.

### 12.4 M8, retirement (optional)

The decision whether to retire the WinForms app or keep both.

### 12.5 Open decisions (section 9)

None. Question 16 (CI) was decided on 2026-11-24: off on purpose, set up by hand by the user later. The others were
decided by 2026-11-22.
