using Avalonia.Controls;
using GitCommands;
using GitCommands.Remotes;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's Repository > Manage submodules... (FormBrowse's <c>ManageSubmodulesToolStripMenuItemClick</c>, which opens
///  <c>FormSubmodules</c>).
/// </summary>
public partial class MainWindow
{
    private void WireSubmodulesMenu()
        => ManageSubmodulesMenuItem.Click +=
            (_, _) => RunOnRepositoryFolder(path => Run(() => ManageSubmodulesAsync(path)));

    // Upstream's StartSubmodulesDialog, then UpdateSubmodulesStructure: the repository is read again.
    private async Task ManageSubmodulesAsync(string path)
    {
        await NewSubmodulesWindow(path).ShowDialog(this);
        RefreshRepository();
    }

    private SubmodulesWindow NewSubmodulesWindow(string path)
        => new(path, arguments => RunGitWithOutputAsync(path, arguments), PullSubmoduleAsync);

    // FormSubmodules' Pull_Click: upstream's pull dialog on the submodule (UICommands.WithGitModule(submodule)), filled
    // from the submodule's own remotes and branches.
    private async Task PullSubmoduleAsync(Window owner, string submodulePath)
    {
        RepositorySnapshot snapshot = await _repositoryService.GetSnapshotAsync(submodulePath);
        PullDialog dialog = new(snapshot.Remotes ?? [], snapshot.CurrentBranch ?? "", GitPullAction.None,
            _preferences.DefaultPullAction, defaultRemote: null, snapshot.TrackingRemote);
        PullWindowGit git = new(
            async branch => await AppServices.GitConfig.GetAsync(ConfigScope.Local, $"branch.{branch}.remote",
                submodulePath) is { Length: > 0 } remote
                ? remote
                : null,
            (remote, remoteBranch, currentBranch) =>
                _actions.RebasesMergeCommitAsync(submodulePath, remote, remoteBranch, currentBranch),
            async (remotesOwner, remote) =>
            {
                await new RemotesWindow(submodulePath, _preferences,
                    fetch => _actions.FetchAsync(submodulePath, fetch, prune: false), remote).ShowDialog(remotesOwner);
                return await Task.Run(() => new ConfigFileRemoteSettingsManager(() => GitModules.Open(submodulePath))
                    .LoadRemotes(false).Select(loaded => loaded.Name ?? "").ToList());
            });
        PullRequest? request = await new PullWindow(dialog, _preferences,
                PathUtil.GetDisplayPath(submodulePath.EnsureTrailingPathSeparator()),
                [.. snapshot.Branches.Where(branch => branch.IsRemote).Select(branch => branch.Name)],
                [.. snapshot.Branches.Where(branch => !branch.IsRemote).Select(branch => branch.Name)],
                await Task.Run(() => PullDialog.IsShallowRepository(submodulePath)), git)
            .ShowDialog<PullRequest?>(owner);
        if (request is not null)
        {
            await _actions.PullAsync(submodulePath, request);
        }
    }
}
