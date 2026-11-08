# Developing on macOS

Written 2026-11-08, on Windows, to move the cross-platform work to a Mac. It covers what has to happen before leaving
Windows, how to set up and work on the Mac, what is expected to break, and the order of the work (phases MAC0 to MAC5). The
other plans stay valid: `PLAN.md` for the milestones and `UI-PARITY.md` for the remaining features, which resume after MAC2.

## 1. What is known

*Updated 2026-11-08 from the first full run on a Mac (MacBook Air, Apple Silicon, macOS 27.2, Apple git 2.54, Homebrew .NET
SDK 10.0.401).* Every suite passes there; section 8, MAC2, has the results and what is still open. **No CI job has run yet**,
so Windows and Linux have no result from this code. The rest of this section and the predictions below were written on
Windows; each is corrected where a real result exists.

| Check | Result | How |
|---|---|---|
| Runs on a Mac | Yes | Latest: core 956, app 752 and shadow projects 3821 pass, 0 fail, and the packaged bundle starts through `open` (MAC2 to MAC5). Earlier partial runs: a test run on 2026-10-16, and commits `da72971a1`, `735c6ae4d` |
| Compiles for Apple Silicon | Yes | `publish-app.sh osx-arm64` on Windows, 2026-11-08, 0 errors; on the Mac too, and there it packs a working zip |
| Bundle layout | Right | `Contents/MacOS/GitExtensions` is an arm64 Mach-O file. `libAvaloniaNative`, `libSkiaSharp` and `libHarfBuzzSharp` are there, as are the Plugins (17 entries) and Themes folders. Not started |
| Packing the bundle on Windows | **Fails, and the script still prints the zip's path** | `Compress-Archive` threw and no zip was written. Files from NTFS also have no executable bit. Pack only on a Mac or CI |
| Repository on a case-insensitive disk | No conflict | `git ls-files`: no paths differ only by case, none are non-ASCII, no symlinks |
| Windows-only project types in `GitExtensions.xplat.slnx` | None | No `net10.0-windows` or `UseWindowsForms=true` under `src/xplat` or `tests/xplat` |
| Windows calls in the new code | Guarded by platform | Only `XplatRegistryKey.cs` and `VisualStudio.cs` use Windows APIs. Check them on the first Mac run |
| CI has ever run | Once, with failures | Run `42521621315` on 2026-11-08: see the status under MAC1. The schedule and manual runs stay unavailable while `origin/master` lacks the file |
| Linux app tests | Pass | In that run, on Ubuntu. Before it, last attempted on 2026-10-11, and killed for lack of memory |

### Assumptions

Each of these changes the plan if it is wrong (`PLAN.md` section 9, question 12):

1. The Mac becomes the main development machine.
2. Windows stays reachable: CI for tests, and a Windows machine or VM now and then for the items in section 7.
3. Linux is checked by CI only, because the WSL setup stays behind on the Windows machine.
4. Either Mac CPU works. Use `osx-arm64` on Apple Silicon and `osx-x64` on Intel. `macos-latest` in CI is arm64.

## 2. Before leaving Windows (MAC0, part 1)

Some of this cannot be done later, or only with much more effort.

1. **Push everything.** At the time of writing `HEAD` equals `origin/feature/cross-os-ui` (`4e556130c`). These plan changes
   are uncommitted on top. A clone carries nothing else.
2. **Run a baseline on the commit you push**, so a failure on the Mac can be told apart from one that already exists. Latest
   numbers, none of them from one commit:

   | Suite | Result | Date |
   |---|---|---|
   | Core | 898 of 898 | 2026-11-08 |
   | App | 701 of 702. The failure was fixed and its class then passed 7 of 7; 2 tests were added later. Not re-run in full | 2026-11-08 |
   | Shadow projects | Windows 3482 pass with 1 known failure (`GitCommands.Tests`, `AsyncLoaderTests`), and 487 pass in the other projects | 2026-10-03, 2026-11-02 |

3. **Take the reference pictures of the WinForms app** (section 6). It does not run on a Mac. Take them by hand, or have
   each step approved: synthetic input once typed text into the user's real Git Extensions.
