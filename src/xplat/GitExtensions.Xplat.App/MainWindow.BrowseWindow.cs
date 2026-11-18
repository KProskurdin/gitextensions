using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;

namespace GitExtensions.Xplat.App;

// The window's side of the browse commands (BrowseToolbarViewModel.Commands, MVVM.md): the windows, dialogs, folders and
// terminals the commands open, as the window's methods open them.
public partial class MainWindow : IBrowseWindow
{
    void IBrowseWindow.ShowError(string message) => ShowError(message);

    Task IBrowseWindow.PickAndOpenRepositoryAsync() => PickAndOpenRepositoryAsync();

    Task IBrowseWindow.CloneAsync() => CloneAsync();

    Task IBrowseWindow.PickAndInitRepositoryAsync() => PickAndInitRepositoryAsync();

    void IBrowseWindow.CloseRepository() => CloseRepository();

    void IBrowseWindow.Exit() => Close();

    void IBrowseWindow.RefreshRepository() => RefreshRepository();

    Task IBrowseWindow.ShowRemotesAsync() => ShowRemotesAsync(this);

    void IBrowseWindow.OpenFolder(string path) => _fileManager.OpenFolder(path);

    void IBrowseWindow.OpenTerminal() => OpenTerminal(TerminalButton.Tag as TerminalChoice);

    void IBrowseWindow.ShowFiles(string revision) => ShowFiles(revision);

    Task IBrowseWindow.ShowReflogAsync() => ShowReflogAsync();

    Task IBrowseWindow.EditGitIgnoreAsync(bool localExclude) => EditGitIgnoreAsync(localExclude);

    Task<bool> IBrowseWindow.UpdateSubmodulesAsync(string repositoryPath, string? submodulePath)
        => UpdateSubmodulesAsync(repositoryPath, submodulePath);

    Task IBrowseWindow.ShowWorktreesAsync() => ShowWorktreesAsync();

    void IBrowseWindow.ShowCommitWindow() => ShowCommitWindow();

    Task IBrowseWindow.ShowPullDialogAsync() => ShowPullDialogAsync();

    Task IBrowseWindow.ShowPushDialogAsync() => ShowPushDialogAsync();

    Task IBrowseWindow.ShowStashesAsync() => ShowStashWindowAsync(this);

    Task<bool> IBrowseWindow.ResetChangesAsync(string path) => ResetChangesAsync(path);

    Task IBrowseWindow.PromptCreateBranchAsync(string? startPoint) => PromptCreateBranchAsync(startPoint);

    Task<bool> IBrowseWindow.CheckoutBranchAsync(string path) => StartCheckoutBranchAsync(path);

    Task<bool> IBrowseWindow.CheckoutRevisionAsync(string path, string? revision)
        => ShowCheckoutRevisionAsync(path, revision);

    Task<bool> IBrowseWindow.MergeAsync(string path) => ShowMergeAsync(path, branch: null);

    void IBrowseWindow.RebaseOnSelectedCommit() => ShowRebaseOnSelectedCommit();

    Task<bool> IBrowseWindow.CreateTagAsync(string commit) => ShowCreateTagAsync(commit);

    Task IBrowseWindow.ShowBisectAsync() => ShowBisectAsync();

    void IBrowseWindow.ShowConflicts() => ShowConflicts();

    void IBrowseWindow.ShowCommandLog() => CommandLogWindow.ShowFor(this);

    Task IBrowseWindow.ShowSettingsAsync(bool pluginsPage) => ShowSettingsAsync(pluginsPage: pluginsPage);

    void IBrowseWindow.ShowAbout() => ShowAbout();

    void IBrowseWindow.Pull() => Pull();

    void IBrowseWindow.Push() => Push();

    Task<bool> IBrowseWindow.ContinueRebaseDialogAsync(string path) => ShowRebaseAsync(path, onto: null);

    void IBrowseWindow.ShowApplyPatch(string path) => ShowApplyPatch(path);

    void IBrowseWindow.ShowCommitDiff() => ShowCommitDiff();

    Task IBrowseWindow.OpenCommitFileInDiffToolAsync() => OpenCommitFileInDiffToolAsync();

    void IBrowseWindow.CherryPickSelectedRevision() => CherryPickSelectedRevision();

    void IBrowseWindow.RevertSelectedRevision() => RevertSelectedRevision();

