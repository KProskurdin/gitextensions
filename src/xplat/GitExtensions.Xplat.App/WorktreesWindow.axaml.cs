using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormManageWorktree</c> in the new shell (rules in <see cref="ManageWorktreeDialog"/>): the worktrees,
///  open or delete the selected one, prune the deleted ones and create one, through upstream's <see cref="WorktreeFlow"/>.
///  Closes after a worktree is opened; <see cref="ShouldRefresh"/> says whether one was created.
/// </summary>
public partial class WorktreesWindow : Window
{
    private readonly string _repositoryPath;
    private readonly IRepositoryService _repositoryService;
    private readonly WorktreeFlow _flow;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public WorktreesWindow(string repositoryPath, IRepositoryService repositoryService, WorktreeFlow flow)
    {
        _repositoryPath = repositoryPath;
        _repositoryService = repositoryService;
        _flow = flow;
        InitializeComponent();
        WorktreeList.SelectionChanged += (_, _) => UpdateButtons();
        WorktreeList.DoubleTapped += (_, _) => Run(OpenSelectedAsync);
        OpenButton.Click += (_, _) => Run(OpenSelectedAsync);
        DeleteButton.Click += (_, _) => Run(DeleteSelectedAsync);
        PruneButton.Click += (_, _) => Run(PruneAsync);
        CreateButton.Click += (_, _) => Run(CreateAsync);
        Opened += (_, _) => Run(LoadAsync);
        UpdateButtons();
    }

    /// <summary>
    ///  The worktrees on show.
    /// </summary>
    public IReadOnlyList<WorktreeInfo> Worktrees { get; private set; } = [];

    /// <summary>
    ///  Upstream's <c>ShouldRefreshRevisionGrid</c>: a worktree was created, so its new branch shows in the grid.
    /// </summary>
    public bool ShouldRefresh { get; private set; }

    /// <summary>
    ///  Completes when the worktrees are first shown, for tests.
    /// </summary>
    public Task WorktreesLoaded => _loaded.Task;

    private void Run(Func<Task> action) =>
        UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));

    // Upstream's Initialize: the worktrees, and Prune only when one other than the main one lost its folder.
    private async Task LoadAsync()
    {
        Worktrees = await _repositoryService.GetWorktreesAsync(_repositoryPath);
        WorktreeList.ItemsSource = Worktrees;
        PruneButton.IsEnabled = ManageWorktreeDialog.CanPrune(Worktrees);
        UpdateButtons();
        _loaded.TrySetResult();
    }

    private void UpdateButtons()
    {
        OpenButton.IsEnabled = ManageWorktreeDialog.CanOpen(Worktrees, WorktreeList.SelectedIndex, _repositoryPath);
        DeleteButton.IsEnabled = ManageWorktreeDialog.CanDelete(Worktrees, WorktreeList.SelectedIndex, _repositoryPath);
    }

    private async Task OpenSelectedAsync()
    {
        if (ManageWorktreeDialog.CanOpen(Worktrees, WorktreeList.SelectedIndex, _repositoryPath)
            && await _flow.SwitchAsync(this, Worktrees[WorktreeList.SelectedIndex].Path))
        {
            Close();
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (ManageWorktreeDialog.CanDelete(Worktrees, WorktreeList.SelectedIndex, _repositoryPath)
            && await _flow.DeleteAsync(this, _repositoryPath, Worktrees[WorktreeList.SelectedIndex].Path))
        {
            await LoadAsync();
        }
    }

    private async Task PruneAsync()
    {
        await _flow.PruneAsync(_repositoryPath);
        await LoadAsync();
    }

    private async Task CreateAsync()
    {
        if (await _flow.CreateAsync(this, _repositoryPath,
                ManageWorktreeDialog.MainWorktreePath(Worktrees, _repositoryPath)))
        {
            ShouldRefresh = true;
            await LoadAsync();
        }
    }
}
