using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormCheckoutRevision</c> (rules in <see cref="CheckoutRevisionDialog"/>): checks out a
///  commit (detaching HEAD), optionally resetting local changes, between the checkout scripts, and updates the submodules
///  when HEAD moved. Closes its window with true after checking out.
/// </summary>
public sealed partial class CheckoutRevisionViewModel : DialogViewModel
{
    private readonly IGitModule _module;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly string? _revision;
    private readonly Func<string, Func<Task<bool>>, Task<bool>> _withScripts;
    private readonly Func<string, Task<bool>> _updateSubmodules;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <param name="revision">The commit to propose (a hash, tag or other ref).</param>
    /// <param name="withScripts">Runs the checkout between the before and after checkout scripts (and plugin events).</param>
    /// <param name="updateSubmodules">Upstream's <c>UpdateSubmodules</c> after the checkout moved HEAD.</param>
    public CheckoutRevisionViewModel(IDialogHost host, IGitModule module, RepositoryOperationsViewModel actions,
        string? revision, Func<string, Func<Task<bool>>, Task<bool>> withScripts, Func<string, Task<bool>> updateSubmodules)
        : base(host)
    {
        _module = module;
        _actions = actions;
        _revision = revision;
        _withScripts = withScripts;
        _updateSubmodules = updateSubmodules;
    }

    /// <summary>
    ///  The dialog state: the commit (bound to the commit picker) and "Force".
    /// </summary>
    public CheckoutRevisionDialog Dialog { get; } = new();

    /// <summary>
    ///  The repository the commit picker resolves commits in.
    /// </summary>
    public IGitModule Module => _module;

    /// <summary>
    ///  Completes when the proposed commit is shown, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  Resolves the proposed commit; one git does not know leaves none chosen. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_revision))
            {
                ObjectId id = await Task.Run(() => _module.RevParse(_revision));
                Dialog.Revision = id.IsZero ? null : id;
            }
        }
        finally
        {
            _loaded.TrySetResult();
        }
    });

    // Upstream's OkClick.
    [RelayCommand]
    private Task CheckoutAsync() => GuardAsync(async () =>
    {
        if (Dialog.Validate() is not null || Dialog.Revision is not { } target)
        {
            await Host.ShowMessageAsync(Host.Translate(CheckoutRevisionDialog.NoRevisionSelected),
                Host.Translate(CheckoutRevisionDialog.NoRevisionSelectedCaption));
            return;
        }

        string repositoryPath = _module.WorkingDir;
        ObjectId original = await Task.Run(_module.GetCurrentCheckout);
        if (!await _withScripts(repositoryPath,
                () => _actions.CheckoutBranchAsync(repositoryPath, target.ToShortString(), Dialog.Arguments())))
        {
            return;
        }

        if (target != original)
        {
            await _updateSubmodules(repositoryPath);
        }

        Host.Close(true);
    });

    [RelayCommand]
    private void Cancel() => Host.Close(false);
}