    Task<bool> IBrowseWindow.ResetCurrentBranchAsync(string path, string revision)
        => ShowResetCurrentBranchAsync(this, path, revision, ResetMode.Soft);

    Task IBrowseWindow.RebaseOnSelectedCommitAsync(bool interactive) => RebaseOnSelectedCommitAsync(interactive);

    Task<bool> IBrowseWindow.RebaseAdvancedAsync(string path, string revision)
        => ShowRebaseAsync(path, revision, from: RebaseRangeStart());

    void IBrowseWindow.MarkSelectedForBisect(GitBisectOption option) => MarkSelectedForBisect(option);

    Task<bool> IBrowseWindow.UndoLastCommitAsync(string path) => UndoLastCommitAsync(path);

    void IBrowseWindow.ArchiveSelectedRevision() => ArchiveSelectedRevision();

    Task IBrowseWindow.ShowFormatPatchAsync(string path) => new FormatPatchWindow(path, _preferences).ShowDialog(this);

    Task IBrowseWindow.ShowViewPatchAsync(string path) => new ViewPatchWindow(path, patchFile: null).ShowDialog(this);

    Task IBrowseWindow.ShowCleanupAsync(string path) => new CleanupRepositoryWindow(path, RefreshRepository).ShowDialog(this);

    void IBrowseWindow.StartGitGui(string path) => GitModules.Open(path).RunGui();

    void IBrowseWindow.StartGitK(string path) => GitModules.Open(path).RunGitK();

    void IBrowseWindow.StartPuttyProgram(string program) => StartPuttyProgram(program);

    Task<bool> IBrowseWindow.EditRepositoryFileAsync(string path, RepositoryTextFile file)
        => EditRepositoryFileAsync(path, file);

    Task IBrowseWindow.ShowSparseWorkingCopyAsync(string path)
        => new SparseWorkingCopyWindow(path, arguments => RunGitWithOutputAsync(path, arguments)).ShowDialog(this);

    Task IBrowseWindow.ShowSettingsPageAsync(string page) => ShowSettingsAsync(page: page);

    Task<bool> IBrowseWindow.RunGitWithOutputAsync(string path, string arguments) => RunGitWithOutputAsync(path, arguments);

    Task IBrowseWindow.VerifyDatabaseAsync(string path) => VerifyDatabaseAsync(path);

    Task IBrowseWindow.DeleteIndexLockAsync(string path) => DeleteIndexLockAsync(path);

    Task IBrowseWindow.EditLocalGitConfigAsync(string path)
        => new EditorWindow(GitModules.Open(path).ResolveGitInternalPath("config"), showWarning: true).ShowDialog(this);

    Task IBrowseWindow.ManageSubmodulesAsync(string path) => ManageSubmodulesAsync(path);

    Task<bool> IBrowseWindow.DeleteBranchAsync(string path) => ShowDeleteBranchAsync(path, []);

    Task<bool> IBrowseWindow.DeleteTagAsync(string path) => ShowDeleteTagAsync(path, tag: null);

    Task IBrowseWindow.ShowQuickSearchHelpAsync() => ShowQuickSearchHelpAsync();

    void IBrowseWindow.OpenSelectedBuildReport() => OpenBuildLink(SelectedBuildStatus()?.Url);

    void IBrowseWindow.OpenReportedBuild() => OpenBuildLink(_reportedBuild?.Url);

    void IBrowseWindow.OpenSelectedPullRequestPage() => OpenBuildLink(SelectedBuildStatus()?.PullRequestUrl);

    void IBrowseWindow.ForkCloneRepository() => ForkCloneRepository();

    void IBrowseWindow.ViewPullRequests()
        => WithRepositoryHost(host => _plugins.StartPullRequestsDialog(_plugins.Owner, host));

    void IBrowseWindow.CreatePullRequest() => WithRepositoryHost(host => _plugins.StartCreatePullRequest(_plugins.Owner, host));

    void IBrowseWindow.AddUpstreamRemote() => WithRepositoryHost(host => _plugins.AddUpstreamRemote(_plugins.Owner, host));

    void IBrowseWindow.ToggleLeftPanel() => ToggleLeftPanel();

    void IBrowseWindow.ShowSplitViewLayout() => ShowSplitViewLayout();

