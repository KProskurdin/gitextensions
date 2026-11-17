using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Repository;

// The browse window's commands (MVVM.md): upstream's FormBrowse menu items and toolstrip buttons, the action bar's buttons
// and the grid menu's fixed items. A command decides whether it applies (a repository, a selected commit) and with which
// revision, runs git through the actions' view model, and asks the window (IBrowseWindow) for its windows. As upstream's,
// a command starts its work and returns, so its item stays enabled while a dialog it opened is shown.
public sealed partial class BrowseToolbarViewModel
{
    /// <summary>
    ///  The window the commands open their windows in; set by the window.
    /// </summary>
    public IBrowseWindow? Window { get; set; }

    private IBrowseWindow BrowseWindow => Window ?? throw new InvalidOperationException("The browse window is not set.");

    private string? OpenPath => _commits.RepositoryPath;

    private string SelectedRevisionOrHead => _commits.Selected?.Hash ?? "HEAD";

    [RelayCommand]
    private void OpenRepository() => Run(BrowseWindow.PickAndOpenRepositoryAsync);

    [RelayCommand]
    private void Clone() => Run(BrowseWindow.CloneAsync);

    [RelayCommand]
    private void InitRepository() => Run(BrowseWindow.PickAndInitRepositoryAsync);

    [RelayCommand]
    private void CloseRepository() => BrowseWindow.CloseRepository();

    [RelayCommand]
    private void Exit() => BrowseWindow.Exit();

    [RelayCommand]
    private void RefreshRepository() => BrowseWindow.RefreshRepository();

    [RelayCommand]
    private void ManageRemotes() => Run(BrowseWindow.ShowRemotesAsync);

    [RelayCommand]
    private void OpenFolder() => OnRepositoryFolder(BrowseWindow.OpenFolder);

    [RelayCommand]
    private void OpenTerminal() => BrowseWindow.OpenTerminal();

    // Upstream's FileTree: the selected commit's files, HEAD's without a selection.
    [RelayCommand]
    private void BrowseFiles() => BrowseWindow.ShowFiles(SelectedRevisionOrHead);

    [RelayCommand]
    private void ShowReflog() => Run(BrowseWindow.ShowReflogAsync);

    [RelayCommand]
    private void EditGitIgnore() => Run(() => BrowseWindow.EditGitIgnoreAsync(localExclude: false));

    [RelayCommand]
    private void EditGitInfoExclude() => Run(() => BrowseWindow.EditGitIgnoreAsync(localExclude: true));

    [RelayCommand]
    private void UpdateAllSubmodules() => OnRepository(path => BrowseWindow.UpdateSubmodulesAsync(path, null));

    [RelayCommand]
    private void SyncAllSubmodules() => OnRepository(path => _actions.SyncSubmodulesAsync(path, path: null));

    [RelayCommand]
    private void ManageWorktrees() => Run(BrowseWindow.ShowWorktreesAsync);

    [RelayCommand]
    private void Commit() => BrowseWindow.ShowCommitWindow();

    [RelayCommand]
    private void PullDialog() => Run(BrowseWindow.ShowPullDialogAsync);

    [RelayCommand]
    private void PushDialog() => Run(BrowseWindow.ShowPushDialogAsync);

    [RelayCommand]
    private void ManageStashes() => Run(BrowseWindow.ShowStashesAsync);

    [RelayCommand]
    private void ResetChanges() => OnRepository(BrowseWindow.ResetChangesAsync);

    [RelayCommand]
    private void CreateBranch() => Run(() => BrowseWindow.PromptCreateBranchAsync(_commits.Selected?.Hash));

    [RelayCommand]
    private void CheckoutBranch() => OnRepository(BrowseWindow.CheckoutBranchAsync);

    [RelayCommand]
    private void CheckoutRevision() => OnRepository(path => BrowseWindow.CheckoutRevisionAsync(path, revision: null));

    [RelayCommand]
    private void Merge() => OnRepository(BrowseWindow.MergeAsync);

    [RelayCommand]
    private void Rebase() => BrowseWindow.RebaseOnSelectedCommit();

    [RelayCommand]
    private void CreateTag() => Run(() => BrowseWindow.CreateTagAsync(SelectedRevisionOrHead));

