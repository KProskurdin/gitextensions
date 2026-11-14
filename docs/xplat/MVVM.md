# MVVM in the Avalonia app

Started 2026-11-13. The rule is `PLAN.md` principle 6 (new UI uses MVVM; reworked UI moves to it), the base is
CommunityToolkit.Mvvm (section 9, question 17), and the plan is section 12.6. This file is the pattern every window follows,
taken from the pilot (`CreateBranchWindow`), and the list of windows done.

## The pattern

| Layer | Where | What it holds |
|---|---|---|
| Rules | `GitExtensions.Xplat.Core/.../*Dialog.cs` | Upstream's form logic without a window (already there for most windows). Made observable: `partial class ... : ObservableObject`, state as `[ObservableProperty] public partial ... { get; set; }`, computed `Can...` properties with `[NotifyPropertyChangedFor]`, rules in `partial void On...Changed`. Its public API stays, so its tests stay. |
| View model | `GitExtensions.Xplat.Core/.../*ViewModel.cs`, derived from `Presentation/DialogViewModel` | What the window did: reading git (through `IGitModule`, so tests substitute it), the rules object (replaced when loading decides it), values for binding, `[RelayCommand]`s, and `LoadAsync` that the window calls when it opens. No Avalonia types. |
| Host | `Presentation/IDialogHost` (Core), `WindowDialogHost` (App) | What the view model asks of its window: `Translate`, `ShowMessageAsync`, `ConfirmAsync` (Yes, No), `AskAsync` (Yes, No, Cancel), `ShowError`, `Close`. One app implementation for every window; tests use `FakeDialogHost` (Core.Tests). |
| View | `GitExtensions.Xplat.App/*Window.axaml(.cs)` | `x:DataType` of the view model, bindings and commands in XAML, `Escape` as a `KeyBinding` to a `CancelCommand`, or `IsCancel="True"` on the cancel button when upstream's form has a `CancelButton` (`DialogKeysParityTests` checks that). Code-behind: create the view model with `new WindowDialogHost(this)`, set `DataContext`, and view concerns only: focus and caret, help buttons, a call such as `NormaliseName` on `LostFocus`. The constructor keeps its signature, so callers do not change. |
| Shared controls | e.g. `CommitPickerView`, `CommitSummaryView` | Bindable `StyledProperty`s (two-way where the user changes the value) next to their old methods and events, so windows not yet converted keep working. |

## Converting a window

1. Read the window and its `*Dialog`. List its state, the git it reads, its commands and what it asks of the user.
2. Make the `*Dialog` observable (table above), keeping its API.
3. Write the view model; move the window's logic into it unchanged in behavior. Each command body runs in `GuardAsync`.
4. Rewrite the XAML with `x:DataType` and bindings; keep every control name (tests and upstream's translations find
   controls by name). Rewrite the code-behind down to view concerns; keep the window's test hooks (`Dialog`, `...Loaded`)
   as forwards to the view model.
5. Tests: view model tests in Core.Tests (plain NUnit, `FakeDialogHost`, `FakeGitOperations`, `Substitute.For<IGitModule>()`);
   the window's headless tests stay and keep passing.
6. Update the window's `PORTING-MAP.md` row and the list below.

Edit existing `.cs` files with scripts, not the IDE edit hook, which reformats whole files (see the session notes).

## Gotchas found in the pilot

- **Translation.** The translation passes (`UpstreamTranslation.Apply` by upstream's field names, `ApplyByText` when a
  window loads) set values on the controls. That does not remove a binding (checked 2026-11-13: a set value stays only
  until the source raises its next change), but it does override a bound value until then. So a text a view model exposes
  is translated by the view model itself, through `Host.Translate(english)` or `Host.Translate(category, name, english)`,
  and the pass then finds nothing to replace. Fixed English texts stay in the XAML. A bound text whose control keeps an
  upstream field's name (so `Apply` does find it) is safe when the window sets `DataContext` after the pass: the binding
  then writes the view model's value over the pass's (`BuildServerCredentialsWindow`).
