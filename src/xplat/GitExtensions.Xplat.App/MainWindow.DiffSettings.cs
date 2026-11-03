using Avalonia.Controls;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream FileStatusList's settings menu over the Diff tab's files, as <c>RevisionDiffControl</c> binds it
///  (<c>canAutoRefresh</c>): "Show untracked files" for the working directory's row (at first as git's
///  <c>status.showuntrackedfiles</c> says), the ignored files' editors, "Refresh artificial commits on form focus" and
///  "Show file differences for all parents".
/// </summary>
public partial class MainWindow
{
    // Git's setting of upstream's initial "Show untracked files", and its value that turns them off.
    private const string ShowUntrackedFilesSetting = "status.showuntrackedfiles";
    private const string NoUntrackedFiles = "no";

    // Git's status letter of an untracked file in the Diff tab's list.
    private const string UntrackedStatus = "?";

    // The repository whose status.showuntrackedfiles set "Show untracked files" (read once per repository, as upstream
    // reads it once per window).
    private string? _showUntrackedReadFor;

    private void WireDiffFileSettings()
    {
        DiffFilesShowUntrackedMenuItem.Click += (_, _) => ShowSelectedCommitFiles();
        DiffFilesEditGitIgnoreMenuItem.Click += (_, _) => Run(() => EditGitIgnoreAsync(localExclude: false));
        DiffFilesEditLocallyIgnoredMenuItem.Click += (_, _) => Run(() => EditGitIgnoreAsync(localExclude: true));
        ((MenuFlyout)DiffFilesSettingsButton.Flyout!).Opening += (_, _) =>
        {
            // Upstream's UpdateToolbar(revisions): untracked files only for the working directory.
            DiffFilesShowUntrackedMenuItem.IsEnabled = SelectedRevisions.Any(row => row.Hash == ArtificialCommits.WorkTreeHash);
            DiffFilesRefreshOnFormFocusMenuItem.IsChecked = _preferences.RefreshCommitDialogOnFocus;
            DiffFilesShowDiffForAllParentsMenuItem.IsChecked = _preferences.ShowDiffForAllParents;
            ToolTip.SetTip(DiffFilesShowDiffForAllParentsMenuItem,
                Ui.UpstreamTranslation.ByText(SettingsWindow.ShowDiffForAllParentsTooltip));
        };
        DiffFilesRefreshOnFormFocusMenuItem.Click += (_, _) =>
        {
            _preferences.RefreshCommitDialogOnFocus = DiffFilesRefreshOnFormFocusMenuItem.IsChecked;
            _preferences.Save();
        };

        // Upstream's ShowDiffForAllParents_Click: the groups are calculated again.
        DiffFilesShowDiffForAllParentsMenuItem.Click += (_, _) =>
        {
            _preferences.ShowDiffForAllParents = DiffFilesShowDiffForAllParentsMenuItem.IsChecked;
            _preferences.Save();
            ShowSelectionDiff();
        };
    }

    // Upstream's BindContextMenu: "Show untracked files" starts as git says, read when a repository opens.
    private async Task ReadShowUntrackedFilesAsync(string path)
    {
        string value = await Task.Run(() => GitModules.Open(path).GetEffectiveSetting(ShowUntrackedFilesSetting));
        DiffFilesShowUntrackedMenuItem.IsChecked = value != NoUntrackedFiles;
        ShowSelectedCommitFiles();
    }

    // The selected row's own files, without the untracked ones while "Show untracked files" is off.
    private IReadOnlyList<CommitFile>? SelectedCommitFiles()
        => _commits.Selected is null
            ? null
            : DiffFilesShowUntrackedMenuItem.IsChecked
                ? _commits.CommitFiles
                : [.. _commits.CommitFiles.Where(file => file.Status != UntrackedStatus)];

    private void ShowSelectedCommitFiles()
    {
        if (_diffGroups is null)
        {
            _diffFiles?.Files = SelectedCommitFiles();
        }
    }
}
