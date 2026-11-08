using Avalonia.Controls;
using GitCommands;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's left panel <c>SubmoduleTree</c>: the top project, its submodules recursively in their folders, with their
///  status images and tooltips (core <see cref="SubmoduleStructure"/>, built by <see cref="RepoObjectsTree"/>), and the
///  submodule items of the tree's menu (<c>RepoObjectsTree.ContextActions</c>).
/// </summary>
public partial class MainWindow
{
    // Upstream's MessageBoxes texts.
    private const string SubmoduleDirectoryDoesNotExistText = "The directory \"{0}\" does not exist for submodule \"{1}\".";
    private const string CannotOpenSubmoduleCaption = "Cannot open submodule";
    private const string DirectoryDoesNotExistText = "The directory \"{0}\" does not exist.";

    // Upstream's FormBrowse.Designer tooltip of toolStripButtonLevelUp.
    private const string SubmodulesText = "Submodules";

    // The last structure read, shown (for its repository only) each time the tree is filled again.
    private SubmoduleStructure? _submoduleStructure;
    private string? _submoduleStructureRepository;

    // A newer read cancels the older one, as upstream's CancellationTokenSequence.
    private CancellationTokenSource? _submoduleStructureCancellation;

    // The repositories other than the shown one that the tree's actions, or a submodule's commit window, work in.
    private readonly List<string> _submoduleActionRepositories = [];

