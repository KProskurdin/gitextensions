using System.ComponentModel;
using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormLog</c>, bound to <see cref="LogViewModel"/>: the revision grid, the selected commits' files in
///  upstream's diff groups, and the selected file's diff. The browse window opens it for a double-click on its grid with
///  nothing selected.
/// </summary>
public partial class LogWindow : Window
{
    private readonly string _repositoryPath;
    private readonly RevisionFileList<CommitFile> _files;

    public LogWindow()
        : this("", AppServices.Preferences)
    {
    }

    /// <param name="repositoryPath">The repository whose commits are listed.</param>
    /// <param name="preferences">The grid's columns, dates and artificial commits, as the browse window shows them.</param>
    public LogWindow(string repositoryPath, IAppPreferences preferences)
    {
        _repositoryPath = repositoryPath;
        ViewModel = new LogViewModel(new WindowDialogHost(this), repositoryPath, new GitCommitHistory(
            () => new CommitDateStyle(preferences.RelativeDate, preferences.ShowAuthorDate),
            () => preferences.RevisionSortOrder,
            showArtificialCommits: () => preferences.RevisionGraphShowArtificialCommits,
            gridView: () => preferences.GridViewOptions, showSessionRefs: () => preferences.ShowSessionRefs),
            StoredRevisionFilter.Initial(preferences));
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, LogViewModel.Category);

        // View concerns: the grid's columns, the file list (its filter, grouping and group nodes) and the diff.
        RevisionGridColumns.ShowAsSettingsSay(this, RevisionGrid, preferences, ViewModel.Commits);
        FileListToolbar.Attach(DiffFilesByPathButton, preferences);
        _files = new RevisionFileList<CommitFile>(DiffFiles, DiffFilesFilterBox, DiffFilesToolbar, DiffFilesNoFilesText,
            preferences, file => file.Path, file => file.Status, file => file.Group,
            file => file.DiffFile?.DiffStatus ?? DiffBranchStatus.Unknown, oldPath: file => file.DiffFile?.OldPath);
        DiffFilesFilterBox.WorkingDirectory = () => _repositoryPath;
        ViewModel.PropertyChanged += OnViewModelChanged;
        RevisionGrid.SelectionChanged += (_, e) => Run(() => ViewModel.SelectionChangedAsync(
            [.. RevisionGrid.SelectedItems?.OfType<CommitListItem>().Select(item => item.Row) ?? []],
            [.. e.AddedItems.OfType<CommitListItem>().Select(item => item.Row)]));

        // Upstream's DiffFilesSelectedIndexChanged, and its FileViewer_TopScrollReached and FileViewer_BottomScrollReached.
        DiffFiles.SelectionChanged += (_, _) => Run(ShowSelectedFileDiffAsync);
        diffViewer.TopScrollReached += (_, _) =>
        {
            FileListNodes.SelectNeighbourFile(DiffFiles, next: false, item => item is CommitFile);
            diffViewer.ScrollToBottom();
        };
        diffViewer.BottomScrollReached += (_, _) =>
        {
            FileListNodes.SelectNeighbourFile(DiffFiles, next: true, item => item is CommitFile);
            diffViewer.ScrollToTop();
        };
        Opened += (_, _) => Run(ViewModel.LoadAsync);
    }

    public LogViewModel ViewModel { get; }

    /// <summary>
    ///  The files the list shows, for tests.
    /// </summary>
    public IReadOnlyList<CommitFile> Files => _files.Files ?? [];

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LogViewModel.Files))
        {
            _files.Files = ViewModel.Files;
            if (ViewModel.Files.Count == 0)
            {
                diffViewer.Clear();
            }
        }
    }

    // Upstream's ViewSelectedFileDiff: the file's diff between its group's revisions; a range diff group shows what git
    // range-diff says.
    private async Task ShowSelectedFileDiffAsync()
    {
        if (DiffFiles.SelectedItem is not CommitFile { Group: { } group, DiffFile: { } file })
        {
            return;
        }

        if (group.IsRangeDiff)
        {
            diffViewer.ShowText(file.Path, $"git range-diff {group.First}...{group.Second}");
            IReadOnlyList<DiffLine> lines = await Task.Run(() =>
                DiffGroups.RangeDiffAsync(GitModules.Open(_repositoryPath), group, diffViewer.Options, ""));
            diffViewer.ShowDiffLines(file.Path, lines);
            return;
        }

        await diffViewer.ShowDiffAsync(_repositoryPath, file.Path, file.Path,
            options => DiffGroups.DiffAsync(GitModules.Open(_repositoryPath), group, file, options),
            FileRevision.Of(group.Second));
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}
