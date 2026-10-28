using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The selected branches checked out in other worktrees, as upstream's <c>ClassifyWorktreeBranches</c> sorts them:
///  whether one is in a worktree whose folder is gone, those in the main worktree (which cannot go), and those in linked
///  ones.
/// </summary>
public sealed record WorktreeBranches(
    bool HasDeletedWorktrees,
    IReadOnlyList<(string Branch, WorktreeInfo Worktree)> MainWorktreeBranches,
    IReadOnlyList<(string Branch, WorktreeInfo Worktree)> LinkedWorktreeBranches)
{
    /// <summary>
    ///  True when no selected branch is held by another worktree.
    /// </summary>
    public bool IsEmpty => !HasDeletedWorktrees && MainWorktreeBranches.Count == 0 && LinkedWorktreeBranches.Count == 0;
}

/// <summary>
///  Upstream's <c>FormDeleteBranch</c> and its <c>BranchComboBox</c> without their windows: the branches the box's text
///  names, and the selected branches other worktrees hold.
/// </summary>
public static class DeleteBranchDialog
{
    // Upstream's texts.
    public const string DeleteBranchCaption = "Delete Branches";
    public const string CannotDeleteCurrentBranch = "Cannot delete the branch “{0}” which you are currently on.";

    public const string BranchUsedByWorktreeQuestion =
        "The following branches are checked out in worktrees and cannot be deleted directly:\n\n{0}\n\n"
        + "Do you want to delete the worktrees and branches together?";

    public const string CannotDeleteBranchInMainWorktree =
        "The branch “{0}” cannot be deleted because it is checked out in the main worktree at:\n{1}";

    public const string BranchNotSelectable =
        "Branch '{0}' is not selectable, this branch has been removed from the selection.";

    /// <summary>
    ///  Upstream's <c>BranchComboBox.GetSelectedBranches</c>: the names in the text, split on spaces, that are branches;
    ///  the others are upstream's error.
    /// </summary>
    public static (IReadOnlyList<string> Branches, IReadOnlyList<string> Unknown) Selection(string? text,
        IReadOnlyList<string> branches)
    {
        string[] names = (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return ([.. names.Where(branches.Contains)], [.. names.Where(name => !branches.Contains(name))]);
    }

    /// <summary>
    ///  Upstream's text for several branches: their names with spaces between.
    /// </summary>
    public static string SelectionText(IEnumerable<string> branches) => string.Join(" ", branches);

    /// <summary>
    ///  Upstream's <c>ClassifyWorktreeBranches</c>; the worktree of <paramref name="currentWorkingDir"/> is left out, as
    ///  its branch is the current branch.
    /// </summary>
    public static WorktreeBranches ClassifyWorktreeBranches(IReadOnlyList<string> selectedBranches,
        IReadOnlyList<WorktreeInfo> worktrees, string currentWorkingDir)
    {
        string current = Folder(currentWorkingDir);
        bool hasDeletedWorktrees = false;
        List<(string Branch, WorktreeInfo Worktree)> main = [];
        List<(string Branch, WorktreeInfo Worktree)> linked = [];
        foreach (WorktreeInfo worktree in worktrees)
        {
            if (worktree.Branch is not { } branch || !selectedBranches.Contains(branch))
            {
                continue;
            }

            if (worktree.IsDeleted)
            {
                hasDeletedWorktrees = true;
                continue;
            }

            if (string.Equals(Folder(worktree.Path), current, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            (worktree.IsMain ? main : linked).Add((branch, worktree));
        }

        return new WorktreeBranches(hasDeletedWorktrees, main, linked);

        static string Folder(string path)
            => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