    // Upstream's UpdateSubmodulesStructure after each refresh, with the status update upstream's git status monitor then
    // asks for when upstream's ShowSubmoduleStatus is on (offered only while the git status is shown).
    private void LoadSubmoduleStructure()
    {
        // Not disposed: the read it was handed to may still look at its token.
        _submoduleStructureCancellation?.Cancel();
        if (RepositoryPath is not { } path)
        {
            return;
        }

        CancellationTokenSource cancellation = new();
        _submoduleStructureCancellation = cancellation;
        bool showStatus = _preferences.ShowSubmoduleStatus
                          && (_preferences.ShowGitStatusInBrowseToolbar || _preferences.ShowGitStatusForArtificialCommits);
        Run(async () =>
        {
            SubmoduleStructure structure;
            try
            {
                structure = await SubmoduleStructure.LoadAsync(path,
                    UpstreamTranslation.ByText(SubmoduleStructure.NoBranchText), showStatus, cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                return;
            }

            // The tree is filled again only for a changed structure, so that a refresh does not refill it twice.
            if (cancellation.IsCancellationRequested || RepositoryPath != path
                || (ShownSubmoduleStructure(path) is { } shown && shown.SameAs(structure)))
            {
                return;
            }

            _submoduleStructure = structure;
            _submoduleStructureRepository = path;
            ShowRepoObjects();
            ShowSubmodulesButton();
        });
    }

    // Upstream's toolStripButtonLevelUp: in a submodule its image goes up and it opens the superproject; it is disabled in a
    // bare repository.
    private void ShowSubmodulesButton()
    {
        SubmoduleStructure? structure = RepositoryPath is { } path ? ShownSubmoduleStructure(path) : null;
        bool inSubmodule = structure?.SuperProject is not null;
        SubmodulesButton.Image = inSubmodule ? "NavigateUp" : "SubmodulesManage";
        ToolTip.SetTip(SubmodulesButton,
            UpstreamTranslation.ByText(inSubmodule ? SubmoduleStructure.GoToSuperprojectText : SubmodulesText));
        SubmodulesButton.IsEnabled = !(_commits.IsLoading || _repository.IsLoading || _actions.IsBusy)
                                     && RepositoryPath is not null && structure?.IsBareRepository is not true;
    }

    // Upstream's toolStripButtonLevelUp_ButtonClick: the superproject, else the drop-down.
    private void OnSubmodulesButtonClick()
    {
        if (RepositoryPath is { } path && ShownSubmoduleStructure(path)?.SuperProject is { } superproject)
        {
            Run(() => OpenSubmoduleProjectAsync(superproject));
        }
        else
        {
            SubmodulesButton.Flyout?.ShowAt(SubmodulesButton);
        }
    }

    // Upstream's SubmoduleToolStripButtonClick: a project of the drop-down opens as the browse window's repository.
    private async Task OpenSubmoduleProjectAsync(SubmoduleProject project)
    {
        if (!Directory.Exists(project.Path))
        {
            await new MessageBoxWindow(string.Format(UpstreamTranslation.ByText(DirectoryDoesNotExistText), project.Path),
                UpstreamTranslation.ByText(CannotOpenSubmoduleCaption), MessageBoxButtons.OK).ShowDialog(this);
            return;
        }

        await OpenRepositoryAsync(FolderOf(project.Path));
    }

    // Upstream's UpdateSubmoduleToolStripMenuItemClick: the shown repository is updated from its superproject.
    private async Task UpdateCurrentSubmoduleAsync(SubmoduleStructure structure)
    {
        if (RepositoryPath is { } path && structure.SuperProject is { } superproject)
        {
            string superPath = FolderOf(superproject.Path);
            await InSubmoduleRepositoryAsync(superPath, repository =>
                UpdateSubmodulesAsync(repository, Path.GetRelativePath(superPath, path).ToPosixPath()));
        }
    }

    // Another repository's structure is not shown for this one.
    private SubmoduleStructure? ShownSubmoduleStructure(string path)
        => _submoduleStructureRepository == path ? _submoduleStructure : null;

    private void WireSubmoduleTreeMenu()
    {
        TreeOpenSubmoduleMenuItem.Click += (_, _) => OnSelected<SubmoduleProject>(OpenSubmodule);
        TreeOpenSubmoduleNewInstanceMenuItem.Click += (_, _) => OnSelected<SubmoduleProject>(OpenSubmoduleInNewInstance);
        TreeManageSubmodulesMenuItem.Click += (_, _) => RunOnRepositoryFolder(path => Run(() => ManageSubmodulesAsync(path)));
        TreeUpdateSubmoduleMenuItem.Click += (_, _) => OnSelected<SubmoduleProject>(submodule =>
            Run(() => InSubmoduleRepositoryAsync(FolderOf(submodule.SuperPath), superPath => UpdateSubmodulesAsync(superPath,
                submodule.LocalPath.Length == 0 ? null : submodule.LocalPath))));
        TreeSynchronizeSubmoduleMenuItem.Click += (_, _) =>
            RunOnRepository(path => _actions.SyncSubmodulesAsync(path, path: null));
        TreeResetSubmoduleMenuItem.Click += (_, _) => OnSelected<SubmoduleProject>(submodule =>
            Run(() => ResetSubmoduleAsync(FolderOf(submodule.Path))));
        TreeStashSubmoduleMenuItem.Click += (_, _) => OnSelected<SubmoduleProject>(submodule =>
            Run(() => InSubmoduleRepositoryAsync(FolderOf(submodule.Path), path =>
                _actions.StashAsync(path, "", _preferences.IncludeUntrackedFilesInManualStash, keepIndex: false))));
        TreeCommitSubmoduleMenuItem.Click += (_, _) => OnSelected<SubmoduleProject>(submodule =>
            Run(() => CommitSubmoduleAsync(FolderOf(submodule.Path))));
    }

    // Upstream's EnableSubmoduleContextMenu: Open for a project other than the shown one; Manage and Synchronize for the
    // shown one; Reset, Stash and Commit unless the shown repository is bare.
    private void ShowSubmoduleTreeMenu(RepoObjectKind? kind, RepoObjectNode? node)
    {
        SubmoduleProject? project = kind == RepoObjectKind.Submodule ? node?.Item as SubmoduleProject : null;
        bool bare = ShownSubmoduleStructure(RepositoryPath ?? "")?.IsBareRepository ?? false;
        Show(TreeOpenSubmoduleMenuItem, project is { IsCurrent: false });
        Show(TreeOpenSubmoduleNewInstanceMenuItem, project is not null);
        Show(TreeManageSubmodulesMenuItem, project is { IsCurrent: true } && !bare);
        Show(TreeUpdateSubmoduleMenuItem, project is not null);
        Show(TreeSynchronizeSubmoduleMenuItem, project is { IsCurrent: true } && !bare);
        Show(TreeResetSubmoduleMenuItem, project is not null && !bare);
        Show(TreeStashSubmoduleMenuItem, project is not null && !bare);
        Show(TreeCommitSubmoduleMenuItem, project is not null && !bare);
    }

    // Upstream's SubmoduleNode.OnDoubleClick: the shown project opens in a new instance, another one here.
    private void OpenSubmoduleOnDoubleClick(SubmoduleProject project)
    {
        if (project.IsCurrent)
        {
            OpenSubmoduleInNewInstance(project);
        }
        else
        {
            OpenSubmodule(project);
        }
    }

    // Upstream's SubmoduleNode.Open: the project opens as the browse window's repository.
    private void OpenSubmodule(SubmoduleProject project)
        => Run(async () =>
        {
            if (await SubmoduleDirectoryExistsAsync(project))
            {
                await OpenRepositoryAsync(FolderOf(project.Path));
            }
        });

    // Upstream's SubmoduleNode.LaunchGitExtensions.
    private void OpenSubmoduleInNewInstance(SubmoduleProject project)
        => Run(async () =>
        {
            if (await SubmoduleDirectoryExistsAsync(project))
            {
                NewInstanceCommand.Start(_launcher, FolderOf(project.Path));
            }
        });

    private async Task<bool> SubmoduleDirectoryExistsAsync(SubmoduleProject project)
    {
        if (Directory.Exists(project.Path))
        {
            return true;
        }

        await new MessageBoxWindow(
            string.Format(UpstreamTranslation.ByText(SubmoduleDirectoryDoesNotExistText), project.Path, project.Text),
            UpstreamTranslation.ByText(CannotOpenSubmoduleCaption), MessageBoxButtons.OK).ShowDialog(this);
        return false;
    }

    // Upstream's SubmoduleTree.ResetSubmodule: upstream's reset dialog with both kinds of files offered, then all changes of
    // the submodule reset.
    private async Task ResetSubmoduleAsync(string path)
    {
        ResetChangesWindow.ResetAction action = await ResetChangesWindow.ShowResetDialogAsync(this,
            hasExistingFiles: true, hasNewFiles: true);
        if (action != ResetChangesWindow.ResetAction.Cancel)
        {
            await InSubmoduleRepositoryAsync(path, submodule => _actions.ResetAllChangesAsync(submodule,
                clean: action == ResetChangesWindow.ResetAction.ResetAndDelete, onlyWorkTree: false));
        }
    }

    // Upstream's SubmoduleTree.CommitSubmodule: upstream's commit dialog on the submodule (UICommands.WithWorkingDirectory),
    // here a commit window with the submodule's own repository state; the browse window reads its repository again after.
    private async Task CommitSubmoduleAsync(string path)
    {
        if (RepositoryPath is { } shown && RecentRepositoryPaths.Same(shown, path))
        {
            ShowCommitWindow();
            return;
        }

        if (!_plugins.RaisePre(PluginEvent.Commit))
        {
            return;
        }

        RepositoryViewModel repository = new(_repositoryService);
        await repository.RefreshAsync(path);
        CommitWindow window = new(path, repository, _actions, _repositoryService, _preferences,
            () => repository.RefreshAsync(path), _commitMessages, _plugins.ExecuteByName,
            (owner, page) => ShowSettingsAsync(owner: owner, page: page));
        _submoduleActionRepositories.Add(path);
        window.Closed += (_, _) =>
        {
            _submoduleActionRepositories.Remove(path);
            _plugins.RaisePost(PluginEvent.Commit, actionDone: true);
            RefreshRepository();
        };
        window.Show(this);
    }

    // An action in another repository than the shown one (a submodule or a superproject) leaves the browse window on its
    // repository, which it reads again, as upstream's submodule commands notify the browse window's repository.
    private async Task InSubmoduleRepositoryAsync(string path, Func<string, Task<bool>> action)
    {
        if (RepositoryPath is { } shown && RecentRepositoryPaths.Same(shown, path))
        {
            await action(shown);
            return;
        }

        _submoduleActionRepositories.Add(path);
        try
        {
            await action(path);
        }
        finally
        {
            _submoduleActionRepositories.Remove(path);
        }
    }

    // The operations' RepositoryChanged: the repository an action changed is opened, unless it is one of the submodule
    // tree's other repositories.
    private void OnActionChangedRepository(string path)
    {
        if (_submoduleActionRepositories.Any(other => RecentRepositoryPaths.Same(other, path)))
        {
            RefreshRepository();
            return;
        }

        Run(() => OpenRepositoryAsync(path));
    }

    // Upstream's paths end in a separator.
    private static string FolderOf(string path) => Path.TrimEndingDirectorySeparator(path);
}
