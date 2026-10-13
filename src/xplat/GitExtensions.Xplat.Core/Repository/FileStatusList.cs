using System.Text.RegularExpressions;
using GitCommands;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Upstream's file list (<c>FileStatusList</c>) rules that need no UI: the status image of a file, the order of the
///  group-by choices, and the file name filter.
/// </summary>
public static class FileStatusList
{
    /// <summary>
    ///  Upstream's <c>GetItemImageKey</c> for a working directory or index file: deleted, then new or untracked, then
    ///  unmerged, then changed, then renamed.
    /// </summary>
    public static string ImageFor(ChangeKind kind) => kind switch
    {
        ChangeKind.Deleted => "FileStatusRemoved",
        ChangeKind.Added or ChangeKind.Untracked => "FileStatusAdded",
        ChangeKind.Conflict => "Unmerged",
        ChangeKind.Renamed => "FileStatusRenamed",
        _ => "FileStatusModified",
    };

    /// <summary>
    ///  The files in the order of upstream's group-by choice (<c>DiffListSorting</c>): by path, by extension and then path, or
    ///  by status (in the order of upstream's status images: unmerged, modified, renamed, added, removed) and then path. The
    ///  list is flat, so the tree and flat variants of a choice give the same order.
    /// </summary>
    public static IReadOnlyList<FileChange> Sort(IEnumerable<FileChange> files, DiffListSortType sorting)
    {
        IOrderedEnumerable<FileChange> ordered = sorting switch
        {
            DiffListSortType.FileExtension or DiffListSortType.FileExtensionFlat
                => files.OrderBy(file => Path.GetExtension(file.Path), StringComparer.OrdinalIgnoreCase),
            DiffListSortType.FileStatus or DiffListSortType.FileStatusFlat
                => files.OrderBy(file => StatusRank(file.Kind)),
            _ => files.OrderBy(_ => 0),
        };
        return [.. ordered.ThenBy(file => file.Path, StringComparer.Ordinal)];

        static int StatusRank(ChangeKind kind) => kind switch
        {
            ChangeKind.Conflict => 0,
            ChangeKind.Modified => 1,
            ChangeKind.Renamed => 2,
            ChangeKind.Added or ChangeKind.Untracked => 3,
            _ => 4,
        };
    }

    /// <summary>
    ///  Upstream's filter box: a regular expression matched against the path, ignoring case. An empty text shows every file;
    ///  an invalid expression gives null and <paramref name="error"/>, and upstream then keeps the list as it was.
    /// </summary>
    public static Regex? Filter(string? text, out string? error)
    {
        error = null;
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        try
        {
            return new Regex(text, RegexOptions.IgnoreCase);
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return null;
        }
    }
}

/// <summary>
///  Which items of upstream's file list menu (<c>FileStatusList.UpdateStatusOfMenuItems</c>, with
///  <c>RevisionDiffController</c>'s rules) apply to the files selected in the commit window's unstaged or staged list.
/// </summary>
public sealed record FileMenuState(
    bool Stage,
    bool Unstage,
    bool ResetFile,
    bool OpenWorkingFile,
    bool EditWorkingFile,
    bool DeleteFile,
    bool CopyPaths,
    bool ShowInFolder,
    bool History,
    bool Blame,
    bool Ignore,
    bool StopTracking)
{
    /// <param name="selected">The selected files of one list.</param>
    /// <param name="staged">Whether the list is the staged one (index) rather than the unstaged one (working directory).</param>
    /// <param name="exists">Whether a repository-relative path exists as a file.</param>
    public static FileMenuState For(IReadOnlyList<FileChange> selected, bool staged, Func<string, bool> exists)
    {
        int count = selected.Count;
        bool any = count > 0;
        bool anyTracked = selected.Any(file => file.Kind != ChangeKind.Untracked);
        bool allExist = any && selected.All(file => exists(file.Path));
        bool single = count == 1;
        return new FileMenuState(
            Stage: !staged && any,
            Unstage: staged && any,
            ResetFile: any && anyTracked,
            OpenWorkingFile: single && allExist,
            EditWorkingFile: single && allExist,
            DeleteFile: allExist,
            CopyPaths: any,
            ShowInFolder: any,
            History: single && anyTracked,
            Blame: single && anyTracked,
            Ignore: !staged && any,
            StopTracking: single && anyTracked);
    }
}
