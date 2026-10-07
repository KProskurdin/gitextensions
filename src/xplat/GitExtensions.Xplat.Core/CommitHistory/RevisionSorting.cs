using GitCommands;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  The grid's commit order as upstream's <c>RevisionReader</c> asks git for it (upstream's <c>RevisionSortOrder</c>).
/// </summary>
public static class RevisionSorting
{
    /// <summary>
    ///  The <c>git log</c> flag for an order: none for git's default, <c>--author-date-order</c> or <c>--topo-order</c>.
    /// </summary>
    public static string Argument(RevisionSortOrder order)
        => order switch
        {
            RevisionSortOrder.AuthorDate => "--author-date-order",
            RevisionSortOrder.Topology => "--topo-order",
            _ => "",
        };
}
