using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Scripts;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  What the browse window's commands (<see cref="BrowseToolbarViewModel"/>) ask of the window: the windows and dialogs it
///  opens, the folder and terminal it starts, and its error display. Each member is one of upstream's FormBrowse actions;
///  the view model decides when it applies and with which revision.
/// </summary>
public interface IBrowseWindow
{
    void ShowError(string message);

    Task PickAndOpenRepositoryAsync();

    Task CloneAsync();

    Task PickAndInitRepositoryAsync();

    void CloseRepository();

    void Exit();

    void RefreshRepository();

    Task ShowRemotesAsync();

    void OpenFolder(string path);

    void OpenTerminal();

    void ShowFiles(string revision);

    Task ShowReflogAsync();

    Task EditGitIgnoreAsync(bool localExclude);

    Task<bool> UpdateSubmodulesAsync(string repositoryPath, string? submodulePath);

    Task ShowWorktreesAsync();

    void ShowCommitWindow();

    Task ShowPullDialogAsync();

    Task ShowPushDialogAsync();

    Task ShowStashesAsync();

    Task<bool> ResetChangesAsync(string path);

    Task PromptCreateBranchAsync(string? startPoint);

    Task<bool> CheckoutBranchAsync(string path);

    Task<bool> CheckoutRevisionAsync(string path, string? revision);

    Task<bool> MergeAsync(string path);

    void RebaseOnSelectedCommit();

    Task<bool> CreateTagAsync(string commit);

    Task ShowBisectAsync();

    void ShowConflicts();

    void ShowCommandLog();

    Task ShowSettingsAsync(bool pluginsPage);

    void ShowAbout();

    void Pull();

    void Push();

    /// <summary>
    ///  Upstream's FormRebase over a stopped rebase (StartTheContinueRebaseDialog).
    /// </summary>
    Task<bool> ContinueRebaseDialogAsync(string path);

    void ShowApplyPatch(string path);

    void ShowCommitDiff();

    Task OpenCommitFileInDiffToolAsync();

    void CherryPickSelectedRevision();

    void RevertSelectedRevision();

    Task<bool> ResetCurrentBranchAsync(string path, string revision);

    Task RebaseOnSelectedCommitAsync(bool interactive);

    Task<bool> RebaseAdvancedAsync(string path, string revision);

    void MarkSelectedForBisect(GitBisectOption option);

    Task<bool> UndoLastCommitAsync(string path);

    void ArchiveSelectedRevision();

    Task ShowFormatPatchAsync(string path);

    Task ShowViewPatchAsync(string path);

    Task ShowCleanupAsync(string path);

    void StartGitGui(string path);

    void StartGitK(string path);

    /// <summary>
    ///  Starts one of PuTTY's programs (upstream's Pageant or PuTTYgen) for the repository.
    /// </summary>
    void StartPuttyProgram(string program);

    Task<bool> EditRepositoryFileAsync(string path, RepositoryTextFile file);

    Task ShowSparseWorkingCopyAsync(string path);

    /// <summary>
    ///  The settings window on upstream's page type.
    /// </summary>
    Task ShowSettingsPageAsync(string page);

    /// <summary>
    ///  Runs git with its output shown, as upstream's FormProcess.
    /// </summary>
    Task<bool> RunGitWithOutputAsync(string path, string arguments);

    Task VerifyDatabaseAsync(string path);

    Task DeleteIndexLockAsync(string path);

    Task EditLocalGitConfigAsync(string path);

    Task ManageSubmodulesAsync(string path);

    Task<bool> DeleteBranchAsync(string path);

    Task<bool> DeleteTagAsync(string path);

    Task ShowQuickSearchHelpAsync();

    void OpenSelectedBuildReport();

    void OpenReportedBuild();

    void OpenSelectedPullRequestPage();

    void ForkCloneRepository();

    void ViewPullRequests();

    void CreatePullRequest();

    void AddUpstreamRemote();

    void ToggleLeftPanel();

    /// <summary>
    ///  Shows the tabs below the grid as <c>ShowSplitViewLayout</c> says.
    /// </summary>
    void ShowSplitViewLayout();

    void SetCommitInfoPosition(CommitInfoPosition position);

    Task OpenSubmoduleProjectAsync(SubmoduleProject project);

