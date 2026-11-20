using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The browse window's flows behind upstream's <c>GitUICommands.Start...Dialog</c> methods, for the plugins
///  (<see cref="PluginHost"/>): each is what the browse window's menus run, with the arguments a plugin gives, and its
///  answer is upstream's (true when the dialog was confirmed or the action done). <c>path</c> is the plugin host's
///  repository; the flows that only exist for the open repository use that one.
/// </summary>
internal interface IPluginFlows
{
    Task<bool> AddFilesAsync(Window owner, string path, string? files);

    Task<bool> AddToGitIgnoreAsync(Window owner, string path, bool localExclude, IReadOnlyList<string> patterns);

    Task<bool> ApplyPatchAsync(Window owner, string path, string? patchFile);

    Task<bool> ArchiveAsync(Window owner, string path, string revision, string? diffRevision, string? filePath);

    Task<bool> BrowseAsync(string path, string revisionFilter, string pathFilter, string? commit);

    Task<bool> CheckoutBranchAsync(Window owner, string path, string branch, bool remote,
        IReadOnlyList<ObjectId>? containObjectIds);

    Task<bool> CheckoutRemoteBranchAsync(string path, string branch);

    Task<bool> CheckoutRevisionAsync(string path, string? revision);

    Task<bool> CherryPickAsync(Window owner, string path, IReadOnlyList<string> revisions);

    Task<bool> CleanupAsync(Window owner, string path);

    Task<bool> CompareRevisionsAsync(Window owner, string path);

    Task<bool> ContinueRebaseAsync(string path);

    Task<bool> CreateBranchAsync(Window owner, string path, string? revision, string? namePrefix);

    Task<bool> CreateTagAsync(Window owner, string revision);

    Task<bool> DeleteBranchesAsync(string path, IReadOnlyList<string> branches);

    Task<bool> DeleteRemoteBranchAsync(string path, string remoteBranch);

    Task<bool> DeleteTagAsync(string path, string? tag);

    Task<bool> EditFileAsync(Window owner, string file, bool showWarning);

    Task<bool> EditGitIgnoreAsync(bool localExclude);

    Task<bool> EditRepositoryFileAsync(string path, RepositoryTextFile file);

    Task<bool> FormatPatchAsync(Window owner, string path);

    Task<string?> InitAsync(Window owner, string? folder);

    Task<bool> ManageSubmodulesAsync(string path);

    Task<bool> MergeAsync(string path, string? branch);

    Task<bool> PullAsync(Window owner, string? remoteBranch, string? remote, GitPullAction action, bool pullAtOnce);

    Task<bool> PushAsync(bool pushAtOnce, string? branch);

    Task<bool> RebaseAsync(string path, string from, string? to, string? onto, bool interactive, bool immediately);

    Task<bool> RemotesAsync(Window owner, string? preselectRemote, string? preselectLocal);

    Task<bool> RenameBranchAsync(string branch);

    Task<bool> ResetChangesAsync(Window owner, string path, bool hasExistingFiles, bool hasNewFiles, bool onlyWorkTree);

    Task<bool> ResetCurrentBranchAsync(string path, string revision);

    Task<bool> ResolveConflictsAsync(Window owner, string path);

    Task<bool> RevertCommitAsync(Window owner, string path, string revision);

    Task<bool> RunCommandAsync(CommandLineRequest request);

    Task<bool> RunRemoteGitAsync(string path, string? remote, string arguments);

    Task<bool> ShowCommitDiffAsync(Window owner, string path, string revision);

    Task<bool> ShowFileHistoryAsync(Window owner, string path, string file, string? revision, bool showBlame);

    Task<bool> SparseWorkingCopyAsync(string path);

    Task<bool> SpecialCommitAsync(string path, CommitKind kind, ObjectId id);

    Task<bool> StashWindowAsync(Window owner, bool manageStashes, string? initialStash);

    Task<bool> UpdateSubmodulesAsync(string repositoryPath, string? submodulePath);

    Task<bool> VerifyDatabaseAsync(string path);

    Task<bool> ViewPatchAsync(Window owner, string path, string? patchFile);

    Task<bool> WorktreeCreateAsync(Window owner, string path, string mainWorktreePath);

    Task<bool> WorktreeDeleteAsync(Window owner, string path, string worktreePath);

    Task<bool> WorktreeSwitchAsync(Window owner, string worktreePath);
}
