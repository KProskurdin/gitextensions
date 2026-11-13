using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormSelectMultipleBranches</c>, opened by the branch box's button, bound to
///  <see cref="SelectMultipleBranchesViewModel"/>: the branches to check. As upstream, the checked branches count however
///  the window is closed.
/// </summary>
public partial class SelectMultipleBranchesWindow : Window
{
    public SelectMultipleBranchesWindow()
        : this([], [])
    {
    }

    /// <param name="branches">The branches to choose from.</param>
    /// <param name="selected">The branches checked at first (upstream's <c>SelectBranch</c>).</param>
    public SelectMultipleBranchesWindow(IReadOnlyList<string> branches, IEnumerable<string> selected)
    {
        ViewModel = new SelectMultipleBranchesViewModel(new WindowDialogHost(this), branches, selected);
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, SelectMultipleBranchesViewModel.Category);
    }

    public SelectMultipleBranchesViewModel ViewModel { get; }

    /// <summary>
    ///  Upstream's <c>GetSelectedBranches</c>: the checked branches in the list's order.
    /// </summary>
    public IReadOnlyList<string> SelectedBranches => ViewModel.SelectedBranches;
}
