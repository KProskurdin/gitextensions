using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Reads repository state with the shared git engine.
/// </summary>
public sealed class GitRepositoryService : IRepositoryService
{
    public Task<RepositorySnapshot> GetSnapshotAsync(string repositoryPath) =>
        Task.Run(() => GetSnapshot(repositoryPath));

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
        return new RepositorySnapshot(currentBranch.Length == 0 ? null : currentBranch, branches, changes, isMerging);
    }
}