4. **Carry over what is only on this machine:**

   | What | From | To | Note |
   |---|---|---|---|
   | Claude's project memory | `%USERPROFILE%\.claude\projects\D--projects-gitextensions\memory\` | `~/.claude/projects/<key>/memory/` | `<key>` is the clone's full path with `/` replaced by `-`, such as `-Users-me-projects-gitextensions`. Confirm it against the folder Claude Code creates on the first run in the clone |
   | Global rules (never commit or push unasked) | `%USERPROFILE%\.claude\CLAUDE.md` | `~/.claude/CLAUDE.md` | Without it, only the repository's `CLAUDE.md` applies |
   | Status line | `~/.claude/settings.json` | Rewrite | It runs a `.ps1` file through `pwsh` |
   | Rider's quality hook | `.claude/settings.local.json` (not in git) | Rider writes it again | It reformats files on save (section 5) |
   | SSH key for GitHub | | | Needed for `origin` and for the submodules, whose URLs are relative to it |

   Past chat sessions do not move. Anything that matters must be in these documents or in memory.

## 3. Setting up the Mac (MAC0, part 2)

Clone fresh. **Do not copy the Windows working tree**: `core.autocrlf` is on there, so it holds CRLF and mixed files.

```sh
xcode-select --install                 # git, and later codesign and iconutil
brew install git                       # optional, see below
# The .NET 10 SDK: the Arm64 or x64 installer from dotnet.microsoft.com. global.json allows only 10.0.x, and Homebrew's
# dotnet-sdk cask may have moved on to a newer version.
dotnet --version                       # must print 10.0.x
# With Homebrew's dotnet instead (it works, and has been used): the built app host does not look in Homebrew's folder, so it
# stops with "You must install .NET". Run it with DOTNET_ROOT, here for SDK 10.0.401; a published bundle does not need it:
# export DOTNET_ROOT=/opt/homebrew/Cellar/dotnet/10.0.401/libexec

git clone --recurse-submodules git@github.com:KProskurdin/gitextensions.git
cd gitextensions
git switch feature/cross-os-ui
git submodule update --init --recursive
git remote add upstream https://github.com/gitextensions/gitextensions.git
git fetch upstream
```

- **Submodules are required.** The app builds `externals/Git.hub`, and the core embeds syntax definitions from
  `externals/ICSharpCode.TextEditor`.
- **Which git:** Apple's git lags behind Homebrew's. Use Homebrew's for daily work, and run the smoke test (MAC2) once with
  Apple's too, because that is what a user without Homebrew has.
- **Line endings:** do not set `core.autocrlf` on the Mac. `.gitattributes` keeps LF in the repository and LF is right in the
  working tree. `CLAUDE.md`'s CRLF rule describes the Windows working tree.

### Exit of MAC0

`dotnet build GitExtensions.xplat.slnx -v:q -nologo` ends with 0 errors, and the app opens this repository through
`dotnet run --project src/xplat/GitExtensions.Xplat.App -- "$PWD"`.

## 4. Daily commands

| Task | On macOS |
|---|---|
| Build everything | `dotnet build GitExtensions.xplat.slnx -v:q -nologo`. A plain `dotnet build` fails or picks the wrong solution: the repository root has two, and `GitExtensions.slnx` is WinForms |
| Build and test one class | `dotnet build tests/xplat/GitExtensions.Xplat.App.Tests -v:q -nologo && dotnet test tests/xplat/GitExtensions.Xplat.App.Tests --no-build --filter "FullyQualifiedName~SomeTests" -v:q -nologo` |
| Core tests | The same with `tests/xplat/GitExtensions.Xplat.Core.Tests` |
| Shadow tests | `bash tests/xplat/run-tests.sh` |
| Failures | The newest `artifacts/Debug/TestsResults/*.trx` |
| The built app | `artifacts/Debug/bin/xplat/GitExtensions.Xplat.App/net10.0/GitExtensions` (no `.exe`) |
| Screenshot | `XPLAT_SCREENSHOT=/tmp/shot.png XPLAT_SCREENSHOT_WINDOW=commit <built app> "$PWD"` |
| A real package | `bash src/xplat/eng/publish-app.sh osx-arm64` |

Things to know:

- **Keep the `&&`.** `--no-build` after a failed build tests the old DLLs and reports success. That once gave a false 118 of 118.
- **Call scripts through `bash`.** All six are mode `100644` in git, so `./tests/xplat/run-tests.sh` is refused. Running
  `chmod +x` and committing that is a small cleanup for the user, since it writes the index.
- **Two output folders.** `run-tests.sh` writes to `~/xplat-artifacts`, a choice made so Linux runs would not overwrite
  Windows output. Commands without `-p:ArtifactsDir` write to `artifacts/`. Each builds everything once.
- **macOS has bash 3.2.** `xplat-drift.sh` uses `declare -A` and fails there. Either `brew install bash` (the scripts use
  `/usr/bin/env bash`) or leave it to the nightly Linux job. No other script uses a feature checked for (`mapfile`, `sed -i`,
  `grep -P`, `readlink -f`, `date -d`, and the like).
