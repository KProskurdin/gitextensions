using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormRebase</c> (rules in <see cref="RebaseDialog"/>, the commit list in
///  <see cref="RebasePatches"/>): rebases the current branch on a branch, tag or commit with upstream's options and
///  specific range, and while a rebase is in progress shows its commits and offers Commit, Edit todo, Skip, Solve
///  conflicts, Continue and Abort. Closes its window with true when the rebase is done or aborted.
/// </summary>
public sealed partial class RebaseViewModel : DialogViewModel
{
    private const string AutosquashSetting = "rebase.autosquash";
    private const string UpdateRefsSetting = "rebase.updaterefs";

    private readonly string _repositoryPath;
    private readonly GitModule _module;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;
    private readonly string? _onto;
    private readonly string? _defaultTo;
    private readonly bool _startRebaseImmediately;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _supportUpdateRefs;
    private bool _configuredUpdateRefs;
    private bool _busy;
    private bool _showing;

    /// <param name="module">The repository.</param>
    /// <param name="actions">Runs the rebase and its steps.</param>
    /// <param name="preferences">Upstream's advanced options and the kept "Auto stash".</param>
    /// <param name="from">Upstream's specific range start (excluded); empty for none.</param>
    /// <param name="to">The branch the specific range ends at; null for the current one.</param>
    /// <param name="onto">The branch or commit to rebase on; null for none.</param>
    /// <param name="interactive">Upstream's "Interactive Rebase" to start with.</param>
    /// <param name="startRebaseImmediately">Upstream's: rebase at once, as the grid's "Selected commit" does.</param>
    public RebaseViewModel(IDialogHost host, string repositoryPath, GitModule module,
        RepositoryOperationsViewModel actions, IAppPreferences preferences, string? from, string? to, string? onto,
        bool interactive, bool startRebaseImmediately)
        : base(host)
    {
        _repositoryPath = repositoryPath;
        _module = module;
        _actions = actions;
        _preferences = preferences;
        _onto = onto;
        _defaultTo = to;
        _startRebaseImmediately = startRebaseImmediately;
        OptionsVisible = preferences.AlwaysShowAdvOpt;
        Dialog.From = from ?? "";
        Dialog.Interactive = interactive;
        Dialog.SpecificRange = !string.IsNullOrEmpty(from);
        From = Dialog.From;
        ContinueText = AccessKeyText(RebaseDialog.ContinueRebaseText);
        SolveConflictsText = AccessKeyText(RebaseDialog.SolveConflictsText);
        ShowOptionsState();
    }

    /// <summary>
    ///  Raised when the refs to rebase on are listed: the window focuses the box.
    /// </summary>
    public event EventHandler? BranchesShown;

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public RebaseDialog Dialog { get; } = new();

    /// <summary>
    ///  Completes when the branches, the options and the rebase state are shown, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  Picks a commit on the current branch (preselected, down to the merge base), returning its hash; the window sets
    ///  it, as it opens upstream's FormChooseCommit.
    /// </summary>
    public Func<string, string?, Task<string?>>? ChooseCommit { get; set; }

    /// <summary>
    ///  Opens upstream's FormAddFiles; the window sets it.
    /// </summary>
    public Func<Task>? ShowAddFiles { get; set; }

    /// <summary>
    ///  Opens the commit window; the window sets it.
    /// </summary>
    public Action? ShowCommit { get; set; }

    /// <summary>
    ///  Opens the conflicts window; the window sets it.
    /// </summary>
    public Action? ShowConflicts { get; set; }

    [ObservableProperty]
    public partial string CurrentBranch { get; private set; } = "";

    /// <summary>
    ///  The branches, remote branches and tags to rebase on.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Refs { get; private set; } = [];

    /// <summary>
    ///  The local branches the specific range can end at.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Heads { get; private set; } = [];

    [ObservableProperty]
    public partial string? Onto { get; set; }

    [ObservableProperty]
    public partial string? From { get; set; }

    [ObservableProperty]
    public partial string? To { get; set; }

    /// <summary>
    ///  "Rebase on" shows unless a rebase was in progress when the window opened.
    /// </summary>
    [ObservableProperty]
    public partial bool RebasePanelVisible { get; private set; } = true;

    /// <summary>
    ///  Upstream's options panels, shown by "Show options", always (AlwaysShowAdvOpt) or once the window opened.
    /// </summary>
    [ObservableProperty]
    public partial bool OptionsVisible { get; private set; }

    [ObservableProperty]
    public partial bool UpdateRefsVisible { get; private set; } = true;

    [ObservableProperty]
    public partial bool Interactive { get; set; }

    [ObservableProperty]
    public partial bool PreserveMerges { get; set; }

    [ObservableProperty]
    public partial bool AutoSquash { get; set; }

    [ObservableProperty]
    public partial bool AutoStash { get; set; }

    [ObservableProperty]
    public partial bool IgnoreDate { get; set; }

    [ObservableProperty]
    public partial bool CommitterDateIsAuthorDate { get; set; }

    [ObservableProperty]
    public partial bool UpdateRefs { get; set; }

    [ObservableProperty]
    public partial bool SpecificRange { get; set; }

    [ObservableProperty]
    public partial bool CanInteractive { get; private set; }

    [ObservableProperty]
    public partial bool CanPreserveMerges { get; private set; }

    [ObservableProperty]
    public partial bool CanAutoSquash { get; private set; }

    [ObservableProperty]
    public partial bool CanIgnoreDate { get; private set; }

    [ObservableProperty]
    public partial bool CanCommitterDateIsAuthorDate { get; private set; }

    /// <summary>
    ///  Upstream's <c>EnableButtons</c>: which buttons show and which is the default.
    /// </summary>
    [ObservableProperty]
    public partial RebaseButtons Buttons { get; private set; } = new(CanChangeBranch: true, Rebase: true,
        AutoStash: false, Steps: false, Continue: false, SolveConflicts: false, UnresolvedConflicts: false,
        RebaseDefaultButton.Rebase);

    /// <summary>
    ///  A merge stopped on conflicts: Solve conflicts is framed in yellow.
    /// </summary>
    [ObservableProperty]
    public partial bool Conflicted { get; private set; }

    /// <summary>
    ///  "Continue rebase", highlighted when it is the default button, with its access key.
    /// </summary>
    [ObservableProperty]
    public partial string ContinueText { get; private set; } = "";

    /// <summary>
    ///  "Solve conflicts", highlighted when it is the default button, with its access key.
    /// </summary>
    [ObservableProperty]
    public partial string SolveConflictsText { get; private set; } = "";

    /// <summary>
    ///  The commits of the rebase.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<RebasePatch> Patches { get; private set; } = [];

    /// <summary>
    ///  Upstream's <c>OnShown</c>: the current branch, the refs to rebase on (none when rebasing at once), the branches
    ///  for the range's end, git's autosquash and update refs settings and the kept "Auto stash"; then the rebase at once
    ///  or the options shown. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
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
        UpdateRefsVisible = supportUpdateRefs;
        CurrentBranch = head;
        Refs = refs;
        if (_onto is not null)
        {
            Onto = _onto;
        }

        BranchesShown?.Invoke(this, EventArgs.Empty);
        Heads = heads;
        To = _defaultTo ?? head;
        RebasePanelVisible = !inRebase;
        await RefreshStateAsync();
        Dialog.AutoSquash = autosquash;
        Dialog.UpdateRefs = updateRefs;
        Dialog.AutoStash = _preferences.RebaseAutoStash;
        ShowOptionsState();
        _loaded.TrySetResult();
        if (immediately)
        {
            await RebaseStepAsync();
        }
        else
        {
            OptionsVisible = true;
        }
    });

    /// <summary>
    ///  Upstream refreshes after its modal commit and conflicts dialogs; here the windows are not modal, so the window
    ///  calls this when it is active again, and the state is read again unless a step is running.
    /// </summary>
    public void Refresh()
    {
        if (_loaded.Task.IsCompleted && !_busy)
        {
            Run(RefreshStateAsync);
        }
    }

    partial void OnInteractiveChanged(bool value) => Edit(() => Dialog.Interactive = value);

    partial void OnPreserveMergesChanged(bool value) => Edit(() => Dialog.PreserveMerges = value);

    partial void OnAutoSquashChanged(bool value) => Edit(() => Dialog.AutoSquash = value);

    partial void OnAutoStashChanged(bool value) => Edit(() => Dialog.AutoStash = value);

    partial void OnIgnoreDateChanged(bool value) => Edit(() => Dialog.IgnoreDate = value);

    partial void OnCommitterDateIsAuthorDateChanged(bool value) => Edit(() => Dialog.CommitterDateIsAuthorDate = value);

    partial void OnUpdateRefsChanged(bool value) => Edit(() => Dialog.UpdateRefs = value);

    partial void OnSpecificRangeChanged(bool value) => Edit(() => Dialog.SpecificRange = value);

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
        Interactive = Dialog.Interactive;
        CanInteractive = Dialog.CanInteractive;
        PreserveMerges = Dialog.PreserveMerges;
        CanPreserveMerges = Dialog.CanPreserveMerges;
        AutoSquash = Dialog.AutoSquash;
        CanAutoSquash = Dialog.CanAutoSquash;
        AutoStash = Dialog.AutoStash;
        IgnoreDate = Dialog.IgnoreDate;
        CanIgnoreDate = Dialog.CanIgnoreDate;
        CommitterDateIsAuthorDate = Dialog.CommitterDateIsAuthorDate;
        CanCommitterDateIsAuthorDate = Dialog.CanCommitterDateIsAuthorDate;
        UpdateRefs = Dialog.UpdateRefs;
        SpecificRange = Dialog.SpecificRange;
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
        ContinueText = AccessKeyText(buttons.Default == RebaseDefaultButton.Continue
            ? RebaseDialog.ContinueRebaseHighlightedText
            : RebaseDialog.ContinueRebaseText);
        SolveConflictsText = AccessKeyText(buttons.Default == RebaseDefaultButton.SolveConflicts
            ? RebaseDialog.SolveConflictsHighlightedText
            : RebaseDialog.SolveConflictsText);
        Conflicted = conflicted;
        Buttons = buttons;
        Patches = patches;
    }

    private string AccessKeyText(string text) => Host.Translate(text).Replace('&', '_');

    [RelayCommand]
    private void ShowOptions() => OptionsVisible = true;

    // Upstream's btnChooseFromRevision_Click: FormChooseCommit on the current branch down to its merge base with "Rebase
    // on", the box's commit (else HEAD's first parent) selected; the chosen commit's short hash goes into the box.
    [RelayCommand]
    private Task ChooseFromRevisionAsync() => GuardAsync(async () =>
    {
        string from = From ?? "";
        string onto = Onto ?? "";
        (string preselect, string? mergeBase) = await Task.Run(() => RebaseDialog.ChooseFromRevision(_module, from, onto));
        if (ChooseCommit is not null && await ChooseCommit(preselect, mergeBase) is { } hash)
        {
            From = ObjectId.Parse(hash).ToShortString();
        }
    });

    [RelayCommand]
    private void Commit() => ShowCommit?.Invoke();

    [RelayCommand]
    private void SolveConflicts() => ShowConflicts?.Invoke();

    [RelayCommand]
    private Task AddFilesAsync() => GuardAsync(() => ShowAddFiles?.Invoke() ?? Task.CompletedTask);

    // Upstream's OkClick: the check, the kept "Auto stash", the rebase with git's output; the window closes unless the
    // rebase stopped, and continues by itself when git reused a recorded resolution.
    [RelayCommand]
    private Task RebaseAsync() => GuardAsync(RebaseStepAsync);

    private async Task RebaseStepAsync()
    {
        Dialog.Onto = Onto ?? "";
        Dialog.From = From ?? "";
        Dialog.To = To ?? "";
        if (Dialog.Validate() is { } error)
        {
            await Host.ShowMessageAsync(Host.Translate(error), Host.Translate(RebaseDialog.NoBranchSelectedCaption));
            return;
        }

        _preferences.RebaseAutoStash = Dialog.AutoStash;
        _preferences.Save();
        RebasePatches.Skipped.Clear();
        await RunStepAsync(() => _actions.RebaseAsync(_repositoryPath, Dialog.Onto,
            Dialog.Arguments(_supportUpdateRefs, _configuredUpdateRefs), Dialog.Interactive),
            rebaseOnly: false, output: true);
    }

    // Upstream's ResolvedClick.
    [RelayCommand]
    private Task ContinueAsync() => GuardAsync(ContinueStepAsync);

    private Task ContinueStepAsync()
        => RunStepAsync(() => _actions.ContinueRebaseShowingOutputAsync(_repositoryPath), rebaseOnly: true, output: true);

    // Upstream's SkipClick: the commit being applied is marked skipped first.
    [RelayCommand]
    private Task SkipAsync() => GuardAsync(() =>
    {
        if (Patches.FirstOrDefault(patch => patch.IsNext) is { } applying)
        {
            applying.IsSkipped = true;
            RebasePatches.Skipped.Add(applying);
        }

        return RunStepAsync(() => _actions.SkipRebaseAsync(_repositoryPath), rebaseOnly: true, output: false);
    });

    // Upstream's AbortClick and EditTodoClick: the skipped list is cleared when the rebase is over.
    [RelayCommand]
    private Task AbortAsync() => GuardAsync(() =>
        RunStepAsync(() => _actions.AbortRebaseAsync(_repositoryPath), rebaseOnly: true, output: false,
            clearSkipped: true));

    [RelayCommand]
    private Task EditTodoAsync() => GuardAsync(() =>
        RunStepAsync(() => _actions.EditRebaseTodoAsync(_repositoryPath), rebaseOnly: true, output: false,
            clearSkipped: true));

    // Upstream closes after the rebase unless an action or patch is in progress, and after the other steps unless the
    // rebase is.
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

                Host.Close(true);
                return;
            }

            await RefreshStateAsync();
            if (canContinue)
            {
                await ContinueStepAsync();
            }
        }
        finally
        {
            _busy = false;
        }
    }
}
