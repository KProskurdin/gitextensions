using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  A branch of upstream's <c>FormSelectMultipleBranches</c> list with its check box.
/// </summary>
public sealed partial class SelectableBranch : ObservableObject
{
    public SelectableBranch(string name, bool isSelected)
    {
        Name = name;
        IsSelected = isSelected;
    }

    public string Name { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
///  The view model of upstream's <c>FormSelectMultipleBranches</c>: the branches with check boxes, and OK.
/// </summary>
public sealed partial class SelectMultipleBranchesViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormSelectMultipleBranches";

    /// <param name="branches">The branches to choose from.</param>
    /// <param name="selected">The branches checked at first (upstream's <c>SelectBranch</c>).</param>
    public SelectMultipleBranchesViewModel(IDialogHost host, IReadOnlyList<string> branches,
        IEnumerable<string> selected)
        : base(host)
    {
        HashSet<string> checkedBranches = [.. selected];
        Branches = [.. branches.Select(branch => new SelectableBranch(branch, checkedBranches.Contains(branch)))];
    }

    public IReadOnlyList<SelectableBranch> Branches { get; }

    /// <summary>
    ///  Upstream's <c>GetSelectedBranches</c>: the checked branches in the list's order.
    /// </summary>
    public IReadOnlyList<string> SelectedBranches =>
        [.. Branches.Where(branch => branch.IsSelected).Select(branch => branch.Name)];

    [RelayCommand]
    private void Ok() => Host.Close(null);
}