    void IBrowseWindow.SetCommitInfoPosition(CommitInfoPosition position) => SetCommitInfoPosition(position);

    Task IBrowseWindow.OpenSubmoduleProjectAsync(SubmoduleProject project) => OpenSubmoduleProjectAsync(project);

    void IBrowseWindow.ShowSubmodulesMenu() => SubmodulesButton.Flyout?.ShowAt(SubmodulesButton);

    void IBrowseWindow.PullWith(GitPullAction action) => PullWith(action);

    Task IBrowseWindow.StashChangesAsync() => StashAsync();

    void IBrowseWindow.PopTopStash() => PopTopStash();

    Task IBrowseWindow.ShowCreateStashAsync() => ShowStashWindowAsync(this, manageStashes: false);

    void IBrowseWindow.ShowRepoObjects() => ShowRepoObjects();

    void IBrowseWindow.SearchRepoObjects() => SearchRepoObjects();

    void IBrowseWindow.OpenTerminal(TerminalChoice terminal, string path)
        => new SystemTerminalLauncher(_launcher, terminal.Command).OpenTerminal(path);

    Task IBrowseWindow.UpdateCurrentSubmoduleAsync(SubmoduleStructure structure) => UpdateCurrentSubmoduleAsync(structure);

    Task IBrowseWindow.OpenWorktreeFolderAsync(string worktreePath) => OpenWorktreeFolderAsync(worktreePath);

    Task IBrowseWindow.CreateWorktreeAsync() => CreateWorktreeAsync();

    Task<bool> IBrowseWindow.PruneWorktreesAsync(string path) => WorktreeFlow().PruneAsync(path);

    Task<bool> IBrowseWindow.CheckoutNamedBranchAsync(string path, string branch) => StartCheckoutBranchAsync(path, branch);

    void IBrowseWindow.OpenRecentRepository(string path) => OpenRecentRepository(path);

    Task IBrowseWindow.ConfigureRecentRepositoriesAsync() => ConfigureRecentRepositoriesAsync();

    void IBrowseWindow.OpenUrl(string url) => OpenUrl(url);

    Task IBrowseWindow.ReportIssueAsync() => ReportIssueAsync();

    void IBrowseWindow.ShowChangeLog() => _ = new ChangeLogWindow().ShowDialog(this);

    void IBrowseWindow.ShowDonate() => _ = new DonateWindow(OpenUrl).ShowDialog(this);

    Task IBrowseWindow.RefreshDashboardAsync() => Dashboard.LoadAsync();

    bool IBrowseWindow.RunGridCommand(GridCommand command) => RunGridHotkey(command);

    void IBrowseWindow.OpenSelectedCommitsWithDifftool(string? tool) => DiffSelectedCommitsWithDifftool(tool);

    Task IBrowseWindow.PushBranchAsync(string branch) => ShowPushDialogAsync(branch);

    Task<bool> IBrowseWindow.DeleteRemoteBranchAsync(string path, string branch) => ShowDeleteRemoteBranchAsync(path, branch);

    Task<bool> IBrowseWindow.DeleteNamedTagAsync(string path, string tag) => ShowDeleteTagAsync(path, tag);

    void IBrowseWindow.SelectInLeftPanel(string name, bool isRemote) => SelectInLeftPanel(name, isRemote);

    Task<bool> IBrowseWindow.ResetAnotherBranchAsync(string path, string revision) => ShowResetAnotherBranchAsync(path, revision);

    void IBrowseWindow.ExecutePlugin(IGitPlugin plugin) => ExecutePlugin(plugin);

    Task<bool> IBrowseWindow.RunUserScriptAsync(string path, ScriptDefinition script)
        => Scripts.RunAsync(script, path, SelectedHashes, FileScriptOptions());

    void IBrowseWindow.CloneFromHost(string hostName)
    {
        if (_loadedPlugins?.OfType<IRepositoryHostPlugin>().FirstOrDefault(host => host.Name == hostName) is { } host)
        {
            _plugins.StartCloneForkFromHoster(_plugins.Owner, host, gitModuleChanged: null);
        }
    }

    void IBrowseWindow.ShowUserManual(string subfolder, string anchor)
        => OsShellUtil.OpenUrlInDefaultBrowser(GitUI.UserManual.UserManual.UrlFor(subfolder, anchor));
}
