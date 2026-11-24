# Developing on macOS

Setup, commands and notes for working on the fork on a Mac, the macOS decisions in force, and the checks still open. Written
2026-11-08 to move the work to a Mac; cleaned up 2026-11-22: the completed phases were removed (their history is in git), and
section numbers are kept, as other documents refer to them.

## 1. What is known

The Mac (MacBook Air, Apple Silicon, Homebrew .NET SDK 10.0.x, Apple git) runs every suite: latest 2026-11-20, core 1468,
app 876 and shadow projects 3823 pass, 0 fail (201 ignored with reasons), and `verify-upstream.sh` passes. The packaged
bundle starts through `open`. **CI is off** on purpose until the user sets it up by hand (`PLAN.md` section 9, question 16), so Linux is checked
only in WSL.

| Check | Result |
|---|---|
| Packing the bundle on Windows | Not possible: files from NTFS have no executable bit, and `publish-app.sh` refuses to pack for macOS except on a Mac |
| Repository on a case-insensitive disk | No conflict: no paths differ only by case, none are non-ASCII, no symlinks |
| Windows-only project types in `GitExtensions.xplat.slnx` | None |
| Windows calls in the new code | Guarded by platform |

### Assumptions

Decided 2026-11-22 (`PLAN.md` section 9, question 12):

1. The Mac and Windows are both in use for development, so each result names its OS.
2. Windows stays reachable for the items in section 7.
3. Linux is checked in WSL on the Windows machine, and by CI once the user has set it up.
4. Either Mac CPU works. Use `osx-arm64` on Apple Silicon and `osx-x64` on Intel. `macos-latest` in CI is arm64.

## 2. Before leaving Windows

Done 2026-11-08 (phase MAC0). When moving to another machine again, carry over Claude's project memory
(`~/.claude/projects/<key>/memory/`, where `<key>` is the clone's full path with `/` replaced by `-`), the global rules
(`~/.claude/CLAUDE.md`) and the SSH key for GitHub; past chat sessions do not move.

## 3. Setting up the Mac

Clone fresh. **Do not copy a Windows working tree**: `core.autocrlf` is on there, so it holds CRLF and mixed files.

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
- **Which git:** Apple's git lags behind Homebrew's. Use Homebrew's for daily work, and try the app once with Apple's too,
  because that is what a user without Homebrew has.
- **Line endings:** do not set `core.autocrlf` on the Mac. `.gitattributes` keeps LF in the repository and LF is right in the
  working tree. `CLAUDE.md`'s CRLF rule describes the Windows working tree.

A working setup: `dotnet build GitExtensions.xplat.slnx -v:q -nologo` ends with 0 errors, and the app opens this repository
through `dotnet run --project src/xplat/GitExtensions.Xplat.App -- "$PWD"`.

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
  `/usr/bin/env bash`) or leave it to the nightly Linux job.
- **The screenshot aid renders the window's content only**: no title bar and no native menu bar. For those use
  `screencapture`, which needs the Screen Recording permission for the terminal.
- **Do not build during a test run.** The full app suite takes about 15 minutes. `TestIsolation.cs` closes the windows a
  test leaves open; a test must not count on its window outliving it.
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
- Say which platform a result comes from in every status report.
- **A real run of the app uses the user's settings**, and on macOS `HOME` does not move them. Start every trial run with
  `GITEXTENSIONS_XPLAT_USER_DATA=<scratch folder>` (through `open`: `open -n -W --env GITEXTENSIONS_XPLAT_USER_DATA=<folder>`).
  Tests set it themselves (`tests/xplat/XplatTestUserData.cs`); a test setup without it once deleted the user's settings file
  (2026-11-08), so after changing the test setup, hash the settings folder and `~/.gitconfig` before and after a run.
- `--no-build` after a failed build shows old results, also when the commands are joined by `;` instead of `&&`.

## 6. Comparing with the WinForms app from a Mac

`PLAN.md` section 6, parity rule 5, checks each step against the running original, which is impossible on a Mac. The order
of sources:

1. **The upstream `*.Designer.cs` and form code**: items, order, texts, icons and logic. This was always the specification,
   and it is all readable on a Mac.
2. **Reference pictures** taken on Windows, for layout and spacing, in `docs/xplat/reference/windows/<form>[-<page>].png`
   (decided 2026-11-22, `PLAN.md` section 9, question 14; to take, `PLAN.md` section 12.2). The folder is fork-owned, so it
   cannot conflict with an upstream merge.
3. **A Windows machine or VM**, when the first two leave a question open. Record it with the step.

