using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Repository;
using GitUI;
using GitUI.CommandsDialogs;
using GitUI.UserControls;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  The items below the separator of upstream's "Open with difftool" submenu (<c>OpenWithDifftool_DropDownOpening</c>):
///  "Diff the selected files" for two files, "Diff with ..." the remembered file, and "Remember Second / First for diff"
///  for one. A text that is <see langword="null"/> hides its item.
/// </summary>
public sealed record DiffRememberState(
    bool Separator,
    bool DiffTwoSelected,
    bool DiffTwoSelectedEnabled,
    string? DiffWithRemembered,
    bool DiffWithRememberedEnabled,
    bool RememberSecond,
    bool RememberSecondEnabled,
    bool RememberFirst,
    bool RememberFirstEnabled);

/// <summary>
///  Upstream's remembered file of the file lists (<c>RememberFileContextMenuController.Default</c>, linked unchanged): one
///  file at one revision, kept for the whole app as upstream's, which a later selected file (or two selected files) is
///  compared with in the difftool.
/// </summary>
public static class DiffFileRemember
{
    // The repository the remembered file is in. Upstream forgets the file when the browse window opens another
    // repository (FormBrowse calls RevisionDiffControl.RepositoryChanged); here a file of another repository is not offered.
    private static string? _rememberedIn;

    private static RememberFileContextMenuController Controller => RememberFileContextMenuController.Default;

    /// <summary>
    ///  The item states for <paramref name="selection"/>.
    /// </summary>
    /// <param name="workingDir">The repository's working directory.</param>
    /// <param name="focused">The index in <see cref="DiffMenuSelection.Files"/> of the file selected last (upstream's
    /// <c>FocusedItem</c>), or -1.</param>
    public static DiffRememberState For(DiffMenuSelection selection, string workingDir, int focused)
    {
        FileStatusItem[] items = DiffFileMenu.Items(selection);
        (int firstIndex, int secondIndex) = TwoSelectedOrder(items.Length, focused);
        FileStatusItem? remembered = Remembered(workingDir);
        bool one = items.Length == 1;
        bool two = items.Length == 2;
        return new DiffRememberState(
            Separator: one || two,
            DiffTwoSelected: two,
            DiffTwoSelectedEnabled: two && Controller.ShouldEnableFirstItemDiff(items[firstIndex])
                                        && Controller.ShouldEnableSecondItemDiff(items[secondIndex]),
            DiffWithRemembered: one && remembered is not null
                ? string.Format(TranslatedStrings.DiffSelectedWithRememberedFile, remembered.Item.Name)
                : null,
            DiffWithRememberedEnabled: one && remembered is not null && !IsRemembered(items[0], remembered)
                                       && Controller.ShouldEnableSecondItemDiff(items[0]),
            RememberSecond: one,
            RememberSecondEnabled: one && Controller.ShouldEnableFirstItemDiff(items[0], isSecondRevision: true),
            RememberFirst: one,
            RememberFirstEnabled: one && Controller.ShouldEnableFirstItemDiff(items[0], isSecondRevision: false));
    }

    /// <summary>
    ///  Upstream's <c>RememberSecondRevDiff_Click</c> and <c>RememberFirstRevDiff_Click</c>: the selected file at its second
    ///  (B) or first (A) revision, by its name there.
    /// </summary>
    public static void Remember(DiffMenuSelection selection, string workingDir, bool first)
    {
        if (DiffFileMenu.Items(selection) is not [{ } item])
        {
            return;
        }

        if (first)
        {
            if (item.FirstRevision is null)
            {
                return;
            }

            // Upstream swaps the revisions, and the file's names so that the first revision is read under its old name.
            GitItemStatus status = item.Item;
            if (!string.IsNullOrWhiteSpace(status.OldName))
            {
                (status.Name, status.OldName) = (status.OldName, status.Name);
            }

            item = new FileStatusItem(firstRev: item.SecondRevision, secondRev: item.FirstRevision, item: status);
        }

        Controller.RememberedDiffFileItem = item;
        _rememberedIn = workingDir;
    }

    /// <summary>
    ///  Forgets the remembered file.
    /// </summary>
    public static void Forget()
    {
        Controller.RememberedDiffFileItem = null;
        _rememberedIn = null;
    }

