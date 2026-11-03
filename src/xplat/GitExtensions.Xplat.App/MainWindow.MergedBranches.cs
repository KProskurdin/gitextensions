using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>RepoObjectsTree.SelectionChanged</c>, which <c>FormBrowse</c> calls when the grid's selection changes:
///  the left panel's local and remote branches that the selected commit contains get the merged image and tooltip. Rules
///  in <see cref="RepoObjectsTree"/> (<c>KeepsMerged</c>, <c>MergedBranchesCommit</c>, <c>ShowMerged</c>).
/// </summary>
public partial class MainWindow
{
    // The last answer, put back on the nodes each time the tree is filled again.
    private IReadOnlyList<string> _mergedBranches = [];
    private CommitRow? _mergedBranchesOf;
    private string? _mergedBranchesRepository;

    // Upstream's _selectionCancellationTokenSequence: a newer selection cancels the read for the older one.
    private CancellationTokenSource? _mergedBranchesCancellation;

    private void ShowMergedBranchesOfSelection()
    {
        IReadOnlyList<CommitRow> selected = SelectedRevisions;
        if (RepoObjectsTree.KeepsMerged(selected, SelectedRepoObject))
        {
            return;
        }

        // Not disposed: the read it was handed to may still look at its token.
        _mergedBranchesCancellation?.Cancel();
        CancellationTokenSource cancellation = new();
        _mergedBranchesCancellation = cancellation;
        string? commit = RepoObjectsTree.MergedBranchesCommit(selected);
        CommitRow? row = selected.Count > 0 ? selected[0] : null;
        string? path = RepositoryPath;
        Run(async () =>
        {
            IReadOnlyList<string> merged = commit is null || path is null
                ? []
                : await GitRepositoryService.GetMergedBranchesAsync(path, commit, cancellation.Token);
            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            _mergedBranches = merged;
            _mergedBranchesOf = row;
            _mergedBranchesRepository = path;
            ShowMergedBranches();
        });
    }

    // Another repository's answer is not put on this one's branches.
    private void ShowMergedBranches()
    {
        bool same = string.Equals(_mergedBranchesRepository, RepositoryPath, StringComparison.Ordinal);
        RepoObjectsTree.ShowMerged(_repoObjects, same ? _mergedBranches : [], same ? _mergedBranchesOf : null,
            UpstreamTranslation.ByText);
    }
}
