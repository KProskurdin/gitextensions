using GitExtensions.Extensibility.Configurations;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.App;

// The window's side of the left panel's commands (RepoObjectMenuViewModel.Commands, MVVM.md): the windows, questions and
// flows they start, as the window's methods start them.
public partial class MainWindow : IRepoObjectWindow
{
    // What the left panel's menu acts on now.
    private RepoObjectMenuInput RepoObjectMenuState()
    {
        RepoObjectNode? node = SelectedRepoObject;
        Uri? url = RemoteWebUrl(node);
        return new RepoObjectMenuInput(_repoObjects, node, IsOpen: RepositoryPath is not null && !IsBusy,
            SelectedRevisions.Count, _repository.Remotes.Count, HasRemoteWebUrl: url is not null,
            IsBareRepository: ShownSubmoduleStructure(RepositoryPath ?? "")?.IsBareRepository ?? false,
            _preferences.RefsSortBy, RepositoryPath, url);
    }

    void IRepoObjectWindow.FilterBranches(string branchFilter)
    {
        _preferences.ShowCurrentBranchOnly = false;
        ApplyFilter(_commits.Filter with { CurrentBranchOnly = false, ShowFilteredBranches = true, BranchFilter = branchFilter });
    }

    void IRepoObjectWindow.CheckoutSelectedBranch() => CheckoutSelectedBranch();

    void IRepoObjectWindow.RebaseOnSelectedBranch() => RebaseOnSelectedBranch();

    void IRepoObjectWindow.DeleteSelectedRepoObject() => DeleteSelectedRepoObject();

    Task<bool> IBrowseWindow.MergeBranchAsync(string path, string branch) => ShowMergeAsync(path, branch);

    Task<bool> IRepoObjectWindow.RebaseOnRevisionAsync(string path, string revision) => ShowRebaseAsync(path, revision);

    Task IRepoObjectWindow.PromptCreateBranchInFolderAsync(string folder)
        => PromptCreateBranchAsync(startPoint: null, namePrefix: $"{folder}/");

    Task IBrowseWindow.RenameBranchAsync(string branch) => RenameBranchAsync(branch);

    Task<bool> IRepoObjectWindow.DeleteRemoteTagAsync(string path, string tag) => DeleteRemoteTagAfterConfirmAsync(path, tag);

    Task<bool> IBrowseWindow.DeleteBranchesAsync(string path, IReadOnlyList<string> branches)
        => ShowDeleteBranchAsync(path, branches);

    Task IRepoObjectWindow.ManageRemoteAsync(string remote) => ShowRemotesAsync(this, remote);

    void IRepoObjectWindow.Fetch(string remote, bool prune) => Fetch(remote, prune);

    void IRepoObjectWindow.SetRemoteActive(string remote, bool active, bool fetch) => SetRemoteActive(remote, active, fetch);

    Task IRepoObjectWindow.OpenUrlAsync(Uri url) => Launcher.LaunchUriAsync(url);

    Task<bool> IBrowseWindow.CheckoutRemoteBranchAsync(string path, string branch)
        => StartCheckoutBranchAsync(path, branch, remote: true);

    Task<bool> IRepoObjectWindow.PullBranchAsync(string path, string remote, string branch, bool rebase)
        => PullAndUpdateSubmodulesAsync(path, () => QuickPullAsync(path, remote, branch, rebase), remote);

    Task IRepoObjectWindow.StashAllAsync() => StashAsync();

    Task IRepoObjectWindow.ShowStashAsync(string stash) => ShowStashWindowAsync(this, initialStash: stash);

    Task<bool> IBrowseWindow.ConfirmDropStashAsync()
        => ConfirmAsync(Confirmation.StashDrop, Confirmations.StashDropQuestion, "Drop", "Drop Stash Confirmation");

    void IRepoObjectWindow.OpenWorktree(string worktreePath) => Run(() => WorktreeFlow().SwitchAsync(this, worktreePath));

    Task<bool> IRepoObjectWindow.DeleteWorktreeAsync(string path, string worktreePath)
        => WorktreeFlow().DeleteAsync(this, path, worktreePath);

    Task IBrowseWindow.CopyTextAsync(string text) => CopyTextAsync(text);

    void IRepoObjectWindow.MoveSelectedTree(bool up) => MoveSelectedTree(up);

    void IRepoObjectWindow.SetRefsSortBy(GitRefsSortBy sortBy) => SaveRefsSort(() => _preferences.RefsSortBy = sortBy);

    void IRepoObjectWindow.SetRefsSortOrder(GitRefsSortOrder order) => SaveRefsSort(() => _preferences.RefsSortOrder = order);

    private void SaveRefsSort(Action choose)
    {
        choose();
        _preferences.Save();
        RefreshRepository();
    }

    void IRepoObjectWindow.OpenSubmodule(SubmoduleProject project) => OpenSubmodule(project);

    void IRepoObjectWindow.OpenSubmoduleInNewInstance(SubmoduleProject project) => OpenSubmoduleInNewInstance(project);

    Task IRepoObjectWindow.UpdateSubmoduleAsync(SubmoduleProject project)
        => InSubmoduleRepositoryAsync(FolderOf(project.SuperPath), superPath => UpdateSubmodulesAsync(superPath,
            project.LocalPath.Length == 0 ? null : project.LocalPath));

    Task IRepoObjectWindow.ResetSubmoduleAsync(SubmoduleProject project) => ResetSubmoduleAsync(FolderOf(project.Path));

    Task IRepoObjectWindow.StashSubmoduleAsync(SubmoduleProject project)
        => InSubmoduleRepositoryAsync(FolderOf(project.Path), path =>
            _actions.StashAsync(path, "", _preferences.IncludeUntrackedFilesInManualStash, keepIndex: false));

    Task IRepoObjectWindow.CommitSubmoduleAsync(SubmoduleProject project) => CommitSubmoduleAsync(FolderOf(project.Path));
}
