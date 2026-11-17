using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Operations;

namespace GitExtensions.Xplat.Core.Repository;

// The left panel's menu commands (upstream's RepoObjectsTree.ContextActions, MVVM.md): each takes the node selected when
// it runs (Read), decides what it acts on (the ref, the remote, the folder's branches, the marked nodes), runs git through
// the actions' view model and asks the window (IRepoObjectWindow) for the rest.
public sealed partial class RepoObjectMenuViewModel
{
    private RepositoryOperationsViewModel? _actions;
    private IRepoObjectWindow? _window;

    /// <summary>
    ///  What the menu acts on now: the tree, the selected node and the repository; set by the window.
    /// </summary>
    public Func<RepoObjectMenuInput>? Read { get; set; }

    /// <summary>
    ///  Connects the commands to the actions' view model and the window.
    /// </summary>
    public void Attach(RepositoryOperationsViewModel actions, IRepoObjectWindow window)
    {
        _actions = actions;
        _window = window;
    }

    private IRepoObjectWindow Window => _window ?? throw new InvalidOperationException("The window is not attached.");

    private RepositoryOperationsViewModel Actions
        => _actions ?? throw new InvalidOperationException("The window is not attached.");

    private RepoObjectNode? Node => Read?.Invoke().Node;

    private string? OpenPath => Read?.Invoke().RepositoryPath;

    // Upstream's "Filter for selected" path of a ref: the branch name with its remote, or the tag name.
    private static string? RefName(RepoObjectNode? node)
        => node switch
        {
            { Branch: { } branch } => branch.Name,
            { Kind: RepoObjectKind.Tag, Item: string tag } => tag,
            _ => null,
        };

    [RelayCommand]
    private void RunFilterSelected()
    {
        if (Read?.Invoke() is { } input && RepoObjectSelection.RefsFilter(Targets(input.Roots, input.Node)) is { } name)
        {
            Window.FilterBranches(name);
        }
    }

    // A tag acts as a revision; branches go through the branch actions, which skip the checked-out branch.
    [RelayCommand]
    private void RunCheckoutRef()
    {
        switch (Node)
        {
            case { Kind: RepoObjectKind.Tag, Item: string tag }:
                OnRepository(path => Window.CheckoutRevisionAsync(path, tag));
                break;
            case { Branch: not null }:
                Window.CheckoutSelectedBranch();
                break;
        }
    }

    [RelayCommand]
    private void RunMergeRef()
    {
        if (RefName(Node) is { } name)
        {
            OnRepository(path => Window.MergeBranchAsync(path, name));
        }
    }

    [RelayCommand]
    private void RunRebaseOnRef()
    {
        switch (Node)
        {
            case { Kind: RepoObjectKind.Tag, Item: string tag }:
                OnRepository(path => Window.RebaseOnRevisionAsync(path, tag));
                break;
            case { Branch: not null }:
                Window.RebaseOnSelectedBranch();
                break;
        }
    }

    [RelayCommand]
    private void RunCreateBranchFromRef()
    {
        if (RefName(Node) is { } name)
        {
            Run(() => Window.PromptCreateBranchAsync(name));
        }
    }

    [RelayCommand]
    private void RunResetToRef()
    {
        if (RefName(Node) is { } name)
        {
            OnRepository(path => Window.ResetCurrentBranchAsync(path, name));
        }
    }

    [RelayCommand]
    private void RunRenameBranch()
    {
        if (Node?.Branch is { IsRemote: false } branch)
        {
            Run(() => Window.RenameBranchAsync(branch.Name));
        }
    }

    [RelayCommand]
    private void RunDeleteRef() => Window.DeleteSelectedRepoObject();

    [RelayCommand]
    private void RunDeleteRemoteTag()
    {
        if (Node is { Kind: RepoObjectKind.Tag, Item: string tag })
        {
            OnRepository(path => Window.DeleteRemoteTagAsync(path, tag));
        }
    }

    [RelayCommand]
    private void RunManageRemotes() => Run(Window.ShowRemotesAsync);

    [RelayCommand]
    private void RunFetchAllRemotes() => Window.Fetch("--all", prune: false);

    [RelayCommand]
    private void RunPruneAllRemotes() => Window.Fetch("--all", prune: true);

    [RelayCommand]
    private void RunManageRemote() => OnRemote(name => Run(() => Window.ManageRemoteAsync(name)));

    // Upstream's RemoteRepoNode.Enable and Disable; an activated remote can be fetched at once.
    [RelayCommand]
    private void RunActivateRemote() => OnRemote(name => Window.SetRemoteActive(name, active: true, fetch: false));