- **The screenshot aid renders the window's content only**: no title bar and no native menu bar. For those use
  `screencapture`, which needs the Screen Recording permission for the terminal.
- **Do not build during a test run.** The full app suite took about 22 minutes on Windows, and one run there was killed for
  lack of memory, so close other heavy programs first.
- **Not on a Mac:** `GitExtensions.slnx`, `update-loc.cmd`, `src/native`, and `src/xplat/eng/Generate-KeysShim.ps1`. The
  fork does not need them: `Keys.g.cs` is checked in, and the fork does not change `English.xlf`.

## 5. Notes for Claude Code on the Mac

These live in the repository so they survive even if memory is not copied.

- Never commit, push, move branches, or run git commands that write the index (`git add`, `mv`, `rm`, `stash`). Use plain
  `mv` and `rm`. The rules are in `CLAUDE.md`.
- The Windows notes in memory do not apply: Git Bash path mangling of `/v:q`, the `cmd.exe` wrapper in `run-tests.sh`, and
  the `spec.pl` editing script in a temp folder, which was never in the repository. Use the Edit tool.
- Rider's hook can reformat unrelated code in the file just edited. Check `git diff` after editing an existing file and undo
  anything unrelated.
- Tests must never run git in the repository itself. `TestAppBuilder` moves the current directory to a temp folder and sets
  `GIT_CEILING_DIRECTORIES` for this, after a failed run left a proxy setting and a branch here. After any crashed run, check
  that `git config --get http.proxy` and `git branch --list 'pr/*'` print nothing.
- Say which platform a result comes from in every status report. From now on, "passes" means on macOS unless stated.
- **A real run of the app uses the user's settings**, and on macOS `HOME` does not move them. Start every trial run with
  `GITEXTENSIONS_XPLAT_USER_DATA=<scratch folder>` (through `open`: `open -n -W --env GITEXTENSIONS_XPLAT_USER_DATA=<folder>`).
  Tests set it themselves (`tests/xplat/XplatTestUserData.cs`). See MAC2 for what happened without it.
- `--no-build` after a failed build shows old results, also when the commands are joined by `;` instead of `&&`. It happened
  again here.

## 6. Comparing with the WinForms app from a Mac

`UI-PARITY.md`'s principle 5 checks each step against the running original, which is impossible on a Mac. The order of
sources from now on:

1. **The upstream `*.Designer.cs` and form code**: items, order, texts, icons and logic. This was always the specification,
   and it is all readable on a Mac.
2. **Reference pictures** taken on Windows (section 2), for layout and spacing.
   Proposed place: `docs/xplat/reference/windows/<form>[-<page>].png`. The folder is fork-owned, so it cannot conflict with an
   upstream merge (`PLAN.md` section 9, question 14). Take at least: the browse window with each tab, the commit window, every
   settings page, and the windows in the gap list (git-grep, theme editor, clone, push).
3. **A Windows machine or VM**, when the first two leave a question open. Record it in the step's Progress entry.

Every picture has a light theme version, and a dark one where the step concerns colors. A difference that macOS itself
causes is not a parity bug: fonts, the title bar, the menu bar's place, the Cmd key.

## 7. What a Mac cannot verify

Keep these in a list and check them in one go on Windows, instead of guessing. Do not start new work on them from the Mac.

| Area | Items |
|---|---|
| PuTTY | Pageant, plink, the clone prompts, the process dialog's retry, the Tools > PuTTY items |
| Windows integration | The registry side of seam S1, Credential Manager settings, the Visual Studio integration, the HOME check and FormFixHome, the checklist's installer rows, "Download Git" |
| Not ported, Windows only | ConEmu console tabs, the shell extension, the WiX installer |
| Window behavior | Taskbar icons, focus, placement, `cmd.exe` consoles for "Interactive add" |
| Earlier checks still open | Real Pageant call, and the Ctrl+Shift+H and Alt+arrow presses (these can also be checked on a Mac, with Cmd and Option) |

CI's Windows job protects everything the tests cover, including a shared code path changed for the Mac. This is why MAC1
comes first.

## 8. Phases

Sizes are rough and assume one person. MAC1 and MAC2 come before any more parity work: otherwise new failures mix with
macOS failures and neither can be read. MAC3 and MAC4 can then alternate with `UI-PARITY.md`'s list.

### MAC0. Handoff and setup (half a day)

Sections 2 and 3.

### MAC1. Make CI really run (half a day to a day)

CI is the only guard for Windows and Linux after the move. All changes are in the fork-owned `.github/workflows/xplat.yml`.

