using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's <c>FormManageWorktree</c> without its window: which worktrees can be opened, deleted or pruned, and the
///  texts of upstream's <c>GitUICommands.WorktreeSwitch</c>, <c>WorktreeDelete</c> and the browse window's worktree
///  drop-down.
/// </summary>
public static class ManageWorktreeDialog
{
    // Upstream's TranslatedStrings.
    public const string SwitchCaption = "Open worktree";
    public const string DeleteQuestion = "Are you sure you want to delete the worktree at {0}?";
    public const string DeleteCaption = "Delete worktree";
    public const string CannotBeUndone = "This action cannot be undone.";
    public const string DeleteFailed = "Failed to delete worktree: {0}";
    public const string DirectoryNotFound = "Worktree directory does not exist: {0}";
    public const string CreateWorktree = "Create worktree...";
    public const string PruneWorktrees = "Prune worktrees";
    public const string ManageWorktrees = "Manage worktrees...";
    public const string Error = "Error";

    /// <summary>
    ///  Upstream enables "Prune deleted worktrees" only when a worktree other than the main one has lost its folder.
    /// </summary>
    public static bool CanPrune(IReadOnlyList<WorktreeInfo> worktrees) => worktrees.Skip(1).Any(worktree => worktree.IsDeleted);

    /// <summary>
    ///  Upstream's <c>CanActOnSelectedWorkspace</c>: another worktree than the one open, whose folder still exists, when
    ///  there is more than one.
    /// </summary>
    public static bool CanOpen(IReadOnlyList<WorktreeInfo> worktrees, int index, string workingDirectory)
        => worktrees.Count > 1 && index >= 0 && index < worktrees.Count && !worktrees[index].IsDeleted
           && !IsCurrent(worktrees[index], workingDirectory);

    /// <summary>
    ///  Upstream's <c>CanDeleteSelectedWorkspace</c>: as <see cref="CanOpen"/>, but never the main worktree, which holds
    ///  the repository.
    /// </summary>
    public static bool CanDelete(IReadOnlyList<WorktreeInfo> worktrees, int index, string workingDirectory)
        => CanOpen(worktrees, index, workingDirectory) && index != 0;

    /// <summary>
    ///  Upstream's <c>IsCurrentlyOpenedWorktree</c>; the trailing separator trimmed is the OS's, where upstream trims '\'.
    /// </summary>
    public static bool IsCurrent(WorktreeInfo worktree, string workingDirectory)
        => string.Equals(FullPath(worktree.Path), FullPath(workingDirectory),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    ///  The browse window's drop-down item: the folder's name with upstream's <c>GitWorktree.GetDisplayName</c>.
    /// </summary>
    public static string MenuText(WorktreeInfo worktree)
        => new GitWorktree(worktree.Path, worktree.HeadType, worktree.Sha1, worktree.Branch, worktree.IsDeleted)
            .GetDisplayName(Path.GetFileName(Path.TrimEndingDirectorySeparator(worktree.Path)));

    /// <summary>
    ///  Where upstream's create starts: the main worktree's folder, or the open repository's when there is none.
    /// </summary>
    public static string MainWorktreePath(IReadOnlyList<WorktreeInfo> worktrees, string workingDirectory)
        => worktrees.Count > 0 ? worktrees[0].Path : workingDirectory;

    private static string FullPath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
