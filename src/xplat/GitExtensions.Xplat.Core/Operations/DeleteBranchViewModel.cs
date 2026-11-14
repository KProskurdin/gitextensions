using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormDeleteBranch</c> (rules in <see cref="DeleteBranchDialog"/>): the local branches to
///  delete, named in the box or checked in upstream's multiple branches window. The current branch is refused; the rest
///  goes to the caller's delete, and the window closes with true once it succeeded.
/// </summary>
public sealed partial class DeleteBranchViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormDeleteBranch";

    private readonly string? _currentBranch;
    private readonly Func<IReadOnlyList<string>, Task<bool>>? _delete;
    private readonly Func<IReadOnlyList<string>, Task<IReadOnlyList<string>>>? _selectMultiple;

    /// <param name="branches">The local branches (upstream's <c>GetRefs(RefsFilter.Heads)</c>).</param>
    /// <param name="currentBranch">The checked out branch, which cannot be deleted; null on a detached HEAD.</param>
    /// <param name="defaultBranches">The branches selected at first.</param>
    /// <param name="delete">Deletes the branches, asking what upstream asks; true when they were deleted.</param>
    /// <param name="selectMultiple">Upstream's <c>FormSelectMultipleBranches</c> with the given branches checked; the
    /// branches checked when it closed.</param>
    public DeleteBranchViewModel(IDialogHost host, IReadOnlyList<string> branches, string? currentBranch,
        IEnumerable<string> defaultBranches, Func<IReadOnlyList<string>, Task<bool>>? delete,
        Func<IReadOnlyList<string>, Task<IReadOnlyList<string>>>? selectMultiple)
        : base(host)
    {
        Branches = branches;
        _currentBranch = currentBranch;
        _delete = delete;
        _selectMultiple = selectMultiple;
        BranchesText = DeleteBranchDialog.SelectionText(defaultBranches);
    }

    public IReadOnlyList<string> Branches { get; }

    /// <summary>
    ///  Upstream's <c>BranchComboBox</c> text: the branches' names with spaces between.
    /// </summary>
    [ObservableProperty]
    public partial string? BranchesText { get; set; }

    // Upstream's selectMultipleBranchesButton_Click: the box names the branches checked in the window.
    [RelayCommand]
    private Task SelectMultipleBranchesAsync() => GuardAsync(async () =>
    {
        if (_selectMultiple is not null)
        {
            BranchesText = DeleteBranchDialog.SelectionText(await _selectMultiple(await SelectedBranchesAsync()));
        }
    });

    // Upstream's Delete_Click.
    [RelayCommand]
    private Task DeleteAsync() => GuardAsync(async () =>
    {
        IReadOnlyList<string> selected = await SelectedBranchesAsync();
        if (selected.Count == 0)
        {
            return;
        }

        if (_currentBranch is not null && selected.Contains(_currentBranch))
        {
            await Host.ShowMessageAsync(
                string.Format(Host.Translate(DeleteBranchDialog.CannotDeleteCurrentBranch), _currentBranch),
                Host.Translate(DeleteBranchDialog.DeleteBranchCaption));
            return;
        }

        if (_delete is not null && await _delete(selected))
        {
            Host.Close(true);
        }
    });

    // Upstream's BranchComboBox.GetSelectedBranches: each name that is no branch gets upstream's error and is left out.
    private async Task<IReadOnlyList<string>> SelectedBranchesAsync()
    {
        (IReadOnlyList<string> selected, IReadOnlyList<string> unknown) =
            DeleteBranchDialog.Selection(BranchesText, Branches);
        foreach (string name in unknown)
        {
            await Host.ShowMessageAsync(string.Format(Host.Translate(DeleteBranchDialog.BranchNotSelectable), name),
                Host.Translate(ManageWorktreeDialog.Error));
        }

        return selected;
    }
}