    [RelayCommand]
    private void Bisect() => Run(BrowseWindow.ShowBisectAsync);

    [RelayCommand]
    private void ResolveConflicts() => BrowseWindow.ShowConflicts();

    [RelayCommand]
    private void ShowCommandLog() => BrowseWindow.ShowCommandLog();

    [RelayCommand]
    private void OpenSettings() => Run(() => BrowseWindow.ShowSettingsAsync(pluginsPage: false));

    [RelayCommand]
    private void PluginSettings() => Run(() => BrowseWindow.ShowSettingsAsync(pluginsPage: true));

    [RelayCommand]
    private void About() => BrowseWindow.ShowAbout();

    [RelayCommand]
    private void Pull() => BrowseWindow.Pull();

    [RelayCommand]
    private void Push() => BrowseWindow.Push();

    [RelayCommand]
    private void LoadMore() => Run(_commits.LoadMoreAsync);

    // Upstream's ContinueButton_Click: the action's own continue command.
    [RelayCommand]
    private void ContinueAction()
    {
        switch (ActionBanner?.Action)
        {
            case GitAction.Rebase:
                OnRepository(_actions.ContinueRebaseAsync);
                break;
            case GitAction.Merge:
                OnRepository(_actions.ContinueMergeAsync);
                break;
            case GitAction.Patch:
                OnRepository(_actions.ContinuePatchAsync);
                break;
        }
    }

    // Upstream's AbortButton_Click: the command runs at once.
    [RelayCommand]
    private void AbortAction()
    {
        switch (ActionBanner?.Action)
        {
            case GitAction.Rebase:
                OnRepository(_actions.AbortRebaseAsync);
                break;
            case GitAction.Merge:
                OnRepository(_actions.AbortMergeAsync);
                break;
            case GitAction.Patch:
                OnRepository(_actions.AbortPatchAsync);
                break;
        }
    }

    // Upstream's More... of a rebase opens FormRebase (StartTheContinueRebaseDialog), of a patch FormApplyPatch
    // (StartApplyPatchDialog), which shows the stopped git am's patches with Skip, Resolved and Abort.
    [RelayCommand]
    private void MoreAction()
    {
        switch (ActionBanner?.Action)
        {
            case GitAction.Rebase:
                OnRepository(BrowseWindow.ContinueRebaseDialogAsync);
                break;
            case GitAction.Patch:
                OnRepositoryFolder(BrowseWindow.ShowApplyPatch);
                break;
        }
    }

    [RelayCommand]
    private void ShowCommitDiff() => BrowseWindow.ShowCommitDiff();

    [RelayCommand]
    private void OpenCommitFileInDiffTool() => Run(BrowseWindow.OpenCommitFileInDiffToolAsync);

    [RelayCommand]
    private void CheckoutCommit() => OnSelectedCommit((path, hash) => BrowseWindow.CheckoutRevisionAsync(path, hash));

    [RelayCommand]
    private void CherryPick() => BrowseWindow.CherryPickSelectedRevision();

    [RelayCommand]
    private void Revert() => BrowseWindow.RevertSelectedRevision();

    [RelayCommand]
    private void ResetCurrentBranch() => OnSelectedCommit(BrowseWindow.ResetCurrentBranchAsync);

    [RelayCommand]
    private void RebaseOnCommit() => Run(() => BrowseWindow.RebaseOnSelectedCommitAsync(interactive: false));

    [RelayCommand]
    private void RebaseInteractive() => Run(() => BrowseWindow.RebaseOnSelectedCommitAsync(interactive: true));

    [RelayCommand]
    private void RebaseAdvanced() => OnSelectedCommit(BrowseWindow.RebaseAdvancedAsync);

    [RelayCommand]
    private void MarkBisect(GitBisectOption option) => BrowseWindow.MarkSelectedForBisect(option);

    [RelayCommand]
    private void StopBisect() => OnRepository(_actions.StopBisectAsync);

    // Upstream's argument for "Compress git database".
    private const string CompressGitDatabaseArguments = "gc";

    // Upstream's settings page of "Repository settings".
    private const string GitConfigSettingsPage = "GitConfigSettingsPage";

    [RelayCommand]
    private void UndoLastCommit() => OnRepository(BrowseWindow.UndoLastCommitAsync);