    /// <summary>
    ///  Upstream's <c>DiffTwoSelected_Click</c>: the two selected files in the difftool, the one selected last as the second;
    ///  each at its second revision when it exists there, else at its first.
    /// </summary>
    /// <param name="tool">A diff tool of git's, or <see langword="null"/> for the default one.</param>
    public static Task DiffTwoSelectedAsync(DiffMenuSelection selection, string workingDir, int focused,
        string? tool = null)
        => Task.Run(() =>
        {
            (string? first, string? second) = TwoSelectedCommits(selection, workingDir, focused);
            GitModules.Open(workingDir).OpenFilesWithDifftool(first, second, customTool: tool);
        });

    /// <summary>
    ///  Upstream's <c>DiffWithRemembered_Click</c>: the remembered file against the selected one at its second revision when
    ///  it exists there, else at its first.
    /// </summary>
    /// <param name="tool">A diff tool of git's, or <see langword="null"/> for the default one.</param>
    public static Task DiffWithRememberedAsync(DiffMenuSelection selection, string workingDir, string? tool = null)
        => Task.Run(() =>
        {
            (string? first, string? second) = WithRememberedCommits(selection, workingDir);
            GitModules.Open(workingDir).OpenFilesWithDifftool(first, second, customTool: tool);
        });

    /// <summary>
    ///  The two commitishes (<c>commit:path</c>, a blob for the index, a path for the working directory)
    ///  <see cref="DiffTwoSelectedAsync"/> compares; null for each without two files.
    /// </summary>
    public static (string? First, string? Second) TwoSelectedCommits(DiffMenuSelection selection, string workingDir,
        int focused)
    {
        FileStatusItem[] items = DiffFileMenu.Items(selection);
        if (items.Length != 2)
        {
            return (null, null);
        }

        (int firstIndex, int secondIndex) = TwoSelectedOrder(items.Length, focused);
        GitModule module = GitModules.Open(workingDir);

        // Upstream: "Fallback to first revision if second revision cannot be used".
        bool isFirstItemSecondRev = Controller.ShouldEnableFirstItemDiff(items[firstIndex], isSecondRevision: true);
        bool isSecondItemSecondRev = Controller.ShouldEnableSecondItemDiff(items[secondIndex], isSecondRevision: true);
        return (Controller.GetGitCommit(module.GetFileBlobHash, items[firstIndex], isFirstItemSecondRev),
            Controller.GetGitCommit(module.GetFileBlobHash, items[secondIndex], isSecondItemSecondRev));
    }

    /// <summary>
    ///  The two commitishes <see cref="DiffWithRememberedAsync"/> compares: the remembered file's, then the selected one's.
    /// </summary>
    public static (string? First, string? Second) WithRememberedCommits(DiffMenuSelection selection, string workingDir)
    {
        if (DiffFileMenu.Items(selection) is not [{ } item] || Remembered(workingDir) is not { } remembered)
        {
            return (null, null);
        }

        GitModule module = GitModules.Open(workingDir);
        bool isSecond = Controller.ShouldEnableSecondItemDiff(item, isSecondRevision: true);
        return (Controller.GetGitCommit(module.GetFileBlobHash, remembered, isSecondRevision: true),
            Controller.GetGitCommit(module.GetFileBlobHash, item, isSecond));
    }

    // Upstream: "The order is always the order in the list, not clicked order, but the (last) selected is known".
    private static (int First, int Second) TwoSelectedOrder(int count, int focused)
    {
        int firstIndex = count == 2 && focused == 0 ? 1 : 0;
        return (firstIndex, 1 - firstIndex);
    }

    private static FileStatusItem? Remembered(string workingDir)
        => _rememberedIn is not null && SamePath(_rememberedIn, workingDir) ? Controller.RememberedDiffFileItem : null;

    private static bool SamePath(string first, string second)
        => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    // Upstream compares the selected item with the remembered one by reference; the menu's items are made for each
    // opening here, so the remembered second revision counts as the same file when its name and revisions are the same.
    private static bool IsRemembered(FileStatusItem item, FileStatusItem remembered)
        => item.Item.Name == remembered.Item.Name && item.SecondRevision.ObjectId == remembered.SecondRevision.ObjectId
                                                  && item.FirstRevision?.ObjectId == remembered.FirstRevision?.ObjectId;
}
