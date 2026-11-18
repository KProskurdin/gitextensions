using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Upstream's <c>IRevisionGridFilter</c>: the revision grid a filter toolbar (<see cref="FilterToolBarViewModel"/>)
///  filters.
/// </summary>
public interface IFilterToolBarGrid
{
    /// <summary>
    ///  The repository the grid shows, or null.
    /// </summary>
    string? RepositoryPath { get; }

    /// <summary>
    ///  The filter the grid applies.
    /// </summary>
    RevisionFilter Filter { get; }

    /// <summary>
    ///  Applies a filter (upstream's <c>SetAndApply...Filter</c>), keeping the options upstream stores.
    /// </summary>
    Task ApplyFilterAsync(RevisionFilter filter);

    /// <summary>
    ///  Upstream's <c>ShowRevisionFilterDialog</c>: the advanced filter window.
    /// </summary>
    Task ShowRevisionFilterDialogAsync();

    /// <summary>
    ///  The branches and tags the "Branches:" box offers.
    /// </summary>
    Task<(IReadOnlyList<BranchInfo> Branches, IReadOnlyList<string> Tags)> RefsAsync();
}