- **Errors.** An exception that escapes an asynchronous command ends the app. Command bodies run in `GuardAsync`, which
  shows the error as the windows did (`UiActions.Run`) and ignores cancellation.
- **Clicking in tests.** Every test raises click events through `UiClicks.Raise(control, event)` (changed in all test classes
  2026-11-13): it raises the event as before, but a button bound to a command gets a headless mouse click and a menu item bound
  to one runs it, since `RaiseEvent(new RoutedEventArgs(Button.ClickEvent))` alone runs no command and Avalonia 12 made
  `IClickableControl` internal. New tests do the same (`UiClicks.Click(control)`). Since 2026-11-14 a command-bound button
  outside its headless window or under a neighbour (the headless fonts are wider, so rows overflow) gets its command run
  instead of a click no mouse could make,
  and a menu item's command runs only when its own click handling did not (Avalonia's `MenuItem` runs it on `Click`,
  which ran commands twice before). A button that is hidden cannot be clicked. Pending dispatcher jobs run before the
  click: a command that just finished (a message box closed a moment ago) re-enables its button only then, and a
  disabled button is skipped by hit-testing.
- **Editable lists.** `IsEditable="True"` must come before a `Text` binding on a `ComboBox`: setting it afterwards resets the text from the (empty) selection, which the two-way binding writes back to the view model before the window shows (`PushWindow`).
- **Context menus.** A context menu gets its data context only when it opens, so its items' command bindings are empty
  before; the window sets the menu's `DataContext` to the view model (`ReflogWindow`).
- **Git in view model tests.** Core tests must not run git. A read the view model does with git (a reflog, a folder's
  files encoding) is a constructor parameter that defaults to the git read, and tests pass a fake.
- **Generated partial methods.** `On<Property>Changed(old, new)` is generated with non-nullable parameters for a non-nullable
  property, but the first assignment passes null for `old`: keep the null check, and match the generated signature (CS8826).
- **NSubstitute.** Create a substitute's return values before `Returns(...)`; configuring another substitute inside it
  fails with `CouldNotSetReturnDueToNoLastCallException`.