    [RelayCommand]
    private void Archive() => BrowseWindow.ArchiveSelectedRevision();

    [RelayCommand]
    private void FormatPatch() => OnRepositoryFolder(path => Run(() => BrowseWindow.ShowFormatPatchAsync(path)));

    [RelayCommand]
    private void ApplyPatch() => OnRepositoryFolder(BrowseWindow.ShowApplyPatch);

    [RelayCommand]
    private void ViewPatch() => OnRepositoryFolder(path => Run(() => BrowseWindow.ShowViewPatchAsync(path)));

    [RelayCommand]
    private void Cleanup() => OnRepositoryFolder(path => Run(() => BrowseWindow.ShowCleanupAsync(path)));

    [RelayCommand]
    private void GitGui() => OnRepositoryFolder(BrowseWindow.StartGitGui);

    [RelayCommand]
    private void GitK() => OnRepositoryFolder(BrowseWindow.StartGitK);

    [RelayCommand]
    private void StartAuthenticationAgent() => BrowseWindow.StartPuttyProgram(_preferences.Pageant);

    [RelayCommand]
    private void GenerateOrImportKey() => BrowseWindow.StartPuttyProgram(_preferences.Puttygen);

    [RelayCommand]
    private void EditGitAttributes()
        => OnRepository(path => BrowseWindow.EditRepositoryFileAsync(path, RepositoryTextFiles.GitAttributes));

    [RelayCommand]
    private void EditMailMap() => OnRepository(path => BrowseWindow.EditRepositoryFileAsync(path, RepositoryTextFiles.MailMap));

    [RelayCommand]
    private void SparseWorkingCopy() => OnRepositoryFolder(path => Run(() => BrowseWindow.ShowSparseWorkingCopyAsync(path)));

    [RelayCommand]
    private void RepositorySettings() => Run(() => BrowseWindow.ShowSettingsPageAsync(GitConfigSettingsPage));

    [RelayCommand]
    private void CompressGitDatabase()
        => OnRepositoryFolder(path => Run(() => BrowseWindow.RunGitWithOutputAsync(path, CompressGitDatabaseArguments)));

    [RelayCommand]
    private void RecoverLostObjects() => OnRepositoryFolder(path => Run(() => BrowseWindow.VerifyDatabaseAsync(path)));

    [RelayCommand]
    private void DeleteIndexLock() => OnRepositoryFolder(path => Run(() => BrowseWindow.DeleteIndexLockAsync(path)));

    [RelayCommand]
    private void EditLocalGitConfig() => OnRepositoryFolder(path => Run(() => BrowseWindow.EditLocalGitConfigAsync(path)));

    [RelayCommand]
    private void ManageSubmodules() => OnRepositoryFolder(path => Run(() => BrowseWindow.ManageSubmodulesAsync(path)));

    [RelayCommand]
    private void DeleteBranch() => OnRepository(BrowseWindow.DeleteBranchAsync);

    [RelayCommand]
    private void DeleteTag() => OnRepository(BrowseWindow.DeleteTagAsync);

    [RelayCommand]
    private void QuickSearchHelp() => Run(BrowseWindow.ShowQuickSearchHelpAsync);

    [RelayCommand]
    private void OpenBuildReport() => BrowseWindow.OpenSelectedBuildReport();

    [RelayCommand]
    private void OpenReportedBuild() => BrowseWindow.OpenReportedBuild();

    [RelayCommand]
    private void OpenPullRequestPage() => BrowseWindow.OpenSelectedPullRequestPage();

    [RelayCommand]
    private void ForkCloneRepository() => BrowseWindow.ForkCloneRepository();

    [RelayCommand]
    private void ViewPullRequests() => BrowseWindow.ViewPullRequests();

    [RelayCommand]
    private void CreatePullRequest() => BrowseWindow.CreatePullRequest();

    [RelayCommand]
    private void AddUpstreamRemote() => BrowseWindow.AddUpstreamRemote();

    [RelayCommand]
    private void ToggleLeftPanel() => BrowseWindow.ToggleLeftPanel();

    [RelayCommand]
    private void ToggleSplitView()
    {
        _preferences.ShowSplitViewLayout = !_preferences.ShowSplitViewLayout;
        BrowseWindow.ShowSplitViewLayout();
    }