    [RelayCommand]
    private void RunActivateAndFetchRemote() => OnRemote(name => Window.SetRemoteActive(name, active: true, fetch: true));

    [RelayCommand]
    private void RunDeactivateRemote() => OnRemote(name => Window.SetRemoteActive(name, active: false, fetch: false));

    [RelayCommand]
    private void RunFetchRemote() => OnRemote(name => Window.Fetch(name, prune: false));

    [RelayCommand]
    private void RunPruneRemote() => OnRemote(name => Window.Fetch(name, prune: true));

    // Upstream's RemoteRepoNode.IsRemoteUrlUsingHttp: only a web address opens in the browser.
    [RelayCommand]
    private void RunOpenRemoteUrl()
    {
        if (Read?.Invoke().RemoteWebUrl is { } url)
        {
            Run(() => Window.OpenUrlAsync(url));
        }
    }

    // Upstream's RemoteBranchNode actions; the new shell fetches the whole remote.
    [RelayCommand]
    private void RunFetchAndCheckout()
        => OnRemoteBranch((path, remote, _, branch) =>
            FetchThenAsync(path, remote, () => Window.CheckoutRemoteBranchAsync(path, branch)));

    [RelayCommand]
    private void RunFetchAndMerge()
        => OnRemoteBranch((path, remote, name, _) => Window.PullBranchAsync(path, remote, name, rebase: false));

    [RelayCommand]
    private void RunFetchAndRebase()
        => OnRemoteBranch((path, remote, name, _) => Window.PullBranchAsync(path, remote, name, rebase: true));

    [RelayCommand]
    private void RunFetchAndCreateBranch()
        => OnRemoteBranch((path, remote, _, branch) => FetchThenAsync(path, remote, async () =>
        {
            await Window.PromptCreateBranchAsync(branch);
            return true;
        }));

    [RelayCommand]
    private void RunFetchBranch() => OnRemoteBranch((path, remote, _, _) => Actions.FetchAsync(path, remote, prune: false));

    [RelayCommand]
    private void RunCreateBranchInFolder()
    {
        if (Node is { Kind: RepoObjectKind.Folder, Item: string folder })
        {
            Run(() => Window.PromptCreateBranchInFolderAsync(folder));
        }
    }

    // Upstream's BranchPathNode.DeleteAll: upstream's delete branch window with the folder's branches but the current.
    [RelayCommand]
    private void RunDeleteAllBranches()
    {
        if (Node is { Kind: RepoObjectKind.Folder } folder)
        {
            List<string> branches =
            [
                .. folder.Descendants().Select(node => node.Branch)
                    .OfType<BranchInfo>().Where(branch => !branch.IsCurrent).Select(branch => branch.Name)
            ];
            OnRepository(path => Window.DeleteBranchesAsync(path, branches));
        }
    }

    [RelayCommand]
    private void RunStashAll() => Run(Window.StashAllAsync);

    [RelayCommand]
    private void RunStashStaged() => OnRepository(Actions.StashStagedAsync);

    [RelayCommand]
    private void RunManageStashes() => Run(Window.ShowStashesAsync);

    [RelayCommand]
    private void RunOpenStash() => OnItem<StashInfo>(stash => Run(() => Window.ShowStashAsync(stash.Name)));

    [RelayCommand]
    private void RunApplyStash() => OnItem<StashInfo>(stash => OnRepository(path => Actions.ApplyStashAsync(path, stash.Name)));

    [RelayCommand]
    private void RunPopStash() => OnItem<StashInfo>(stash => OnRepository(path => Actions.PopStashAsync(path, stash.Name)));

    [RelayCommand]
    private void RunDropStash()
        => OnItem<StashInfo>(stash => OnRepository(async path =>
            await Window.ConfirmDropStashAsync() && await Actions.DropStashAsync(path, stash.Name)));

    [RelayCommand]
    private void RunCreateWorktree() => Run(Window.CreateWorktreeAsync);

    [RelayCommand]
    private void RunPruneWorktrees() => OnRepository(Window.PruneWorktreesAsync);

    [RelayCommand]
    private void RunManageWorktrees() => Run(Window.ShowWorktreesAsync);

    [RelayCommand]
    private void RunOpenWorktree() => OnItem<WorktreeInfo>(worktree => Window.OpenWorktree(worktree.Path));