A difference that macOS itself causes is not a parity bug: fonts, the title bar, the menu bar's place, the Cmd key.

## 7. What a Mac cannot verify

Keep these in a list and check them in one go on Windows (`PLAN.md` section 12.2), instead of guessing. Do not start new work
on them from the Mac.

| Area | Items |
|---|---|
| PuTTY | A run against a real PuTTY server and Pageant (the prompts and the retry are in the code, with plink and Pageant faked in the tests), the Tools > PuTTY items |
| Windows integration | The registry side of seam S1, Credential Manager settings, the Visual Studio integration, the Explorer shell extension and its settings page |
| Not ported, Windows only | ConEmu console tabs, the WiX installer |
| Window behavior | Taskbar icons, focus, placement, `cmd.exe` consoles for "Interactive add" |

## 8. Phases

MAC0 (handoff and setup), MAC2 (first results on the Mac) and MAC5 (packaging) are done but for their checks at the screen,
which are section 10. MAC3 and MAC4 left decisions that are still in force, below. MAC1 waits for the user.

### MAC1. Make CI really run

On hold: CI is off on purpose, and the user sets it up by hand later (`PLAN.md` section 9, question 16, decided
2026-11-24). Until then nothing here is started, and `.github/workflows/xplat.yml` stays commented out. The notes below are
for that setup. Once on, CI is the only guard for Linux, and for Windows when no Windows machine is at hand; all changes are
in the fork-owned `xplat.yml`.

1. **User action:** turn the workflow back on and enable Actions on the fork. The nightly `upstream-merge-check` needs the
   workflow on the default branch, but `master` is a mirror of upstream and does not have it (question 13).
2. Read the results and fix or list each failure. Each suite's `.trx` is turned into job annotations
   (`tests/xplat/ci-report.py`), readable without a sign-in: GET a job's `check_run_url` + `/annotations`.

Exit: all three OSes have completed a run, and their failures are understood.

Open from the only run (`42521621315`, 2026-11-08, on `5ea555567`): `shadow-tests (windows-latest)` failed its shadow step
(not understood: all 12 projects pass on a CRLF clone on the Mac), and `app-screenshot` hung in its launch step on all three
OSes for over 40 minutes; `tests/xplat/run-with-watchdog.sh` now reports the processes and the app's stacks when the app
outlives its limit, and the app's build is a step of its own.

### MAC2. First real results on the Mac

Done 2026-11-08: every suite passes on the Mac. What the run taught, for the next new machine:

- The temporary folder is a link (`/var/folders` is really `/private/var/folders`), and git prints the real path: compare
  paths after resolving them (`RealPath` in the core, `TestTemporaryFolder` in the tests).
- Tests press `Hotkeys.CommandModifier` (Cmd on macOS), not Ctrl; `TestKeys.Command` for raw input. Avalonia's headless
  platform keeps Ctrl for its own list keys, so Cmd+A in a real list is a check at the screen.
- Upstream tests that name Windows programs (`ping.exe`, `cmd.exe`, `notepad.exe`) are skipped with a reason and have POSIX
  counterparts (`XplatLinuxExecutableTests`, `XplatLinuxDiffMergeToolConfigurationTests`).

### MAC3. Keys and pointer

Done 2026-11-08, but for trying the keys at the screen (section 10, row 11). The macOS defaults that differ from upstream's,
kept in `HotkeyTable`'s macOS table so the other OSes are unchanged and a stored hotkey still wins
(`Hotkeys.MacOSReservedKeys` and `MacOSKeysTests` check them on every OS):

| Decision | Why |
|---|---|
| Commit: Cmd+Return (upstream Ctrl+Space) | Cmd+Space is Spotlight's. The same key commits in the commit window it opens |
| Merge: Cmd+Option+M (upstream Ctrl+M) | Cmd+M is Minimize, and Cmd+Shift+M is the grid's "hide merge commits" |
| Left panel multi-select: Option+Space (upstream Ctrl+Space) | Spotlight again. The command means "toggle" on macOS |
| Cmd+W stays "close repository", Cmd+, stays settings | Close enough to the convention, and the convention itself |
| Commit message completion: Ctrl+Space stays, Option+Esc added | Ctrl+Space is the system's next input source when there are several; Option+Esc is macOS's own completion key |
| The viewer's Replace: Cmd+Option+F (upstream Ctrl+H) | Cmd+H is Hide. This is macOS's own key for find and replace |
| No Cmd+R for refresh | Cmd+R is the grid's "compare to base", so F5 (Fn+F5 on a laptop) and the toolbar button remain. Decide after trying it |

