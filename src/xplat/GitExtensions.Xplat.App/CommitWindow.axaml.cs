using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GitCommands.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;
using GitUI.ScriptsEngine;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Stages files and commits them: the new shell's version of upstream <c>FormCommit</c>. It works on the main window's
///  repository state and operations, so a change made here refreshes both windows.
/// </summary>
public partial class CommitWindow : Window
{
    private const string DefaultRemote = "origin";

    private readonly string _repositoryPath;
    private readonly RepositoryViewModel _repository;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IRepositoryService _repositoryService;
    private readonly IAppPreferences _preferences;
    private readonly Func<Task> _refresh;
    private readonly ICommitMessageStore _messages;
    private readonly Func<string, bool>? _runPlugin;
    private string _messageBeforeAmend = "";

    // True while the remembered Amend state is put back; the draft is then the amend message already.
    private bool _restoringAmend;

    // The file whose diff is on show, and whether it is the staged or the unstaged diff.
    private FileChange? _diffChange;
    private bool _diffStaged;

    // Upstream FormCommit's texts of the templates menu and the message box menu.
    private const string TemplateSettingsText = "_Edit commit message templates and settings...";
    private const string ConventionalCommitText = "Conven_tional Commits";
    private const string ConventionalCommitDocumentationText = "Documentation...";
    private const string WordWrapBodyText = "_Word wrap (except subject line)";
    private const string SubmoduleChangesText = "Generate a list of changes in submodules";
    private const string ShowOnlyMyMessagesText = "Show only my messages";
    private const string CommitMessageDisabledText = "Commit Message is requested during commit";

    // The Conventional Commits submenu as last built, and whether its types add "()" for a scope (Ctrl+Shift+T).
    private MenuItem? _conventionalCommitsItem;
    private bool _insertScopeParentheses;

    // True while the message is reformatted, so the change it makes is not formatted again.
    private bool _formatting;

    // Counts the openings of the "Commit message" menu, so a slow read does not fill a menu opened again since.
    private int _messageMenuOpenings;

    // Upstream's UseFormCommitMessage, read once per window as upstream does: off, git asks for the message in its editor.
    private readonly bool _useFormCommitMessage;

