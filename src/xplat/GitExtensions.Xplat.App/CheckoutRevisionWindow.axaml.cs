using Avalonia.Controls;
using Avalonia.Input;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormCheckoutRevision</c> in the new shell (rules in <see cref="CheckoutRevisionDialog"/>): checks out a
///  commit (detaching HEAD), optionally resetting local changes, between the checkout scripts, and updates the submodules
///  when HEAD moved. Closes with true after checking out.
/// </summary>
public partial class CheckoutRevisionWindow : Window
{
    private readonly string _repositoryPath;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly Func<string, Func<Task<bool>>, Task<bool>> _withScripts;
    private readonly Func<string, Task<bool>> _updateSubmodules;
    private readonly CheckoutRevisionDialog _dialog = new();
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly GitModule _module;

    /// <param name="revision">The commit to propose (a hash, tag or other ref).</param>
    /// <param name="withScripts">Runs the checkout between the before and after checkout scripts (and plugin events).</param>
    /// <param name="updateSubmodules">Upstream's <c>UpdateSubmodules</c> after the checkout moved HEAD.</param>
    public CheckoutRevisionWindow(string repositoryPath, RepositoryOperationsViewModel actions, string? revision,
        Func<string, Func<Task<bool>>, Task<bool>> withScripts, Func<string, Task<bool>> updateSubmodules)
    {
        _repositoryPath = repositoryPath;
        _actions = actions;
        _withScripts = withScripts;
        _updateSubmodules = updateSubmodules;
        _module = GitModules.Open(repositoryPath);
        InitializeComponent();
        CommitPicker.Module = _module;
        CheckoutButton.Click += (_, _) => Run(CheckoutAsync);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close(false);
            }
        };
        Opened += (_, _) => Run(async () =>
        {
            await CommitPicker.SetCommitAsync(revision);
            _loaded.TrySetResult();
        });
    }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public CheckoutRevisionDialog Dialog => _dialog;

    /// <summary>
    ///  Completes when the proposed commit is shown, for tests.
    /// </summary>
    public Task RevisionLoaded => _loaded.Task;

    // Upstream's OkClick.
    private async Task CheckoutAsync()
    {
        _dialog.Revision = CommitPicker.Selected;
        _dialog.Force = ForceCheck.IsChecked == true;
        if (_dialog.Validate() is not null || _dialog.Revision is not { } target)
        {
            await new MessageBoxWindow(UpstreamTranslation.ByText(CheckoutRevisionDialog.NoRevisionSelected),
                UpstreamTranslation.ByText(CheckoutRevisionDialog.NoRevisionSelectedCaption), MessageBoxButtons.OK).ShowDialog(this);
            return;
        }

        ObjectId original = await Task.Run(_module.GetCurrentCheckout);
        if (!await _withScripts(_repositoryPath,
                () => _actions.CheckoutBranchAsync(_repositoryPath, target.ToShortString(), _dialog.Arguments())))
        {
            return;
        }

        if (target != original)
        {
            await _updateSubmodules(_repositoryPath);
        }

        Close(true);
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}
