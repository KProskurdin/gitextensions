# MVVM in the Avalonia app

Started 2026-11-13; the move of every window to MVVM was complete on 2026-11-18 (its plan, `PLAN.md` section 12.6, was removed on 2026-11-22 and is in git history). The rule is `PLAN.md` principle 6 (new UI uses MVVM; reworked UI moves to it), the base is
CommunityToolkit.Mvvm (section 9, question 17). This file is the pattern every window follows,
taken from the pilot (`CreateBranchWindow`), its gotchas, what is left outside it and the pieces to reuse.

## The pattern

| Layer | Where | What it holds |
|---|---|---|
| Rules | `GitExtensions.Xplat.Core/.../*Dialog.cs` | Upstream's form logic without a window (already there for most windows). Made observable: `partial class ... : ObservableObject`, state as `[ObservableProperty] public partial ... { get; set; }`, computed `Can...` properties with `[NotifyPropertyChangedFor]`, rules in `partial void On...Changed`. Its public API stays, so its tests stay. |
| View model | `GitExtensions.Xplat.Core/.../*ViewModel.cs`, derived from `Presentation/DialogViewModel` | What the window did: reading git (through `IGitModule`, so tests substitute it), the rules object (replaced when loading decides it), values for binding, `[RelayCommand]`s, and `LoadAsync` that the window calls when it opens. No Avalonia types. |
| Host | `Presentation/IDialogHost` (Core), `WindowDialogHost` (App) | What the view model asks of its window: `Translate`, `ShowMessageAsync`, `ConfirmAsync` (Yes, No), `AskAsync` (Yes, No, Cancel), `ShowError`, `Close`. One app implementation for every window; tests use `FakeDialogHost` (Core.Tests). |
| View | `GitExtensions.Xplat.App/*Window.axaml(.cs)` | `x:DataType` of the view model, bindings and commands in XAML, `Escape` as a `KeyBinding` to a `CancelCommand`, or `IsCancel="True"` on the cancel button when upstream's form has a `CancelButton` (`DialogKeysParityTests` checks that). Code-behind: create the view model with `new WindowDialogHost(this)`, set `DataContext`, and view concerns only: focus and caret, help buttons, a call such as `NormaliseName` on `LostFocus`. The constructor keeps its signature, so callers do not change. |
| Window services | e.g. `Repository/IBrowseWindow` (Core), implemented by `MainWindow.BrowseWindow.cs` | For a window whose commands open other windows (the browse window): an interface of upstream's actions that need the window (a dialog, a folder, a terminal, its error display). The view model's commands decide whether an action applies and with which revision, run git through the actions' view model, and call the interface for the rest; tests substitute it with NSubstitute. |
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

- **A part with its own view model.** An element inside a window that binds to another view model (its own `x:DataType`, such as the browse window's View menu or the dashboard's list) gets `DataContext="{x:Null}"` in the XAML, and the code sets its view model before the window's. Otherwise it inherits the window's view model for a moment, and a compiled binding casts that to its own type and throws (2026-11-17: "Unable to cast BrowseToolbarViewModel to GridViewMenuViewModel" at start-up).
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
- **Flyouts.** A button's `MenuFlyout` gives its items a data context only when it opens, like a context menu, so the window
  sets each item's `DataContext` in code (`CommitWindow`'s Options, `FileHistoryWindow`'s toolstrip); otherwise a value set
  before the menu was ever opened never reaches the view model.
- **Pages not shown.** A command-bound button on a settings page that is not shown is not in the visual tree, so a test
  shows its tab first (`Tabs.SelectedItem`); a raised click event alone used to work only because the handler did.
- **Templates by kind.** A list whose rows need different controls by a value (plugin settings by kind) gets an
  `IDataTemplate` in the view that builds only the row's control, not one template with every control hidden but one:
  tests and the settings search find the controls a page shows.
- **Disabled buttons.** A command-bound button that is disabled cannot be clicked, as a user cannot; a test that raised
  a click event on it passed before only because the event bypassed the state (settings' OK while git config loads).
- **Menu check items.** `MenuItem.IsChecked` binds one way by default. Avalonia's menu (its `DefaultMenuInteractionHandler`,
  read 2026-11-15) sets `IsChecked` itself on a real click or Enter, then raises the click, whose handler runs the command;
  a raised click event runs the command but checks nothing. So an item bound `IsChecked` one way plus a `Command` that
  toggles the setting works for both (`FileHistoryWindow`, `BlameView`): the menu's own check matches the setting the
  binding then shows. A two-way `IsChecked` with no command works only for real clicks (`CommitWindow`, `DiffView`), and
  its tests set `IsChecked` (see Check items in tests). New items use the command.
