using Avalonia.Controls;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The browse window's side of upstream's <c>GitUICommands.Start...Dialog</c> methods for the plugins
///  (<see cref="IPluginFlows"/>): each runs what the menus run.
/// </summary>
public partial class MainWindow : IPluginFlows
{
    private IBrowseWindow Browse => this;

    async Task<bool> IPluginFlows.AddFilesAsync(Window owner, string path, string? files)
    {
        await new AddFilesWindow(files ?? ".", arguments => RunGitWithOutputAsync(path, arguments)).ShowDialog(owner);
        return true;
    }

    Task<bool> IPluginFlows.AddToGitIgnoreAsync(Window owner, string path, bool localExclude,
        IReadOnlyList<string> patterns)
        => AddToGitIgnoreWindow.ShowAsync(owner, path, localExclude, patterns);

    async Task<bool> IPluginFlows.ApplyPatchAsync(Window owner, string path, string? patchFile)
    {
        ApplyPatchWindow apply = new(path, _preferences, arguments => RunGitWithOutputAsync(path, arguments),
            ShowConflicts, RefreshRepository);
        if (patchFile is not null)
        {
            apply.ViewModel.PatchFile = patchFile;
        }

        await apply.ShowDialog(owner);
        return true;
    }

    async Task<bool> IPluginFlows.ArchiveAsync(Window owner, string path, string revision, string? diffRevision,
        string? filePath)
    {
        await ArchiveAsync(owner, path, revision, diffRevision, filePath);
        return true;
    }

    // Upstream's StartBrowseDialog: another browse window, on the repository with the arguments.
    Task<bool> IPluginFlows.BrowseAsync(string path, string revisionFilter, string pathFilter, string? commit,
        string? firstCommit, bool fileHistoryMode)
    {
        ShowAnotherBrowseWindow(path, new BrowseFilters(revisionFilter, pathFilter, commit, firstCommit, fileHistoryMode));
        return Task.FromResult(true);
    }

    // A browse window of its own, as upstream's FormBrowse that StartBrowseDialog shows.
    private void ShowAnotherBrowseWindow(string? repository, BrowseFilters? filters)
        => new MainWindow(_git) { StartRepository = repository, StartFilters = filters }.Show();

    Task<bool> IPluginFlows.CheckoutBranchAsync(Window owner, string path, string branch, bool remote,
        IReadOnlyList<ObjectId>? containObjectIds)
        => CheckoutFlow().StartAsync(owner, path, branch, remote, containObjectIds);

    Task<bool> IPluginFlows.CheckoutRemoteBranchAsync(string path, string branch)
        => Browse.CheckoutRemoteBranchAsync(path, branch);

    Task<bool> IPluginFlows.CheckoutRevisionAsync(string path, string? revision)
        => ShowCheckoutRevisionAsync(path, revision);

    Task<bool> IPluginFlows.CherryPickAsync(Window owner, string path, IReadOnlyList<string?> revisions)
        => CherryPickRevisionsAsync(owner, path, revisions);

    async Task<bool> IPluginFlows.CleanupAsync(Window owner, string path)
    {
        await new CleanupRepositoryWindow(path, RefreshRepository).ShowDialog(owner);
        return true;
    }

    // Upstream's StartCompareRevisionsDialog, whose FormLog is the log window here.
    async Task<bool> IPluginFlows.CompareRevisionsAsync(Window owner, string path)
    {
        await new LogWindow(path, _preferences).ShowDialog(owner);
        return true;
    }

    Task<bool> IPluginFlows.ContinueRebaseAsync(string path) => Browse.ContinueRebaseDialogAsync(path);

    Task<bool> IPluginFlows.CreateBranchAsync(Window owner, string path, string? revision, string? namePrefix)
        => new CreateBranchWindow(path, _actions, _preferences, revision, namePrefix).ShowDialog<bool>(owner);

    Task<bool> IPluginFlows.CreateTagAsync(Window owner, string revision) => ShowCreateTagAsync(revision, owner);

    Task<bool> IPluginFlows.DeleteBranchesAsync(string path, IReadOnlyList<string> branches)
        => Browse.DeleteBranchesAsync(path, branches);

    Task<bool> IPluginFlows.DeleteRemoteBranchAsync(string path, string remoteBranch)
        => Browse.DeleteRemoteBranchAsync(path, remoteBranch);

    Task<bool> IPluginFlows.DeleteTagAsync(string path, string? tag)
        => tag is null ? Browse.DeleteTagAsync(path) : Browse.DeleteNamedTagAsync(path, tag);

