using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormFormatPatch</c> (rules in <see cref="FormatPatchDialog"/>): <c>git format-patch</c> of the commits
///  selected in the grid into the output folder, which is kept (upstream's <c>LastFormatPatchDir</c>); git's output is
///  shown, or upstream's error when it made nothing.
/// </summary>
public partial class FormatPatchWindow : Window
{
    private readonly string _repositoryPath;
    private readonly IAppPreferences _preferences;
    private readonly GitModule _module;
    private readonly CommitListViewModel _commits;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public FormatPatchWindow()
        : this("", AppServices.Preferences)
    {
    }

    public FormatPatchWindow(string repositoryPath, IAppPreferences preferences)
    {
        _repositoryPath = repositoryPath;
        _preferences = preferences;
        _module = GitModules.Open(repositoryPath);
        _commits = new CommitListViewModel(new GitCommitHistory(
            () => new CommitDateStyle(preferences.RelativeDate, preferences.ShowAuthorDate),
            () => preferences.RevisionSortOrder));
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormFormatPatch");
        RevisionGridColumns.ShowAsSettingsSay(this, RevisionGrid, preferences, _commits);
        OutputPath.Text = preferences.LastFormatPatchDir;

        // Upstream's OutputPath_TextChanged: a folder that exists is kept.
        OutputPath.TextChanged += (_, _) =>
        {
            if (Directory.Exists(OutputPath.Text))
            {
                _preferences.LastFormatPatchDir = OutputPath.Text;
            }
        };
        Browse.Click += (_, _) => Run(BrowseAsync);
        FormatPatch.Click += (_, _) => Run(FormatPatchAsync);
        Opened += (_, _) => Run(LoadAsync);
    }

    /// <summary>
    ///  Completes when the commits and the current branch are shown, for tests.
    /// </summary>
    public Task CommitsLoaded => _loaded.Task;

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));

    // Upstream's FormFormatPath_Load: the current branch and the grid.
    private async Task LoadAsync()
    {
        string branch = await Task.Run(() => _module.GetSelectedBranch());
        SelectedBranch.Text = UpstreamTranslation.ByText(FormatPatchDialog.CurrentBranch) + " " + branch;
        await _commits.OpenAsync(_repositoryPath);
        RevisionGrid.ItemsSource = _commits.VisibleRows;
        _loaded.TrySetResult();
    }

    // Upstream's Browse_Click: OsShellUtil.PickFolder.
    private async Task BrowseAsync()
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions());
        if (folders.FirstOrDefault()?.TryGetLocalPath() is { } folder)
        {
            OutputPath.Text = folder;
        }
    }

    // Upstream's FormatPatch_Click.
    private async Task FormatPatchAsync()
    {
        string output = OutputPath.Text ?? "";
        if (output.Length == 0)
        {
            await ShowAsync(FormatPatchDialog.NoOutputPathEntered, ManageWorktreeDialog.Error);
            return;
        }

        // The selection oldest first, as upstream's SortDirection.Descending orders the grid's rows.
        List<(string Hash, string? FirstParent)> selected =
        [
            .. (RevisionGrid.SelectedItems ?? Array.Empty<object>()).OfType<CommitListItem>()
                .OrderByDescending(item => _commits.IndexOfHash(item.Row.Hash) ?? 0)
                .Select(item => (item.Row.Hash, item.Row.ParentHashes is [{ } parent, ..] ? parent : null))
        ];
        if (selected.Count == 0)
        {
            await ShowAsync(FormatPatchDialog.RevisionsNeeded, FormatPatchDialog.RevisionsNeededCaption);
            return;
        }

        IReadOnlyList<FormatPatchRange> ranges = FormatPatchDialog.Ranges(selected);
        string result = await Task.Run(() => string.Concat(ranges.Select(range =>
            _module.FormatPatch(range.From, range.To, output, range.Start))));
        if (string.IsNullOrEmpty(result))
        {
            await ShowAsync(FormatPatchDialog.FailCreatePatch, FormatPatchDialog.RevisionsNeededCaption);
            return;
        }

        await new MessageBoxWindow(result, UpstreamTranslation.ByText(FormatPatchDialog.PatchResultCaption),
            MessageBoxButtons.OK).ShowDialog(this);
        Close();
    }

    private Task ShowAsync(string text, string caption)
        => new MessageBoxWindow(UpstreamTranslation.ByText(text), UpstreamTranslation.ByText(caption), MessageBoxButtons.OK)
            .ShowDialog(this);
}
