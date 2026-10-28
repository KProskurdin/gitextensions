using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's Navigate menu commands that ask git for the commit: Go to commit... (<c>GotoCommitExecute</c> with
///  <c>FormGoToCommit</c>) and Go to common ancestor (<c>GoToMergeBase</c>).
/// </summary>
public partial class MainWindow
{
    // Upstream's GotoCommitExecute: the commit the window names, selected in the grid, or upstream's messages.
    private async Task GoToCommitAsync()
    {
        if (RepositoryPath is not { } path
            || await new GoToCommitWindow(path).ShowDialog<ObjectId?>(this) is not { } commit)
        {
            return;
        }

        if (commit.IsZero)
        {
            ShowError(UpstreamTranslation.ByText(GoToCommit.NoRevisionFound));
            return;
        }

        Navigate(commit.ToString());
    }

    // Upstream's GoToMergeBase: the common ancestor of the selected commit and HEAD (the grid selects one commit).
    private async Task GoToMergeBaseAsync()
    {
        if (RepositoryPath is not { } path || _commits.Selected is not { } row)
        {
            return;
        }

        MergeBaseResult result = await Task.Run(() => GoToCommit.MergeBase(GitModules.Open(path), [row.Hash]));
        if (result.NoCommonAncestor)
        {
            ShowError(UpstreamTranslation.ByText(GoToCommit.NoMergeBaseCommit));
        }
        else if (result.Hash is { } hash)
        {
            Navigate(hash);
        }
    }
}
