using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormFormatPatch</c>, bound to <see cref="FormatPatchViewModel"/> (rules in <see cref="FormatPatchDialog"/>):
///  <c>git format-patch</c> of the commits selected in the grid into the output folder.
/// </summary>
public partial class FormatPatchWindow : Window
{
    public FormatPatchWindow()
        : this("", AppServices.Preferences)
    {
    }

    public FormatPatchWindow(string repositoryPath, IAppPreferences preferences)
    {
        ViewModel = new FormatPatchViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath), preferences,
            new GitCommitHistory(
                () => new CommitDateStyle(preferences.RelativeDate, preferences.ShowAuthorDate),
                () => preferences.RevisionSortOrder, gridView: () => preferences.GridViewOptions,
                showSessionRefs: () => preferences.ShowSessionRefs))
        {
            PickFolder = PickFolderAsync,
        };
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, FormatPatchViewModel.Category);

        // View concerns: the grid's columns as the settings say, and its selection, which a list cannot bind.
        RevisionGridColumns.ShowAsSettingsSay(this, RevisionGrid, preferences, ViewModel.Commits);
        RevisionGrid.SelectionChanged += (_, _) => ViewModel.SelectedRows =
        [
            .. (RevisionGrid.SelectedItems ?? Array.Empty<object>()).OfType<CommitListItem>().Select(item => item.Row)
        ];
        Opened += (_, _) => _ = ViewModel.LoadAsync(repositoryPath);
    }

    public FormatPatchViewModel ViewModel { get; }

    /// <summary>
    ///  Completes when the commits and the current branch are shown, for tests.
    /// </summary>
    public Task CommitsLoaded => ViewModel.Loaded;

    // Upstream's OsShellUtil.PickFolder.
    private async Task<string?> PickFolderAsync()
    {
        IReadOnlyList<IStorageFolder> folders =
            await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions());
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }
}