1. Add `feature/**` to `on.push.branches`, which makes the first run happen.
2. Add `submodules: recursive` to the checkout steps of `app-screenshot` and `app-packages`. They build the app, which needs
   `externals/`, and only `shadow-tests` has it now.
3. **User action:** enable Actions on the fork. The nightly `upstream-merge-check` needs the workflow on the default branch,
   but `master` is a mirror of upstream and does not have it (`PLAN.md` section 9, question 13).
4. Read the first results and fix or list each failure. Keep each OS's `.trx` as an artifact, because the logs are long.

Exit: all three OSes have completed a run, and their failures are understood. This also gives the first macOS and Linux app
test results, ideally before the first Mac run.

Status 2026-11-08: the first run happened (`42521621315`, on `5ea555567`). Packages built on all three OSes. Ubuntu's and macOS's shadow, core and app
tests passed, Ubuntu's being the first Linux app result since 2026-10-11. **Open:**

- `shadow-tests (windows-latest)` failed in its shadow and core steps; its app step passed. **Core, one cause found:** `InteractivePatch.MacScript`
  takes its line endings from the source file, and a Windows checkout gives it CRLF. Reproduced on the Mac with a clone made with
  `core.autocrlf=true`: two failures, none after the fix. There may be others on Windows. **Shadow, not understood:** all 12 projects pass on that
  clone, so it is not the line endings.
- `app-screenshot` hung in its launch step on **all three OSes** for over 40 minutes, so the exit of MAC1 is not met. This was the workflow's first run
  ever, so the step has never passed on CI. The same clone and `dotnet run` take 21 seconds on the Mac, with no terminal and stdin closed. Having no
  real display is a guess; nothing shows it yet.
- The nightly `upstream-merge-check` stays skipped on pushes, and needs the workflow on the default branch (item 3).

Job logs and artifacts need a sign-in, but a job's annotations do not, so the workflow now reports through them, for the next run:
`tests/xplat/ci-report.py` gives each suite's counts and the failed tests, and `tests/xplat/run-with-watchdog.sh` gives the processes and the app's
managed stacks when the app outlives its limit. The app's build is a step of its own, so the step times show which of the two a hang is in. To read them:
GET a job's `check_run_url` + `/annotations` (60 requests an hour without a sign-in).

### MAC2. First real results on the Mac (1 to 3 days, the least certain)

Run in this order, from fast to slow: core, shadow projects, then the app. Treat each failure as one of:

- **A product bug on macOS:** fix it in `src/xplat`.
- **An assumption in a fork test:** fix the test in `tests/xplat`.
- **An upstream test that does not fit macOS:** skip it with a reason and add a macOS counterpart. Upstream test files stay
  unchanged.

No group of tests is skipped without looking at each one.

**First complete results, 2026-11-08, on macOS, all from one tree (MAC3 to MAC5 added tests since: core 912, app 718):**

| Suite | Result |
|---|---|
| Core | 900 of 900 |
| App | 704 of 704, about 17 minutes. One more is not run: `Another_HOME...`, Windows only |
| Shadow projects | 3821 pass, 0 fail, 201 ignored with reasons, of 4022 |

Only Apple's git has been used. There were 25 failures and one crash at first. Nothing needed a macOS-only skip or an
`XplatMac*.cs` test, so the macOS skip list is not added; `xplat-drift.sh` already reads `XplatMac*.cs` for the day one exists.

