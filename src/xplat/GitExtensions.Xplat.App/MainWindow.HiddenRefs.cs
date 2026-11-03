using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>RepoObjectsTree.RefreshRevisionsLoaded</c>, which <c>FormBrowse</c> calls once the grid has loaded: the
///  left panel's branches, tags and stashes whose commit the grid does not show are hidden (upstream's
///  <c>UpdateVisibility</c>, closed eye and gray text). Rules in <see cref="RepoObjectsTree.ShowHidden"/>.
/// </summary>
public partial class MainWindow
{
    private void ShowHiddenRepoObjects()
    {
        // As upstream's UpdateVisibility, which waits for the grid's loading to complete.
        if (_commits.IsLoading)
        {
            return;
        }

        RepoObjectsTree.ShowHidden(_repoObjects, _commits.Rows, UpstreamTranslation.ByText);
    }
}
