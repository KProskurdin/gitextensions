using GitExtensions.Extensibility.Git;

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
/// <param name="AheadBehind">Upstream's left panel ahead and behind texts (<c>AheadBehindData.ToDisplay</c>) by branch
/// name: a local branch by its name, a remote branch tracked by one by its name with the remote ("origin/main").</param>
/// <param name="RemoteUrls">The fetch URL of each remote, by its name, the inactive ones included.</param>
/// <param name="UserEmail">The effective <c>user.email</c>, whose commits the grid highlights until one is selected.</param>
/// <param name="InactiveRemotes">The remotes upstream's <c>FormRemotes</c> deactivated (kept in .git/config, invisible to
/// git), which upstream's left panel lists apart.</param>
/// <param name="RemoteColors">The colors set on remotes in upstream's remotes window, by remote name, which the grid's
/// labels of their branches take (upstream's <c>GitModule.GetRemoteColors</c>).</param>
/// <param name="RelatedBranches">Upstream's <c>BaseBranchLeafNode.RelatedBranch</c>, by the branch names of
/// <paramref name="AheadBehind"/>: a local branch's upstream, a remote branch's local branch, as full ref names.</param>
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
    bool IsBisecting = false,
    IReadOnlyDictionary<string, string>? AheadBehind = null,
    IReadOnlyList<WorktreeInfo>? Worktrees = null,
    IReadOnlyDictionary<string, string>? RemoteUrls = null,
    string? UserEmail = null,
    IReadOnlyList<string>? InactiveRemotes = null,
    IReadOnlyDictionary<string, System.Drawing.Color>? RemoteColors = null,
    IReadOnlyDictionary<string, string>? RelatedBranches = null);

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
    /// <summary>
    ///  Upstream's <c>GitItemStatus.OldName</c>: the name a renamed file had.
    /// </summary>
    public string? OldPath { get; init; }

    /// <summary>
    ///  The paths upstream's <c>BatchUnstageFiles</c> takes out of the index for this file: a rename is also the removal of its
    ///  old name, which would otherwise stay staged.
    /// </summary>
    public IEnumerable<string> IndexPaths => Kind == ChangeKind.Renamed && OldPath is { } oldPath ? [Path, oldPath] : [Path];

    /// <summary>
    ///  Upstream's <c>GitItemStatus.IsSkipWorktree</c>: git's skip-worktree flag of the file in the index.
    /// </summary>
    public bool IsSkipWorktree { get; init; }

    /// <summary>
    ///  Upstream's <c>GitItemStatus.IsAssumeUnchanged</c>: git's assume-unchanged flag of the file in the index.
    /// </summary>
    public bool IsAssumeUnchanged { get; init; }

    /// <summary>
    ///  Upstream's <c>GitItemStatus.IsSubmodule</c>: the entry is a submodule.
    /// </summary>
    public bool IsSubmodule { get; init; }

    /// <summary>
    ///  Upstream's <c>GitItemStatus.IsChanged</c> of a submodule: its commit changed.
    /// </summary>
    public bool IsSubmoduleChanged { get; init; }

    /// <summary>
    ///  Upstream's <c>GitItemStatus.IsDirty</c> of a submodule: it has changes of its own (modified or untracked files).
    /// </summary>
    public bool IsSubmoduleDirty { get; init; }

    public string Display => $"{Kind}{(Staged ? " (staged)" : "")}: {Path}";

    /// <summary>
    ///  The entry in a list that already separates staged from unstaged changes, e.g. "Modified: a.txt".
    /// </summary>
    public string Label => $"{Kind}: {Path}";

    /// <summary>
    ///  The name of upstream's status image for the file (<see cref="FileStatusList.ImageFor"/>).
    /// </summary>
    public string Image => FileStatusList.ImageFor(Kind);
}

/// <summary>
///  A worktree of the repository. The main worktree (the first git lists) holds the repository and cannot be removed.
///  <see cref="HeadType"/>, <see cref="Branch"/> and <see cref="Sha1"/> are upstream's <c>GitWorktree</c> fields.
/// </summary>
public sealed record WorktreeInfo(string Path, string Display, bool IsMain, bool IsDeleted,
    GitWorktreeHeadType HeadType = GitWorktreeHeadType.Branch, string? Branch = null, string? Sha1 = null);

/// <summary>
///  A submodule as <c>git submodule status</c> reports it, read through upstream's <c>GitModule.GetSubmodulesInfo</c>.
/// </summary>
public sealed record SubmoduleInfo(string Path, bool IsInitialized, bool IsUpToDate)
{
    public string Display => !IsInitialized ? $"{Path} (not initialized)" : IsUpToDate ? Path : $"{Path} (changed)";
}
