using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Lists and manages the repository's worktrees: the new shell's version of upstream <c>FormManageWorktree</c> and
///  <c>FormCreateWorktree</c>. Closes with the path of a worktree to open, or null.
/// </summary>
public partial class WorktreesWindow : Window
{
    private readonly string _repositoryPath;
    private readonly IRepositoryService _repositoryService;
    private readonly RepositoryOperationsViewModel _actions;

    public WorktreesWindow(string repositoryPath, IRepositoryService repositoryService,
        RepositoryOperationsViewModel actions)
    {
        _repositoryPath = repositoryPath;
        _repositoryService = repositoryService;
        _actions = actions;
        InitializeComponent();
        WorktreeList.SelectionChanged += (_, _) => UpdateButtons();
        OpenWorktreeButton.Click += (_, _) => Close(SelectedWorktree()?.Path);
        AddWorktreeButton.Click += (_, _) => Run(AddAsync);
        RemoveWorktreeButton.Click += (_, _) => Run(RemoveAsync);
        PruneWorktreesButton.Click += (_, _) =>
            Run(() => RunThenReloadAsync(() => _actions.PruneWorktreesAsync(_repositoryPath)));
        CloseButton.Click += (_, _) => Close(null);
        Opened += (_, _) => Run(LoadAsync);
        UpdateButtons();
    }

    /// <summary>
    ///  The worktrees on show.
    /// </summary>
    public IReadOnlyList<WorktreeInfo> Worktrees { get; private set; } = [];

    private void Run(Func<Task> action) => UiActions.Run(action, ex => ErrorText.Text = ex.Message);

    private WorktreeInfo? SelectedWorktree() => WorktreeList.SelectedItem as WorktreeInfo;

    private async Task LoadAsync()
    {
        Worktrees = await _repositoryService.GetWorktreesAsync(_repositoryPath);
        WorktreeList.ItemsSource = Worktrees;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        WorktreeInfo? selected = SelectedWorktree();
        OpenWorktreeButton.IsEnabled = selected is { IsDeleted: false };

        // As upstream (065da3680), the main worktree holds the repository and is never offered for removal.
        RemoveWorktreeButton.IsEnabled = selected is { IsMain: false };
    }

    private async Task AddAsync()
    {
        PromptResult? result = await new PromptWindow("Add worktree",
                "Folder of the new worktree. It starts at the current commit, on a new branch when a name is given.",
                "Add",
                secondPlaceholder: "New branch name (optional; empty: detached at the current commit)")
            .ShowDialog<PromptResult?>(this);
        if (result is not null)
        {
            // The new branch's name is fixed up as upstream's FormCreateWorktree does.
            IAppPreferences preferences = AppServices.Preferences;
            string branch = BranchNames.Normalise(result.SecondValue, preferences.AutoNormaliseBranchName,
                preferences.AutoNormaliseSymbol);
            await RunThenReloadAsync(() => _actions.AddWorktreeAsync(_repositoryPath, result.Value, "HEAD", branch));
        }
    }

    private async Task RemoveAsync()
    {
        if (SelectedWorktree() is not { IsMain: false } worktree)
        {
            return;
        }

        string message =
            $"Remove the worktree {worktree.Path}? Its folder is deleted; changes that are not committed are lost.";
        if (await new ConfirmWindow(message, "Remove").ShowDialog<bool>(this))
        {
            await RunThenReloadAsync(() => _actions.RemoveWorktreeAsync(_repositoryPath, worktree.Path, force: true));
        }
    }

    private async Task RunThenReloadAsync(Func<Task<bool>> action)
    {
        ErrorText.Text = "";
        if (!await action())
        {
            ErrorText.Text = _actions.ErrorMessage ?? "";
            _actions.ClearError();
        }

        await LoadAsync();
    }
}
