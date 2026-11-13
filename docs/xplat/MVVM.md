# MVVM in the Avalonia app

Started 2026-11-13. The rule is `PLAN.md` principle 6 (new UI uses MVVM; reworked UI moves to it), the base is
CommunityToolkit.Mvvm (section 9, question 17), and the plan is section 12.6. This file is the pattern every window follows,
taken from the pilot (`CreateBranchWindow`), and the list of windows done.

## The pattern

| Layer | Where | What it holds |
|---|---|---|
| Rules | `GitExtensions.Xplat.Core/.../*Dialog.cs` | Upstream's form logic without a window (already there for most windows). Made observable: `partial class ... : ObservableObject`, state as `[ObservableProperty] public partial ... { get; set; }`, computed `Can...` properties with `[NotifyPropertyChangedFor]`, rules in `partial void On...Changed`. Its public API stays, so its tests stay. |
| View model | `GitExtensions.Xplat.Core/.../*ViewModel.cs`, derived from `Presentation/DialogViewModel` | What the window did: reading git (through `IGitModule`, so tests substitute it), the rules object (replaced when loading decides it), values for binding, `[RelayCommand]`s, and `LoadAsync` that the window calls when it opens. No Avalonia types. |
| Host | `Presentation/IDialogHost` (Core), `WindowDialogHost` (App) | What the view model asks of its window: `Translate`, `ShowMessageAsync`, `ShowError`, `Close`. One app implementation for every window; tests use `FakeDialogHost` (Core.Tests). |
| View | `GitExtensions.Xplat.App/*Window.axaml(.cs)` | `x:DataType` of the view model, bindings and commands in XAML, `Escape` as a `KeyBinding` to a `CancelCommand`. Code-behind: create the view model with `new WindowDialogHost(this)`, set `DataContext`, and view concerns only: focus and caret, help buttons, a call such as `NormaliseName` on `LostFocus`. The constructor keeps its signature, so callers do not change. |
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
  and the pass then finds nothing to replace. Fixed English texts stay in the XAML.
- **Errors.** An exception that escapes an asynchronous command ends the app. Command bodies run in `GuardAsync`, which
  shows the error as the windows did (`UiActions.Run`) and ignores cancellation.
- **Clicking in tests.** Every test raises click events through `UiClicks.Raise(control, event)` (changed in all test classes
  2026-11-13): it raises the event as before, but a button bound to a command gets a headless mouse click and a menu item bound
  to one runs it, since `RaiseEvent(new RoutedEventArgs(Button.ClickEvent))` alone runs no command and Avalonia 12 made
  `IClickableControl` internal. New tests do the same (`UiClicks.Click(control)`).
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
  (`ResetCurrentBranchWindow`). Questions are `Host.ConfirmAsync` (Yes and No).
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
| `ViewTextWindow` | none | Waits for `DiffView`: the window only hands the text to that control and keeps no state of its own. |

Shared controls made bindable: `CommitPickerView` (`Selected`, `Module`), `CommitSummaryView` (`Summary`).
