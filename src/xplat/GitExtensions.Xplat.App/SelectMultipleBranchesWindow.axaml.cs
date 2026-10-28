using Avalonia.Controls;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormSelectMultipleBranches</c>, opened by the branch box's button: the branches to check. As upstream,
///  the checked branches count however the window is closed.
/// </summary>
public partial class SelectMultipleBranchesWindow : Window
{
    private readonly List<CheckBox> _checks;

    public SelectMultipleBranchesWindow()
        : this([], [])
    {
    }

    /// <param name="branches">The branches to choose from.</param>
    /// <param name="selected">The branches checked at first (upstream's <c>SelectBranch</c>).</param>
    public SelectMultipleBranchesWindow(IReadOnlyList<string> branches, IEnumerable<string> selected)
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormSelectMultipleBranches");
        HashSet<string> checkedBranches = [.. selected];
        _checks =
        [
            .. branches.Select(branch => new CheckBox { Content = branch, IsChecked = checkedBranches.Contains(branch) })
        ];
        Branches.ItemsSource = _checks;
        okButton.Click += (_, _) => Close();
    }

    /// <summary>
    ///  Upstream's <c>GetSelectedBranches</c>: the checked branches in the list's order.
    /// </summary>
    public IReadOnlyList<string> SelectedBranches
        => [.. _checks.Where(check => check.IsChecked == true).Select(check => (string)check.Content!)];
}