    async Task<bool> IPluginFlows.EditFileAsync(Window owner, string file, bool showWarning, int? lineNumber)
    {
        EditorWindow editor = new(file, showWarning, lineNumber);
        await editor.ShowDialog(owner);
        return editor.Accepted;
    }

    async Task<bool> IPluginFlows.EditGitIgnoreAsync(bool localExclude)
    {
        await EditGitIgnoreAsync(localExclude);
        return true;
    }

    Task<bool> IPluginFlows.EditRepositoryFileAsync(string path, RepositoryTextFile file)
        => EditRepositoryFileAsync(path, file);

    async Task<bool> IPluginFlows.FormatPatchAsync(Window owner, string path)
    {
        await new FormatPatchWindow(path, _preferences).ShowDialog(owner);
        return true;
    }

    Task<string?> IPluginFlows.InitAsync(Window owner, string? folder) => InitAsync(owner, folder);

    async Task<bool> IPluginFlows.ManageSubmodulesAsync(string path)
    {
        await ManageSubmodulesAsync(path);
        return true;
    }

    Task<bool> IPluginFlows.MergeAsync(string path, string? branch) => ShowMergeAsync(path, branch);

    // Upstream's StartPullDialog, and StartPullDialogAndPullImmediately (the action given, else the stored default).
    Task<bool> IPluginFlows.PullAsync(Window owner, string? remoteBranch, string? remote, GitPullAction action,
        bool pullAtOnce)
        => pullAtOnce
            ? PullAtOnceAsync(action is GitPullAction.None ? _preferences.DefaultPullAction : action)
            : ShowPullDialogAsync(owner, remoteBranch, remote, action);

    // Upstream's StartPushDialog: with pushOnShow, the current branch to its tracked remote at once; forceWithLease checks
    // the dialog's "Force with lease".
    Task<bool> IPluginFlows.PushAsync(bool pushAtOnce, string? branch, bool forceWithLease)
        => pushAtOnce ? PushAtOnceAsync() : ShowPushDialogAsync(branch, forceWithLease);

    Task<bool> IPluginFlows.RebaseAsync(string path, string from, string? to, string? onto, bool interactive,
        bool immediately)
        => NewRebaseWindow(path, onto, interactive, immediately, from, to).ShowDialog<bool>(this);

    async Task<bool> IPluginFlows.RemotesAsync(Window owner, string? preselectRemote, string? preselectLocal)
    {
        await ShowRemotesAsync(owner, preselectRemote, preselectLocal);
        return true;
    }

    async Task<bool> IPluginFlows.RenameBranchAsync(string branch)
    {
        await Browse.RenameBranchAsync(branch);
        return true;
    }

    // Upstream's StartResetChangesDialog with the work tree's files: the question, then all changes reset.
    async Task<bool> IPluginFlows.ResetChangesAsync(Window owner, string path, bool hasExistingFiles, bool hasNewFiles,
        bool onlyWorkTree)
    {
        ResetChangesAction action = await ResetChangesWindow.ShowResetDialogAsync(owner, hasExistingFiles, hasNewFiles);
        return action != ResetChangesAction.Cancel
               && await _actions.ResetAllChangesAsync(path, clean: action == ResetChangesAction.ResetAndDelete,
                   onlyWorkTree);
    }

    Task<bool> IPluginFlows.ResetCurrentBranchAsync(string path, string revision)
        => Browse.ResetCurrentBranchAsync(path, revision);

    async Task<bool> IPluginFlows.ResolveConflictsAsync(Window owner, string path, bool offerCommit)
    {
        await NewConflictsWindow(path, offerCommit).ShowDialog(owner);
        return true;
    }

    async Task<bool> IPluginFlows.RevertCommitAsync(Window owner, string path, string revision)
    {
        await RevertCommitAsync(owner, path, revision);
        return true;
    }

