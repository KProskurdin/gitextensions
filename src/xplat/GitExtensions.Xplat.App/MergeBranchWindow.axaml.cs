using Avalonia.Controls;
using Avalonia.Input;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormMergeBranch</c> in the new shell (rules in <see cref="MergeBranchDialog"/>): merges a branch, remote
///  branch or tag into the current branch, fast-forward or with a merge commit, optionally without committing, with the
///  advanced options; a merge that stops on conflicts offers the conflicts window, as upstream's
///  <c>MergeConflictHandler</c>. Closes with true after merging (or stopping on conflicts).
/// </summary>
public partial class MergeBranchWindow : Window
{
    private readonly string _repositoryPath;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;
    private readonly string? _defaultBranch;
    private readonly Func<string, Func<Task<bool>>, Task<bool>> _withScripts;
    private readonly Action _showConflicts;
    private readonly GitModule _module;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private MergeBranchDialog _dialog = new(new MergeRepositorySettings(false, false, 20), noCommit: false,
        advanced: false);

    private bool _showing;

    /// <param name="defaultBranch">The branch to merge; null for the branch the current one tracks.</param>
    /// <param name="withScripts">Runs the merge between the before and after merge scripts.</param>
    /// <param name="showConflicts">Opens the conflicts window.</param>
    public MergeBranchWindow(string repositoryPath, RepositoryOperationsViewModel actions, IAppPreferences preferences,
        string? defaultBranch, Func<string, Func<Task<bool>>, Task<bool>> withScripts, Action showConflicts)
    {
        _repositoryPath = repositoryPath;
        _actions = actions;
        _preferences = preferences;
        _defaultBranch = defaultBranch;
        _withScripts = withScripts;
        _showConflicts = showConflicts;
        _module = GitModules.Open(repositoryPath);
        InitializeComponent();
        HelpImage.Load(preferences, "MergeBranches");
        HelpImage.NoticeText = UpstreamTranslation.ByText(MergeBranchDialog.HoverShowImageText);
        HelpImage.Image1 = UpstreamImages.Named("HelpCommandMerge");
        HelpImage.Image2 = UpstreamImages.Named("HelpCommandMergeFastForward");
        HelpImage.IsOnHoverShowImage2 = true;

        FastForwardRadio.IsCheckedChanged += (_, _) => Edit(() =>
        {
            _dialog.NoFastForward = FastForwardRadio.IsChecked != true;
            HelpImage.IsOnHoverShowImage2 = !_dialog.NoFastForward;
        });
        NoCommitCheck.IsCheckedChanged += (_, _) => Edit(() => _dialog.NoCommit = NoCommitCheck.IsChecked == true);
        AdvancedCheck.IsCheckedChanged += (_, _) => Edit(() => _dialog.Advanced = AdvancedCheck.IsChecked == true);
        UseStrategyCheck.IsCheckedChanged +=
            (_, _) => Edit(() => _dialog.UseStrategy = UseStrategyCheck.IsChecked == true);
        StrategyBox.SelectionChanged += (_, _) => Edit(() =>
            _dialog.Strategy = (StrategyBox.SelectedItem as ComboBoxItem)?.Content as string ?? "");
        SquashCheck.IsCheckedChanged += (_, _) => Edit(() => _dialog.Squash = SquashCheck.IsChecked == true);
        AllowUnrelatedCheck.IsCheckedChanged += (_, _) =>
            Edit(() => _dialog.AllowUnrelatedHistories = AllowUnrelatedCheck.IsChecked == true);
        AddLogMessagesCheck.IsCheckedChanged += (_, _) =>
            Edit(() => _dialog.AddLogMessages = AddLogMessagesCheck.IsChecked == true);
        LogMessagesCountBox.ValueChanged += (_, _) =>
            Edit(() => _dialog.LogMessagesCount = (int)(LogMessagesCountBox.Value ?? 0));
        AddMergeMessageCheck.IsCheckedChanged += (_, _) =>
            Edit(() => _dialog.AddMergeMessage = AddMergeMessageCheck.IsChecked == true);
        StrategyHelpLink.Click += (_, _) =>
            OsShellUtil.OpenUrlInDefaultBrowser(GitUI.UserManual.UserManual.UrlFor("branches", "advanced-merge-options"));
        MergeButton.Click += (_, _) => Run(MergeAsync);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close(false);
            }
        };
        Opened += (_, _) => Run(LoadAsync);
    }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public MergeBranchDialog Dialog => _dialog;

    /// <summary>
    ///  Completes when the branches and settings are read, for tests.
    /// </summary>
    public Task MergeLoaded => _loaded.Task;

    // Upstream's constructor and FormMergeBranchLoad: the settings, the refs to offer and the branch to propose.
    private async Task LoadAsync()
    {
        (string current, IReadOnlyList<string> refs, string? proposed, MergeRepositorySettings settings) =
            await Task.Run(() =>
            {
                string head = _module.GetSelectedBranch();
                IReadOnlyList<string> names =
                [
                    .. _module.GetRefs(RefsFilter.Heads | RefsFilter.Remotes | RefsFilter.Tags)
                        .Select(gitRef => gitRef.Name)
                ];
                string? branch = _defaultBranch ?? _module.GetRemoteBranch(head);
                return (head, names, string.IsNullOrEmpty(branch) ? null : branch,
                    AppServices.MergeSettings.Load(_module));
            });
        _dialog = new MergeBranchDialog(settings, _preferences.DontCommitMerge, _preferences.AlwaysShowAdvOpt)
        {
            Branch = proposed ?? "",
        };
        CurrentBranchText.Text = current;
        BranchesBox.ItemsSource = refs;
        BranchesBox.Text = _dialog.Branch;
        ShowState();
        BranchesBox.Focus();
        _loaded.TrySetResult();
    }

    private void Edit(Action change)
    {
        if (_showing)
        {
            return;
        }

        change();
        ShowState();
    }

    private void ShowState()
    {
        _showing = true;
        FastForwardRadio.IsChecked = !_dialog.NoFastForward;
        NoFastForwardRadio.IsChecked = _dialog.NoFastForward;
        NoCommitCheck.IsChecked = _dialog.NoCommit;
        AdvancedCheck.IsChecked = _dialog.Advanced;
        AdvancedPanel.IsVisible = _dialog.Advanced;
        UseStrategyCheck.IsChecked = _dialog.UseStrategy;
        StrategyBox.IsVisible = StrategyHelpLink.IsVisible = _dialog.UseStrategy;
        if (_dialog.Strategy.Length == 0)
        {
            StrategyBox.SelectedItem = null;
        }

        SquashCheck.IsChecked = _dialog.Squash;
        SquashCheck.IsEnabled = _dialog.CanSquash;
        AllowUnrelatedCheck.IsChecked = _dialog.AllowUnrelatedHistories;
        AddLogMessagesCheck.IsChecked = _dialog.AddLogMessages;
        LogMessagesCountBox.Value = _dialog.LogMessagesCount;
        LogMessagesCountBox.IsEnabled = _dialog.AddLogMessages;
        AddMergeMessageCheck.IsChecked = _dialog.AddMergeMessage;
        MergeMessageBox.IsEnabled = _dialog.AddMergeMessage;
        _showing = false;
    }

    // Upstream's OkClick: the settings kept, the merge message written, the merge run; a merge that stops on conflicts
    // offers the conflicts window and closes too.
    private async Task MergeAsync()
    {
        _dialog.Branch = BranchesBox.Text ?? "";
        _dialog.MergeMessage = MergeMessageBox.Text ?? "";
        MergeRepositorySettings settings = _dialog.RepositorySettings;
        await Task.Run(() => AppServices.MergeSettings.Save(_module, settings));
        _preferences.DontCommitMerge = _dialog.NoCommit;
        _preferences.Save();

        string? messagePath = _dialog.AddMergeMessage
            ? await MergeBranchDialog.WriteMergeMessageAsync(_module, _dialog.MergeMessage)
            : null;
        bool merged = await _withScripts(_repositoryPath, () => _actions.MergeBranchAsync(_repositoryPath,
            _dialog.Branch.Trim(),
            _dialog.Arguments(messagePath, _module.GetPathForGitExecution)));
        if (merged)
        {
            Close(true);
            return;
        }

        if (await Task.Run(() => _module.InTheMiddleOfConflictedMerge()))
        {
            if (await ConfirmWindow.AskAsync(this, _preferences, Confirmation.ResolveConflicts,
                    UpstreamTranslation.ByText(Confirmations.ResolveConflictsQuestion), "Yes",
                    UpstreamTranslation.ByText(Confirmations.ResolveConflictsCaption)))
            {
                _showConflicts();
            }

            Close(true);
        }
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}