| What | Tests | Kind | Fix |
|---|---|---|---|
| **The shadow tests deleted the user's settings** | `TestAppSettingsAttribute` | Bug in the test setup, **data loss** | See below |
| Temporary folder is a link, and the check for the open worktree compared text | `Worktrees_menu...`, and through it `CanDelete` | **Product bug**, also on Linux with a link: the open worktree could be deleted | `RealPath` in the core, used by `ManageWorktreeDialog.IsCurrent`; `RealPathTests`, `SymlinkedRepositoryTests` |
| Temporary folder is a link | `Stash_on_a_submodule...`, `Open_selected...`, `The_window_creates...` | Fork test | `TestTemporaryFolder` sets `TMPDIR` to the real folder. Only 3 of about 70 uses cared |
| Tests pressed Ctrl where the app uses Cmd | 12 tests | Fork test | `Hotkeys.CommandModifier`, and `TestKeys.Command` for raw input |
| Windows programs: `ping.exe`, `cmd.exe`, `notepad.exe` | 4 upstream tests, 5 cases | Upstream test. They name Windows programs, so plain Linux should fail them too; WSL, the only Linux so far, finds Windows programs on its PATH, which would explain why they passed there (not checked: no WSL here) | Skipped with a reason; POSIX counterparts in `XplatLinuxExecutableTests` and `XplatLinuxDiffMergeToolConfigurationTests` |
| `\` as a separator | `FileHistoryDialogTests`, `CleanupDialogTests` | Fork test | The native separator: `ToPosixPath` converts on Windows only |
| Host crash, a stack overflow, ended the run at 551 tests | `Upstreams_plugins_load...` | Fork test | It passed assemblies to `Initialise`, which looks up a resource before seam S8's guard exists. The app passes none, so it is not affected. **S8's cause is wider than its note said:** `en-NL` is enough, not only a non-English culture |
| Result reached the grid before the type was recorded | `The_grid_shows_the_build_status...` | Fork test, a race seen on a faster machine | It waits for the type too |

Predictions that did not happen: FSEvents timing, `core.ignorecase` and `precomposeunicode`, the global git config, Apple's
version text. They may still appear with another git or configuration. Avalonia's headless platform does keep Ctrl for its own
list keys, as predicted, so the headless tests cannot show what Cmd+A does in a real list: try it by hand (MAC3).

**Against upstream:** `verify-upstream.sh` passes on `c584b9740`, 55 commits past the fork base (3902 pass, 0 fail, 222 ignored).
Every seam still applies, through upstream's rework of `AppSettings`. It needed two things: the script now uses the real
temporary path (MSBuild found no references from the `/var` link), and upstream's 8 new `RecentRepoSplitterTests` methods with
Windows path inputs are in the skip list. `port-drift.sh` lists 12 of 132 rows with upstream changes to review at the next merge.
`xplat-drift.sh` was not run: it needs a newer bash (section 4).

**The settings loss.** Upstream's `TestAppSettingsAttribute` deletes the settings file because a test host has its own folder
on Windows. The shim's `UserAppDataPath` has one folder per user, so off Windows `run-tests.sh` deleted the real
`GitExtensions.settings` and wrote test values there. Both parts date from the shadow projects' first commit, so by the code it hit every Linux and
macOS run of these tests. It did hit the user's settings on this Mac on 2026-11-08, and possibly earlier (nothing older could be
recovered: no local snapshots, Time Machine's disk not mounted). The Windows file is a different one, by reading the code; not
checked. Now `GITEXTENSIONS_XPLAT_USER_DATA` moves the folder, every test assembly sets it when loaded, and the attribute
refuses to delete outside that folder. Checked by hashing the settings folder and `~/.gitconfig` before and after every suite:
unchanged. Do a run like this after changing the test setup. Tests still leave `xplat-*` litter in the temporary folder.

**The predictions, written on Windows (kept for what has not been seen):**

| Cause | Where | Likely fix |
|---|---|---|
| The temp folder is a symlink: `/var/folders/...` is really `/private/var/folders/...`, and git prints the real path | `TestRepository.cs:10`, and about 70 other uses of `Path.GetTempPath()` in the fork's tests. Any test comparing a path with git's output | Resolve the path once in the test helpers. If the app itself mishandles it, that is a product bug, as a user's repository can be behind a symlink too |
| Tests press Ctrl while the app expects Cmd on macOS (`Hotkeys.CommandModifier`) | `CommitInfoTests.cs:93`, `DiffViewFindTests.cs:28,96`, `DiffViewOptionsTests.cs:78-84`, `MainWindowTests.cs:318`, two `"Ctrl+` texts in `MainWindowTests.cs` | Use `Hotkeys.CommandModifier`, as other tests do |
| Avalonia's headless platform probably keeps Ctrl for its own list and text keys while the app uses Cmd | Tests of multi-selection and text box keys | Find out on the first run, then note it here |
| The global or system git config differs (`osxkeychain`, `pull.rebase`, signing, `init.defaultBranch`) | Only a few tests isolate it (`GitConfigServiceTests`) | Test with `GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1` to confirm. If that is the cause, isolate the test helpers |
| Apple's version text (`git version 2.x (Apple Git-nnn)`) or an older git | Version checks; features needing a newer git | Run the suites with both gits |
| New repositories get `core.ignorecase=true` and `core.precomposeunicode=true` | Tests with case-only renames or accented file names. Case-sensitive-only logic ported from a Linux check | Per test |
| File watcher timing (FSEvents batches events) | Tests waiting for a refresh | Wait on the result, not a delay. Root-cause fix only, no longer sleeps |
| Assumed Linux behavior in `XplatLinux*.cs`, which also run on macOS | 14 files, `[Platform(Include = "Linux,MacOsX")]` | Case by case. Move a Linux-only fact to Linux only |

**Smoke test of the real app.** Run it from `dotnet run`, then repeat items 1, 3, 4 and 8 from a bundle started through `open`:

