using GitExtensions.Xplat.Core.CommitHistory;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's grid in a submodule (<c>RevisionGridControl.GetSuperprojectCheckoutAsync</c>, drawn by
///  <c>MessageColumnProvider</c>): the superproject's checkout, conflict and refs as dashed labels, with the View menu's
///  "Show superproject ..." choices (the View menu's view model's, which read the grid again).
/// </summary>
public partial class MainWindow
{
    // Counts the reads, so that a slower read for an earlier refresh does not replace a later one.
    private int _superprojectReads;

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
