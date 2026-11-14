using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormManageWorktree</c> in the new shell, bound to <see cref="WorktreesViewModel"/> (rules in
///  <see cref="ManageWorktreeDialog"/>): the worktrees, open or delete the selected one, prune the deleted ones and create
///  one, through upstream's <see cref="WorktreeFlow"/>. Closes after a worktree is opened; <see cref="ShouldRefresh"/> says
///  whether one was created.
/// </summary>
public partial class WorktreesWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public WorktreesWindow() => InitializeComponent();
#pragma warning restore CS8618

    public WorktreesWindow(string repositoryPath, IRepositoryService repositoryService, WorktreeFlow flow)
    {
        ViewModel = new WorktreesViewModel(new WindowDialogHost(this), repositoryPath, repositoryService,
            new OwnedWorktreeActions(flow, this));
        DataContext = ViewModel;
        InitializeComponent();
        InitialFocus.Attach(this, () => WorktreeList);
        WorktreeList.DoubleTapped += (_, _) => ViewModel.OpenCommand.Execute(null);
        Opened += (_, _) => _ = ViewModel.LoadAsync();
    }

    public WorktreesViewModel ViewModel { get; }

    /// <summary>
    ///  The worktrees on show.
    /// </summary>
    public IReadOnlyList<WorktreeInfo> Worktrees => ViewModel.Worktrees;

    /// <summary>
    ///  Upstream's <c>ShouldRefreshRevisionGrid</c>: a worktree was created, so its new branch shows in the grid.
    /// </summary>
    public bool ShouldRefresh => ViewModel.ShouldRefresh;

    /// <summary>
    ///  Completes when the worktrees are first shown, for tests.
    /// </summary>
    public Task WorktreesLoaded => ViewModel.Loaded;

    // The flow's questions and windows are owned by this window.
    private sealed class OwnedWorktreeActions(WorktreeFlow flow, Window owner) : IWorktreeActions
    {
        public Task<bool> SwitchAsync(string worktreePath) => flow.SwitchAsync(owner, worktreePath);

        public Task<bool> DeleteAsync(string repositoryPath, string worktreePath)
            => flow.DeleteAsync(owner, repositoryPath, worktreePath);

        public Task<bool> PruneAsync(string repositoryPath) => flow.PruneAsync(repositoryPath);

        public Task<bool> CreateAsync(string repositoryPath, string mainWorktreePath)
            => flow.CreateAsync(owner, repositoryPath, mainWorktreePath);
    }
}
