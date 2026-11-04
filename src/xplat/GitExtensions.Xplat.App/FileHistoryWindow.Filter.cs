using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>ToolStripFilters.Bind(() => Module, RevisionGrid)</c> in FormFileHistory: the filter toolstrip
///  (<see cref="FilterToolBar"/>) filters the file's grid, which starts from the filter options upstream keeps in settings
///  (<see cref="StoredRevisionFilter"/>), as the browse window's does.
/// </summary>
public partial class FileHistoryWindow : IFilterToolBarGrid
{
    string? IFilterToolBarGrid.RepositoryPath => _repositoryPath;

    RevisionFilter IFilterToolBarGrid.Filter => _commits.Filter;

    /// <summary>
    ///  The filter toolstrip, for tests.
    /// </summary>
    public FilterToolBar FilterToolBar => ToolStripFilters;

    // Upstream's grid filter: the options are stored as the browse window's; the grid is read again once it shows (before
    // Load the filter only waits).
    Task IFilterToolBarGrid.ApplyFilterAsync(RevisionFilter filter)
    {
        StoredRevisionFilter.Store(filter, _preferences);
        return ApplyGridFilterAsync(StoredRevisionFilter.WithStoredHistory(filter, _preferences));
    }

    // Upstream's ShowRevisionFilterDialog, with FormRevisionFilter's stored choices.
    async Task IFilterToolBarGrid.ShowRevisionFilterDialogAsync()
    {
        if (await new FilterWindow(_commits.Filter, _preferences.MaxRevisionGraphCommits)
                .ShowDialog<RevisionFilter?>(this) is { } filter)
        {
            StoredRevisionFilter.StoreDialogChoice(filter, _preferences);
            ShowOptionChecks();
            await ((IFilterToolBarGrid)this).ApplyFilterAsync(filter);
        }
    }

    // Upstream's UpdateBranchFilterItems reads the module's branches and tags.
    async Task<(IReadOnlyList<BranchInfo> Branches, IReadOnlyList<string> Tags)> IFilterToolBarGrid.RefsAsync()
    {
        IReadOnlyList<IGitRef> refs = await Task.Run(() =>
            _module.GetRefs(RefsFilter.Heads | RefsFilter.Remotes | RefsFilter.Tags));
        return (
            [
                .. refs.Where(gitRef => gitRef.IsHead || gitRef.IsRemote)
                    .Select(gitRef => new BranchInfo(gitRef.Name, gitRef.IsRemote, IsCurrent: false))
            ],
            [.. refs.Where(gitRef => gitRef.IsTag).Select(gitRef => gitRef.Name)]);
    }

    private void WireFilterToolStrip(string? revision, bool filterByRevision)
    {
        _ = _commits.ApplyFilterAsync(StoredRevisionFilter.Initial(_preferences));
        ToolStripFilters.Bind(this, _preferences);

        // Upstream's filterByRevision (the command line's "filterbyrevision"): the commit in the "Filter:" box.
        if (filterByRevision && revision is not null)
        {
            ToolStripFilters.SetRevisionFilter(revision);
        }
    }

    // The grid reads the filter once it shows, and selects the caller's commit at first, then the one selected before
    // (upstream's grid keeps its selection), else the newest.
    private async Task ApplyGridFilterAsync(RevisionFilter filter)
    {
        await _commits.ApplyFilterAsync(filter);
        if (!HistoryList.IsVisible)
        {
            return;
        }

        if (_commits.RepositoryPath is null)
        {
            await _commits.OpenAsync(_repositoryPath);
        }

        Run(LaunchBuildServerAsync);
        string? select = _selectedOnce ? _selectedHash : _revision;
        _selectedOnce = true;
        CommitListItem? selected = select is null
            ? null
            : _commits.VisibleRows.FirstOrDefault(item =>
                item.Row.Hash.StartsWith(select, StringComparison.OrdinalIgnoreCase));
        HistoryList.SelectedItem = selected ?? _commits.VisibleRows.FirstOrDefault();
    }
}
