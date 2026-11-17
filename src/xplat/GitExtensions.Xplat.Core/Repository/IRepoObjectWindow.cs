using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  What the left panel's commands (<see cref="RepoObjectMenuViewModel"/>) ask of the browse window besides
///  <see cref="IBrowseWindow"/>: upstream's RepoObjectsTree actions that open a window, ask a question or run one of the
///  window's flows (scripts around a fetch, PuTTY keys, worktree switches).
/// </summary>
public interface IRepoObjectWindow : IBrowseWindow
{
    /// <summary>
    ///  Upstream's "Filter for selected": the grid shows the branches of <paramref name="branchFilter"/>.
    /// </summary>
    void FilterBranches(string branchFilter);

    void CheckoutSelectedBranch();

    void RebaseOnSelectedBranch();

    void DeleteSelectedRepoObject();

    Task<bool> RebaseOnRevisionAsync(string path, string revision);

    Task PromptCreateBranchInFolderAsync(string folder);

    Task<bool> DeleteRemoteTagAsync(string path, string tag);

    Task ManageRemoteAsync(string remote);

    /// <summary>
    ///  Upstream's fetch: a prune asks first, and the scripts and PuTTY keys go around it.
    /// </summary>
    void Fetch(string remote, bool prune);

    void SetRemoteActive(string remote, bool active, bool fetch);

    Task OpenUrlAsync(Uri url);

    /// <summary>
    ///  Upstream's pull of one branch of a remote, with the submodules updated after.
    /// </summary>
    Task<bool> PullBranchAsync(string path, string remote, string branch, bool rebase);

    Task StashAllAsync();

    Task ShowStashAsync(string stash);

    void OpenWorktree(string worktreePath);

    Task<bool> DeleteWorktreeAsync(string path, string worktreePath);

    void MoveSelectedTree(bool up);

    /// <summary>
    ///  Upstream's sort choice: stored and saved, and the repository read again.
    /// </summary>
    void SetRefsSortBy(GitRefsSortBy sortBy);

    /// <inheritdoc cref="SetRefsSortBy"/>
    void SetRefsSortOrder(GitRefsSortOrder order);

    void OpenSubmodule(SubmoduleProject project);

    void OpenSubmoduleInNewInstance(SubmoduleProject project);

    Task UpdateSubmoduleAsync(SubmoduleProject project);

    Task ResetSubmoduleAsync(SubmoduleProject project);

    Task StashSubmoduleAsync(SubmoduleProject project);

    Task CommitSubmoduleAsync(SubmoduleProject project);
}
