using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using GitExtUtils.GitUI.Theming;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormRebase</c> in the new shell (rules in <see cref="RebaseDialog"/>, the commit list in
///  <see cref="RebasePatches"/>): rebases the current branch on a branch, tag or commit with upstream's options and
///  specific range, and while a rebase is in progress shows its commits and offers Commit, Edit todo, Skip, Solve
///  conflicts, Continue and Abort. Closes with true when the rebase is done or aborted.
/// </summary>
public partial class RebaseWindow : Window
{
    private const string AutosquashSetting = "rebase.autosquash";
    private const string UpdateRefsSetting = "rebase.updaterefs";
    private const string HelpDialogId = "Rebase";
    private const string HelpImageName = "HelpCommandRebase";

    // Upstream's OtherColors.MergeConflictsColor, its MergeToolPanel colors (yellow, or 136,136,0 in dark mode) and the
    // OrangeRed of the commit being applied.
    private static readonly IBrush _conflictsBrush = new SolidColorBrush(Color.FromRgb(230, 99, 99));

    private readonly string _repositoryPath;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;
    private readonly string? _onto;
    private readonly string? _defaultTo;
    private readonly bool _startRebaseImmediately;
    private readonly Action<Window> _showCommit;
    private readonly Action<Window> _showConflicts;
    private readonly GitModule _module;
    private readonly RebaseDialog _dialog = new();
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IReadOnlyList<RebasePatch> _patches = [];
    private bool _supportUpdateRefs;
    private bool _configuredUpdateRefs;
    private bool _busy;
    private bool _showing;

    /// <param name="from">Upstream's specific range start (excluded); empty for none.</param>
    /// <param name="to">The branch the specific range ends at; null for the current one.</param>
    /// <param name="onto">The branch or commit to rebase on; null for none.</param>
    /// <param name="interactive">Upstream's "Interactive Rebase" to start with.</param>
    /// <param name="startRebaseImmediately">Upstream's: rebase at once, as the grid's "Selected commit" does.</param>
    /// <param name="showCommit">Opens the commit window over this one.</param>
    /// <param name="showConflicts">Opens the conflicts window over this one.</param>
    public RebaseWindow(string repositoryPath, RepositoryOperationsViewModel actions, IAppPreferences preferences,
        string? from, string? to, string? onto, bool interactive, bool startRebaseImmediately, Action<Window> showCommit,
        Action<Window> showConflicts)
    {
        _repositoryPath = repositoryPath;
        _actions = actions;
        _preferences = preferences;
        _onto = onto;
        _defaultTo = to;
        _startRebaseImmediately = startRebaseImmediately;
        _showCommit = showCommit;
        _showConflicts = showConflicts;
        _module = GitModules.Open(repositoryPath);
        InitializeComponent();
        HelpImage.Load(preferences, HelpDialogId);
        HelpImage.NoticeText = UpstreamTranslation.ByText(RebaseDialog.HoverShowImageText);
        HelpImage.Image1 = UpstreamImages.Named(HelpImageName);
        UnresolvedConflictsButton.Background = _conflictsBrush;
        if (preferences.AlwaysShowAdvOpt)
        {
            ShowOptions();
        }

        _dialog.From = from ?? "";
        _dialog.Interactive = interactive;
        _dialog.SpecificRange = !string.IsNullOrEmpty(from);
        FromBox.Text = _dialog.From;

        InteractiveCheck.IsCheckedChanged += (_, _) => Edit(() => _dialog.Interactive = InteractiveCheck.IsChecked == true);
        PreserveMergesCheck.IsCheckedChanged +=
            (_, _) => Edit(() => _dialog.PreserveMerges = PreserveMergesCheck.IsChecked == true);
        AutosquashCheck.IsCheckedChanged += (_, _) => Edit(() => _dialog.AutoSquash = AutosquashCheck.IsChecked == true);
        AutoStashCheck.IsCheckedChanged += (_, _) => Edit(() => _dialog.AutoStash = AutoStashCheck.IsChecked == true);
        IgnoreDateCheck.IsCheckedChanged += (_, _) => Edit(() => _dialog.IgnoreDate = IgnoreDateCheck.IsChecked == true);
        CommitterDateCheck.IsCheckedChanged +=
            (_, _) => Edit(() => _dialog.CommitterDateIsAuthorDate = CommitterDateCheck.IsChecked == true);
        UpdateRefsCheck.IsCheckedChanged += (_, _) => Edit(() => _dialog.UpdateRefs = UpdateRefsCheck.IsChecked == true);
        SpecificRangeCheck.IsCheckedChanged +=
            (_, _) => Edit(() => _dialog.SpecificRange = SpecificRangeCheck.IsChecked == true);
        ShowOptionsLink.Click += (_, _) => ShowOptions();
        RebaseButton.Click += (_, _) => Run(RebaseAsync);
        ContinueButton.Click += (_, _) => Run(ContinueAsync);
        SkipButton.Click += (_, _) => Run(SkipAsync);
        AbortButton.Click += (_, _) => Run(AbortAsync);
        EditTodoButton.Click += (_, _) => Run(EditTodoAsync);
        CommitButton.Click += (_, _) => _showCommit(this);
        SolveConflictsButton.Click += (_, _) => _showConflicts(this);
        UnresolvedConflictsButton.Click += (_, _) => _showConflicts(this);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close(false);
            }
        };
        Opened += (_, _) => Run(LoadAsync);

        // Upstream refreshes after its modal commit and conflicts dialogs; here the windows are not modal, so the state is
        // read again when this window is active again.
        Activated += (_, _) =>
        {
            if (_loaded.Task.IsCompleted && !_busy)
            {
                Run(RefreshStateAsync);
            }
        };
        ShowOptionsState();
    }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public RebaseDialog Dialog => _dialog;

    /// <summary>
    ///  Completes when the branches, the options and the rebase state are shown, for tests.
    /// </summary>
    public Task RebaseLoaded => _loaded.Task;

    /// <summary>
    ///  The rows of the commit list, for tests.
    /// </summary>
    public IReadOnlyList<RebasePatch> Patches => _patches;

    // Upstream's OnShown: the current branch, the refs to rebase on (none when rebasing at once), the branches for the
    // range's end, git's autosquash and update refs settings and the kept "Auto stash"; then the rebase at once or the
    // options shown.
    private async Task LoadAsync()
    {
        bool immediately = _startRebaseImmediately;
        (string head, IReadOnlyList<string> refs, IReadOnlyList<string> heads, bool inRebase, bool autosquash,
            bool supportUpdateRefs, bool updateRefs) = await Task.Run(() =>
        {
            IReadOnlyList<IGitRef> gitRefs = immediately
                ? []
                : _module.GetRefs(RefsFilter.Heads | RefsFilter.Remotes | RefsFilter.Tags);
            bool support = _module.GitVersion.SupportUpdateRefs;
            return (_module.GetSelectedBranch(), (IReadOnlyList<string>)[.. gitRefs.Select(gitRef => gitRef.Name)],
                (IReadOnlyList<string>)[.. gitRefs.Where(gitRef => gitRef.IsHead).Select(gitRef => gitRef.Name)],
                _module.InTheMiddleOfRebase(), _module.GetEffectiveSetting<bool>(AutosquashSetting) is true, support,
                support && _module.GetEffectiveSetting<bool>(UpdateRefsSetting) is true);
        });
        _supportUpdateRefs = supportUpdateRefs;
        _configuredUpdateRefs = updateRefs;
        UpdateRefsCheck.IsVisible = supportUpdateRefs;
        CurrentBranchText.Text = head;
        BranchesBox.ItemsSource = refs;
        if (_onto is not null)
        {
            BranchesBox.Text = _onto;
        }

        BranchesBox.Focus();
        ToBox.ItemsSource = heads;
        ToBox.Text = _defaultTo ?? head;
        RebasePanel.IsVisible = !inRebase;
        await RefreshStateAsync();
        _dialog.AutoSquash = autosquash;
        _dialog.UpdateRefs = updateRefs;
        _dialog.AutoStash = _preferences.RebaseAutoStash;
        ShowOptionsState();
        _loaded.TrySetResult();
        if (immediately)
        {
            await RebaseAsync();
        }
        else
        {
            ShowOptions();
        }
    }

    private void ShowOptions()
    {
        ShowOptionsLink.IsVisible = false;
        OptionsTopPanel.IsVisible = true;
        OptionsBottomPanel.IsVisible = true;
    }

    private void Edit(Action change)
    {
        if (_showing)
        {
            return;
        }

        change();
        ShowOptionsState();
    }

    private void ShowOptionsState()
    {
        _showing = true;
        InteractiveCheck.IsChecked = _dialog.Interactive;
        InteractiveCheck.IsEnabled = _dialog.CanInteractive;
        PreserveMergesCheck.IsChecked = _dialog.PreserveMerges;
        PreserveMergesCheck.IsEnabled = _dialog.CanPreserveMerges;
        AutosquashCheck.IsChecked = _dialog.AutoSquash;
        AutosquashCheck.IsEnabled = _dialog.CanAutoSquash;
        AutoStashCheck.IsChecked = _dialog.AutoStash;
        IgnoreDateCheck.IsChecked = _dialog.IgnoreDate;
        IgnoreDateCheck.IsEnabled = _dialog.CanIgnoreDate;
        CommitterDateCheck.IsChecked = _dialog.CommitterDateIsAuthorDate;
        CommitterDateCheck.IsEnabled = _dialog.CanCommitterDateIsAuthorDate;
        UpdateRefsCheck.IsChecked = _dialog.UpdateRefs;
        SpecificRangeCheck.IsChecked = _dialog.SpecificRange;
        FromBox.IsEnabled = ToBox.IsEnabled = _dialog.SpecificRange;
        _showing = false;
    }

    // Upstream's EnableButtons and PatchGrid.Initialize.
    private async Task RefreshStateAsync()
    {
        List<RebasePatch> skipped = [.. RebasePatches.Skipped];
        (bool inRebase, bool conflicted, bool dirty, IReadOnlyList<RebasePatch> patches) = await Task.Run(() =>
        {
            bool rebasing = _module.InTheMiddleOfRebase();
            return (rebasing, _module.InTheMiddleOfConflictedMerge(), !rebasing && _module.IsDirtyDir(),
                RebasePatches.Read(_module, skipped));
        });
        RebaseButtons buttons = RebaseDialog.Buttons(inRebase, conflicted, dirty);
        BranchesBox.IsEnabled = buttons.CanChangeBranch;
        RebaseButton.IsVisible = buttons.Rebase;
        AutoStashCheck.IsEnabled = buttons.AutoStash;
        CommitButton.IsVisible = EditTodoButton.IsVisible = SkipButton.IsVisible = AbortButton.IsVisible = buttons.Steps;
        ContinueButton.IsVisible = buttons.Continue;
        SolveConflictsButton.IsVisible = buttons.SolveConflicts;
        UnresolvedConflictsButton.IsVisible = buttons.UnresolvedConflicts;
        RebaseButton.IsDefault = buttons.Default == RebaseDefaultButton.Rebase;
        ContinueButton.IsDefault = buttons.Default == RebaseDefaultButton.Continue;
        SolveConflictsButton.IsDefault = buttons.Default == RebaseDefaultButton.SolveConflicts;
        ContinueText.Text = AccessKeyText(buttons.Default == RebaseDefaultButton.Continue
            ? RebaseDialog.ContinueRebaseHighlightedText
            : RebaseDialog.ContinueRebaseText);
        SolveConflictsText.Text = AccessKeyText(buttons.Default == RebaseDefaultButton.SolveConflicts
            ? RebaseDialog.SolveConflictsHighlightedText
            : RebaseDialog.SolveConflictsText);
        MergeToolPanel.Background = conflicted
            ? new SolidColorBrush(ThemeBrushes.Current.IsDark
                ? Color.FromRgb(136, 136, 0)
                : ToAvalonia(System.Drawing.Color.Yellow.AdaptBackColor()))
            : Brushes.Transparent;
        if (buttons.Default == RebaseDefaultButton.Continue)
        {
            ContinueButton.Focus();
        }
        else if (buttons.Default == RebaseDefaultButton.SolveConflicts)
        {
            SolveConflictsButton.Focus();
        }

        ShowPatches(patches);
    }

    // Upstream's DisplayPatches: the commit being applied selected, in OrangeRed.
    private void ShowPatches(IReadOnlyList<RebasePatch> patches)
    {
        _patches = patches;
        IBrush applying = new SolidColorBrush(ToAvalonia(System.Drawing.Color.OrangeRed.AdaptForeColor(
            ThemeBrushes.Current.IsDark ? System.Drawing.Color.FromArgb(0x20, 0x20, 0x20) : System.Drawing.Color.White)));
        List<RebasePatchRow> rows = [.. patches.Select(patch => new RebasePatchRow(patch, patch.IsNext ? applying : null))];
        PatchesList.ItemsSource = rows;
        PatchesList.SelectedItem = rows.FirstOrDefault(row => row.Patch.IsNext);
        if (PatchesList.SelectedItem is { } current)
        {
            PatchesList.ScrollIntoView(current);
        }
    }

    // Upstream's OkClick: the check, the kept "Auto stash", the rebase with git's output; the window closes unless the
    // rebase stopped, and continues by itself when git reused a recorded resolution.
    private async Task RebaseAsync()
    {
        _dialog.Onto = BranchesBox.Text ?? "";
        _dialog.From = FromBox.Text ?? "";
        _dialog.To = ToBox.Text ?? "";
        if (_dialog.Validate() is { } error)
        {
            await new MessageBoxWindow(UpstreamTranslation.ByText(error),
                UpstreamTranslation.ByText(RebaseDialog.NoBranchSelectedCaption), MessageBoxButtons.OK).ShowDialog(this);
            return;
        }

        _preferences.RebaseAutoStash = _dialog.AutoStash;
        _preferences.Save();
        RebasePatches.Skipped.Clear();
        await RunStepAsync(() => _actions.RebaseAsync(_repositoryPath, _dialog.Onto,
            _dialog.Arguments(_supportUpdateRefs, _configuredUpdateRefs), _dialog.Interactive),
            rebaseOnly: false, output: true);
    }

    // Upstream's ResolvedClick.
    private Task ContinueAsync()
        => RunStepAsync(() => _actions.ContinueRebaseAsync(_repositoryPath), rebaseOnly: true, output: false);

    // Upstream's SkipClick: the commit being applied is marked skipped first.
    private Task SkipAsync()
    {
        if (_patches.FirstOrDefault(patch => patch.IsNext) is { } applying)
        {
            applying.IsSkipped = true;
            RebasePatches.Skipped.Add(applying);
        }

        return RunStepAsync(() => _actions.SkipRebaseAsync(_repositoryPath), rebaseOnly: true, output: false);
    }

    // Upstream's AbortClick and EditTodoClick: the skipped list is cleared when the rebase is over.
    private Task AbortAsync()
        => RunStepAsync(() => _actions.AbortRebaseAsync(_repositoryPath), rebaseOnly: true, output: false,
            clearSkipped: true);

    private Task EditTodoAsync()
        => RunStepAsync(() => _actions.EditRebaseTodoAsync(_repositoryPath), rebaseOnly: true, output: false,
            clearSkipped: true);

    // Upstream closes after the rebase unless an action or patch is in progress, and after the other steps unless the rebase
    // is.
    private async Task RunStepAsync(Func<Task<bool>> step, bool rebaseOnly, bool output, bool clearSkipped = false)
    {
        _busy = true;
        try
        {
            await step();
            string text = output ? string.Join(Environment.NewLine, _actions.OutputLines) : "";
            (bool inAction, bool canContinue) = await Task.Run(() =>
                (rebaseOnly
                    ? _module.InTheMiddleOfRebase()
                    : _module.InTheMiddleOfAction() || _module.InTheMiddleOfPatch(),
                    RebaseDialog.CanContinue(text, _module)));
            if (!inAction)
            {
                if (clearSkipped)
                {
                    RebasePatches.Skipped.Clear();
                }

                Close(true);
                return;
            }

            await RefreshStateAsync();
            if (canContinue)
            {
                await ContinueAsync();
            }
        }
        finally
        {
            _busy = false;
        }
    }

    private static string AccessKeyText(string text) => UpstreamTranslation.ByText(text).Replace('&', '_');

    private static Color ToAvalonia(System.Drawing.Color color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}

/// <summary>
///  A row of the rebase window's commit list: the commit, its subject on one line, and the color of the one being applied.
/// </summary>
public sealed record RebasePatchRow(RebasePatch Patch, IBrush? Foreground)
{
    public string Subject => (Patch.Subject ?? "").ReplaceLineEndings(" ").Trim();
}
