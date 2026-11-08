using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream FileViewer's line patching in the Diff tab's viewer (<see cref="DiffLinePatching"/>): the working directory's
///  and the index's lines are staged, unstaged or reset, a commit's lines applied to the working directory or undone.
/// </summary>
public partial class MainWindow
{
    private void WireDiffLinePatching()
    {
        CommitDiff.LinesHotkey += (_, command) =>
        {
            if (RepositoryPath is { } path && CommitFilesList.SelectedItem is CommitFile file)
            {
                Run(() => DiffLinePatching.RunAsync(this, CommitDiff, command, FileListMenuServices, path, file.Status,
                    () =>
                    {
                        ShowSelectedCommitFileDiff();
                        return Task.CompletedTask;
                    }));
            }
        };

        // Upstream RevisionDiffControl's SetFileLoader(GetNextPatchFile): the viewer's search goes on in the list's next
        // file, around at its ends.
        CommitDiff.NextFile = backward =>
            FileListNodes.SelectNextFile(CommitFilesList, backward, loop: true, item => item is CommitFile);

        // Upstream RevisionDiffControl's FileViewer_TopScrollReached and FileViewer_BottomScrollReached.
        CommitDiff.TopScrollReached += (_, _) =>
        {
            FileListNodes.SelectNeighbourFile(CommitFilesList, next: false, item => item is CommitFile);
            CommitDiff.ScrollToBottom();
        };
        CommitDiff.BottomScrollReached += (_, _) =>
        {
            FileListNodes.SelectNeighbourFile(CommitFilesList, next: true, item => item is CommitFile);
            CommitDiff.ScrollToTop();
        };
        FileContentView.TopScrollReached += (_, _) =>
        {
            SelectNeighbourTreeFile(next: false);
            FileContentView.ScrollToBottom();
        };
        FileContentView.BottomScrollReached += (_, _) =>
        {
            SelectNeighbourTreeFile(next: true);
            FileContentView.ScrollToTop();
        };
    }

    // Upstream's SupportLinePatching for the Diff tab's selected file: none for a folder, a grep match or a combined diff.
    private DiffLineAction SelectedFileLineAction()
    {
        if (RepositoryPath is not { } path || CommitFilesList.SelectedItem is not CommitFile file
            || FileListNodes.SelectedNode(CommitFilesList) is not null)
        {
            return DiffLineAction.None;
        }

        if (GroupFile(file) is { } grouped)
        {
            return grouped.Group.IsCombinedDiff || grouped.File.GrepString is not null
                ? DiffLineAction.None
                : DiffLinePatching.For(path, file.Path, file.Status, grouped.Group.Second);
        }

        if (_commits.Selected is not { } row)
        {
            return DiffLineAction.None;
        }

        ObjectId second = row.Hash == ArtificialCommits.WorkTreeHash ? ObjectId.WorkTreeId
            : row.Hash == ArtificialCommits.IndexHash ? ObjectId.IndexId
            : ObjectId.TryParse(row.Hash, out ObjectId id) ? id
            : default;
        return second.IsZero ? DiffLineAction.None : DiffLinePatching.For(path, file.Path, file.Status, second);
    }
}
