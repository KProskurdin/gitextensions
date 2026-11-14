using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormDeleteRemoteBranch</c>, bound to <see cref="DeleteRemoteBranchViewModel"/> (rules in
///  <see cref="DeleteRemoteBranchDialog"/>): the remote branches to delete, named in the box or checked in upstream's
///  multiple branches window, deleted on the remote, and their local tracking branches offered when asked.
/// </summary>
public partial class DeleteRemoteBranchWindow : Window
{
    public DeleteRemoteBranchWindow()
        : this("", null, null, null)
    {
    }

    /// <param name="repositoryPath">The repository.</param>
    /// <param name="defaultRemoteBranch">The remote branch selected at first, e.g. "origin/feature".</param>
    /// <param name="deleteOnRemote">Deletes the branches (local names) on the remote, with the push scripts.</param>
    /// <param name="deleteLocalBranches">Upstream's StartDeleteBranchDialog with the local tracking branches.</param>
    public DeleteRemoteBranchWindow(string repositoryPath, string? defaultRemoteBranch,
        Func<string, IReadOnlyList<string>, Task<bool>>? deleteOnRemote,
        Func<Window, IReadOnlyList<string>, Task>? deleteLocalBranches)
    {
        ViewModel = new DeleteRemoteBranchViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath),
            defaultRemoteBranch, deleteOnRemote,
            deleteLocalBranches is null ? null : branches => deleteLocalBranches(this, branches),
            async (branches, selected) =>
            {
                SelectMultipleBranchesWindow window = new(branches, selected);
                await window.ShowDialog(this);
                return window.SelectedBranches;
            });
        DataContext = ViewModel;
        InitializeComponent();
        DialogHelp.Attach(this, HelpButton, DialogHelp.Manual("branches", "delete-branch"));
        UpstreamTranslation.Apply(this, DeleteRemoteBranchViewModel.Category);
        InitialFocus.Attach(this, () => Branches);
        Opened += (_, _) => _ = ViewModel.LoadAsync();
    }

    public DeleteRemoteBranchViewModel ViewModel { get; }

    /// <summary>
    ///  Completes when the branches are listed, for tests.
    /// </summary>
    public Task BranchesLoaded => ViewModel.Loaded;
}
