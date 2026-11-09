using Avalonia.Controls;
using GitExtensions.Xplat.Core.CommitHistory;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's grid in a submodule (<c>RevisionGridControl.GetSuperprojectCheckoutAsync</c>, drawn by
///  <c>MessageColumnProvider</c>): the superproject's checkout, conflict and refs as dashed labels, with the View menu's
///  "Show superproject ..." choices; and the menu's "Show session checkpoints".
/// </summary>
public partial class MainWindow
{
    // Counts the reads, so that a slower read for an earlier refresh does not replace a later one.
    private int _superprojectReads;

    private void WireSuperprojectLabels()
    {
        // Upstream's ToggleShowSessionRefs and the three superproject toggles read the grid again (PerformRefreshRevisions).
        ToggleOnRefresh(ShowSessionCheckpointsMenuItem, () => _preferences.ShowSessionRefs,
            value => _preferences.ShowSessionRefs = value);
        ToggleOnRefresh(ShowSuperprojectTagsMenuItem, () => _preferences.ShowSuperprojectTags,
            value => _preferences.ShowSuperprojectTags = value);
        ToggleOnRefresh(ShowSuperprojectRemoteBranchesMenuItem, () => _preferences.ShowSuperprojectRemoteBranches,
            value => _preferences.ShowSuperprojectRemoteBranches = value);
        ToggleOnRefresh(ShowSuperprojectBranchesMenuItem, () => _preferences.ShowSuperprojectBranches,
            value => _preferences.ShowSuperprojectBranches = value);
    }

    private void ToggleOnRefresh(MenuItem item, Func<bool> get, Action<bool> set)
    {
        item.IsChecked = get();
        item.Click += (_, _) =>
        {
            set(!get());
            item.IsChecked = get();
            RefreshRepository();
        };
    }

    // As upstream, after the commits: the grid shows them at once and gets the labels when they are read.
    private async Task ShowSuperprojectLabelsAsync(string repositoryPath)
    {
        int read = ++_superprojectReads;
        SuperprojectLabels? labels = await SuperprojectLabels.ReadAsync(repositoryPath, SuperprojectLabels.Filter(
            _preferences.ShowSuperprojectBranches, _preferences.ShowSuperprojectRemoteBranches,
            _preferences.ShowSuperprojectTags));
        if (read == _superprojectReads && RepositoryPath == repositoryPath)
        {
            _commits.SetSuperprojectLabels(labels);
        }
    }
}
