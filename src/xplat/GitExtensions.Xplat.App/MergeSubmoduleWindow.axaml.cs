using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormMergeSubmodule</c> in the new shell, bound to <see cref="MergeSubmoduleViewModel"/> (rules in
///  <see cref="MergeSubmoduleDialog"/>): a conflicted submodule's commits, opened in a new instance, a branch containing
///  the local and remote commits checked out in it, or its current commit staged. Closes with true once the submodule was
///  staged.
/// </summary>
public partial class MergeSubmoduleWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public MergeSubmoduleWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="fileName">The submodule's path in the repository.</param>
    /// <param name="checkoutBranch">Upstream's <c>StartCheckoutBranch</c> in the submodule's folder, limited to the branches
    ///  that contain the given commits; without it "Checkout Branch" is disabled.</param>
    /// <param name="launcher">Starts the new instance "Open submodule" opens.</param>
    public MergeSubmoduleWindow(string repositoryPath, string fileName,
        Func<Window, string, IReadOnlyList<ObjectId>, Task<bool>>? checkoutBranch = null,
        IProcessLauncher? launcher = null)
    {
        IProcessLauncher processLauncher = launcher ?? new SystemProcessLauncher();
        ViewModel = new MergeSubmoduleViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath), fileName,
            checkoutBranch is null ? null : (path, commits) => checkoutBranch(this, path, commits),
            path => NewInstanceCommand.Start(processLauncher, path));
        DataContext = ViewModel;
        InitializeComponent();
        InitialFocus.Attach(this);
        Opened += (_, _) => _ = ViewModel.LoadAsync();
    }

    public MergeSubmoduleViewModel ViewModel { get; }

    /// <summary>
    ///  Completes when the commits are shown, for tests.
    /// </summary>
    public Task SubmoduleLoaded => ViewModel.Loaded;
}
