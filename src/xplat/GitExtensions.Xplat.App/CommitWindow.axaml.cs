using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GitCommands.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
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
        CommitAndPushButton.IsVisible = preferences.ShowCommitAndPush;

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
        CommitButton.Click += (_, _) => Run(CommitAsync);
        CommitAndPushButton.Click += (_, _) => Run(CommitAndPushAsync);
        AmendCheck.IsCheckedChanged += (_, _) => Run(ToggleAmendAsync);
        CommitTemplatesFlyout.Opening += (_, _) => ShowCommitTemplates();
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
        if (string.IsNullOrEmpty(CommitMessageBox.Text))
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

    private void ShowError(string message) => _ = new ErrorWindow(message).ShowDialog(this);

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
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
            Run(() => new ScriptHost(this, _actions, runPlugin: _runPlugin).RunAsync(script, _repositoryPath, []));
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

    private async Task OpenDiffToolAsync()
    {
        if (_diffChange is { } change)
        {
            await _actions.RunDiffToolAsync(_repositoryPath, change.Path, commit: null, _diffStaged);
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

        string message = CommitMessageFormat.Format(CommitMessageBox.Text ?? "",
            _preferences.EnsureCommitMessageSecondLineEmpty);
        bool committed = await _actions.CommitAsync(_repositoryPath, message,
            AmendCheck.IsChecked == true,
            SignOffCheck.IsChecked == true, CommitAuthorBox.Text ?? "");
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

    // Upstream's commitTemplatesToolStripMenuItem_DropDownOpening, read each time it opens: the plugins' templates (e.g.
    // the GitHub plugin's assigned issues), then the user's own. A template replaces the message. Not ported: the
    // conventional commit items and the template settings window.
    private void ShowCommitTemplates()
    {
        CommitTemplatesFlyout.Items.Clear();
        IReadOnlyList<CommitTemplate> registered = CommitTemplates.Registered();
        IReadOnlyList<CommitTemplate> own = CommitTemplates.FromSettings();
        foreach (CommitTemplate template in registered)
        {
            CommitTemplatesFlyout.Items.Add(TemplateItem(template));
        }

        if (registered.Count > 0 && own.Count > 0)
        {
            CommitTemplatesFlyout.Items.Add(new Separator());
        }

        foreach (CommitTemplate template in own)
        {
            CommitTemplatesFlyout.Items.Add(TemplateItem(template));
        }

        if (CommitTemplatesFlyout.Items.Count == 0)
        {
            CommitTemplatesFlyout.Items.Add(new MenuItem { Header = "(none)", IsEnabled = false });
        }

        MenuItem TemplateItem(CommitTemplate template)
        {
            // The name as it is: a template named after an issue may have underscores, which are not access keys here.
            MenuItem item = new() { Header = new TextBlock { Text = template.Name } };
            item.Click += (_, _) =>
            {
                CommitMessageBox.Text = CommitTemplates.Apply(template, _repository.CurrentBranch);
                CommitMessageBox.Focus();
            };
            return item;
        }
    }
}