    [RelayCommand]
    private void RunDeleteWorktree()
        => OnItem<WorktreeInfo>(worktree => OnRepository(path => Window.DeleteWorktreeAsync(path, worktree.Path)));

    [RelayCommand]
    private void RunCopyWorktreePath() => OnItem<WorktreeInfo>(worktree => Run(() => Window.CopyTextAsync(worktree.Path)));

    [RelayCommand]
    private void RunShowWorktreeInFolder()
        => OnItem<WorktreeInfo>(worktree =>
        {
            if (OpenPath is not null)
            {
                Guard(() => Window.OpenFolder(worktree.Path));
            }
        });

    // Upstream's "Collapse all" of the left panel's toolstrip.
    [RelayCommand]
    private void RunCollapseAll()
    {
        if (Read?.Invoke() is not { } input)
        {
            return;
        }

        foreach (RepoObjectNode node in input.Roots.SelectMany(root => root.Descendants()))
        {
            node.IsExpanded = false;
        }
    }

    [RelayCommand]
    private void RunCollapseNodes()
    {
        if (Read?.Invoke() is not { } input)
        {
            return;
        }

        foreach (RepoObjectNode target in Targets(input.Roots, input.Node).Where(target => target.IsExpanded))
        {
            target.IsExpanded = false;
        }
    }

    // Upstream's ExpandAll of each collapsed one.
    [RelayCommand]
    private void RunExpandNodes()
    {
        if (Read?.Invoke() is not { } input)
        {
            return;
        }

        foreach (RepoObjectNode node in Targets(input.Roots, input.Node)
                     .Where(target => target.Children.Count > 0 && !target.IsExpanded)
                     .SelectMany(target => target.Descendants()).ToList())
        {
            node.IsExpanded = true;
        }
    }

    [RelayCommand]
    private void RunMoveTreeUp() => Window.MoveSelectedTree(up: true);

    [RelayCommand]
    private void RunMoveTreeDown() => Window.MoveSelectedTree(up: false);

    [RelayCommand]
    private void RunOpenSubmoduleProject() => OnItem<SubmoduleProject>(Window.OpenSubmodule);

    [RelayCommand]
    private void RunOpenSubmoduleInNewInstance() => OnItem<SubmoduleProject>(Window.OpenSubmoduleInNewInstance);

    [RelayCommand]
    private void RunManageSubmodules()
    {
        if (OpenPath is { } path)
        {
            Run(() => Window.ManageSubmodulesAsync(path));
        }
    }

    [RelayCommand]
    private void RunUpdateSubmodule() => OnItem<SubmoduleProject>(project => Run(() => Window.UpdateSubmoduleAsync(project)));

    [RelayCommand]
    private void RunSynchronizeSubmodules() => OnRepository(path => Actions.SyncSubmodulesAsync(path, path: null));

    [RelayCommand]
    private void RunResetSubmodule() => OnItem<SubmoduleProject>(project => Run(() => Window.ResetSubmoduleAsync(project)));

    [RelayCommand]
    private void RunStashSubmodule() => OnItem<SubmoduleProject>(project => Run(() => Window.StashSubmoduleAsync(project)));

    [RelayCommand]
    private void RunCommitSubmodule() => OnItem<SubmoduleProject>(project => Run(() => Window.CommitSubmoduleAsync(project)));

    private async Task<bool> FetchThenAsync(string path, string remote, Func<Task<bool>> then)
        => await Actions.FetchAsync(path, remote, prune: false) && await then();

    private void OnItem<T>(Action<T> action)
        where T : class
    {
        if (Node?.Item is T item)
        {
            action(item);
        }
    }

    private void OnRemote(Action<string> action)
    {
        if (Node is { Kind: RepoObjectKind.Remote, Item: string name })
        {
            action(name);
        }
    }

    // A remote branch's remote and its name there ("origin/feature/x": "origin" and "feature/x").
    private void OnRemoteBranch(Func<string, string, string, string, Task<bool>> action)
    {
        if (Node?.Branch is { IsRemote: true } branch && branch.Name.IndexOf('/') is var slash and > 0)
        {
            OnRepository(path => action(path, branch.Name[..slash], branch.Name[(slash + 1)..], branch.Name));
        }
    }

    private void OnRepository(Func<string, Task<bool>> action)
    {
        if (OpenPath is { } path)
        {
            Run(() => action(path));
        }
    }

    private void Run(Func<Task> action) => _ = RunAsync(action);

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Window.ShowError(ex.Message);
        }
    }

    private void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Window.ShowError(ex.Message);
        }
    }
}
