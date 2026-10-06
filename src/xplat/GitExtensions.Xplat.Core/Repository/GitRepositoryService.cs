using GitCommands;
using GitCommands.Config;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Reads repository state with the shared git engine.
/// </summary>
public sealed class GitRepositoryService : IRepositoryService
{
    public Task<RepositorySnapshot> GetSnapshotAsync(string repositoryPath) =>
        Task.Run(() => GetSnapshot(repositoryPath));

    public Task<string> GetHeadMessageAsync(string repositoryPath) =>
        Task.Run(() => new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath)
            .GitExecutable.GetOutput(new GitArgumentBuilder("log") { "-1", "--format=%B" }).TrimEnd());

    // Upstream's GitModule.GetWorktrees parses "worktree list --porcelain -z"; git lists the main worktree first.
    public Task<IReadOnlyList<WorktreeInfo>> GetWorktreesAsync(string repositoryPath) =>
        Task.Run<IReadOnlyList<WorktreeInfo>>(() =>
        {
            IReadOnlyList<GitWorktree> worktrees = new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath).GetWorktrees();
            return [.. worktrees.Select((worktree, index) => new WorktreeInfo(worktree.Path,
                worktree.GetDisplayName(worktree.Path), IsMain: index == 0, worktree.IsDeleted))];
        });

    private static string? GetTrackingRemote(GitModule module, string currentBranch)
    {
        string remote = currentBranch.Length == 0 ? "" : module.GetSetting(string.Format(SettingKeyString.BranchRemote, currentBranch));
        return remote.Length == 0 ? null : remote;
    }

    private static SyncStatus? GetSync(GitModule module)
    {
        ExecutionResult result = module.GitExecutable.Execute(
            new GitArgumentBuilder("rev-list") { "--left-right", "--count", "@{u}...HEAD" }, throwOnErrorExit: false);
        if (!result.ExitedSuccessfully)
        {
            return null;
        }

        string[] counts = result.StandardOutput.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
        return counts.Length == 2 && int.TryParse(counts[0], out int behind) && int.TryParse(counts[1], out int ahead)
            ? new SyncStatus(ahead, behind)
            : null;
    }

    // The list comes from .gitmodules (upstream's GetSubmodulesLocalPaths). Upstream's status parser needs git's describe
    // suffix, which "git submodule status" leaves out for a submodule that is not initialized; such a submodule has no status
    // entry and is shown as not initialized.
    private static IReadOnlyList<SubmoduleInfo> GetSubmodules(GitModule module)
    {
        IReadOnlyList<string> paths = module.GetSubmodulesLocalPaths(recursive: false);
        if (paths.Count == 0)
        {
            return [];
        }

        Dictionary<string, IGitSubmoduleInfo> status = module.GetSubmodulesInfo().OfType<IGitSubmoduleInfo>()
            .GroupBy(info => info.LocalPath, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        return [.. paths.Select(path => status.TryGetValue(path, out IGitSubmoduleInfo? info)
            ? new SubmoduleInfo(path, info.IsInitialized, info.IsUpToDate)
            : new SubmoduleInfo(path, IsInitialized: false, IsUpToDate: false))];
    }

    public static FileChange ToFileChange(GitItemStatus status)
    {
        ChangeKind kind = status switch
        {
            { IsUnmerged: true } => ChangeKind.Conflict,
            { IsTracked: false } => ChangeKind.Untracked,
            { IsRenamed: true } => ChangeKind.Renamed,
            { IsDeleted: true } => ChangeKind.Deleted,
            { IsNew: true } => ChangeKind.Added,
            _ => ChangeKind.Modified,
        };

        return new FileChange(status.Name, kind, Staged: status.Staged == StagedStatus.Index);
    }

    private static RepositorySnapshot GetSnapshot(string path)
    {
        GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()), path);
        if (!module.IsValidGitWorkingDir())
        {
            throw new InvalidOperationException($"Not a git repository: {path}");
        }

        string currentBranch = module.GetSelectedBranch(emptyIfDetached: true);

        List<BranchInfo> branches =
        [
            .. module.GetRefs(RefsFilter.Heads | RefsFilter.Remotes)
                .Select(branch => new BranchInfo(branch.Name, branch.IsRemote,
                    IsCurrent: branch.IsHead && branch.Name == currentBranch)),
        ];

        List<FileChange> changes = [.. module.GetAllChangedFilesWithSubmodulesStatus().Select(ToFileChange)];

        bool isMerging = File.Exists(Path.Combine(module.WorkingDirGitDir, "MERGE_HEAD"));
        bool isRebasing = Directory.Exists(Path.Combine(module.WorkingDirGitDir, "rebase-merge"))
                          || Directory.Exists(Path.Combine(module.WorkingDirGitDir, "rebase-apply"));
        List<StashInfo> stashes = [.. module.GetStashes().Select(stash => new StashInfo(stash.Name, stash.Message))];
        List<string> tags = [.. module.GitExecutable.GetOutput(new GitArgumentBuilder("tag") { "--list" })
            .Split(Delimiters.LineFeed, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

        return new RepositorySnapshot(currentBranch.Length == 0 ? null : currentBranch, branches, changes, isMerging, stashes, tags, isRebasing, module.GetRemoteNames(), GetSync(module), GetTrackingRemote(module, currentBranch), GetSubmodules(module));
    }
}
