namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  The blame menu's "Blame previous revision": its text and the parents to go by, null when it is disabled.
/// </summary>
public sealed record BlamePreviousRevision(string Text, IReadOnlyList<string>? Parents);

/// <summary>
///  The commit a blame view asks its window to show, and the path of the blamed file in that commit (upstream's
///  <c>IRevisionGridFileUpdate.SelectFileInRevision</c>).
/// </summary>
public sealed record BlameCommitRequest(string Hash, string FileName);

/// <summary>
///  The rules of upstream <c>BlameControl</c>'s context menu.
/// </summary>
public static class BlameMenu
{
    public const string BlameActualPreviousRevision = "&Blame previous revision";
    public const string BlameVisiblePreviousRevision = "&Blame previous visible revision";

    /// <summary>
    ///  Upstream's <c>contextMenu_Opened</c>: the commit's real first parent when the grid shows it, else the first parent
    ///  of the grid's row, which a filtered grid rewrites to the previous row it shows.
    /// </summary>
    public static BlamePreviousRevision PreviousRevision(IReadOnlyList<string>? gridParents,
        IReadOnlyList<string> actualParents, Func<string, bool> isInGrid)
    {
        if (actualParents is [var actualParent, ..] && isInGrid(actualParent))
        {
            return new BlamePreviousRevision(BlameActualPreviousRevision, actualParents);
        }

        return new BlamePreviousRevision(BlameVisiblePreviousRevision,
            gridParents is [var gridParent, ..] && isInGrid(gridParent) ? gridParents : null);
    }
}
