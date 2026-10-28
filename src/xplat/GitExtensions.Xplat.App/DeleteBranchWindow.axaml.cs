using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormDeleteBranch</c> (rules in <see cref="DeleteBranchDialog"/>): the local branches to delete, named in
///  the box or checked in upstream's multiple branches window. The current branch is refused; the rest goes to the
///  caller's delete (upstream's worktree handling, the unmerged question and <c>branch -D</c>), and the window closes with
///  true once it succeeded.
/// </summary>
public partial class DeleteBranchWindow : Window
{
    private readonly IReadOnlyList<string> _branches;
    private readonly string? _currentBranch;
    private readonly Func<Window, IReadOnlyList<string>, Task<bool>>? _delete;

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
        _branches = branches;
        _currentBranch = currentBranch;
        _delete = delete;
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormDeleteBranch");
        InitialFocus.Attach(this, () => Branches);
        Branches.ItemsSource = branches;
        Branches.Text = DeleteBranchDialog.SelectionText(defaultBranches);
        selectMultipleBranchesButton.Click += (_, _) => Run(SelectMultipleBranchesAsync);
        Delete.Click += (_, _) => Run(DeleteAsync);
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));

    // Upstream's selectMultipleBranchesButton_Click: the box names the branches checked in the window.
    private async Task SelectMultipleBranchesAsync()
    {
        SelectMultipleBranchesWindow window = new(_branches, await SelectedBranchesAsync());
        await window.ShowDialog(this);
        Branches.Text = DeleteBranchDialog.SelectionText(window.SelectedBranches);
    }

    // Upstream's Delete_Click.
    private async Task DeleteAsync()
    {
        IReadOnlyList<string> selected = await SelectedBranchesAsync();
        if (selected.Count == 0)
        {
            return;
        }

        if (_currentBranch is not null && selected.Contains(_currentBranch))
        {
            await new MessageBoxWindow(
                string.Format(UpstreamTranslation.ByText(DeleteBranchDialog.CannotDeleteCurrentBranch), _currentBranch),
                UpstreamTranslation.ByText(DeleteBranchDialog.DeleteBranchCaption), MessageBoxButtons.OK)
                .ShowDialog(this);
            return;
        }

        if (_delete is not null && await _delete(this, selected))
        {
            Close(true);
        }
    }

    // Upstream's BranchComboBox.GetSelectedBranches: each name that is no branch gets upstream's error and is left out.
    private async Task<IReadOnlyList<string>> SelectedBranchesAsync()
    {
        (IReadOnlyList<string> selected, IReadOnlyList<string> unknown) =
            DeleteBranchDialog.Selection(Branches.Text, _branches);
        foreach (string name in unknown)
        {
            await new MessageBoxWindow(
                string.Format(UpstreamTranslation.ByText(DeleteBranchDialog.BranchNotSelectable), name),
                UpstreamTranslation.ByText(ManageWorktreeDialog.Error), MessageBoxButtons.OK).ShowDialog(this);
        }

        return selected;
    }
}