1. Open a repository: grid, graph, details, diff with git colors, an image, a binary file.
2. Restart: recent repositories, window placement and settings survive, in `~/Library/Application Support`.
3. Commit, amend, and an interactive rebase. These cover the editor (`GitEditorCommand`) and the macOS sequence editor
   (`RewriteCommit.SequenceEditor`).
4. Push and pull over SSH with a passphrase (askpass; recorded as never checked since M4), and over HTTPS with `osxkeychain`.
5. Open Terminal, reveal a file in Finder, run a difftool (FileMerge), and a user script.
6. Commit from an outside terminal: the app refreshes.
7. Light and dark theme on a Retina screen; plugins load; a non-English language.
8. **From Finder or `open`:** git is found, and so are tools found through `PATH` (see MAC4).
9. **Paths with a space:** the bundle is at `.../Git Extensions.app/...`, unlike every earlier run. The editor, askpass and
   "new instance" commands all build on `Environment.ProcessPath`.

**Smoke results so far, 2026-11-08, all from the published bundle at a path with a space, on a scratch repository, with no
person at the screen:**

| Item | Result |
|---|---|
| 1 | Grid, graph and details: yes, screenshot. Diff with git colors, image, binary file: **not looked at** |
| 2 | Restart keeps the recent list, which the second start shows, in the data folder. Window placement and the other settings: **not checked** |
| 3 | `git rebase -i` and `git commit --amend` with the bundle as both editors: complete, the windows open. Editing them, and starting these from the app's own menus: **not done** |
| 4 | The prompt window opens when git runs the bundle as `SSH_ASKPASS`, with the path's space. A typed answer, a passphrase, real ssh and `osxkeychain`: **not done** |
| 5, 6, 7 | **Not done:** they need a person, except that plugins load |
| 8 | Through `open`, and with `PATH=/usr/bin:/bin:/usr/sbin:/sbin` and no `DOTNET_ROOT`: git is found. Tools through `PATH`: **not done** |
| 9 | Editor and askpass: yes. "New instance": **not done** |

The bundle's executable is ad-hoc signed by the SDK, so MAC5's item 4 has its minimum already, without a step in the script.

Exit: every test passes or has a recorded reason (**met**). The smoke list is done with its failures fixed or listed (**open:
the items above need a person at the screen**). Results are in `SYNC-LOG.md`, and `PLAN.md` section 2 and section 12.2 are
updated (**done**).

### MAC3. Keys and pointer (1 to 2 days)

`Hotkeys.CommandModifier` already maps upstream's Ctrl to Cmd for the hotkey tables. What remains:

1. **Literal Ctrl in code** that should follow the platform: `ConflictsWindow.axaml.cs:808`,
   `SparseWorkingCopyWindow.axaml.cs:51`, `VerifyDatabaseWindow.axaml.cs:295`.
2. **Ctrl+click is a right-click on macOS**, so these need Cmd: `MainWindow.GridMenu.cs:159`, `MainWindow.StartMenu.cs:138`.
3. **Literal `Ctrl+` gesture texts:** 10 in `CommandLogWindow.axaml`, `ConflictsWindow.axaml` and `VerifyDatabaseWindow.axaml`,
   which show the wrong key. The commit window's Ctrl+Space (`CommitWindow.AutoComplete.cs:154`) stays Ctrl, but macOS uses
   that key for switching input source by default, so check it.
4. **Default keys macOS takes for itself:**

   | Key on macOS | Command | `Hotkeys.cs` | Problem |
   |---|---|---|---|
   | Cmd+Space | `BrowseCommand.Commit`, `LeftPanelCommand.MultiSelect` | 342, 536 | Spotlight takes it, so **Commit's hotkey never arrives** |
   | Cmd+M | `BrowseCommand.Merge` | 357 | Minimize, by convention |
   | Cmd+W | `BrowseCommand.CloseRepository` | 341 | Close window, by convention. Closing the repository may be close enough |
   | Cmd+, | `BrowseCommand.Settings` | 350 | Already the macOS convention; keep it |

   Add a macOS table of different defaults for these, keeping upstream's setting format, so a stored user hotkey still wins.
   Add a test on every OS: no macOS default is on a list of reserved keys.
5. **To try on real keys:** Option as Alt (it types characters, and Avalonia must report the key, not the character), the
   function keys on a laptop (F5 needs Fn; consider Cmd+R as well), trackpad scrolling in the grid and diff, and menu mnemonics
   (`_File` underscores, which macOS does not use).

Exit: tests for items 1 to 4, and item 5 tried and recorded.