- **Errors after closing.** A command that fails after its window closed (a test removing the repository while the commit
  still stages the superproject) reports to a closed window. `WindowDialogHost.ShowError` traces it then; before the move
  to commands such an error was lost in a discarded task.
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
- **Controls.** A user control's view model is the data context of its inner panel (`DiffView`'s `LayoutRoot`), not of the
  control, which keeps the data context of the window around it; its context menu still needs its own (see Context menus).
  A control whose view model is set later than its constructor (`FilterToolBar.Bind`) declares the panel's
  `DataContext="{x:Null}"`: until then the panel would take the window's, and its compiled bindings fail on the cast
  (`InvalidCastException` at start-up on macOS, 2026-11-18; the headless tests log the binding error instead of throwing, so
  `FilterToolBarTests` checks the panel's data context).
- **View models that own a control's.** A view model that holds a control's view model (`CommitDiffViewModel.Diff`) hands
  it to the control by a bindable `ViewModel` property (`DiffView.ViewModelProperty`, `DiffView.NewViewModel()` for one
  with the app's services); the control makes its own when nothing binds one.
- **Toolbars shown on hover.** `DiffView`'s toolbar shows only while the pointer is over the viewer, and a hidden
  command-bound button cannot be clicked: tests move the mouse over the viewer first (`window.MouseMove`).
- **Check items in tests.** A `ToggleType="CheckBox"` item toggles its own `IsChecked` on a real click, which the two-way
  binding passes on, but a raised click event does not toggle it: tests set `IsChecked`, as `CommitBottomPanelTests` do.
- **Closed menus.** An item of a menu that is not open reports `IsEffectivelyEnabled` false once it has a command: Avalonia asks
  the command only for an open menu. Code that runs an item while its menu is closed (`FileStatusListMenu.ExecuteCommand`
  for upstream's hotkeys) checks `IsVisible`, `IsEnabled` and the command's `CanExecute`, and runs the command itself.
- **Parent items with commands.** A sub-item's click bubbles to its parent menu item. A sub-item with a command marks the click handled, so a parent's command (the item itself opening the default difftool, "Reset file(s) to") does not run as well; before, the parent's handler had to check `e.Source`. A parent's command must not be disabled (no `CanExecute`, `AllowConcurrentExecutions = true` for an asynchronous one): a disabled parent cannot open its submenu.
- **Menu entries in XAML.** A menu item whose items are a view model's `MenuEntry` list binds `ItemsSource` to it through `MenuEntriesConverter` (the difftool items); `MenuEntries.Fill` stays for menus filled in code as they open.
- **Group-by toggles.** A `ToggleButton` unchecks itself when clicked while checked, and a choice that stays the same raises no change, so the binding would leave it unchecked. `FileListGroupingViewModel` raises every property (an empty name) after each command, which checks it again.
- **Menus filled as they open, in parts.** A menu whose items are partly the view's (the commit message box's: the misspelled word's items and the editing items act on the box's selection) takes the view model's entries for the rest through `MenuEntries.Item` (`CommitViewModel.DictionaryEntry`, `MessageBoxSettingEntries`, `WordWrapEntry`).
- **Shared helpers over a window's controls.** A helper that wires controls each window declares (`GitGrepBox` over the box, the delete button and the split button the Diff and File tree tabs name) binds them in code to its view model (`Bind` with the view model as `Source`, or a control's `DataContext` set to it) and sets their `Command`s; the window's XAML stays as it is.
- **Toolbars of a shared view model.** A file list's toolbar panel declares `x:DataType="fl:FileListToolbarViewModel"` and `DataContext="{x:Null}"`, and the list (`RevisionFileList`, the stash window) sets it (`FileListToolbar.Bind`), so the windows' own view models do not change. A button in that panel bound to another view model (the git-grep split button) gets its own `DataContext`.
- **Shared reads.** Logic two windows did alike moves to their rules class once (`CherryPickDialog.ReadCommit` for the
  cherry pick and revert windows), not into each view model.

## State

Every window, dialog and user control of the app has its view model since 2026-11-18, and button and menu actions are
commands; the per-window log of the move is in git history (this file before 2026-11-24). Exceptions:

- `ViewTextWindow` has no view model: it only hands its text to `DiffView`.
- The plugins' own windows are not moved; they move when they are reworked.
- View concerns still on a click handler: the commit message box's editing items; find, replace and go to line in
  `DiffView` and `EditorWindow`, with the editor's cut, copy and paste; `HelpImageView`'s show and hide;
  `OverflowToolStrip`; the filter toolbar's branch button and the dashboard's "Actions" link (each only opens its menu);
  the message box's buttons, made in code because tests raise their `Click`; `DialogHelp`. A new handler outside this list
  is a command.

## Pieces to reuse

| Piece | Where | Use it for |
|---|---|---|
| `DialogViewModel`, `IDialogHost` | Core `Presentation/`; `WindowDialogHost` (App), `FakeDialogHost` (Core.Tests) | Every window's view model: `GuardAsync`, `Translate`, questions, errors, `OpenUrlCommand` |
| `MenuEntry`, `MenuEntries.Fill`, `MenuEntriesConverter` | Core `Presentation/MenuEntry.cs`; App | A menu a view model lists (text, picture, check or radio, children, command) |
| `MenuItemState` | Core `Presentation/` | A fixed menu whose items are shown and enabled one by one (`RepoObjectMenuViewModel`) |
| Window services | `Repository/IBrowseWindow`, `IRepoObjectWindow`, `IDashboardWindow` | Commands that need a window: dialogs, pickers, a terminal; tests substitute them |
| Shared view models | `Diff/CustomDiffToolsViewModel`, `Diff/FileListGroupingViewModel`, `Diff/FileListToolbarViewModel`, `Diff/CopyPathsMenuViewModel`, `CommitHistory/GridViewMenuViewModel`, `CommitHistory/CommitPickerViewModel` | The difftool submenus, the file lists' toolbars, "Copy path(s)", the View menu and the commit picker, wherever they appear again |
| Controls with a `ViewModel` property | `DiffView`, `BlameView` (`ViewModelProperty`) | A view model that owns a control's view model hands it over by binding |
| Bindable shared controls | `CommitPickerView` (`Selected`, `Module`), `CommitSummaryView` (`Summary`) | Binding them from a window's XAML |
