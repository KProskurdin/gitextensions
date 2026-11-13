using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Settings;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormResetAnotherBranch</c> (rules in <see cref="ResetAnotherBranchDialog"/>): moves a
///  local branch that is not checked out to a commit, when it fast-forwards or when forced, and checks it out when asked.
///  Closes its window with true after the reset.
/// </summary>
public sealed partial class ResetAnotherBranchViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormResetAnotherBranch";

    private readonly IGitModule _module;
    private readonly string _revisionText;
    private readonly IAppPreferences _preferences;
    private readonly Func<ArgumentString, Task<bool>> _runGit;
    private readonly Func<string, Task> _checkout;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IReadOnlyList<IGitRef>? _candidates;
    private ObjectId _revision;
    private string? _validatedBranch;
    private CancellationTokenSource? _validation;

    /// <param name="revision">The commit to reset a branch to.</param>
    /// <param name="preferences">Holds "Checkout branch after reset".</param>
    /// <param name="runGit">Runs a git command with its output shown; true when it succeeded.</param>
    /// <param name="checkout">Upstream's <c>StartCheckoutBranch</c> for the branch that was reset.</param>
    public ResetAnotherBranchViewModel(IDialogHost host, IGitModule module, string revision,
        IAppPreferences preferences,
        Func<ArgumentString, Task<bool>> runGit, Func<string, Task> checkout)
        : base(host)
    {
        _module = module;
        _revisionText = revision;
        _preferences = preferences;
        _runGit = runGit;
        _checkout = checkout;
        BranchNames = [];
        CheckoutAfterReset = preferences.CheckoutOtherBranchAfterReset;
    }

    /// <summary>
    ///  The local branches that can be reset (upstream's <c>InitLocalBranchesWithoutCurrent</c>).
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> BranchNames { get; private set; }

    /// <summary>
    ///  The branch typed or chosen.
    /// </summary>
    [ObservableProperty]
    public partial string? BranchText { get; set; }

    /// <summary>
    ///  Upstream's "Force reset for a non-fast-forward reset".
    /// </summary>
    [ObservableProperty]
    public partial bool Force { get; set; }

    /// <summary>
    ///  Upstream's "Checkout branch after reset", kept in its setting.
    /// </summary>
    [ObservableProperty]
    public partial bool CheckoutAfterReset { get; set; }

    /// <summary>
    ///  Upstream's <c>CommitSummaryUserControl</c> of the commit; null when git does not know it.
    /// </summary>
    [ObservableProperty]
    public partial CommitSummary? Summary { get; private set; }

    /// <summary>
    ///  OK is enabled: a listed branch that fast-forwards to the commit, or any listed branch when forced.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    public partial bool CanReset { get; private set; }

    /// <summary>
    ///  The name is not one of the listed branches; the window marks it once the list loses focus.
    /// </summary>
    [ObservableProperty]
    public partial bool IsBranchUnlisted { get; private set; }

    /// <summary>
    ///  Git refused the move as not a fast-forward; the window marks OK.
    /// </summary>
    [ObservableProperty]
    public partial bool IsMoveRefused { get; private set; }

    /// <summary>
    ///  Completes when the branches and the commit's summary are shown, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  Completes when the last fast-forward check has finished, for tests.
    /// </summary>
    public Task Validated { get; private set; } = Task.CompletedTask;

    /// <summary>
    ///  Upstream's <c>FormResetAnotherBranch_Load</c> and <c>InitLocalBranchesWithoutCurrent</c>: the commit, the branches
    ///  that can be reset, and the one proposed. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        try
        {
            (ObjectId revision, GitRevision? details, IReadOnlyList<IGitRef> candidates, string? proposed) =
                await Task.Run(() =>
                {
                    ObjectId id = _module.RevParse(_revisionText);
                    GitRevision? revision =
                        id.IsZero ? null : _module.GetRevision(id, shortFormat: true, loadRefs: true);
                    IReadOnlyList<IGitRef> refs = [.. revision?.Refs ?? []];
                    IReadOnlyList<IGitRef> candidates = ResetAnotherBranchDialog.Candidates(
                        _module.GetRefs(RefsFilter.Heads), _module.GetSelectedBranch(), id, refs);
                    return (id, revision, candidates, ResetAnotherBranchDialog.ProposedBranch(candidates, refs));
                });
            _revision = revision;
            _candidates = candidates;
            BranchNames = [.. candidates.Select(gitRef => gitRef.Name)];
            if (proposed is not null)
            {
                BranchText = proposed;
            }

            Summary = details is null ? null : CreateBranchDialog.Summarize(details);
            Validate();
        }
        finally
        {
            _loaded.TrySetResult();
        }
    });

    partial void OnBranchTextChanged(string? value) => Validate();

    partial void OnForceChanged(bool value) => Validate();

    partial void OnCheckoutAfterResetChanged(bool value) => _preferences.CheckoutOtherBranchAfterReset = value;

    // Upstream's Ok_Click: the branch moved with update-ref, then checked out when asked.
    [RelayCommand(CanExecute = nameof(CanReset))]
    private Task ResetAsync() => GuardAsync(async () =>
    {
        string branch = BranchText ?? "";
        if (_candidates?.FirstOrDefault(gitRef => gitRef.Name == branch) is not { } gitRefToReset)
        {
            await Host.ShowMessageAsync(string.Format(Host.Translate(ResetAnotherBranchDialog.LocalRefInvalid), branch),
                Host.Translate(ManageWorktreeDialog.Error));
            return;
        }

        if (!await _runGit(ResetAnotherBranchDialog.Arguments(gitRefToReset, _revision)))
        {
            return;
        }

        if (CheckoutAfterReset)
        {
            await _checkout(gitRefToReset.Name);
        }

        Host.Close(true);
    });

    [RelayCommand]
    private void Cancel() => Host.Close(false);

    // Upstream's Validate: OK needs one of the listed branches, and is enabled when forced or when the branch is an ancestor
    // of the commit, which git checks off the UI thread; a later change cancels the check.
    private void Validate()
    {
        string branch = BranchText ?? "";
        if (_candidates is null || (branch == _validatedBranch && !Force))
        {
            return;
        }

        _validatedBranch = null;
        _validation?.Cancel();
        CancellationTokenSource validation = new();
        _validation = validation;

        IGitRef? gitRefToReset = _candidates.FirstOrDefault(gitRef => gitRef.Name == branch);
        IsBranchUnlisted = gitRefToReset is null;
        IsMoveRefused = false;
        CanReset = gitRefToReset is not null && Force;
        if (gitRefToReset is null || Force)
        {
            return;
        }

        _validatedBranch = branch;
        Validated = CheckAncestorAsync(ResetAnotherBranchDialog.IsAncestorArguments(gitRefToReset, _revision),
            validation.Token);
    }

    private async Task CheckAncestorAsync(ArgumentString arguments, CancellationToken cancellationToken)
    {
        try
        {
            ExecutionResult result = await Task.Run(() => _module.GitExecutable.ExecuteAsync(arguments,
                throwOnErrorExit: false, cancellationToken: cancellationToken), cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            CanReset = result.ExitedSuccessfully;
            IsMoveRefused = !result.ExitedSuccessfully;
        }
        catch (OperationCanceledException)
        {
            // A later change started another check.
        }
    }
}