    public CommitWindow(string repositoryPath, RepositoryViewModel repository, RepositoryOperationsViewModel actions,
        IRepositoryService repositoryService, IAppPreferences preferences, Func<Task> refresh,
        ICommitMessageStore messages, Func<string, bool>? runPlugin = null)
    {
        // Runs a plugin named by a script's plugin command, as the browse window does.
        _runPlugin = runPlugin;
        _repositoryPath = repositoryPath;
        _repository = repository;
        _actions = actions;
        _repositoryService = repositoryService;
        _preferences = preferences;
        _refresh = refresh;
        InitializeComponent();
        WindowPlacementTracker.Attach(this, "Xplat.CommitWindow");
        Title = $"Commit - {RecentRepositoryPaths.DisplayName(repositoryPath)}";
        _messages = messages;
        Opened += (_, _) => Run(LoadMessageAsync);
        Closing += (_, _) => SavedDraft = SaveDraftAsync(CommitMessageBox.Text ?? "", AmendCheck.IsChecked == true);
        _useFormCommitMessage = preferences.UseFormCommitMessage;
        if (!_useFormCommitMessage)
        {
            CommitMessageBox.IsEnabled = false;
            CommitMessageBox.PlaceholderText = CommitMessageDisabledText;
            CommitTemplatesButton.IsEnabled = false;
            CommitMessageMenuButton.IsEnabled = false;
        }

        CommitAndPushButton.IsVisible = preferences.ShowCommitAndPush;
        ResetAllButton.IsVisible = preferences.ShowResetAllChanges;
        ResetUnstagedButton.IsVisible = preferences.ShowResetWorkTreeChanges;
        ResetAllButton.Click += (_, _) => Run(() => ResetChangesAsync(onlyWorkTree: false));
        ResetUnstagedButton.Click += (_, _) => Run(() => ResetChangesAsync(onlyWorkTree: true));
        CommitMessageBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                FormatMessage();
            }
        };
        AddMessageMenu();
        InitializeAutoCompletion();

        StageButton.Click += (_, _) => StageSelected(staged: false);
        UnstageButton.Click += (_, _) => StageSelected(staged: true);
        StageAllButton.Click += (_, _) => StageAll(staged: false);
        UnstageAllButton.Click += (_, _) => StageAll(staged: true);
        UseOursButton.Click += (_, _) => ResolveSelectedConflicts(ours: true);
        UseTheirsButton.Click += (_, _) => ResolveSelectedConflicts(ours: false);
        DiscardButton.Click += (_, _) => Run(DiscardAsync);
        DeleteUntrackedButton.Click += (_, _) => Run(DeleteUntrackedAsync);
        StashSelectedButton.Click += (_, _) => Run(StashSelectedAsync);
        IgnoreButton.Click += (_, _) => Run(IgnoreSelectedAsync);
        DiffToolButton.Click += (_, _) => Run(OpenDiffToolAsync);
        DiffToolChoices.Attach(DiffToolChoiceButton, tool => OpenDiffToolAsync(tool), ShowError);
        CommitButton.Click += (_, _) => Run(CommitAsync);
        CommitAndPushButton.Click += (_, _) => Run(CommitAndPushAsync);
        AmendCheck.IsCheckedChanged += (_, _) => Run(ToggleAmendAsync);
        CommitTemplatesFlyout.Opening += (_, _) => ShowCommitTemplates();
        CommitMessageFlyout.Opening += (_, _) => Run(ShowPreviousMessagesAsync);
        UnstagedContextMenu.Opening += (_, _) => ShowFileListScripts(UnstagedContextMenu, UnstagedRunScriptMenuItem);
        StagedContextMenu.Opening += (_, _) => ShowFileListScripts(StagedContextMenu, StagedRunScriptMenuItem);
        UnstagedList.SelectionChanged += (_, _) => OnFileSelected(UnstagedList, StagedList, staged: false);
        StagedList.SelectionChanged += (_, _) => OnFileSelected(StagedList, UnstagedList, staged: true);
        Diff.LineSelectionChanged += (_, _) => UpdateState();
        StageLinesButton.Click += (_, _) => Run(() => StageSelectedLinesAsync(unstage: false));
        UnstageLinesButton.Click += (_, _) => Run(() => StageSelectedLinesAsync(unstage: true));
        Diff.LinesHotkey += (_, command) =>
        {
            Button button = command == DiffCommand.StageLines ? StageLinesButton : UnstageLinesButton;
            if (button.IsEnabled)
            {
                Run(() => StageSelectedLinesAsync(unstage: command == DiffCommand.UnstageLines));
            }
        };
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);

        _repository.PropertyChanged += OnRepositoryChanged;
        _actions.PropertyChanged += OnActionsChanged;
        Closed += (_, _) =>
        {
            _repository.PropertyChanged -= OnRepositoryChanged;
            _actions.PropertyChanged -= OnActionsChanged;
        };

        ShowChanges();
        UpdateState();
    }

    /// <summary>
    ///  The save of the typed message when the window closed, so the next commit window can wait for it before it reads.
    /// </summary>
    public Task SavedDraft { get; private set; } = Task.CompletedTask;

    // Like upstream, the window starts from the prepared message: git's merge message while a merge is stopped, otherwise the
    // draft left last time (in this app or the WinForms one). A message typed before the read finished is kept.
    private async Task LoadMessageAsync()
    {
        string message = await _messages.LoadAsync(_repositoryPath);
        if (_useFormCommitMessage && string.IsNullOrEmpty(CommitMessageBox.Text))
        {
            CommitMessageBox.Text = message.TrimEnd();
        }

        // As upstream: Amend comes back checked, not while a merge is stopped, and the draft stays as it was left.
        if (_preferences.RememberAmendCommitState && !_repository.IsMerging &&
            await _messages.LoadAmendAsync(_repositoryPath))
        {
            _restoringAmend = true;
            AmendCheck.IsChecked = true;
            _restoringAmend = false;
        }
    }

    // On close, as upstream's FormCommit: the draft and whether Amend was checked.
    private async Task SaveDraftAsync(string message, bool amend)
    {
        await _messages.SaveAsync(_repositoryPath, message);
        await _messages.SaveAmendAsync(_repositoryPath, _preferences.RememberAmendCommitState && amend);
    }

    private void Run(Func<Task> action) => UiActions.Run(action, ex => ShowError(ex.Message));

    // Upstream's file list menu (FileStatusList): scripts shown in file lists are items of the menu, the others under "Run
    // script".
    private void ShowFileListScripts(ContextMenu menu, MenuItem host)
        => ScriptMenus.Fill(menu, host, script => script.OnEvent == ScriptEvent.ShowInFileList,
            script => Run(() => RunScriptAsync(script)));

    // As upstream FormCommit's script options: the files selected in the list last used and the line selected in the diff.
    private Task<bool> RunScriptAsync(ScriptDefinition script)
        => new ScriptHost(this, _actions, runPlugin: _runPlugin).RunAsync(script, _repositoryPath, [],
            ScriptFileOptions.For(
                SelectedChanges(_diffStaged ? StagedList : UnstagedList).Select(change => change.Path),
                Diff.CurrentFileLine));

    private void ShowError(string message) => _ = new ErrorWindow(message).ShowDialog(this);

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (HandleAutoCompleteKey(e))
        {
            e.Handled = true;
            return;
        }

        bool command = e.KeyModifiers.HasFlag(Hotkeys.CommandModifier);
        if (e.Key == Key.Enter && command && e.Source == CommitMessageBox && CommitButton.IsEnabled)
        {
            Run(CommitAsync);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (Hotkeys.Commit.Match(e) is { } hotkey)
        {
            e.Handled = RunHotkey(hotkey);
        }
        else if (ScriptHost.MatchHotkey(e) is { } script)
        {
            e.Handled = true;
            Run(() => RunScriptAsync(script));
        }
    }

    // The commands of upstream FormCommit's hotkeys; false when the command cannot run now, so the key goes on.
    private bool RunHotkey(CommitCommand command)
    {
        switch (command)
        {
            case CommitCommand.FocusUnstagedFiles:
                return UnstagedList.Focus();
            case CommitCommand.FocusSelectedDiff:
                return Diff.FindControl<ListBox>("DiffList")?.Focus() == true;
            case CommitCommand.FocusStagedFiles:
                return StagedList.Focus();
            case CommitCommand.FocusCommitMessage:
                return CommitMessageBox.Focus();
            case CommitCommand.StageAll when StageAllButton.IsEnabled:
                StageAll(staged: false);
                return true;
            case CommitCommand.OpenWithDifftool when DiffToolButton.IsEnabled:
                Run(OpenDiffToolAsync);
                return true;
            case CommitCommand.Refresh:
                Run(_refresh);
                return true;
            case CommitCommand.SelectNext:
                return MoveSelection(1);
            case CommitCommand.SelectPrevious:
                return MoveSelection(-1);
            case CommitCommand.ConventionalCommitPrefixMessage:
                return OpenConventionalCommitMenu(insertScope: false);
            case CommitCommand.ConventionalCommitPrefixMessageWithScope:
                return OpenConventionalCommitMenu(insertScope: true);
            default:
                return false;
        }
    }

    // As upstream, next and previous move through the list whose file is shown in the diff.
    private bool MoveSelection(int step)
    {
        ListBox list = _diffStaged ? StagedList : UnstagedList;
        int index = list.SelectedIndex + step;
        if (list.ItemCount == 0 || index < 0 || index >= list.ItemCount)
        {
            return false;
        }

        list.SelectedIndex = index;
        return true;
    }

    private void OnRepositoryChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RepositoryViewModel.Changes):
                ShowChanges();
                RefreshAutoCompleteWords();
                UpdateState();
                break;
            case nameof(RepositoryViewModel.IsLoading):
            case nameof(RepositoryViewModel.IsMerging):
            case nameof(RepositoryViewModel.IsRebasing):
            case nameof(RepositoryViewModel.CurrentBranch):
                UpdateState();
                break;
        }
    }

    private void OnActionsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RepositoryOperationsViewModel.IsBusy):
                StatusText.Text = _actions.IsBusy ? "Working..." : _actions.StatusMessage;
                UpdateState();
                break;
            case nameof(RepositoryOperationsViewModel.StatusMessage):
                StatusText.Text = _actions.StatusMessage;
                break;
        }
    }

    private void ShowChanges()
    {
        UnstagedList.ItemsSource = _repository.Changes.Where(change => !change.Staged).ToList();
        StagedList.ItemsSource = _repository.Changes.Where(change => change.Staged).ToList();
        UnstagedHeader.Text = $"Unstaged changes ({UnstagedList.ItemCount})";
        StagedHeader.Text = $"Staged changes ({StagedList.ItemCount})";
    }

    // Upstream shows one diff at a time, so selecting in one list clears the other.
    private void OnFileSelected(ListBox list, ListBox other, bool staged)
    {
        UpdateState();
        if (list.SelectedItems is not { Count: 1 } || list.SelectedItem is not FileChange change)
        {
            return;
        }

        other.SelectedItems?.Clear();
        _diffChange = change;
        _diffStaged = staged;
        Run(() => Diff.ShowAsync(_repositoryPath, commitHash: null, change.Path, staged, change.Label));
    }

    private void UpdateState()
    {
        bool idle = !_actions.IsBusy && !_repository.IsLoading;
        List<FileChange> unstaged = SelectedChanges(UnstagedList);
        bool hasUnstaged = _repository.Changes.Any(change => !change.Staged && change.Kind != ChangeKind.Conflict);
        bool hasStaged = _repository.Changes.Any(change => change.Staged);

        StageButton.IsEnabled = idle && unstaged.Any(change => change.Kind != ChangeKind.Conflict);
        StageAllButton.IsEnabled = idle && hasUnstaged;
        UnstageButton.IsEnabled = idle && SelectedChanges(StagedList).Count > 0;
        UnstageAllButton.IsEnabled = idle && hasStaged;
        DiscardButton.IsEnabled = idle && DiscardablePaths(unstaged).Count > 0;
        DeleteUntrackedButton.IsEnabled = idle && UntrackedPaths(unstaged).Count > 0;
        bool conflictSelected = idle && ConflictPaths(unstaged).Count > 0;
        UseOursButton.IsEnabled = conflictSelected;
        UseTheirsButton.IsEnabled = conflictSelected;
        StashSelectedButton.IsEnabled = idle && unstaged.Count > 0;
        IgnoreButton.IsEnabled = idle && UntrackedPaths(unstaged).Count > 0;
        DiffToolButton.IsEnabled = idle && _diffChange is { Kind: not (ChangeKind.Untracked or ChangeKind.Conflict) };
        CommitButton.IsEnabled = idle;
        CommitAndPushButton.IsEnabled = idle && _repository.CurrentBranch.Length > 0;

        // As upstream, lines can be staged from an edited or untracked file and unstaged from an edited or added one.
        bool linesSelected = idle && Diff.SelectedRange() is not null;
        StageLinesButton.IsEnabled = linesSelected && !_diffStaged
                                                   && _diffChange is
                                                       { Kind: ChangeKind.Modified or ChangeKind.Untracked };
        UnstageLinesButton.IsEnabled = linesSelected && _diffStaged
                                                     && _diffChange is
                                                         { Kind: ChangeKind.Modified or ChangeKind.Added };

        string state = _repository.IsRebasing
            ? "A rebase is in progress: commit continues it only after the conflicts are staged."
            : _repository.IsMerging
                ? "A merge is in progress: committing completes the merge."
                : "";
        StateText.Text = state;
        StateText.IsVisible = state.Length > 0;
        StateText.Foreground = ThemeBrushes.Current.Warning;
    }

    private static List<FileChange> SelectedChanges(ListBox list) =>
        list.SelectedItems?.OfType<FileChange>().ToList() ?? [];

    private static List<string> ConflictPaths(IEnumerable<FileChange> changes)
        => [.. changes.Where(change => change.Kind == ChangeKind.Conflict).Select(change => change.Path)];

    private static List<string> UntrackedPaths(IEnumerable<FileChange> changes)
        => [.. changes.Where(change => change.Kind == ChangeKind.Untracked).Select(change => change.Path)];

    // Only unstaged edits to tracked files can be discarded; untracked files are left alone.
    private static List<string> DiscardablePaths(IEnumerable<FileChange> changes)
        =>
        [
            .. changes.Where(change => change.Kind is ChangeKind.Modified or ChangeKind.Deleted)
                .Select(change => change.Path)
        ];

    private async Task IgnoreSelectedAsync()
    {
        List<string> patterns = [.. UntrackedPaths(SelectedChanges(UnstagedList)).Select(GitIgnoreFile.PatternFor)];
        if (patterns.Count > 0 && await new GitIgnoreWindow(_repositoryPath, patterns).ShowDialog<bool>(this))
        {
            await _refresh();
        }
    }

    private async Task OpenDiffToolAsync() => await OpenDiffToolAsync(tool: null);

    private async Task OpenDiffToolAsync(string? tool)
    {
        if (_diffChange is { } change)
        {
            await _actions.RunDiffToolAsync(_repositoryPath, change.Path, commit: null, _diffStaged, tool);
        }
    }

    private async Task StageSelectedLinesAsync(bool unstage)
    {
        if (_diffChange is not { } change || Diff.SelectedRange() is not { } range)
        {
            return;
        }

        if (await _actions.StageLinesAsync(_repositoryPath, range.Text, range.Start, range.Length, unstage,
                isNewFile: change.Kind == ChangeKind.Added))
        {
            // The same file's diff, read again, now without the lines that moved.
            await Diff.ShowAsync(_repositoryPath, commitHash: null, change.Path, _diffStaged, change.Label);
        }
    }

    private void StageSelected(bool staged)
    {
        List<string> paths =
        [
            .. SelectedChanges(staged ? StagedList : UnstagedList)
                .Where(change => change.Kind != ChangeKind.Conflict)
                .Select(change => change.Path)
        ];
        if (paths.Count > 0)
        {
            Run(() => staged
                ? _actions.UnstageAsync(_repositoryPath, paths)
                : _actions.StageAsync(_repositoryPath, paths));
        }
    }

    private void StageAll(bool staged)
    {
        List<string> paths =
        [
            .. _repository.Changes
                .Where(change => change.Staged == staged && change.Kind != ChangeKind.Conflict)
                .Select(change => change.Path)
        ];
        if (paths.Count > 0)
        {
            Run(() => staged
                ? _actions.UnstageAsync(_repositoryPath, paths)
                : _actions.StageAsync(_repositoryPath, paths));
        }
    }

    // Takes the chosen side of each selected conflicted file and stages it, which marks the file resolved.
    private void ResolveSelectedConflicts(bool ours)
    {
        if (ConflictPaths(SelectedChanges(UnstagedList)) is { Count: > 0 } paths)
        {
            Run(() => _actions.ResolveConflictsAsync(_repositoryPath, paths, ours));
        }
    }

    private async Task DiscardAsync()
    {
        if (DiscardablePaths(SelectedChanges(UnstagedList)) is not { Count: > 0 } paths)
        {
            return;
        }

        string message = $"Discard the unstaged changes to {paths.Count} file(s)? This cannot be undone.";
        if (await new ConfirmWindow(message, "Discard").ShowDialog<bool>(this))
        {
            await _actions.DiscardChangesAsync(_repositoryPath, paths);
        }
    }

    private async Task DeleteUntrackedAsync()
    {
        if (UntrackedPaths(SelectedChanges(UnstagedList)) is not { Count: > 0 } paths)
        {
            return;
        }

        string message = $"Delete {paths.Count} untracked file(s) from disk? This cannot be undone.";
        if (await new ConfirmWindow(message, "Delete").ShowDialog<bool>(this))
        {
            await _actions.DeleteUntrackedAsync(_repositoryPath, paths);
        }
    }

    private async Task StashSelectedAsync()
    {
        List<string> paths = [.. SelectedChanges(UnstagedList).Select(change => change.Path)];
        if (paths.Count > 0)
        {
            await _actions.StashAsync(_repositoryPath, message: "", includeUntracked: true, keepIndex: false, paths);
        }
    }

    // As upstream's FormCommit: the "before commit" scripts can stop the commit; the "after commit" ones run once it is done.
    private async Task<bool> CommitChangesAsync()
    {
        // Upstream's FormCommit questions: amend rewrites history, and a commit with no branch checked out (and no rebase
        // under way) can be lost.
        if (AmendCheck.IsChecked == true &&
            !await ConfirmWindow.AskAsync(this, _preferences, Confirmation.Amend, Confirmations.AmendQuestion, "Amend",
                "Amend commit"))
        {
            return false;
        }

        // Upstream's IsCommitMessageValid: each failed check of the commit message settings asks whether to go on.
        foreach (string question in _useFormCommitMessage
                     ? CommitMessageValidation.Questions(CommitMessageBox.Text ?? "", _preferences.CommitValidation)
                     : [])
        {
            if (!await new ConfirmWindow(question, "Yes", CommitMessageValidation.Caption, cancelText: "No")
                    .ShowDialog<bool>(this))
            {
                return false;
            }
        }

        bool detached = _repository.CurrentBranch.Length == 0 && !_repository.IsRebasing;
        if (detached &&
            !await ConfirmWindow.AskAsync(this, _preferences, Confirmation.CommitWithoutBranch,
                Confirmations.NotOnBranchQuestion, "Continue", "Not on a branch"))
        {
            return false;
        }

        ScriptHost scripts = new(this, _actions, runPlugin: _runPlugin);
        if (!await scripts.RunEventAsync(ScriptEvent.BeforeCommit, _repositoryPath, []))
        {
            return false;
        }

        bool committed;
        if (_useFormCommitMessage)
        {
            // As upstream: the typed message is kept in the settings, so the "Commit message" menu of any repository
            // offers it.
            _preferences.LastCommitMessage = CommitMessageBox.Text ?? "";
            string message = CommitMessageFormat.Format(CommitMessageBox.Text ?? "",
                _preferences.EnsureCommitMessageSecondLineEmpty);
            committed = await _actions.CommitAsync(_repositoryPath, message, AmendCheck.IsChecked == true,
                SignOffCheck.IsChecked == true, CommitAuthorBox.Text ?? "");
        }
        else
        {
            committed = await _actions.CommitWithEditorAsync(_repositoryPath, AmendCheck.IsChecked == true,
                SignOffCheck.IsChecked == true, CommitAuthorBox.Text ?? "");
        }

        if (committed)
        {
            _messageBeforeAmend = "";
            CommitMessageBox.Text = "";
            AmendCheck.IsChecked = false;
            await _messages.ResetAsync(_repositoryPath);
            await scripts.RunEventAsync(ScriptEvent.AfterCommit, _repositoryPath, []);
        }

        return committed;
    }

    private async Task CommitAsync()
    {
        if (await CommitChangesAsync() && _preferences.CloseCommitDialogAfterCommit)
        {
            Close();
        }
    }

    // Pushes the current branch only when the commit succeeded, to the remote the branch tracks.
    private async Task CommitAndPushAsync()
    {
        string branch = _repository.CurrentBranch;

        // Read before the commit, which clears Amend. As upstream's FormCommit: an amended commit replaces the pushed one,
        // so with CommitAndPushForcedWhenAmend it is pushed with force-with-lease.
        bool forced = AmendCheck.IsChecked == true && _preferences.CommitAndPushForcedWhenAmend;
        if (branch.Length == 0 || !await CommitChangesAsync())
        {
            return;
        }

        string remote = _repository.TrackingRemote ?? DefaultRemote;
        bool pushed = forced
            ? await _actions.PushAsync(_repositoryPath,
                new PushRequest(remote, branch, branch, ForcePushOptions.ForceWithLease, Track: false))
            : await _actions.PushAsync(_repositoryPath, remote, branch);
        if (pushed && _preferences.CloseCommitDialogAfterCommit)
        {
            Close();
        }
    }

    // Upstream's ResetChanges (GitUICommands.StartResetChangesDialog): the question is about the unstaged files in both cases,
    // as upstream passes them for "Reset all changes" too.
    private async Task ResetChangesAsync(bool onlyWorkTree)
    {
        List<FileChange> unstaged = [.. _repository.Changes.Where(change => !change.Staged)];
        ResetChangesWindow.ResetAction action = await ResetChangesWindow.ShowResetDialogAsync(this,
            hasExistingFiles: unstaged.Any(change => !IsNew(change)), hasNewFiles: unstaged.Any(IsNew));
        if (action != ResetChangesWindow.ResetAction.Cancel)
        {
            await _actions.ResetAllChangesAsync(_repositoryPath,
                clean: action == ResetChangesWindow.ResetAction.ResetAndDelete, onlyWorkTree);
        }

        return;

        static bool IsNew(FileChange change) => change.Kind is ChangeKind.Untracked or ChangeKind.Added;
    }

    // Amend starts from the HEAD message; unchecking puts back what was typed before.
    private async Task ToggleAmendAsync()
    {
        if (_restoringAmend)
        {
            _messageBeforeAmend = CommitMessageBox.Text ?? "";
            return;
        }

        if (AmendCheck.IsChecked != true)
        {
            CommitMessageBox.Text = _messageBeforeAmend;
            return;
        }

        _messageBeforeAmend = CommitMessageBox.Text ?? "";
        try
        {
            CommitMessageBox.Text = await _repositoryService.GetHeadMessageAsync(_repositoryPath);
        }
        catch (Exception ex)
        {
            AmendCheck.IsChecked = false;
            ShowError(ex.Message);
        }
    }

    private MenuFlyout CommitTemplatesFlyout => (MenuFlyout)CommitTemplatesButton.Flyout!;

    private MenuFlyout CommitMessageFlyout => (MenuFlyout)CommitMessageMenuButton.Flyout!;

    // Upstream's CommitMessageToolStripMenuItemDropDownOpening, read each time the menu opens; the fixed items show at once and
    // the messages are put before them when git has listed them.
    private async Task ShowPreviousMessagesAsync()
    {
        int opening = ++_messageMenuOpenings;
        CommitMessageFlyout.Items.Clear();
        MenuItem submodules = new() { Header = SubmoduleChangesText };
        submodules.Click += (_, _) => Run(InsertSubmoduleChangesAsync);
        MenuItem onlyMine = new()
        {
            Header = ShowOnlyMyMessagesText,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = _preferences.CommitDialogShowOnlyMyMessages
        };
        onlyMine.Click += (_, _) => _preferences.CommitDialogShowOnlyMyMessages = onlyMine.IsChecked;
        CommitMessageFlyout.Items.Add(new Separator());
        CommitMessageFlyout.Items.Add(submodules);
        CommitMessageFlyout.Items.Add(onlyMine);

        int maxCount = _preferences.CommitDialogNumberOfPreviousMessages;
        IReadOnlyList<string> messages = PreviousCommitMessages.Merge(
            await PreviousCommitMessages.ReadAsync(_repositoryPath, maxCount,
                _preferences.CommitDialogShowOnlyMyMessages),
            _preferences.LastCommitMessage, maxCount);
        if (opening != _messageMenuOpenings)
        {
            return;
        }

        int index = 0;
        foreach (string message in messages)
        {
            MenuItem item = new() { Header = new TextBlock { Text = PreviousCommitMessages.Label(message) } };
            item.Click += (_, _) => ReplaceMessage(message.Trim());
            CommitMessageFlyout.Items.Insert(index++, item);
        }
    }

    // Upstream's generateListOfChangesInSubmodulesChangesToolStripMenuItem_Click, for the staged changes.
    private async Task InsertSubmoduleChangesAsync()
    {
        List<string> staged = [.. _repository.Changes.Where(change => change.Staged).Select(change => change.Path)];
        string message = await PreviousCommitMessages.SubmoduleChangesAsync(_repositoryPath, staged);
        if (message.Length > 0)
        {
            ReplaceMessage(message);
        }
    }

    private void ReplaceMessage(string message)
    {
        if (CommitMessageBox.Text != message)
        {
            SetMessage(message, message.Length);
        }
    }

    // Upstream's commitTemplatesToolStripMenuItem_DropDownOpening, read each time it opens: the plugins' templates (e.g.
    // the GitHub plugin's assigned issues), the user's own, the Conventional Commits items and the settings. A template
    // replaces the message.
    private void ShowCommitTemplates()
    {
        CommitTemplatesFlyout.Items.Clear();
        AddGroup(CommitTemplates.Registered());
        AddGroup(CommitTemplates.FromSettings(_preferences.CommitTemplates));
        _conventionalCommitsItem = ConventionalCommitsItem();
        CommitTemplatesFlyout.Items.Add(_conventionalCommitsItem);
        CommitTemplatesFlyout.Items.Add(new Separator());
        MenuItem settings = new() { Header = TemplateSettingsText };
        settings.Click += (_, _) => Run(() => new CommitTemplateSettingsWindow(_preferences).ShowDialog(this));
        CommitTemplatesFlyout.Items.Add(settings);

        void AddGroup(IReadOnlyList<CommitTemplate> templates)
        {
            foreach (CommitTemplate template in templates)
            {
                // The name as it is: a template named after an issue may have underscores, which are not access keys here.
                MenuItem item = new() { Header = new TextBlock { Text = template.Name } };
                item.Click += (_, _) =>
                {
                    CommitMessageBox.Text = CommitTemplates.Apply(template, _repository.CurrentBranch);
                    CommitMessageBox.Focus();
                };
                CommitTemplatesFlyout.Items.Add(item);
            }

            if (templates.Count > 0)
            {
                CommitTemplatesFlyout.Items.Add(new Separator());
            }
        }
    }

    // Upstream's AddConventionalCommitsItems: the commit types, the footers and the documentation link.
    private MenuItem ConventionalCommitsItem()
    {
        MenuItem conventional = new() { Header = ConventionalCommitText };
        foreach (string type in ConventionalCommits.HeaderTypes)
        {
            MenuItem item = new() { Header = new TextBlock { Text = type } };
            if (type == ConventionalCommits.Feat)
            {
                item.InputGesture = Hotkeys.Commit.GestureFor(CommitCommand.ConventionalCommitPrefixMessage);
            }

            item.Click += (_, _) =>
            {
                (string message, int caret) = ConventionalCommits.ApplyType(CommitMessageBox.Text ?? "",
                    CommitMessageBox.CaretIndex, type, _insertScopeParentheses);
                SetMessage(message, caret);
            };
            conventional.Items.Add(item);
        }

        conventional.Items.Add(new Separator());
        foreach (string keyword in ConventionalCommits.FooterKeywords)
        {
            conventional.Items.Add(FooterItem(keyword, $"{keyword}: "));
        }

        conventional.Items.Add(FooterItem(ConventionalCommits.SkipCi, ConventionalCommits.SkipCi));
        conventional.Items.Add(new Separator());
        MenuItem documentation = new() { Header = ConventionalCommitDocumentationText };
        documentation.Click += (_, _) =>
            Run(() => Launcher.LaunchUriAsync(new Uri(ConventionalCommits.DocumentationUrl)));
        conventional.Items.Add(documentation);
        return conventional;

        MenuItem FooterItem(string text, string footer)
        {
            MenuItem item = new() { Header = new TextBlock { Text = text } };
            item.Click += (_, _) =>
            {
                (string message, int caret) = ConventionalCommits.AppendFooter(CommitMessageBox.Text ?? "",
                    CommitMessageBox.CaretIndex, footer);
                SetMessage(message, caret);
            };
            return item;
        }
    }

    // Upstream's OpenConventionalCommitMenu (Ctrl+T, and Ctrl+Shift+T with a scope): the menu opens at its "feat" item.
    private bool OpenConventionalCommitMenu(bool insertScope)
    {
        _insertScopeParentheses = insertScope;
        CommitTemplatesFlyout.ShowAt(CommitTemplatesButton);
        if (_conventionalCommitsItem is null)
        {
            return true;
        }

        _conventionalCommitsItem.Open();
        _conventionalCommitsItem.Items.OfType<MenuItem>()
            .FirstOrDefault(item => item.Header is TextBlock { Text: ConventionalCommits.Feat })?.Focus();
        return true;
    }

    private void SetMessage(string message, int caret)
    {
        CommitMessageBox.Text = message;
        CommitMessageBox.CaretIndex = Math.Min(caret, message.Length);
        CommitMessageBox.Focus();
    }

    // Upstream FormCommit_FormatAllText: the commit message settings shape the message while it is typed.
    private void FormatMessage()
    {
        CommitValidationOptions options = _preferences.CommitValidation;
        if (_formatting || !(options.SecondLineMustBeEmpty || (options.AutoWrap && options.MaxLineLength > 0)))
        {
            return;
        }

        string text = CommitMessageBox.Text ?? "";
        (string formatted, int caret) = CommitMessageValidation.Format(text, CommitMessageBox.CaretIndex, options);
        if (formatted == text)
        {
            return;
        }

        _formatting = true;
        CommitMessageBox.Text = formatted;
        CommitMessageBox.CaretIndex = caret;
        _formatting = false;
    }

    // Upstream's message box menu: its "Word wrap (except subject line)" item before the editing items.
    private void AddMessageMenu()
    {
        MenuItem wordWrap = new() { Header = WordWrapBodyText };
        wordWrap.Click += (_, _) => CommitMessageBox.Text =
            CommitMessageValidation.WrapBody(CommitMessageBox.Text ?? "", _preferences.CommitValidation.MaxLineLength);
        MenuItem cut = new() { Header = "Cut", InputGesture = new KeyGesture(Key.X, Hotkeys.CommandModifier) };
        cut.Click += (_, _) => CommitMessageBox.Cut();
        MenuItem copy = new() { Header = "Copy", InputGesture = new KeyGesture(Key.C, Hotkeys.CommandModifier) };
        copy.Click += (_, _) => CommitMessageBox.Copy();
        MenuItem paste = new() { Header = "Paste", InputGesture = new KeyGesture(Key.V, Hotkeys.CommandModifier) };
        paste.Click += (_, _) => CommitMessageBox.Paste();
        CommitMessageBox.ContextFlyout = new MenuFlyout
        {
            Items =
            {
                wordWrap,
                AutoCompletionMenuItem(),
                new Separator(),
                cut,
                copy,
                paste
            }
        };
    }
}
