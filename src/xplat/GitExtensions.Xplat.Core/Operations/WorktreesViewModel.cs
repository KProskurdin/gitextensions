using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  What the worktrees window does with a worktree, with upstream's questions and windows: the app's
///  <c>WorktreeFlow</c> for the window.
/// </summary>
public interface IWorktreeActions
{
    /// <summary>
    ///  Opens the worktree at <paramref name="worktreePath"/>; true when it was opened.
    /// </summary>
    Task<bool> SwitchAsync(string worktreePath);

    /// <summary>
    ///  Deletes the worktree at <paramref name="worktreePath"/>; true when it was deleted.
    /// </summary>
    Task<bool> DeleteAsync(string repositoryPath, string worktreePath);

    /// <summary>
    ///  Prunes the worktrees whose folders are gone.
    /// </summary>
    Task<bool> PruneAsync(string repositoryPath);

    /// <summary>
    ///  Creates a worktree beside <paramref name="mainWorktreePath"/>; true when one was created.
    /// </summary>
    Task<bool> CreateAsync(string repositoryPath, string mainWorktreePath);
}

/// <summary>
///  The view model of upstream's <c>FormManageWorktree</c> (rules in <see cref="ManageWorktreeDialog"/>): the worktrees,
///  open or delete the selected one, prune the deleted ones and create one. Closes its window after a worktree is opened;
///  <see cref="ShouldRefresh"/> says whether one was created.
/// </summary>
public sealed partial class WorktreesViewModel : DialogViewModel
{
    private readonly string _repositoryPath;
    private readonly IRepositoryService _repositoryService;
    private readonly IWorktreeActions _actions;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public WorktreesViewModel(IDialogHost host, string repositoryPath, IRepositoryService repositoryService,
        IWorktreeActions actions)
        : base(host)
    {
        _repositoryPath = repositoryPath;
        _repositoryService = repositoryService;
        _actions = actions;
    }

    /// <summary>
    ///  The worktrees on show.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(PruneCommand))]
    public partial IReadOnlyList<WorktreeInfo> Worktrees { get; private set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    public partial int SelectedIndex { get; set; } = -1;

    /// <summary>
    ///  Upstream's <c>ShouldRefreshRevisionGrid</c>: a worktree was created, so its new branch shows in the grid.
    /// </summary>
    public bool ShouldRefresh { get; private set; }

    /// <summary>
    ///  Completes when the worktrees are first shown, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    private bool CanOpen => ManageWorktreeDialog.CanOpen(Worktrees, SelectedIndex, _repositoryPath);

    private bool CanDelete => ManageWorktreeDialog.CanDelete(Worktrees, SelectedIndex, _repositoryPath);

    // Upstream's Initialize: Prune only when one other than the main worktree lost its folder.
    private bool CanPrune => ManageWorktreeDialog.CanPrune(Worktrees);

    /// <summary>
    ///  Upstream's <c>Initialize</c>: reads the worktrees. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(ReloadAsync);

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private Task OpenAsync() => GuardAsync(async () =>
    {
        if (CanOpen && await _actions.SwitchAsync(Worktrees[SelectedIndex].Path))
        {
            Host.Close(null);
        }
    });

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private Task DeleteAsync() => GuardAsync(async () =>
    {
        if (CanDelete && await _actions.DeleteAsync(_repositoryPath, Worktrees[SelectedIndex].Path))
        {
            await ReloadAsync();
        }
    });

    [RelayCommand(CanExecute = nameof(CanPrune))]
    private Task PruneAsync() => GuardAsync(async () =>
    {
        await _actions.PruneAsync(_repositoryPath);
        await ReloadAsync();
    });

    [RelayCommand]
    private Task CreateAsync() => GuardAsync(async () =>
    {
        if (await _actions.CreateAsync(_repositoryPath,
                ManageWorktreeDialog.MainWorktreePath(Worktrees, _repositoryPath)))
        {
            ShouldRefresh = true;
            await ReloadAsync();
        }
    });

    private async Task ReloadAsync()
    {
        Worktrees = await _repositoryService.GetWorktreesAsync(_repositoryPath);
        _loaded.TrySetResult();
    }
}
