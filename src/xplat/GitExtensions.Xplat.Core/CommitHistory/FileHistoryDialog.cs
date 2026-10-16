using GitCommands;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  The tabs of upstream's file history window.
/// </summary>
public enum FileHistoryTab
{
    Commit,
    Diff,
    View,
    Blame,
}

/// <summary>
///  The tabs upstream's <c>UpdateSelectedFileViewers</c> shows for a commit, the one to switch to when the shown one is gone,
///  and the Commit tab's caption.
/// </summary>
public sealed record FileHistoryTabs(
    IReadOnlyList<FileHistoryTab> Visible,
    FileHistoryTab? Preferred,
    string CommitCaption);

/// <summary>
///  Upstream's <c>FormFileHistory</c> without its window: the title, which tabs a commit shows, the tab it opens on, whether
///  the history loads when it opens, and when the history menu's items apply.
/// </summary>
public sealed class FileHistoryDialog
{
    // Upstream's texts.
    public const string TitlePrefix = "File History - ";
    public const string FileNotFound = " - Git could not identify the file {0}";
    public const string CommitTabText = "Commit";

    /// <param name="fileName">The file, as the caller names it; upstream removes quotes and uses posix separators.</param>
    /// <param name="isSubmodule">The path is a submodule, which has no Blame tab or Save as.</param>
    public FileHistoryDialog(string fileName, bool isSubmodule = false)
    {
        FileName = fileName.RemoveQuotes().ToPosixPath();
        IsSubmodule = isSubmodule;
    }

    public string FileName { get; }

    public bool IsSubmodule { get; }

    public bool HasBlame => !IsSubmodule;

    /// <summary>
    ///  Upstream's <c>SetTitle</c>: the file, the name it has in the selected commit when that differs, and the repository.
    /// </summary>
    /// <param name="workingDirDisplay">Upstream's <c>PathUtil.GetDisplayPath</c> of the working directory.</param>
    public string Title(string workingDirDisplay, string? alternativeFileName = null)
        => TitlePrefix + FileName
                       + (!string.IsNullOrEmpty(alternativeFileName) && alternativeFileName != FileName
                           ? $" ({alternativeFileName})"
                           : "")
                       + " - " + workingDirDisplay;

    /// <summary>
    ///  Upstream's first tab: Blame when asked for (and there is one), else Diff.
    /// </summary>
    public FileHistoryTab InitialTab(bool showBlame) =>
        HasBlame && showBlame ? FileHistoryTab.Blame : FileHistoryTab.Diff;

    /// <summary>
    ///  Upstream's <c>OnRuntimeLoad</c>: the history is read when the window opens if the settings say so for the tab it
    ///  opens on; otherwise the grid stays hidden until Load.
    /// </summary>
    public static bool LoadsOnShow(FileHistoryTab tab, FileHistoryOptions options)
        => (tab == FileHistoryTab.Blame && options.LoadBlameOnShow) || options.LoadHistoryOnShow;

    /// <summary>
    ///  Upstream's <c>UpdateSelectedFileViewers</c>: the working directory and index rows have no Commit, View or Blame tab
    ///  and prefer Diff; a commit without the file (or a folder) has only the Commit tab, whose caption says so for a file.
    /// </summary>
    public FileHistoryTabs Tabs(bool isArtificial, bool fileAvailable, bool isFolder, string fileName)
    {
        List<FileHistoryTab> visible = [];
        FileHistoryTab? preferred = null;
        if (!isArtificial)
        {
            visible.Add(FileHistoryTab.Commit);
        }
        else
        {
            preferred = FileHistoryTab.Diff;
        }

        if (fileAvailable)
        {
            visible.Add(FileHistoryTab.Diff);
        }
        else
        {
            preferred = FileHistoryTab.Commit;
        }

        if (!isArtificial && fileAvailable)
        {
            visible.Add(FileHistoryTab.View);
            if (HasBlame)
            {
                visible.Add(FileHistoryTab.Blame);
            }
        }

        string caption = CommitTabText +
                         (isFolder || fileAvailable ? "" : string.Format(FileNotFound, fileName.Quote()));
        return new FileHistoryTabs(visible, preferred, caption);
    }

    /// <summary>
    ///  Upstream's <c>FileHistoryContextMenuOpening</c> for the grid's selection: the difftool, Save as, Manipulate commit
    ///  and Copy to clipboard items.
    /// </summary>
    /// <param name="selectedCount">The selected commits.</param>
    /// <param name="firstIsArtificial">The first selected commit is the working directory or index row.</param>
    /// <param name="firstIsWorkTree">The first selected commit is the working directory row.</param>
    /// <param name="fileExistsInWorkTree">The file exists in the working directory.</param>
    public static FileHistoryMenu Menu(int selectedCount, bool firstIsArtificial, bool firstIsWorkTree,
        bool fileExistsInWorkTree)
        => new(
            DiffToolRemoteLocal: selectedCount == 1 && !firstIsWorkTree && fileExistsInWorkTree,
            OpenWithDifftool: selectedCount is >= 1 and <= 2,
            ManipulateCommit: selectedCount == 1 && !firstIsArtificial,
            SaveAs: selectedCount == 1,
            CopyToClipboard: selectedCount >= 1 && !firstIsArtificial);
}

/// <summary>
///  Which of the file history menu's items apply (upstream's <c>FileHistoryContextMenuOpening</c>).
/// </summary>
public sealed record FileHistoryMenu(
    bool DiffToolRemoteLocal,
    bool OpenWithDifftool,
    bool ManipulateCommit,
    bool SaveAs,
    bool CopyToClipboard);
