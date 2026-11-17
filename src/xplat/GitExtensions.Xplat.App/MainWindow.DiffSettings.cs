using Avalonia.Controls;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream FileStatusList's settings menu over the Diff tab's files, as <c>RevisionDiffControl</c> binds it
///  (<c>canAutoRefresh</c>): "Show skip-worktree files" and "Show untracked files" for the working directory's row (the
///  latter at first as git's <c>status.showuntrackedfiles</c> says), the ignored files' editors, "Refresh artificial commits on form focus" and
///  "Show file differences for all parents".
/// </summary>
public partial class MainWindow
{
    // Git's status letter of an untracked file in the Diff tab's list.
    private const string UntrackedStatus = "?";

    // The running git range-diff of the Diff tab, cancelled when another one starts.
    private CancellationTokenSource? _rangeDiffCancellation;

    // The repository whose status.showuntrackedfiles set "Show untracked files" (read once per repository, as upstream
    // reads it once per window).
    private string? _showUntrackedReadFor;

    private DiffFilesSettingsViewModel? _diffFilesSettings;

    /// <summary>
    ///  The state of the settings menu over the Diff tab's files.
    /// </summary>
    public DiffFilesSettingsViewModel DiffFilesSettings
        => _diffFilesSettings ??= new DiffFilesSettingsViewModel(_preferences, new DiffFilesSettingsServices
        {
            RefreshFiles = RefreshDiffFilesFromSettings,
            ShowFiles = ShowSelectedCommitFiles,
            RecalculateGroups = ShowSelectionDiff,
            EditIgnoredFiles = EditGitIgnoreAsync,
            ShowError = ex => ShowError(ex.Message),
        });

    // The menu's checks are the view model's (MVVM.md); its items get their data context here, as a flyout's items do not
    // inherit it. The ignored files' editors are its commands too, which ask the window for the editor.
    private void WireDiffFileSettings()
    {
        MenuFlyout menu = (MenuFlyout)DiffFilesSettingsButton.Flyout!;
        FileListToolbar.Attach(DiffFilesSettingsButton, _preferences, menu);
        foreach (Control item in menu.Items.OfType<Control>())
        {
            item.DataContext = DiffFilesSettings;
        }

        ToolTip.SetTip(DiffFilesShowDiffForAllParentsMenuItem,
            Ui.UpstreamTranslation.ByText(SettingsWindow.ShowDiffForAllParentsTooltip));
        menu.Opening += (_, _) =>
            DiffFilesSettings.Refresh(SelectedRevisions.Any(row => row.Hash == ArtificialCommits.WorkTreeHash));
    }

    // Upstream's BindContextMenu: "Show untracked files" starts as git says, read when a repository opens.
    private Task ReadShowUntrackedFilesAsync(string path) => DiffFilesSettings.ReadShowUntrackedAsync(path);

    // The selected row's own files, without the untracked ones while "Show untracked files" is off.
    private IReadOnlyList<CommitFile>? SelectedCommitFiles()
        => _commits.Selected is null
            ? null
            : DiffFilesSettings.ShowUntracked
                ? _commits.CommitFiles
                : [.. _commits.CommitFiles.Where(file => file.Status != UntrackedStatus)];

    // Upstream's RequestRefresh for the two show items: skip-worktree files come only from the calculator's groups.
    private void RefreshDiffFilesFromSettings()
    {
        ShowSelectionDiff();
        ShowSelectedCommitFiles();
    }

    private void ShowSelectedCommitFiles()
    {
        if (_diffGroups is null)
        {
            _diffFiles?.Files = SelectedCommitFiles();
        }
    }
}
