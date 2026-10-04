namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Reads the state of a repository: current branch, branches and changed files. Implementations run the work off the calling thread.
/// </summary>
public interface IRepositoryService
{
    Task<RepositorySnapshot> GetSnapshotAsync(string repositoryPath);
}

public sealed record RepositorySnapshot(string? CurrentBranch, IReadOnlyList<BranchInfo> Branches, IReadOnlyList<FileChange> Changes, bool IsMerging = false);

public sealed record BranchInfo(string Name, bool IsRemote, bool IsCurrent)
{
    public string Display => IsCurrent ? $"* {Name}" : Name;
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
}
