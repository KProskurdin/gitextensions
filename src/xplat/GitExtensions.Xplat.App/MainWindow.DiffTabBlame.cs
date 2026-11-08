using GitExtensions.Xplat.Core.CommitHistory;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's "Show blame in diff viewer" (<c>UseDiffViewerForBlame</c>): Blame in the Diff tab's list shows the file's blame in
///  place of its diff, until Blame is clicked again or another file is selected.
/// </summary>
public partial class MainWindow
{
    // Upstream's _selectedBlameItem: the file that shows as blame.
    private string? _diffTabBlameFile;

    private void WireDiffTabBlame()
    {
        DiffTabBlame.HideCommitInfo();
        DiffTabBlame.GridRevision = hash => _commits.VisibleRows.FirstOrDefault(item => item.Row.Hash == hash)?.Row;
        DiffTabBlame.CommitRequested += (_, request) => Run(() => GoToRevisionAsync(request.Hash, showNoRevisionMessage: true));
        DiffTabBlame.ErrorOccurred += (_, message) => ShowError(message);
        DiffTabBlame.RepositoryHost = RelevantRepositoryHost;
    }

    // Upstream RevisionDiffControl's BlameFile: the line stays the same either way.
    private void ToggleDiffTabBlame(string file)
    {
        int? line = DiffTabBlame.IsVisible ? DiffTabBlame.CurrentFileLine : CommitDiff.CurrentFileLine;
        _diffTabBlameFile = _diffTabBlameFile is null ? file : null;
        if (_diffTabBlameFile is null)
        {
            CommitDiff.GoToLineWhenShown(line);
        }
        else
        {
            DiffTabBlame.GoToLineWhenBlamed(line);
        }

        ShowSelectedCommitFileDiff();
    }

    // Upstream's ShowSelectedFile: the blame instead of the diff. False when the diff is to be shown.
    private bool ShowDiffTabBlame()
    {
        if (_diffTabBlameFile is { } blamed && FileListNodes.SelectedNode(CommitFilesList) is null
            && RepositoryPath is { } path && _commits.Selected is { } row
            && CommitFilesList.SelectedItem is CommitFile file)
        {
            if (file.Path == blamed)
            {
                // An artificial revision's blame is the checked out commit's, as upstream.
                string hash = GroupFile(file) is { } grouped ? grouped.Group.Second.ToString() : row.Hash;
                CommitDiff.IsVisible = false;
                DiffTabBlame.IsVisible = true;
                Run(() => DiffTabBlame.LoadAsync(path, ArtificialCommits.IsArtificial(hash) ? "HEAD" : hash, file.Path));
                return true;
            }

            // Upstream switches to the diff when the selection changes.
            _diffTabBlameFile = null;
        }
        else if (_diffTabBlameFile is not null && FileListNodes.SelectedNode(CommitFilesList) is not null)
        {
            _diffTabBlameFile = null;
        }

        DiffTabBlame.IsVisible = false;
        CommitDiff.IsVisible = true;
        return false;
    }
}
