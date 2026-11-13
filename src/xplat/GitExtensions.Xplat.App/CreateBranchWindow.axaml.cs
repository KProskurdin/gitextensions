using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormCreateBranch</c> in the new shell, bound to <see cref="CreateBranchViewModel"/> (rules in
///  <see cref="CreateBranchDialog"/>): the branch name (proposed from the commit's refs or a folder), the commit with
///  upstream's commit summary, checkout after create and the orphan options. Closes with true once the branch is created.
/// </summary>
public partial class CreateBranchWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public CreateBranchWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="revision">The commit to create the branch at (a hash or ref); HEAD when null or an artificial commit.</param>
    /// <param name="namePrefix">The proposed name, e.g. a left panel folder's path with a trailing slash.</param>
    /// <param name="checkoutAfterCreation">Upstream's <c>CheckoutAfterCreation</c>: "Checkout after create" to start with.</param>
    /// <param name="userAbleToChangeRevision">Upstream's <c>UserAbleToChangeRevision</c>.</param>
    /// <param name="couldBeOrphan">Upstream's <c>CouldBeOrphan</c>: the orphan options can be used.</param>
    public CreateBranchWindow(string repositoryPath, RepositoryOperationsViewModel actions, IAppPreferences preferences,
        string? revision = null, string? namePrefix = null, bool checkoutAfterCreation = true,
        bool userAbleToChangeRevision = true, bool couldBeOrphan = true)
    {
        ViewModel = new CreateBranchViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath), actions, preferences,
            revision, namePrefix, checkoutAfterCreation, userAbleToChangeRevision, couldBeOrphan);
        DataContext = ViewModel;
        InitializeComponent();
        DialogHelp.Attach(this, HelpButton, DialogHelp.Manual("branches", "create-branch"));

        // View concerns: the name is normalised when its box is left, and focused with the caret at its end once loaded.
        BranchNameBox.LostFocus += (_, _) => ViewModel.NormaliseName();
        Opened += (_, _) => UiActions.Run(ShowAsync, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
    }

    public CreateBranchViewModel ViewModel { get; }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public CreateBranchDialog Dialog => ViewModel.Dialog;

    /// <summary>
    ///  Completes when the commit is read and shown, for tests.
    /// </summary>
    public Task BranchLoaded => ViewModel.Loaded;

    /// <summary>
    ///  True when the created branch was checked out at another commit than before, which upstream follows with a
    ///  submodule update.
    /// </summary>
    public bool CheckedOutOtherCommit => ViewModel.CheckedOutOtherCommit;

    private async Task ShowAsync()
    {
        await ViewModel.LoadAsync();
        BranchNameBox.Focus();
        BranchNameBox.CaretIndex = BranchNameBox.Text?.Length ?? 0;
    }
}
