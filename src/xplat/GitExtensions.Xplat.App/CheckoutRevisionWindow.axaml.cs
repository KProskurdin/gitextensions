using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormCheckoutRevision</c> in the new shell, bound to <see cref="CheckoutRevisionViewModel"/> (rules in
///  <see cref="CheckoutRevisionDialog"/>): checks out a commit (detaching HEAD), optionally resetting local changes, between
///  the checkout scripts, and updates the submodules when HEAD moved. Closes with true after checking out.
/// </summary>
public partial class CheckoutRevisionWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public CheckoutRevisionWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="revision">The commit to propose (a hash, tag or other ref).</param>
    /// <param name="withScripts">Runs the checkout between the before and after checkout scripts (and plugin events).</param>
    /// <param name="updateSubmodules">Upstream's <c>UpdateSubmodules</c> after the checkout moved HEAD.</param>
    public CheckoutRevisionWindow(string repositoryPath, RepositoryOperationsViewModel actions, string? revision,
        Func<string, Func<Task<bool>>, Task<bool>> withScripts, Func<string, Task<bool>> updateSubmodules)
    {
        ViewModel = new CheckoutRevisionViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath), actions, revision,
            withScripts, updateSubmodules);
        DataContext = ViewModel;
        InitializeComponent();
        InitialFocus.Attach(this);
        Opened += (_, _) => UiActions.Run(ViewModel.LoadAsync, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
    }

    public CheckoutRevisionViewModel ViewModel { get; }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public CheckoutRevisionDialog Dialog => ViewModel.Dialog;

    /// <summary>
    ///  Completes when the proposed commit is shown, for tests.
    /// </summary>
    public Task RevisionLoaded => ViewModel.Loaded;
}
