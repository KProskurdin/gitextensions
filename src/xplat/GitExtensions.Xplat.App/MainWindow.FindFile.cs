using Avalonia.Controls;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream FormBrowse's <c>FindFileInSelectedCommit</c> (hotkey Ctrl+Shift+F): "Find file" over the selected commit's
///  files in the File tree tab.
/// </summary>
public partial class MainWindow
{
    // The File tree tab's latest read of a commit's files.
    private Task _fileTreeLoad = Task.CompletedTask;

    /// <summary>
    ///  Upstream's <c>FindFileInSelectedCommit</c>: with several commits or an artificial one selected, the first selected
    ///  (HEAD for an artificial one) becomes the only one; then the File tree tab shows, with the split view on, and the
    ///  file taken in the search window is selected in it.
    /// </summary>
    internal async Task FindFileInSelectedCommitAsync()
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        IReadOnlyList<CommitRow> selected = SelectedRevisions;
        if (selected.Count > 1 || (selected.Count == 1 && ArtificialCommits.IsArtificial(selected[0].Hash)))
        {
            await GoToRevisionAsync(ArtificialCommits.IsArtificial(selected[0].Hash) ? "HEAD" : selected[0].Hash);
        }

        DetailTabs.SelectedItem = FileTreeTab;
        _preferences.ShowSplitViewLayout = true;
        ShowSplitViewLayout();

        string hash = _commits.Selected?.Hash ?? "HEAD";
        IReadOnlyList<(string Path, string? OldPath)> files =
            [.. (await _history.LoadTreeAsync(path, hash)).Select(file => (file, (string?)null))];
        if (await new SearchWindow(text => FindFile.Matches(files, text, path)).ShowAsync(this) is { } file)
        {
            await _fileTreeLoad;
            SelectInFileTree(file);
        }
    }

    // The screenshot aid's search window: HEAD's files, searched for "Form" as if typed.
    private async Task<Window> NewScreenshotSearchWindowAsync(string path)
    {
        IReadOnlyList<(string Path, string? OldPath)> files =
            [.. (await _history.LoadTreeAsync(path, "HEAD")).Select(file => (file, (string?)null))];
        SearchWindow search = new(text => FindFile.Matches(files, text, path));
        search.txtSearchBox.Text = "Form";
        return search;
    }
}
