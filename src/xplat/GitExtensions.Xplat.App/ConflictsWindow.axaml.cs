using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Lists the conflicted files and resolves them: the new shell's version of upstream <c>FormResolveConflicts</c>. It works on
///  the main window's repository state, so the list shrinks as files are resolved.
/// </summary>
public partial class ConflictsWindow : Window
{
    private readonly string _repositoryPath;
    private readonly RepositoryViewModel _repository;
    private readonly RepositoryOperationsViewModel _actions;

    public ConflictsWindow(string repositoryPath, RepositoryViewModel repository, RepositoryOperationsViewModel actions)
    {
        _repositoryPath = repositoryPath;
        _repository = repository;
        _actions = actions;
        InitializeComponent();
        WindowPlacementTracker.Attach(this, "Xplat.ConflictsWindow");
        ConflictList.SelectionChanged += (_, _) => OnSelectionChanged();
        MergeToolButton.Click +=
            (_, _) => RunOnSelected(paths => _actions.RunMergeToolAsync(_repositoryPath, paths[0]));
        UseOursButton.Click += (_, _) =>
            RunOnSelected(paths => _actions.ResolveConflictsAsync(_repositoryPath, paths, ours: true));
        UseTheirsButton.Click += (_, _) =>
            RunOnSelected(paths => _actions.ResolveConflictsAsync(_repositoryPath, paths, ours: false));
        MarkResolvedButton.Click +=
            (_, _) => RunOnSelected(paths => _actions.MarkResolvedAsync(_repositoryPath, paths));
        KeyDown += OnWindowKeyDown;
        _repository.PropertyChanged += OnRepositoryChanged;
        _actions.PropertyChanged += OnActionsChanged;
        Closed += (_, _) =>
        {
            _repository.PropertyChanged -= OnRepositoryChanged;
            _actions.PropertyChanged -= OnActionsChanged;
        };
        ShowConflicts();
    }

    /// <summary>
    ///  The conflicted paths on show.
    /// </summary>
    public IReadOnlyList<string> Conflicts { get; private set; } = [];

    // Upstream FormResolveConflicts' hotkeys: M merge tool, L ours (local), R theirs (remote), F5 rescan.
    private void OnWindowKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        Button? button = Hotkeys.Conflicts.Match(e) switch
        {
            ConflictsCommand.Merge => MergeToolButton,
            ConflictsCommand.ChooseLocal => UseOursButton,
            ConflictsCommand.ChooseRemote => UseTheirsButton,
            _ => null,
        };
        if (button is { IsEnabled: true })
        {
            button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            e.Handled = true;
        }
        else if (Hotkeys.Conflicts.Match(e) == ConflictsCommand.Rescan)
        {
            UiActions.Run(() => _repository.RefreshAsync(_repositoryPath), _ => { });
            e.Handled = true;
        }
    }

    private void OnRepositoryChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RepositoryViewModel.Changes))
        {
            ShowConflicts();
        }
    }

    private void OnActionsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RepositoryOperationsViewModel.IsBusy))
        {
            UpdateButtons();
        }
    }

    private void ShowConflicts()
    {
        Conflicts =
        [
            .. _repository.Changes
                .Where(change => change.Kind == ChangeKind.Conflict)
                .Select(change => change.Path)
                .Distinct(StringComparer.Ordinal)
        ];
        ConflictList.ItemsSource = Conflicts;
        HeaderText.Text = Conflicts.Count == 1 ? "1 unresolved file" : $"{Conflicts.Count} unresolved files";
        DoneText.Text = Conflicts.Count > 0 ? ""
            : _repository.IsRebasing ? "All conflicts are resolved. Continue the rebase from the main window."
            : _repository.IsMerging ? "All conflicts are resolved. Commit to complete the merge."
            : "There are no conflicts.";
        UpdateButtons();
    }

    private List<string> SelectedPaths() => ConflictList.SelectedItems?.OfType<string>().ToList() ?? [];

    private void OnSelectionChanged()
    {
        UpdateButtons();
        if (SelectedPaths() is [{ } path])
        {
            UiActions.Run(() => Diff.ShowAsync(_repositoryPath, commitHash: null, path, staged: false, path),
                ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
        }
    }

    private void UpdateButtons()
    {
        int selected = SelectedPaths().Count;
        bool idle = !_actions.IsBusy;
        MergeToolButton.IsEnabled = idle && selected == 1;
        UseOursButton.IsEnabled = idle && selected > 0;
        UseTheirsButton.IsEnabled = idle && selected > 0;
        MarkResolvedButton.IsEnabled = idle && selected > 0;
    }

    private void RunOnSelected(Func<IReadOnlyList<string>, Task<bool>> action)
    {
        if (SelectedPaths() is { Count: > 0 } paths)
        {
            Diff.Clear();
            UiActions.Run(() => action(paths), ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
        }
    }
}