- **Test preferences.** `InMemoryAppPreferences` normalises branch names (upstream's default), which changes a name before
  validation sees it.
- **Asynchronous display.** A bound control that reads git after its value changes (the commit picker's count) shows it a
  moment later; tests wait for it instead of reading it at once.
- **Other windows.** A view model never creates or receives a window. A window it opens (the commit chooser) and a caller's
  callback that takes the owner window are delegates the window builds: `async current => (await new
  ChooseCommitWindow(...).ShowDialog<CommitRow?>(this))?.Hash`, `(arguments, commit) => revert(this, arguments, commit)`.
- **Enabled buttons.** A button bound to a command is enabled by the command's `CanExecute`, which Avalonia shows in
  `IsEffectivelyEnabled`: `IsEnabled` stays true. Tests check `IsEffectivelyEnabled`; the view model raises
  `CanExecuteChanged` with `[NotifyCanExecuteChangedFor]` on the state it depends on.
- **Radio groups.** Each radio button binds to the enum with `EnumEqualsConverter` and its value as `ConverterParameter`
  (`ResetCurrentBranchWindow`). Questions are `Host.ConfirmAsync` (Yes and No; `defaultIsNo` for upstream's questions that default to No) or `Host.AskAsync` (Yes, No and Cancel; null for Cancel; `defaultIsNo` as well).
- **Shared reads.** Logic two windows did alike moves to their rules class once (`CherryPickDialog.ReadCommit` for the
  cherry pick and revert windows), not into each view model.

## Windows

| Window | View model | State |
|---|---|---|
| `CreateBranchWindow` | `Operations/CreateBranchViewModel.cs` | Done 2026-11-13 (pilot): code-behind 209 to 67 lines, 10 view model tests run in milliseconds. |
| `CreateTagWindow` | `Operations/CreateTagViewModel.cs` | Done 2026-11-13: code-behind 153 to 45 lines, 6 view model tests. `CreateTagDialog` stays a static rules class; the view model holds the state. `IDialogHost` gained translation by upstream field name. |
| `CheckoutRevisionWindow` | `Operations/CheckoutRevisionViewModel.cs` | Done 2026-11-13: code-behind 103 to 47 lines, 5 view model tests. App tests now click through the shared `UiClicks` helper. |
| `RevertCommitWindow` | `Operations/RevertCommitViewModel.cs` | Done 2026-11-13: code-behind 122 to 50 lines, 4 view model tests. The caller's callback that takes the window is wrapped by the window (`(arguments, commit) => revert(this, arguments, commit)`), so the view model never sees it. `CherryPickParent` moved to Core. |
| `CherryPickWindow` | `Operations/CherryPickViewModel.cs` | Done 2026-11-13: code-behind 180 to 81 lines (the rest is the parent row template shared with the revert window), 4 view model tests. Opening another window (FormChooseCommit) is a callback the window supplies. Reading a commit with its merge parents is shared with the revert view model (`CherryPickDialog.ReadCommit`). |
| `ResetCurrentBranchWindow` | `Operations/ResetCurrentBranchViewModel.cs` | Done 2026-11-13: code-behind 156 to 89 lines (the rest is upstream's colors behind the reset types), 4 view model tests. Radio buttons bind to the reset type with `EnumEqualsConverter`; the hard reset's question is `IDialogHost.ConfirmAsync`. |
| `ResetAnotherBranchWindow` | `Operations/ResetAnotherBranchViewModel.cs` | Done 2026-11-13: code-behind 239 to 93 lines, 3 view model tests (the fast-forward check runs git, so the window's tests keep it). The validation is the view model's (`CanReset` is the command's `CanExecute`); the marks are class styles, the unlisted one with `:not(:focus-within)` as upstream marked it once the list lost focus. |
| `ArchiveWindow` | `Operations/ArchiveViewModel.cs` | Done 2026-11-13: code-behind 232 to 78 lines (the rest is the save picker), 5 view model tests. A platform service (the save picker) is a delegate like a window; the window's test hook `PickOutput` sets the view model's. |
| `ErrorWindow`, `HelpWindow` | `Presentation/MessageViewModel.cs`, `Presentation/TextViewModel.cs` | Done 2026-11-13: shared view models for a window that shows a text and closes. |
| `ChooseWindow` | `Presentation/ChooseViewModel.cs` | Done 2026-11-13: the double click that chooses stays a view event that runs the command. |
| `ExceptionDetailWindow` | `Diagnostics/ExceptionDetailViewModel.cs` | Done 2026-11-13: read-only values, bound `Mode=OneWay` (a text box binds two-way by default). |
| `ContributorsWindow` | `Platform/ContributorsViewModel.cs` | Done 2026-11-13: upstream's texts translated by the view model; the resource lists come from the window. |
| `DonateWindow` | `Platform/DonateViewModel.cs` | Done 2026-11-13. The window keeps its constants as aliases for the callers that name them. |
| `SelectMultipleBranchesWindow` | `Operations/SelectMultipleBranchesViewModel.cs` | Done 2026-11-13: the list holds `SelectableBranch` items rendered as check boxes by a typed template, no longer check box controls. |
| `GoToLineWindow` | `Presentation/GoToLineViewModel.cs` | Done 2026-11-13: the number box binds `decimal?` as it keeps it. |
| `PromptWindow` | `Presentation/PromptViewModel.cs` | Done 2026-11-13: `PromptResult` moved to Core. |
| `ChangeLogWindow` | `Platform/ChangeLogViewModel.cs` | Done 2026-11-13: the Avalonia asset is opened by a delegate the window passes. |
| `AddFilesWindow` | `Operations/AddFilesViewModel.cs` | Done 2026-11-13. |
| `AskPassWindow` | `Operations/AskPassViewModel.cs` | Done 2026-11-13: the hidden answer's character and the screenshot aid stay in the view. |
| `ResetChangesWindow` | `Operations/ResetChangesViewModel.cs` | Done 2026-11-13: the window's nested `ResetAction` became Core's `ResetChangesAction` (five callers changed). |
| `DeleteTagWindow` | `Operations/DeleteTagViewModel.cs` | Done 2026-11-13: the user manual's link (a browser) stays in the view. |
| `ConfirmWindow` | `Presentation/ConfirmViewModel.cs` | Done 2026-11-13: `AskAsync` (upstream's `ConfirmSuppressible`) stays on the window, which it opens. |
| `AvailableEncodingsWindow` | `Settings/AvailableEncodingsViewModel.cs` | Done 2026-11-14: the settings dictionary and its save are passed in, so the view model tests run without `AppSettings`. |
| `BuildServerCredentialsWindow` | `BuildServer/BuildServerCredentialsViewModel.cs` | Done 2026-11-14: the access radio buttons bind to the credentials type with `EnumEqualsConverter`. The header is a formatted upstream text, so the window sets its `DataContext` after the translation pass, which would otherwise leave the unformatted translation in place. |
| `RenameBranchWindow` | `Operations/RenameBranchViewModel.cs` | Done 2026-11-14: the caret kept across the normalisation on `LostFocus` stays in the view. |
| `BisectWindow` | `Operations/BisectViewModel.cs` | Done 2026-11-14: the three marks are one command with the step as its parameter; `BisectStep` moved to Core. |
| `DeleteBranchWindow` | `Operations/DeleteBranchViewModel.cs` | Done 2026-11-14: the caller's delete that takes the window and upstream's multiple branches window are delegates the window builds. |
| `AddSubmoduleWindow` | `Operations/AddSubmoduleViewModel.cs` | Done 2026-11-14: the folder picker is a delegate (`PickFolder`); the editable branch list clears its text when its items change, so the window puts the typed branch back after a read. |
| `ProcessWindow` | `Operations/ProcessViewModel.cs` | Done 2026-11-14: wraps `RepositoryOperationsViewModel`, which it follows until the window calls `Detach` on close. "Keep dialog open" is a hand-written property, so setting it from the preference does not save the preference. Scrolling to the newest line and the failed state's theme color stay in the view. |
| `AboutWindow` | `Platform/AboutViewModel.cs` | Done 2026-11-14: the view model holds the product name, the contributor thanked and the environment information; the timer, the screen the information is read for, the links (a browser) and the clipboard stay in the view. |
| `RepositoryTextFileWindow` | `Repository/RepositoryTextFileViewModel.cs` | Done 2026-11-14: the save question on close is `IDialogHost.AskAsync` (added for it). The window's `Closing` cancels while `MayClose` is false and starts `AskToCloseAsync`, which closes the window again once answered. |
| `SearchWindow` | `Repository/SearchViewModel.cs` | Done 2026-11-14: the text box's keys stay a tunnelling handler in the view (the box would handle Up, Down and Enter itself) that runs the commands. |
| `ChooseTranslationWindow` | `Settings/ChooseTranslationViewModel.cs` | Done 2026-11-14: `TranslationChoice` moved to Core with the flag's path, which the app's `BitmapFileConverter` shows; hover selection and one-click activation stay in the view. |
| `WorktreesWindow` | `Operations/WorktreesViewModel.cs` | Done 2026-11-14: the view model reaches `WorktreeFlow`, whose methods take the owner window, through Core's `IWorktreeActions`, which the window implements over the flow. |
| `FileBrowserWindow` | `CommitHistory/FileBrowserViewModel.cs` | Done 2026-11-14: the existing view model became bindable (a settable `SelectedFile`, the history's selected row, the rename choices stored in the preferences, Blame and Show diff commands whose windows are delegates) with its API and tests kept; the error stays until the next one. |
| `MergeSubmoduleWindow` | `Operations/MergeSubmoduleViewModel.cs` | Done 2026-11-14: git's output after staging is `ShowMessageAsync` (it was an error window); the new instance and the caller's checkout that takes the window are delegates. |
| `CompareToBranchWindow` | `Operations/CompareToBranchViewModel.cs` | Done 2026-11-14: the radio buttons bind a bool with `EnumEqualsConverter` and `{x:True}`/`{x:False}`; Compare without a branch raises `BranchRequired`, which the window answers by focusing the list. |
| `GoToCommitWindow` | `CommitHistory/GoToCommitViewModel.cs` | Done 2026-11-14: the focused control is `GoToCommitSource`, which the view sets on focus; picking in a drop-down, Enter in a list, the clipboard read and selecting its text stay in the view. |
| `FormatPatchWindow` | `Operations/FormatPatchViewModel.cs` | Done 2026-11-14: the view model owns the grid's `CommitListViewModel` (`Commits`); the grid's columns and its multiple selection, which a list cannot bind, stay in the view, which passes the selected rows to `SelectedRows`. |
| `CleanupRepositoryWindow` | `Operations/CleanupRepositoryViewModel.cs` | Done 2026-11-14: the mode radio buttons bind `CleanMode` (in namespace `GitCommands`, not `GitCommands.Git`); the folder and file pickers are delegates. |
| `CommitTemplateSettingsWindow` | `Settings/CommitTemplateSettingsViewModel.cs` | Done 2026-11-14: renaming a template replaces the list's lines, which clears the list's selection on the way; the view model restores it and ignores the cleared one. |
| `FindInCommitFilesGitGrepWindow` | `Diff/FindInCommitFilesGitGrepViewModel.cs` | Done 2026-11-14: the expression is nullable, as an editable list can pass null; the window keeps `GitGrepExpressionText`, `SetSearchItems` and `SetShowFindInCommitFilesGitGrep` for `GitGrepBox`. |
| `FilterWindow` | `CommitHistory/FilterViewModel.cs` | Done 2026-11-14: upstream's `UpdateFilters` rules are computed properties (`CanIgnoreCase`, `CanShowCurrentBranchOnly`, `CanFilterBranches`) the check boxes' `IsEnabled` bind to. |
| `AddToGitIgnoreWindow` | `Repository/AddToGitIgnoreViewModel.cs` | Done 2026-11-14: the preview's delay and cancellation are in the view model; the window sets `DataContext` after the translation pass, which would give the local excludes the .gitignore title. The local excludes' path is read with git when the view model is made, so its tests use the .gitignore. |
| `ViewPatchWindow` | `Operations/ViewPatchViewModel.cs` | Done 2026-11-14: the header buttons sort through one command with an `x:Int32` column parameter (the toolkit does not convert strings); the selected file patch is handed to `DiffView` by the view. |
| `ChooseCommitWindow` | `CommitHistory/ChooseCommitViewModel.cs` | Done 2026-11-14: like `FormatPatchWindow` it owns the grid's `CommitListViewModel`; the grid binds `SelectedIndex`, and scrolling to it, the columns and the double-click stay in the view. Upstream's "not visible" messages are `ShowError`. |
| `DeleteRemoteBranchWindow` | `Operations/DeleteRemoteBranchViewModel.cs` | Done 2026-11-14: the unmerged question keeps upstream's default No through `IDialogHost.ConfirmAsync(defaultIsNo: true)` (added for it); the multiple branches window and the caller's local delete are delegates. |
| `MessageBoxWindow` | `Presentation/MessageBoxViewModel.cs` | Done 2026-11-14: the view model turns WinForms' buttons and task dialog pages into `MessageBoxChoice`s; the window still makes the buttons in code, named `YesButton` and so on and pressed with a `Click` handler that runs the view model's command, because many tests find them by name and raise `Click` directly. A message box's buttons keep plain text, a task dialog's an `AccessText`. |
| `FindWindow` | `Presentation/FindViewModel.cs` | Done 2026-11-14: the window's events and properties forward to the view model's, so `DiffView`, `BlameView` and the editor keep using the window; hiding on close stays in the view. |
| `EditorWindow` | `Editing/EditorViewModel.cs` | Done 2026-11-14: reading, saving and the close question are the view model's; find and replace work on the text box's selection, so they stay in the view (`EditorWindow.Find.cs`). A button in the hidden question bar cannot be clicked, so its test closes again first. |
| `ReflogWindow` | `Repository/ReflogViewModel.cs` | Done 2026-11-14: the reflog read is injectable for tests; the menu's data context is set in code (see Context menus); the red warning's theme color, hover selection and opening the menu with a click stay in the view. |
| `CreateWorktreeWindow` | `Operations/CreateWorktreeViewModel.cs` | Done 2026-11-14: `CreateWorktreeDialog` stays the rules object, fed from the view model's properties; Enter in the branch list and normalising on leaving the name box stay in the view. |
| `SubmodulesWindow` | `Operations/SubmodulesViewModel.cs` | Done 2026-11-14: the details bind to `Selected`; the add window and the pull dialog are delegates; the wait cursor follows `IsLoading` in the view. |
| `GitIgnoreWindow` | `Repository/GitIgnoreViewModel.cs` | Done 2026-11-14: the file's path, the bare repository check (both git) and the add window come in from the window; the data context is set after the translation pass, which would give the local excludes the .gitignore title; the close question works as in `RepositoryTextFileWindow`. |
| `SparseWorkingCopyWindow` | `Repository/SparseWorkingCopyViewModel.cs` | Done 2026-11-14: the view binds the existing observable model through `Sparse`; the texts upstream sets in code stay in the code-behind (translated by text). The model takes upstream's `GitModule`, so the view model has no Core tests; the window's headless tests cover it. |
| `MergeBranchWindow` | `Operations/MergeBranchViewModel.cs` | Done 2026-11-14: `MergeBranchDialog` became observable (its side effects in `OnNoFastForwardChanged` and `OnAdvancedChanged`) and the window binds through `Dialog`; the strategies are a list instead of `ComboBoxItem`s; the help picture follows the fast forward choice in the view. The view model takes upstream's `GitModule` (merge settings, merge message file), so its headless tests cover it. |
| `CheckoutBranchWindow` | `Operations/CheckoutBranchViewModel.cs` | Done 2026-11-14: `CheckoutBranchDialog` became observable (hand-written setters keep its derived names; `Branch` takes null as empty, as an editable list passes no text while its items change); the radio groups bind its modes; the remote/local choice goes through `SetRemote`; selecting the custom name when chosen and normalising it on leaving its box stay in the view. |
| `BugReportWindow` | `Diagnostics/BugReportViewModel.cs` | Done 2026-11-14: the browser, the clipboard and the property window are delegates; the window cannot close while `MayClose` is false (the error cannot be ignored); the data context is set after the translation pass, which would leave the title without the application's name. `ErrorReports.GeneralInfo` reads git's version, so the view model has no Core tests. |
| `RecentRepositoriesSettingsWindow` | `Repository/RecentRepositoriesSettingsViewModel.cs` | Done 2026-11-14: the number boxes bind `decimal?`; the combo width's correction is the view model's. The rows' template (missing in red, the combo's width), the lists' menus and double-clicks stay in the view, which passes the selected rows to `Anchor`, `RemoveAsync` and `MenuState`. |
| `ApplyPatchWindow` | `Operations/ApplyPatchViewModel.cs` | Done 2026-11-14: the controls' `IsEnabled` bind `Buttons` (upstream's `EnableButtons`), so the tests that read `IsEnabled` still hold; the highlighted default button (its text, frame and `IsDefault`) and the patch rows' colors follow `Buttons` and `Patches` in the view. Takes upstream's `GitModule`. |
| `CommandLogWindow` | `Repository/CommandLogViewModel.cs` | Done 2026-11-14: the view model follows `CommandLog` and the cache between `Attach` and `Detach`, posting its refreshes through a delegate (the dispatcher). Word wrap, scrolling to the newest row and the tab pages' keys stay in the view; the context menus get their data context in code. |
| `PushWindow` | `Operations/PushViewModel.cs` | Done 2026-11-14: `PushDialog` stays the rules object; a change of remote, URL choice, branch, force or tab goes through `Edit` (the typed values kept, the change, the state shown again), as the window did. The lists are set with the change guard on, as their boxes clear the choice when the items change. The new branch confirmation (`ConfirmWindow`) is a delegate. |
| `CloneWindow` | `Operations/CloneViewModel.cs` | Done 2026-11-14: the location line's red is a bound class (`Classes.error`); the clipboard, the pickers and Pageant are delegates (`GiveKeyToPageant` kept on the window for tests); reading the remote's branches when the list opens stays a view event. |
| `PullWindow` | `Operations/PullViewModel.cs` | Done 2026-11-14: the repository's questions and windows come in a `PullViewModelGit` record the window builds from its `PullWindowGit`; upstream's task dialogs (detached HEAD, missing remote branch) stay in the view behind delegates. `IDialogHost.AskAsync` gained `defaultIsNo` for the rebase question. The help picture follows `HelpImages` in the view. |
| `RebaseWindow` | `Operations/RebaseViewModel.cs` | Done 2026-11-14: the options mirror `RebaseDialog` through `Edit`; the buttons bind `Buttons`; the default button's `IsDefault` and focus, the conflicts' yellow and the patch rows' colors follow the view model in the view. Takes upstream's `GitModule`. |
| `RevisionDiffWindow` | `Diff/RevisionDiffViewModel.cs` | Done 2026-11-14: the view model holds the two sides, the merge base and the changed files; the file list (`RevisionFileList`, its filter, grouping and menu) and the diff stay in the view, which shows `Files` and reads `ShownFrom` and `Second`. The branch and commit pickers are delegates. |
| `VerifyDatabaseWindow` | `Repository/VerifyDatabaseViewModel.cs` | Done 2026-11-14: `LostObjectRow` moved to Core; the header buttons sort through one command with the column (`x:Static`) as parameter; the menu items' `IsEnabled` bind `SelectedIsCommit` and `SelectedIsBlob`; the preview is a `LostObjectPreview` the view hands to `DiffView`. The rows' grid and the wait cursor stay in the view. |
| `RemotesWindow` | `Operations/RemotesViewModel.cs` | Done 2026-11-14: `RemoteListRow` and `PullBehaviorRow` moved to Core; the details mirror `RemotesDialog` through `Edit`; proposals when a box gets the focus, the prefix normalised on leaving it and the branch's remote and merge branch written on leaving their boxes are view events that call the view model. The color swatches run `ChooseColorCommand`; the button's color and the toggle's eye stay in the view. |
| `StashWindow` | `Operations/StashViewModel.cs` | Done 2026-11-14: `StashChoice` and `StashRevisions` moved to Core; the view model reads the stashes and the selected one's files (`Files`), the view groups them into its rows and tree and passes the selected files back (`SelectedFiles`, for "Stash selected changes"). The drop confirmation is a delegate. |
| `ViewTextWindow` | none | Waits for `DiffView`: the window only hands the text to that control and keeps no state of its own. |

Shared controls made bindable: `CommitPickerView` (`Selected`, `Module`), `CommitSummaryView` (`Summary`).