Gesture texts come from `{local:CommandGesture S}` and `{local:CommandShortcutText ...}`, never a literal `Ctrl+`. Ctrl+click is
a right-click on macOS, so the grid's and the Start menu's "with the key held" clicks use Cmd.

### MAC4. Fitting into macOS

Done 2026-11-08 and 2026-11-20, but for the checks at the screen (section 10, rows 9, 10 and 12 to 16, 18). In force:

- **`PATH`:** a program started from Finder gets a minimal `PATH`, so `LoginShellPath` reads the login shell's once at
  startup (3 second limit) for the processes the app starts.
- **Menus:** the application menu has the name, About and Settings (`App.MacOS.cs`); the browse window's menus are copied
  into the system menu bar (`SystemMenuBar`), and the window's own menu row hides.
- **Dock icon:** a bundle's comes from its `.icns`; under `dotnet run` the app sets `NSApplication.applicationIconImage`
  itself (`MacApplicationIcon`).
- **Opening and closing:** a folder sent with `open` or dropped on the Dock icon arrives as an activation event and opens;
  closing the last window quits the app, kept because a window here is a repository browser.
- **"New instance"** runs `open -n` in a bundle (`NewInstanceCommand`).
- **"Interactive add" and "Reset chunk"** run in Terminal through a generated `.command` script that leaves git's exit code
  in a file (`InteractivePatch.MacScript`).
- **Dark mode and the font** (decided 2026-11-22): upstream's theme setting stays as it is (its "follow the operating
  system" choice included), and Inter stays the font on every OS, as `PLAN.md`'s parity rule 3 keeps one layout everywhere.

### MAC5. Packaging on a Mac

Done 2026-11-08 in `publish-app.sh`: an `.icns` and `CFBundleIconFile`, macOS 12.0 as the minimum, an ad-hoc signature on
the whole bundle (`codesign --verify --deep --strict` passes on the copies from the zip and the dmg), and a `.dmg` with an
Applications link. Left: trying a really downloaded, quarantined copy, and notarization (`PLAN.md` section 12.3, question 8).
Without notarization a downloaded build needs `xattr -dr com.apple.quarantine "Git Extensions.app"`, so say so wherever it is
offered.

**The bundle's layout, for the signature.** `codesign` refuses a bundle with anything but native code in `Contents/MacOS`: it
takes the .NET assemblies for unsigned code and `Plugins/GitExtensions.Plugins.*` for bundles. So native libraries and the
executable stay there, everything else is in `Contents/Resources` with a link from `MacOS`, and each native library has a link
back from `Resources`, because the .NET host follows the link to the app's assembly and looks for the runtime beside the real
file. Both zip and dmg keep the links. A new top-level file in the publish folder is handled the same way, with no list to
keep.

## 9. Which parity work to do where

Parity is done (`PLAN.md` section 2). New work that needs Windows goes on section 7's list.

## 10. At the screen: the checklist

*Written 2026-11-20 on macOS* for a person at the Mac. Fill in the Result column (pass, fail with what was seen, or skipped
and why), then record the results in `SYNC-LOG.md`.

**Setup, once.** A scratch data folder so the user's settings stay untouched, and a scratch repository with the files the
checks need:

```sh
export DOTNET_ROOT=/opt/homebrew/Cellar/dotnet/10.0.401/libexec   # with Homebrew's dotnet only
DATA=$(mktemp -d)/data; REPO=$(mktemp -d)/smoke; mkdir -p "$REPO" && cd "$REPO" && git init -q
printf 'one\ntwo\n' > a.txt; cp ~/projects/gitextensions/setup/assets/Logo/git-extensions-logo-512px.png logo.png
head -c 2048 /dev/urandom > blob.bin
python3 -c 'print(",".join("{\"key%d\":[%d,\"value %d\",true,null]}" % (i, i, i) for i in range(150000)))' > minified.json
git add -A && git commit -qm first && printf 'one\nthree\n' > a.txt
cd ~/projects/gitextensions
dotnet build GitExtensions.xplat.slnx -v:q -nologo && bash src/xplat/eng/publish-app.sh osx-arm64
```

Start from `dotnet run` with `GITEXTENSIONS_XPLAT_USER_DATA="$DATA" dotnet run --project src/xplat/GitExtensions.Xplat.App --
"$REPO"`, and from the bundle with `open -n -W --env GITEXTENSIONS_XPLAT_USER_DATA="$DATA" <bundle> --args "$REPO"`, where the
bundle is the `Git Extensions.app` in the zip whose path `publish-app.sh` printed, unzipped (so its path has a space).