**Status 2026-11-08, on macOS: items 1 to 4 are done with tests; item 5 is open, it needs real keys.**

| Decision | Why |
|---|---|
| Commit: Cmd+Return (upstream Ctrl+Space) | Cmd+Space is Spotlight's. The same key commits in the commit window it opens |
| Merge: Cmd+Option+M (upstream Ctrl+M) | Cmd+M is Minimize, and Cmd+Shift+M is the grid's "hide merge commits" |
| Left panel multi-select: Option+Space (upstream Ctrl+Space) | Spotlight again. The command now means "toggle" on macOS, as its handler read Cmd from the keys held and Option+Space would have marked one node alone. A test caught this |
| Cmd+W stays "close repository", Cmd+, stays settings | Close enough to the convention, and the convention itself |
| Commit message completion: Ctrl+Space stays, Option+Esc added | Ctrl+Space is the system's next input source when there are several; Option+Esc is macOS's own completion key |
| The viewer's Replace: Cmd+Option+F (upstream Ctrl+H) | Cmd+H is Hide. This is macOS's own key for find and replace |
| No Cmd+R for refresh | Cmd+R is the grid's "compare to base", so F5 (Fn+F5 on a laptop) and the toolbar button remain. Decide after trying it |

`HotkeyTable` keeps upstream's defaults and a short macOS table, so the other OSes are unchanged and a stored hotkey still wins.
`Hotkeys.MacOSReservedKeys` and `MacOSKeysTests` check every table's macOS defaults on every OS. Gesture texts come from
`{local:CommandGesture S}` and `{local:CommandShortcutText ...}`, not literal `Ctrl+`. These three had no test pressing them, which is
why they passed before: Cmd+digit in the conflicts window, Cmd+Enter in the sparse window, Cmd+T in the lost objects list.

The Cmd right-click and Cmd-click code is tested in the grid. "New instance" with Cmd held has no test: its launcher starts a real
process.

**To try on real keys (item 5), and new from this phase:** Cmd+A in a real list (headless keeps Ctrl for it); a physical Ctrl+click
as a right-click; Cmd+Return, Cmd+Option+M and Option+Space; Option as Alt; function keys; trackpad scrolling in the grid and diff;
and whether menu mnemonics (`_File`) look right.

### MAC4. Fitting into macOS (about a week)

1. **`PATH` for a program started from Finder.** Such a process gets a minimal `PATH`. `XplatGitDiscovery` finds git itself in
   known locations, but whatever git and the app start (git-lfs, gpg, difftools, hooks, credential helpers) is looked up in
   `PATH`. Recommendation: at startup, read the user's login shell `PATH` once, with a timeout, and use it for child processes.
2. **Application menu.** `App.axaml` sets no `Name` and no `NativeMenu` exists, so the app menu will have Avalonia's defaults.
   Give it the name, plus About, Settings (Cmd+,) and Quit.
3. **The menu bar** (`PLAN.md` section 9, question 15). Recommendation: show the same menus in the system menu bar,
   generated from `MainWindow`'s `Menu` so there is one definition and tests still drive that definition. `UI-PARITY.md`'s
   principle 3 already allows it ("the macOS menu bar later").
4. **Dock icon.** Setting `Window.Icon` (2026-11-08) gives Windows and Linux their icons, but a bundle's Dock icon comes from
   its `Info.plist`, which has no `CFBundleIconFile`. See MAC5. Check what a run without a bundle shows.
5. **Opening and closing:** a folder dropped on the Dock icon or sent with `open` arrives as an activation event, not in
   `args`, and `Program.Main` reads only `args`. Decide what a Dock click with no windows does, and whether closing the last
   window quits (the app does now; the usual macOS app stays).
6. **"New instance"** runs `Environment.ProcessPath` directly (`MainWindow.StartMenu.cs:142`). Check that this works within a
   bundle, with a Dock icon of its own, or use `open -n`.
7. **"Interactive add" and "Reset chunk"** are hidden on macOS (`InteractivePatch.cs:31`), because Terminal cannot simply be
   given a command. Doable through a temporary `.command` file or `osascript`. Do it or record why not.
8. **Dark mode and accents:** whether following the system setting fits upstream's theme choice, and whether the title bar's
   color matches the theme.
9. **Native appearance:** the default font is Inter, not San Francisco. Keep it unless it looks wrong, and record the choice.

Exit: items decided, done or recorded in `PORTING-MAP.md` as a macOS note, tested where a test can reach them.

**Status 2026-11-08, on macOS. Checked below means by running the published bundle through `open`, with no person at the screen:**

