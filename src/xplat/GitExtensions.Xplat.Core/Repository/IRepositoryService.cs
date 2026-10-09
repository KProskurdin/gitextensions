namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Reads the state of a repository: current branch, branches, changed files, stashes and tags. Implementations run the work off the calling thread.
/// </summary>
public interface IRepositoryService
{
    Task<RepositorySnapshot> GetSnapshotAsync(string repositoryPath);

    /// <summary>
    ///  The message of the commit at HEAD, as written, so an amend can start from it.
    /// </summary>
    Task<string> GetHeadMessageAsync(string repositoryPath);

    /// <summary>
    ///  The repository's worktrees, the main one first, as git lists them.
    /// </summary>
    Task<IReadOnlyList<WorktreeInfo>> GetWorktreesAsync(string repositoryPath);
}

/// <summary>
///  The state of a repository. <see cref="IsRebasing"/>, <see cref="IsMerging"/>, <see cref="IsApplyingPatch"/> and
///  <see cref="IsBisecting"/> follow upstream's <c>GitModule.InTheMiddleOf*</c> checks: a <c>git am</c> session is a patch,
///  not a rebase, although both keep their state in rebase-apply.
/// </summary>
public sealed record RepositorySnapshot(
    string? CurrentBranch,
    IReadOnlyList<BranchInfo> Branches,
    IReadOnlyList<FileChange> Changes,
    bool IsMerging,
    IReadOnlyList<StashInfo> Stashes,
    IReadOnlyList<string> Tags,
    bool IsRebasing,
    IReadOnlyList<string>? Remotes = null,
    SyncStatus? Sync = null,
    string? TrackingRemote = null,
    IReadOnlyList<SubmoduleInfo>? Submodules = null,
    bool IsApplyingPatch = false,
    bool IsBisecting = false);

/// <summary>
///  Commits the current branch has that its upstream lacks (<see cref="Ahead"/>) and the reverse (<see cref="Behind"/>).
/// </summary>
public sealed record SyncStatus(int Ahead, int Behind);

public sealed record BranchInfo(string Name, bool IsRemote, bool IsCurrent)
{
    public string Display => IsCurrent ? $"* {Name}" : Name;
}

/// <summary>
///  A stash entry. <see cref="Name"/> is the reference git accepts, e.g. "stash@{0}".
/// </summary>
public sealed record StashInfo(string Name, string Message)
{
    public string Display => $"{Name}: {Message}";
}

public enum ChangeKind
{
    Added,
    Modified,
    Deleted,
    Renamed,
    Untracked,
    Conflict
}

public sealed record FileChange(string Path, ChangeKind Kind, bool Staged)
{
    public string Display => $"{Kind}{(Staged ? " (staged)" : "")}: {Path}";

    /// <summary>
    ///  The entry in a list that already separates staged from unstaged changes, e.g. "Modified: a.txt".
    /// </summary>
    public string Label => $"{Kind}: {Path}";
}

/// <summary>
///  A worktree of the repository. The main worktree (the first git lists) holds the repository and cannot be removed.
/// </summary>
public sealed record WorktreeInfo(string Path, string Display, bool IsMain, bool IsDeleted);

/// <summary>
///  A submodule as <c>git submodule status</c> reports it, read through upstream's <c>GitModule.GetSubmodulesInfo</c>.
/// </summary>
public sealed record SubmoduleInfo(string Path, bool IsInitialized, bool IsUpToDate)
{
    public string Display => !IsInitialized ? $"{Path} (not initialized)" : IsUpToDate ? Path : $"{Path} (changed)";
}