    // Upstream's RunCommand from a plugin: the verb on this window's repository, its windows over this window.
    async Task<bool> IPluginFlows.RunCommandAsync(CommandLineRequest request)
    {
        string currentDirectory = RepositoryPath ?? Environment.CurrentDirectory;
        if (CommandLine.MissingFile(request) is { } missing)
        {
            await new MessageBoxWindow(missing.Text, missing.Caption, MessageBoxButtons.OK).ShowDialog(this);
            return false;
        }

        if (CloneArguments.AsksForClone([request.Verb, .. request.Arguments]))
        {
            return await NewCloneWindow(CloneArguments.Url([request.Verb, .. request.Arguments])).ShowDialog<bool>(this);
        }

        if (request.Verb == GitEditorCommand.Verb)
        {
            EditorWindow editor = new(request.Arguments[0]);
            await editor.ShowDialog(this);
            return editor.Accepted;
        }

        // Upstream's browse and openrepo: StartBrowseDialog, another browse window.
        if (CommandLineVerbs.OpensBrowseWindow(request, currentDirectory, out string? repository))
        {
            string[] commits = CommandLine.Parameter(request, "-commit").Split(',');
            ShowAnotherBrowseWindow(repository, new BrowseFilters(CommandLine.Parameter(request, "-filter"),
                CommandLine.Parameter(request, "--pathFilter"), commits[0], commits.Length > 1 ? commits[1] : null,
                request.Arguments.Any(argument => argument.StartsWith("--pathFilter", StringComparison.Ordinal))));
            return true;
        }

        if (CommandLineVerbs.OpensNoWindow(request))
        {
            return await CommandLineVerbs.RunWithoutWindowAsync(request, currentDirectory, AppServices.GitConfig) == 0;
        }

        if (request.Verb == "init")
        {
            return await InitAsync(this, request.Argument(0) is { } folder
                ? Path.GetFullPath(folder, currentDirectory)
                : null) is not null;
        }

        if (RunsCommandLineVerb(request.Verb))
        {
            return RepositoryPath is { } path && await RunRepositoryVerbAsync(request, path, currentDirectory);
        }

        // The verbs with a window of their own (and upstream's usage text), over this window.
        int? exitCode = null;
        await CommandLineVerbs.Open(request, currentDirectory, _git, code => exitCode = code).ShowDialog(this);
        return exitCode == 0;
    }

    // Upstream's GitRemoteCommand.Execute: Pageant for the remote's key, then git in the process window.
    async Task<bool> IPluginFlows.RunRemoteGitAsync(string path, string? remote, string arguments)
        => (string.IsNullOrEmpty(remote) || await LoadPuttyKeysAsync(path, remote))
           && await _actions.RunProgramAsync($"git {arguments}", AppSettings.GitCommand, arguments, path);

    Task<bool> IPluginFlows.ShowCommitDiffAsync(Window owner, string path, string revision)
    {
        new CommitDiffWindow(path, revision).Show(owner);
        return Task.FromResult(true);
    }

    Task<bool> IPluginFlows.ShowFileHistoryAsync(Window owner, string path, string file, string? revision,
        bool showBlame, bool filterByRevision)
    {
        NewFileHistoryWindow(path, file, revision, showBlame, filterByRevision).Show(owner);
        return Task.FromResult(true);
    }

    async Task<bool> IPluginFlows.SparseWorkingCopyAsync(string path)
    {
        await Browse.ShowSparseWorkingCopyAsync(path);
        return true;
    }

    Task<bool> IPluginFlows.SpecialCommitAsync(string path, CommitKind kind, ObjectId id)
        => StartSpecialCommitAsync(path, kind, id, subject: "");

    async Task<bool> IPluginFlows.StashWindowAsync(Window owner, bool manageStashes, string? initialStash)
    {
        await ShowStashWindowAsync(owner, manageStashes, initialStash);
        return true;
    }

    Task<bool> IPluginFlows.UpdateSubmodulesAsync(string repositoryPath, string? submodulePath)
        => Browse.UpdateSubmodulesAsync(repositoryPath, submodulePath);

    async Task<bool> IPluginFlows.VerifyDatabaseAsync(string path)
    {
        await VerifyDatabaseAsync(path);
        return true;
    }

    async Task<bool> IPluginFlows.ViewPatchAsync(Window owner, string path, string? patchFile)
    {
        await new ViewPatchWindow(path, patchFile).ShowDialog(owner);
        return true;
    }

    Task<bool> IPluginFlows.WorktreeCreateAsync(Window owner, string path, string mainWorktreePath)
        => WorktreeFlow().CreateAsync(owner, path, mainWorktreePath);

    Task<bool> IPluginFlows.WorktreeDeleteAsync(Window owner, string path, string worktreePath)
        => WorktreeFlow().DeleteAsync(owner, path, worktreePath);

    Task<bool> IPluginFlows.WorktreeSwitchAsync(Window owner, string worktreePath)
        => WorktreeFlow().SwitchAsync(owner, worktreePath);
}