| # | Check (source) | How | Result |
|---|---|---|---|
| 1 | Diff, image, binary file (MAC2) | Commit tab: `a.txt` shows git's colors; Diff tab of `first`: `logo.png` shows as a picture, `blob.bin` as "Binary file" with a hex dump | |
| 2 | A very long line (viewer fix of 2026-11-20) | File tree tab, `minified.json` (about 7 MB, one line): "Show large file", then the text shows within a few seconds in one color; scroll right; select `key1` and see marks | |
| 3 | Restart keeps state (MAC2) | Move and resize the window, change one setting, quit, start again from the bundle: the recent list, the window's place and the setting are back, in `$DATA` | |
| 4 | Commit, amend, interactive rebase from the app's menus (MAC2) | Commit `a.txt`, amend it, then on `first` the grid's "Rebase > Selected commit interactively...": the editor windows open and the result is right | |
| 5 | SSH with a passphrase, HTTPS with `osxkeychain` (MAC2) | Push and pull with a remote of each kind: the prompt window asks, a typed passphrase works, the keychain answers HTTPS | |
| 6 | Terminal, Finder, difftool, user script (MAC2) | Open Terminal; "Show in folder" on a file; a difftool (FileMerge) on `a.txt`; a user script that runs `git status` | |
| 7 | Refresh after an outside commit (MAC2) | Commit `a.txt` from Terminal: the grid shows the commit without a manual refresh | |
| 8 | Themes, plugins, language (MAC2) | Settings, Colors: light, dark and a dark upstream theme on the Retina screen; the Plugins menu lists the plugins; a non-English language after a restart | |
| 9 | Tools through `PATH` from Finder (MAC4) | Start the bundle from Finder: a difftool or `git lfs` found only through the login shell's `PATH` runs | |
| 10 | Paths with a space (MAC2) | From the bundle: "New instance" opens a second app with its own Dock icon | |
| 11 | Real keys (MAC3) | Cmd+A in the commit list; Ctrl+click opens the grid's menu; Cmd+Return opens the commit window; Cmd+Option+M merge; Option+Space in the left panel; Option as Alt in a text box types the character; F5 (Fn+F5) refreshes; trackpad scrolling in the grid and diff; menu mnemonics show no stray underscores | |
| 12 | Application menu (MAC4) | The app menu has the name, About, Settings (Cmd+,), Hide and Quit, and each works | |
| 13 | Dock icon (MAC4) | Both `dotnet run` and the bundle show the Git Extensions logo in the Dock, not the generic icon | |
| 14 | Interactive add (MAC4) | On `a.txt`, "Interactive add": Terminal opens with `git add -p`, and the app refreshes after it is closed | |
| 15 | Dark mode (MAC4) | Choose "follow the operating system" in Settings, Colors, then switch macOS to dark: the app follows, and the title bar matches the theme (upstream's theme setting is kept, decided 2026-11-22) | |
| 16 | Font (MAC4) | Inter in the grid, lists and dialogs reads well on the Retina screen (Inter is kept, decided 2026-11-22) | |
| 17 | VoiceOver | Cmd+F5, then move over the toolbar: each tool button says its name; in the commit list and file lists each row reads its text | |
| 18 | System menu bar (MAC4) | With a repository open the bar shows Start, Repository, Navigate, View, Commands, Plugins, Tools and Help (Dashboard instead of the repository's menus on the dashboard); Recent repositories lists them; View's checks match the window; Commands' items are greyed as the toolbar's; a click runs once (View > Show tags toggles once). Decide: show the keys beside the items, after checking that Cmd+Option+T then toggles once, not twice | |
| 19 | Command line verbs | From Terminal in the smoke repository, run the built app with `commit`, `push`, `pull --quiet`, `stash`, `blame a.txt`, `filehistory a.txt`, `searchfile`, `settings` and `help`: only the verb's window shows, centred, with no browse window and no extra Dock or Cmd+Tab entry beyond the app's; closing it ends the app (`echo $?`: 0, or 255 for upstream's -1 after a cancel); `browse . -commit=<hash>` opens the browse window on that commit | |
| 20 | New windows of 2026-11-22 | Start > Open...: "Open local repository" lists the folders, Go up and Browse work; a repository with no commits shows "This repository does not yet contain any commits." | |
| 21 | Terminal command (question 10) | Settings, Paths: an empty "Terminal command" opens Terminal; `open -a iTerm {folder}` (with iTerm installed) opens iTerm in the repository's folder | |