| Item | State |
|---|---|
| 1. `PATH` | **Done, checked.** `LoginShellPath` reads it with a 3 second limit when no terminal started the app. A `core.fsmonitor` script in a scratch repository recorded what git saw: `/opt/homebrew/bin` and `~/.local/bin` from the user's fish, colons included |
| 2. Application menu | **Done, not seen.** `Name`, About and Settings (`App.MacOS.cs`) are tested as a menu, but nobody has looked at the real menu bar, or whether macOS adds Hide and Quit as expected |
| 3. System menu bar | **Waiting for `PLAN.md` section 9, question 15.** Nothing done |
| 4. Dock icon | **Done, not seen.** A bundle has an `.icns` and `CFBundleIconFile` (MAC5). Nobody has looked at it, or at a run without a bundle |
| 5. Opening a folder | **Done, checked.** `open -a` with a folder and a new data folder opens that repository, through the activation event. `CFBundleDocumentTypes` lists folders. A real drop on the Dock icon is not tried, and only the first of several items opens. **Closing the last window still quits**, so a Dock click with no windows cannot happen: kept, as a window here is a repository browser. Say if you want the usual macOS behavior |
| 6. "New instance" | **Done.** In a bundle it runs `open -n`, at all four places (`NewInstanceCommand`). `open -n` itself was run; the menu items were not clicked |
| 7. Interactive add, Reset chunk | **Done, half checked.** A generated `.command` script runs in Terminal and leaves git's exit code in a file, also when the window is closed. The script ran for real against a repository with a difficult file name, but **Terminal has not been opened by it** |
| 8, 9. Dark mode, font | **Not decided:** they need someone looking at it |

### MAC5. Packaging on a Mac (part of M7)

`PLAN.md`'s M7 keeps the signing decision (question 8). What a Mac makes possible, and what is wrong now in `publish-app.sh`:

1. Stop on a failed pack: on Windows it prints the zip's path though none was written.
2. An `.icns` from `setup/assets/Logo/git-extensions-logo-512px.png` or the `.svg` with `iconutil`, plus `CFBundleIconFile`.
3. Check `LSMinimumSystemVersion` (12.0 in the script) against what .NET 10 supports.
4. Check the executable's signature: Apple Silicon refuses to run unsigned code, and an ad-hoc signature is the minimum.
5. A `.dmg`. Without notarization a downloaded build needs `xattr -dr com.apple.quarantine "Git Extensions.app"`, so say so
   wherever it is offered.

Exit: a CI-built package, downloaded to the Mac and started, passes the smoke list.

**Status 2026-11-08, on macOS. Items 1 to 5 are done in `publish-app.sh`; the exit is open, as no CI has run:**

| Item | Result |
|---|---|
| 1. Failed pack | The script stops when the file was not written, and refuses to pack for macOS except on a Mac |
| 2. Icon | `.icns` built from the 512-pixel logo with `sips` and `iconutil`, largest 512 pixels. Not seen in the Dock |
| 3. Minimum version | Every binary declares macOS 12.0 or lower (`vtool -show-build`), the same as `Info.plist`. Microsoft's list of supported versions was not compared, and nothing older than 27.2 has been tried |
| 4. Signature | Ad-hoc on the whole bundle, with `org.gitextensions.xplat` and the `Info.plist` bound; `codesign --verify --deep --strict` passes in the script and on the copies from the zip and dmg |
| 5. `.dmg` | Built beside the zip, with an Applications link, and uploaded by CI |

**The bundle's layout changed for the signature.** `codesign` refuses a bundle with anything but native code in `Contents/MacOS`: it
takes the .NET assemblies for unsigned code and `Plugins/GitExtensions.Plugins.*` for bundles. So native libraries and the executable
stay there, everything else is in `Contents/Resources` with a link from `MacOS`, and each native library has a link back from
`Resources`, because the .NET host follows the link to the app's assembly and looks for the runtime beside the real file. Both zip and
dmg keep the links. If a new top-level file appears in the publish folder it is handled the same way, with no list to keep.

Checked from both a fresh zip and a mounted dmg, through `open`, on a scratch repository: the app starts and opens it. From the zip
also: plugins load through the link (Settings lists all eight), the bundle works as git's editor, a folder arrives by event, and
git sees the login shell's `PATH`. Starting the executable directly prints one `sandbox_extension_issue_file_to_process failed` line and
then works; it was seen only since the bundle has a real identifier, and not looked into.

**Not checked:** a really downloaded, quarantined copy (the `xattr` line above is from Apple's rules, not tried here), and a Gatekeeper
prompt. Notarization stays with question 8.

## 9. After MAC2: which parity work to do where

See the section "Continuing on macOS" in `UI-PARITY.md`.
