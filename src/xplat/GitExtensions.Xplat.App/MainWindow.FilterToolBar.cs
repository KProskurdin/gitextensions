using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>ToolStripFilters.Bind(() => Module, RevisionGrid)</c>: the filter toolstrip (<see cref="FilterToolBar"/>)
///  filters the browse window's grid.
/// </summary>
public partial class MainWindow : IFilterToolBarGrid
{
    string? IFilterToolBarGrid.RepositoryPath => RepositoryPath;

    RevisionFilter IFilterToolBarGrid.Filter => _commits.Filter;

    Task IFilterToolBarGrid.ApplyFilterAsync(RevisionFilter filter) => ApplyRevisionFilterAsync(filter);

    Task IFilterToolBarGrid.ShowRevisionFilterDialogAsync() => EditRevisionFilterAsync();

    Task<(IReadOnlyList<BranchInfo> Branches, IReadOnlyList<string> Tags)> IFilterToolBarGrid.RefsAsync()
        => Task.FromResult((_repository.Branches, _repository.Tags));

    private void WireFilterToolStrip() => FilterToolStrip.Bind(this, _preferences);

    private void ShowFilterState() => FilterToolStrip.ShowFilterState();

    private void ShowBranches(bool currentOnly, bool filtered) => FilterToolStrip.ShowBranches(currentOnly, filtered);

    private void ApplyFilter(RevisionFilter filter) => Run(() => ApplyRevisionFilterAsync(filter));
}