    // Upstream's CommitInfoPositionClick: the button moves to the next position, the menu to the chosen one.
    [RelayCommand]
    private void NextCommitInfoPosition()
        => BrowseWindow.SetCommitInfoPosition((CommitInfoPosition)(((int)_preferences.CommitInfoPosition + 1)
                                                                   % Enum.GetValues<CommitInfoPosition>().Length));

    [RelayCommand]
    private void SetCommitInfoPosition(CommitInfoPosition position) => BrowseWindow.SetCommitInfoPosition(position);

    // Upstream's toolStripButtonLevelUp_ButtonClick: the superproject, else the drop-down.
    [RelayCommand]
    private void Submodules()
    {
        if (_submodules?.SuperProject is { } superproject)
        {
            Run(() => BrowseWindow.OpenSubmoduleProjectAsync(superproject));
        }
        else
        {
            BrowseWindow.ShowSubmodulesMenu();
        }
    }

    [RelayCommand]
    private void PullWith(GitPullAction action) => BrowseWindow.PullWith(action);

    [RelayCommand]
    private void StashChanges() => Run(BrowseWindow.StashChangesAsync);

    [RelayCommand]
    private void StashStaged() => OnRepository(_actions.StashStagedAsync);

    [RelayCommand]
    private void PopStash() => BrowseWindow.PopTopStash();

    [RelayCommand]
    private void CreateStash() => Run(BrowseWindow.ShowCreateStashAsync);

    // Upstream's toolstrip of the left panel: a tree shows or hides, and the trees fill again.
    [RelayCommand]
    private void ToggleTree(RepoObjectKind root)
    {
        _preferences.ShowRepoObjectsTree(root, !_preferences.ShowsRepoObjectsTree(root));
        OnPropertyChanged(TreeShownProperty(root));
        BrowseWindow.ShowRepoObjects();
    }

    [RelayCommand]
    private void SearchRepoObjects() => BrowseWindow.SearchRepoObjects();

    private static string TreeShownProperty(RepoObjectKind root) => root switch
    {
        RepoObjectKind.BranchesRoot => nameof(ShowsBranchesTree),
        RepoObjectKind.RemotesRoot => nameof(ShowsRemotesTree),
        RepoObjectKind.WorktreesRoot => nameof(ShowsWorktreesTree),
        RepoObjectKind.TagsRoot => nameof(ShowsTagsTree),
        RepoObjectKind.SubmodulesRoot => nameof(ShowsSubmodulesTree),
        _ => nameof(ShowsStashesTree),
    };

    // Upstream's links of the Help menu and the dashboard.
    public const string DevelopUrl = "https://github.com/gitextensions/gitextensions";
    public const string DonationUrl = "https://opencollective.com/gitextensions";
    public const string TranslateUrl = "https://github.com/gitextensions/gitextensions/wiki/Translations";
    public const string IssuesUrl = "https://github.com/gitextensions/gitextensions/issues";

    [RelayCommand]
    private void OpenUrl(string url) => BrowseWindow.OpenUrl(url);

    [RelayCommand]
    private void ReportIssue() => Run(BrowseWindow.ReportIssueAsync);

    [RelayCommand]
    private void ShowChangeLog() => BrowseWindow.ShowChangeLog();

    [RelayCommand]
    private void Donate() => BrowseWindow.ShowDonate();

    [RelayCommand]
    private void RefreshDashboard() => Run(BrowseWindow.RefreshDashboardAsync);

    // Starts the work and shows its error on the window, as upstream's handlers do.
    private void Run(Func<Task> action) => _ = RunAsync(action);

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            BrowseWindow.ShowError(ex.Message);
        }
    }

    private void OnRepository(Func<string, Task<bool>> action)
    {
        if (OpenPath is { } path)
        {
            Run(() => action(path));
        }
    }

    private void OnRepositoryFolder(Action<string> action)
    {
        if (OpenPath is not { } path)
        {
            return;
        }

        try
        {
            action(path);
        }
        catch (Exception ex)
        {
            BrowseWindow.ShowError(ex.Message);
        }
    }

    private void OnSelectedCommit(Func<string, string, Task<bool>> action)
    {
        if (OpenPath is { } path && _commits.Selected is { } row)
        {
            Run(() => action(path, row.Hash));
        }
    }
}