    /// <summary>
    ///  Opens the submodules button's drop-down.
    /// </summary>
    void ShowSubmodulesMenu();

    /// <summary>
    ///  Upstream's pull button actions: the dialog, a fetch, or a pull with the scripts and PuTTY keys around it.
    /// </summary>
    void PullWith(GitPullAction action);

    /// <summary>
    ///  Upstream's StashSave from the toolbar: the changes, untracked files as the settings say.
    /// </summary>
    Task StashChangesAsync();

    void PopTopStash();

    /// <summary>
    ///  Upstream's stash window opened to create a stash.
    /// </summary>
    Task ShowCreateStashAsync();

    /// <summary>
    ///  Fills the left panel's trees again, as the settings choose them.
    /// </summary>
    void ShowRepoObjects();

    void SearchRepoObjects();

    /// <summary>
    ///  Opens <paramref name="terminal"/> in <paramref name="path"/>.
    /// </summary>
    void OpenTerminal(TerminalChoice terminal, string path);

    Task UpdateCurrentSubmoduleAsync(SubmoduleStructure structure);

    /// <summary>
    ///  Upstream's WorktreeToolStripMenuItem_Click: opens the worktree, or says its folder is gone.
    /// </summary>
    Task OpenWorktreeFolderAsync(string worktreePath);

    Task CreateWorktreeAsync();

    Task<bool> PruneWorktreesAsync(string path);

    Task<bool> CheckoutNamedBranchAsync(string path, string branch);

    /// <summary>
    ///  Opens a repository of the recent and favourite menus, in a new instance when the click held Ctrl (Cmd on macOS).
    /// </summary>
    void OpenRecentRepository(string path);

    /// <summary>
    ///  Upstream's recent repositories settings, then the menus and the button's caption again.
    /// </summary>
    Task ConfigureRecentRepositoriesAsync();

    void OpenUrl(string url);

    /// <summary>
    ///  Upstream's reportAnIssueToolStripMenuItem_Click: the environment information is copied for the issue's text before
    ///  the issues page opens.
    /// </summary>
    Task ReportIssueAsync();

    void ShowChangeLog();

    void ShowDonate();

    /// <summary>
    ///  Reads the dashboard's repositories again.
    /// </summary>
    Task RefreshDashboardAsync();

    Task<bool> MergeBranchAsync(string path, string branch);

    Task RenameBranchAsync(string branch);

    Task<bool> DeleteBranchesAsync(string path, IReadOnlyList<string> branches);

    Task<bool> CheckoutRemoteBranchAsync(string path, string branch);

    Task<bool> ConfirmDropStashAsync();

    Task CopyTextAsync(string text);

    /// <summary>
    ///  Runs one of the grid's hotkey commands (the compare items, the special commits, the Navigate menu's items); false
    ///  when it cannot run now.
    /// </summary>
    bool RunGridCommand(GridCommand command);

    /// <summary>
    ///  Upstream's push window with <paramref name="branch"/> chosen.
    /// </summary>
    Task PushBranchAsync(string branch);

    Task<bool> DeleteRemoteBranchAsync(string path, string branch);

    Task<bool> DeleteNamedTagAsync(string path, string tag);

    /// <summary>
    ///  Upstream's FormBrowse.SelectInLeftPanel: the left panel shown, then the ref's node selected.
    /// </summary>
    void SelectInLeftPanel(string name, bool isRemote);

    /// <summary>
    ///  Upstream's ResetAnotherBranchToHereToolStripMenuItemClick: FormResetAnotherBranch on <paramref name="revision"/>.
    /// </summary>
    Task<bool> ResetAnotherBranchAsync(string path, string revision);

    /// <summary>
    ///  Opens a page of the user manual.
    /// </summary>
    void ShowUserManual(string subfolder, string anchor);

    /// <summary>
    ///  Upstream's dashboard link of a repository host plugin: its clone or fork window.
    /// </summary>
    void CloneFromHost(string hostName);

    /// <summary>
    ///  Upstream's Plugins menu item: runs the plugin (or says why it failed to load).
    /// </summary>
    void ExecutePlugin(IGitPlugin plugin);

    /// <summary>
    ///  Upstream's user menu bar button: runs the script on the selected commits and the shown tab's files.
    /// </summary>
    Task<bool> RunUserScriptAsync(string path, ScriptDefinition script);
}
