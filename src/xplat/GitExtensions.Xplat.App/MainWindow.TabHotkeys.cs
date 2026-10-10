using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's hotkeys of the Diff tab's file list (<c>RevisionDiffControl</c>, section "BrowseDiff") and of the stash window
///  (<c>FormStash</c>, section "Stash", here the left panel's stash list), for the commands the new shell has.
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

        if (IsWithin(e.Source, StashList) && Hotkeys.Stash.Match(e) is { } stashCommand)
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
                new FileBrowserWindow(path, row.Hash, RelevantRepositoryHost, file).Show(this);
                return true;
            case BrowseDiffCommand.Blame when file is not null && !workTree && !index:
                new BlameWindow(path, row.Hash, file, RelevantRepositoryHost).Show(this);
                return true;
            case BrowseDiffCommand.ShowFileTree when file is not null && !workTree && !index:
                DetailTabs.SelectedItem = FileTreeTab;
                SelectInFileTree(file);
                return true;
            case BrowseDiffCommand.EditFile when file is not null:
                new EditorWindow(Path.Combine(path, file)).Show(this);
                return true;
            case BrowseDiffCommand.OpenWorkingDirectoryFile when file is not null:
                Run(() => Launcher.LaunchFileInfoAsync(new FileInfo(Path.Combine(path, file))));
                return true;

            // Upstream's stage, unstage, reset and delete act on the working directory's and index's files.
            case BrowseDiffCommand.StageSelectedFile when workTree && files.Count > 0:
                Run(() => _actions.StageAsync(path, files));
                return true;
            case BrowseDiffCommand.UnStageSelectedFile when index && files.Count > 0:
                Run(() => _actions.UnstageAsync(path, files));
                return true;
            case BrowseDiffCommand.ResetSelectedFiles when workTree && files.Count > 0:
                Run(async () =>
                {
                    if (await new ConfirmWindow(
                                $"Discard the changes to {files.Count} file(s)? This cannot be undone.", "Discard")
                            .ShowDialog<bool>(this))
                    {
                        await _actions.DiscardChangesAsync(path, files);
                    }
                });
                return true;
            case BrowseDiffCommand.DeleteSelectedFiles when workTree && files.Count > 0:
                Run(async () =>
                {
                    if (await new ConfirmWindow($"Delete {files.Count} untracked file(s)? This cannot be undone.",
                                "Delete")
                            .ShowDialog<bool>(this))
                    {
                        await _actions.DeleteUntrackedAsync(path, files);
                    }
                });
                return true;
            default:
                return false;
        }
    }

    private bool RunStashHotkey(StashCommand command)
    {
        switch (command)
        {
            case StashCommand.NextStash when StashList.SelectedIndex + 1 < StashList.ItemCount:
                StashList.SelectedIndex++;
                return true;
            case StashCommand.PreviousStash when StashList.SelectedIndex > 0:
                StashList.SelectedIndex--;
                return true;
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
