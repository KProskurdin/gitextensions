# Developing on macOS

Written 2026-11-08, on Windows, to move the cross-platform work to a Mac. It covers what has to happen before leaving
Windows, how to set up and work on the Mac, what is expected to break, and the order of the work (phases MAC0 to MAC5). The
other plans stay valid: `PLAN.md` for the milestones and `UI-PARITY.md` for the remaining features, which resume after MAC2.

## 1. What is known

**Nothing has run on a Mac yet**, and neither has any CI job, so every "works on macOS" claim so far is unproven. Items
marked *predicted* below come from reading the code. Delete or correct them as the real results come in.

| Check | Result | How |
|---|---|---|
| Compiles for Apple Silicon | Yes | `publish-app.sh osx-arm64` on Windows, 2026-11-08, 0 errors |
| Bundle layout | Right | `Contents/MacOS/GitExtensions` is an arm64 Mach-O file. `libAvaloniaNative`, `libSkiaSharp` and `libHarfBuzzSharp` are there, as are the Plugins (17 entries) and Themes folders. Not started |
| Packing the bundle on Windows | **Fails, and the script still prints the zip's path** | `Compress-Archive` threw and no zip was written. Files from NTFS also have no executable bit. Pack only on a Mac or CI |
| Repository on a case-insensitive disk | No conflict | `git ls-files`: no paths differ only by case, none are non-ASCII, no symlinks |
| Windows-only project types in `GitExtensions.xplat.slnx` | None | No `net10.0-windows` or `UseWindowsForms=true` under `src/xplat` or `tests/xplat` |
| Windows calls in the new code | Guarded by platform | Only `XplatRegistryKey.cs` and `VisualStudio.cs` use Windows APIs. Check them on the first Mac run |
| CI has ever run | **No, it cannot have from this branch** | `xplat.yml` triggers on `xplat/**`, the work is on `feature/cross-os-ui`, and `origin/master` lacks the file, so the schedule and manual runs are unavailable too (MAC1) |
| Linux app tests | **Stale** | Last attempted 2026-10-11, and that run was killed for lack of memory. The app has grown a lot since, so POSIX failures are unknown too |

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

### MAC2. First real results on the Mac (1 to 3 days, the least certain)

Run in this order, from fast to slow: core, shadow projects, then the app. Treat each failure as one of:

- **A product bug on macOS:** fix it in `src/xplat`.
- **An assumption in a fork test:** fix the test in `tests/xplat`.
- **An upstream test that does not fit macOS:** skip it with a reason and add a macOS counterpart. Upstream test files stay
  unchanged.

No group of tests is skipped without looking at each one.

**Test tooling needed first:**

- `XplatSkipOnNonWindowsAttributeBase` can only skip for "not Windows". Add a macOS-only skip list, so a test that still
  passes on Linux keeps running there.
- macOS-only expectations go in `XplatMac*.cs` with `[Platform(Include = "MacOsX")]` and a `// Mirrors:` line. `xplat-drift.sh`
  reads only `XplatLinux*.cs` today (its line 67), so add the new pattern too.

**Predicted failures:**

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

Exit: every test passes or has a recorded reason. The smoke list is done with its failures fixed or listed. Results are in
`SYNC-LOG.md`, and `PLAN.md` section 2 and section 12.2 are updated.

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

### MAC5. Packaging on a Mac (part of M7)

`PLAN.md`'s M7 keeps the signing decision (question 8). What a Mac makes possible, and what is wrong now in `publish-app.sh`:

1. Stop on a failed pack: on Windows it prints the zip's path though none was written.
2. An `.icns` from `setup/assets/Logo/git-extensions-logo-512px.png` or the `.svg` with `iconutil`, plus `CFBundleIconFile`.
3. Check `LSMinimumSystemVersion` (12.0 in the script) against what .NET 10 supports.
4. Check the executable's signature: Apple Silicon refuses to run unsigned code, and an ad-hoc signature is the minimum.
5. A `.dmg`. Without notarization a downloaded build needs `xattr -dr com.apple.quarantine "Git Extensions.app"`, so say so
   wherever it is offered.

Exit: a CI-built package, downloaded to the Mac and started, passes the smoke list.

## 9. After MAC2: which parity work to do where

See the section "Continuing on macOS" in `UI-PARITY.md`.
