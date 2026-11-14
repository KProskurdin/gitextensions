using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormDeleteBranch</c>, bound to <see cref="DeleteBranchViewModel"/> (rules in
///  <see cref="DeleteBranchDialog"/>): the local branches to delete, named in the box or checked in upstream's multiple
///  branches window. The current branch is refused; the rest goes to the caller's delete (upstream's worktree handling,
///  the unmerged question and <c>branch -D</c>), and the window closes with true once it succeeded.
/// </summary>
public partial class DeleteBranchWindow : Window
{
    public DeleteBranchWindow()
        : this([], null, [], null)
    {
    }

    /// <param name="branches">The local branches (upstream's <c>GetRefs(RefsFilter.Heads)</c>).</param>
    /// <param name="currentBranch">The checked out branch, which cannot be deleted; null on a detached HEAD.</param>
    /// <param name="defaultBranches">The branches selected at first.</param>
    /// <param name="delete">Deletes the branches, asking what upstream asks; true when they were deleted.</param>
    public DeleteBranchWindow(IReadOnlyList<string> branches, string? currentBranch,
        IEnumerable<string> defaultBranches, Func<Window, IReadOnlyList<string>, Task<bool>>? delete)
    {
        DataContext = new DeleteBranchViewModel(new WindowDialogHost(this), branches, currentBranch, defaultBranches,
            delete is null ? null : selected => delete(this, selected),
            async selected =>
            {
                SelectMultipleBranchesWindow window = new(branches, selected);
                await window.ShowDialog(this);
                return window.SelectedBranches;
            });
        InitializeComponent();
        DialogHelp.Attach(this, HelpButton, DialogHelp.Manual("branches", "delete-branch"));
        UpstreamTranslation.Apply(this, DeleteBranchViewModel.Category);
        InitialFocus.Attach(this, () => Branches);
    }
}
