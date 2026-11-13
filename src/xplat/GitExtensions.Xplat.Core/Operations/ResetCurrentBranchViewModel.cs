using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Settings;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormResetCurrentBranch</c> (rules in <see cref="ResetCurrentBranchDialog"/>): resets the
///  current branch to a commit with the chosen type, asking first before a hard reset, and updates the submodules as upstream
///  does. Closes its window with whether the reset ran.
/// </summary>
public sealed partial class ResetCurrentBranchViewModel : DialogViewModel
{
    private readonly IGitModule _module;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;
    private readonly string _target;
    private readonly Func<string, Task<bool>> _updateSubmodules;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ObjectId _revision;

    /// <param name="target">The commit to reset to (a hash or ref).</param>
    /// <param name="mode">Upstream's reset type to start with.</param>
    /// <param name="updateSubmodules">Upstream's <c>UpdateSubmodules</c> after a reset that moved HEAD.</param>
    public ResetCurrentBranchViewModel(IDialogHost host, IGitModule module, RepositoryOperationsViewModel actions,
        IAppPreferences preferences, string target, ResetMode mode, Func<string, Task<bool>> updateSubmodules)
        : base(host)
    {
        _module = module;
        _actions = actions;
        _preferences = preferences;
        _target = target;
        _updateSubmodules = updateSubmodules;
        Dialog = new ResetCurrentBranchDialog(mode);
    }

    /// <summary>
    ///  The dialog state: the reset type, bound to the radio buttons.
    /// </summary>
    public ResetCurrentBranchDialog Dialog { get; }

    /// <summary>
    ///  Upstream's "Reset branch '...' to revision:", translated; null until loaded.
    /// </summary>
    [ObservableProperty]
    public partial string? BranchInfo { get; private set; }

    /// <summary>
    ///  Upstream's <c>CommitSummaryUserControl</c> of the commit; null when git does not know it.
    /// </summary>
    [ObservableProperty]
    public partial CommitSummary? Summary { get; private set; }

    /// <summary>
    ///  Completes when the branch and the commit's summary are shown, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  Upstream's <c>FormResetCurrentBranch_Load</c>. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        try
        {
            (string branch, ObjectId id, GitRevision? revision) = await Task.Run(() =>
            {
                ObjectId id = _module.RevParse(_target);
                return (_module.GetSelectedBranch(), id,
                    id.IsZero ? null : _module.GetRevision(id, shortFormat: true, loadRefs: true));
            });
            _revision = id;
            BranchInfo = string.Format(Host.Translate(ResetCurrentBranchDialog.BranchInfo), branch);
            Summary = revision is null ? null : CreateBranchDialog.Summarize(revision);
        }
        finally
        {
            _loaded.TrySetResult();
        }
    });

    // Upstream's Ok_Click: the reset (a hard one after the question), then the submodules when upstream updates them.
    [RelayCommand]
    private Task ResetAsync() => GuardAsync(async () =>
    {
        if (_revision.IsZero)
        {
            return;
        }

        if (Dialog.AsksFirst && !await Host.ConfirmAsync(Host.Translate(ResetCurrentBranchDialog.ResetHardWarning),
                Host.Translate(ResetCurrentBranchDialog.ResetCaption)))
        {
            return;
        }

        string repositoryPath = _module.WorkingDir;
        ObjectId revision = _revision;
        (bool movesHead, bool hasSubmodules) = await Task.Run(() =>
            (_module.GetCurrentCheckout() != revision, _module.HasSubmodules()));
        bool reset = await _actions.ResetAsync(repositoryPath, revision.ToString(), Dialog.Mode);
        if (reset && Dialog.UpdatesSubmodules(movesHead, _preferences.UpdateSubmodulesOnCheckout, hasSubmodules))
        {
            await _updateSubmodules(repositoryPath);
        }

        Host.Close(reset);
    });

    [RelayCommand]
    private void Cancel() => Host.Close(false);
}
