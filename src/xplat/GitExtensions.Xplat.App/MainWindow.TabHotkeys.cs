using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's hotkeys of the Diff tab's file list (<c>RevisionDiffControl</c>, section "BrowseDiff") and of the stash window
///  (<c>FormStash</c>, section "Stash", here on the left panel's stashes), for the commands the new shell has.
/// </summary>
public partial class MainWindow
{
    // Before the window's own hotkeys, as upstream's controls handle their keys first; false when the key is not theirs.
    private bool RunTabHotkey(KeyEventArgs e)
    {
        if (IsWithin(e.Source, CommitFilesList) && Hotkeys.BrowseDiff.Match(e) is { } diffCommand)
        {
            return RunBrowseDiffHotkey(diffCommand);
        }

        // Upstream's File tree tab is a RevisionDiffControl too, with the same hotkeys: the parents' in the grid, the others
        // the file list menu's.
        if (IsWithin(e.Source, FileTreeView) && Hotkeys.BrowseDiff.Match(e) is { } treeCommand)
        {
            return treeCommand is BrowseDiffCommand.GoToFirstParent or BrowseDiffCommand.GoToLastParent
                ? RunBrowseDiffHotkey(treeCommand)
                : FileTreeContextMenu.ExecuteCommand(treeCommand);
        }

        if (IsWithin(e.Source, BranchTreeView) && SelectedRepoObject is { Kind: RepoObjectKind.Stash }
                                               && Hotkeys.Stash.Match(e) is { } stashCommand)
        {
            return RunStashHotkey(stashCommand);
        }

        return false;
    }

    private bool RunBrowseDiffHotkey(BrowseDiffCommand command)
    {
        if (RepositoryPath is not { } path || _commits.Selected is not { } row)
        {
            return false;
        }

        List<string> files = [.. CommitFilesList.SelectedItems?.OfType<CommitFile>().Select(file => file.Path) ?? []];
        string? file = files.FirstOrDefault();
        bool workTree = row.Hash == ArtificialCommits.WorkTreeHash;
        bool index = row.Hash == ArtificialCommits.IndexHash;
        switch (command)
        {
            case BrowseDiffCommand.GoToFirstParent when row.ParentHashes is [{ } first, ..]:
                return SelectGridRow(
                    _commits.VisibleRows.ToList().FindIndex(item => item.Row.Hash == first) is >= 0 and var i
                        ? i
                        : null);
            case BrowseDiffCommand.GoToLastParent when row.ParentHashes is [.., { } last]:
                return SelectGridRow(
                    _commits.VisibleRows.ToList().FindIndex(item => item.Row.Hash == last) is >= 0 and var j
                        ? j
                        : null);
            case BrowseDiffCommand.OpenWithDifftool when file is not null:
                Run(OpenCommitFileInDiffToolAsync);
                return true;
            case BrowseDiffCommand.ShowHistory when file is not null && !workTree && !index:
                NewFileHistoryWindow(path, file, row.Hash, showBlame: false).Show(this);
                return true;
            case BrowseDiffCommand.Blame when file is not null && !workTree && !index:
                BlameDiffFile();
                return true;
            case BrowseDiffCommand.ShowFileTree when file is not null && !workTree && !index:
                Run(() => ShowInFileTreeAsync(file, blame: false));
                return true;
            case BrowseDiffCommand.EditFile when file is not null:
                new EditorWindow(Path.Combine(path, file)).Show(this);
                return true;
            case BrowseDiffCommand.OpenWorkingDirectoryFile when file is not null:
                Run(() => Launcher.LaunchFileInfoAsync(new FileInfo(Path.Combine(path, file))));
                return true;

            // Upstream's stage and unstage act on the working directory's and index's files.
            case BrowseDiffCommand.StageSelectedFile when workTree && files.Count > 0:
                Run(() => _actions.StageAsync(path, files));
                return true;
            case BrowseDiffCommand.UnStageSelectedFile when index && files.Count > 0:
                Run(() => _actions.UnstageAsync(path, files));
                return true;

            // Upstream's ResetSelectedFilesWithConfirmation: to the first revision, when the menu offers it.
            case BrowseDiffCommand.ResetSelectedFiles when files.Count > 0:
                if (CommitFilesContextMenu.State() is not { ResetFile: true, ResetToFirst: not null })
                {
                    return false;
                }

                Run(() => CommitFilesContextMenu.ResetAsync(toFirst: true));
                return true;
            case BrowseDiffCommand.DeleteSelectedFiles when (workTree || index) && files.Count > 0:
                Run(CommitFilesContextMenu.DeleteAsync);
                return true;

            // Upstream's tsmiMove.PerformClick, which does nothing while the item is hidden.
            case BrowseDiffCommand.RenameMove when CommitFilesContextMenu.State() is { RenameMove: true }:
                Run(CommitFilesContextMenu.MoveAsync);
                return true;
            case BrowseDiffCommand.FindFile:
                Run(CommitFilesContextMenu.FindFileAsync);
                return true;
            case BrowseDiffCommand.FindInCommitFilesUsingGitGrepDiffTab or BrowseDiffCommand.OpenAsTempFile
                or BrowseDiffCommand.OpenAsTempFileWith or BrowseDiffCommand.OpenWithDifftoolFirstToLocal
                or BrowseDiffCommand.OpenWithDifftoolSelectedToLocal or BrowseDiffCommand.FilterFileInGrid
                or BrowseDiffCommand.OpenWorkingDirectoryFileWith or BrowseDiffCommand.AddFileToGitIgnore:
                return CommitFilesContextMenu.ExecuteCommand(command);
            default:
                return false;
        }
    }

    // The stash below or above the selected one in the left panel's Stashes tree.
    private bool SelectNeighbourStash(bool next)
    {
        if (SelectedRepoObject is not { } stash
            || _repoObjects.FirstOrDefault(root => root.Kind == RepoObjectKind.StashesRoot) is not { } stashes)
        {
            return false;
        }

        int index = stashes.Children.IndexOf(stash) + (next ? 1 : -1);
        if (index < 0 || index >= stashes.Children.Count)
        {
            return false;
        }

        BranchTreeView.SelectedItem = stashes.Children[index];
        return true;
    }

    private bool RunStashHotkey(StashCommand command)
    {
        switch (command)
        {
            case StashCommand.NextStash or StashCommand.PreviousStash:
                return SelectNeighbourStash(command == StashCommand.NextStash);
            case StashCommand.Refresh:
                RefreshRepository();
                return true;
            default:
                return false;
        }
    }

    // Upstream's "show in file tree": the file selected in the File tree tab, its folders open.
    private void SelectInFileTree(string file)
    {
        if (FileTreeView.ItemsSource is not IEnumerable<FileTreeNode> roots)
        {
            return;
        }

        List<FileTreeNode> path = [];
        if (Find(roots, path))
        {
            foreach (FileTreeNode folder in path.Take(path.Count - 1))
            {
                folder.IsExpanded = true;
            }

            FileTreeView.SelectedItem = path[^1];
        }

        return;

        bool Find(IEnumerable<FileTreeNode> nodes, List<FileTreeNode> trail)
        {
            foreach (FileTreeNode node in nodes)
            {
                trail.Add(node);
                if (node.Path == file || (node.IsFolder && file.StartsWith(node.Path + "/", StringComparison.Ordinal)
                                                        && Find(node.Children, trail)))
                {
                    return true;
                }

                trail.RemoveAt(trail.Count - 1);
            }

            return false;
        }
    }
}
