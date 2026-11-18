using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormFileHistory</c> in the new shell, bound to <see cref="FileHistoryViewModel"/> (rules in
///  <see cref="FileHistoryDialog"/>): the revision grid of the commits that changed a file (following its renames as
///  upstream's settings say), and for the selected one its Commit, Diff, View and Blame tabs; upstream's toolstrip and grid
///  menu. The grid's navigation, quick search, View menu and build column stay here, as the browse window's grid.
/// </summary>
public partial class FileHistoryWindow : Window
{
    // Upstream's MessageBoxes.RevisionFilteredInGrid texts.
    public const string RevisionFiltered = FileHistoryViewModel.RevisionFiltered;
    public const string RevisionFilteredCaption = FileHistoryViewModel.RevisionFilteredCaption;

    private readonly string _repositoryPath;
    private readonly IAppPreferences _preferences;
    private readonly GitModule _module;

    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public FileHistoryWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="fileName">The file in the repository, relative to its working directory.</param>
    /// <param name="revision">The commit to select at first; null for the first one.</param>
    /// <param name="showBlame">Open on the Blame tab instead of Diff.</param>
    /// <param name="cherryPick">Upstream's "Cherry pick commit": cherry-picks a commit (repository, hash).</param>
    /// <param name="revert">Upstream's "Revert commit": reverts a commit (repository, hash).</param>
    /// <param name="repositoryHost">The repository host plugin, whose blame items the Blame tab's menu shows.</param>
    /// <param name="filterByRevision">Upstream's <c>filterByRevision</c>: the grid filtered by the commit's hash.</param>
    public FileHistoryWindow(string repositoryPath, IAppPreferences preferences,
        string fileName, string? revision, bool showBlame, Func<string, string, Task> cherryPick,
        Func<string, string, Task> revert, Func<IRepositoryHostPlugin?>? repositoryHost = null,
        bool filterByRevision = false)
    {
        _repositoryPath = repositoryPath;
        _preferences = preferences;
        _module = GitModules.Open(repositoryPath);
        ViewModel = new FileHistoryViewModel(new WindowDialogHost(this), _module, repositoryPath, preferences, fileName,
            revision, showBlame, new FileHistoryViewModelServices
            {
                CherryPick = cherryPick,
                Revert = revert,
                ShowContent = ShowContentAsync,
                ChangeBlameOptions = options => Blame.ChangeOptionsAsync(options),
                PickSaveFile = PickSaveFileAsync,
                GridReloaded = () => Run(LaunchBuildServerAsync),
                CopyText = CopyTextAsync,
                DiffMergeTools = AppServices.DiffMergeTools,
                RunGridCommand = command => RunGridCommand(command),
                ShowCommandLog = () => CommandLogWindow.ShowFor(this),
                OpenReportedBuild = () => OpenBuildLink(_reportedBuild?.Url),
            });
        InitializeComponent();

        // After the controls exist, so the tab control takes the view model's tab once its tabs are there.
        DataContext = ViewModel;
        HistoryContextMenu.DataContext = ViewModel;

        // The toolstrip's flyouts get their data context only when they open; their check items bind to it.
        foreach (Control item in new[] { LoadButton.Flyout, FullHistoryButton.Flyout, BlameOptionsButton.Flyout }
                     .OfType<MenuFlyout>().SelectMany(flyout => flyout.Items.OfType<Control>()))
        {
            item.DataContext = ViewModel;
        }

        WindowPlacementTracker.Attach(this, "Xplat.FileHistoryWindow");
        ShowGridColumns();
        Blame.RepositoryHost = repositoryHost;
        WireFilterToolStrip(revision, filterByRevision);

        ViewModel.Commits.PropertyChanged += OnCommitsChanged;
        ViewModel.SelectRowRequested += (_, item) =>
        {
            HistoryList.SelectedItem = item;
            if (item is not null)
            {
                HistoryList.ScrollIntoView(item);
            }
        };

        // Upstream's grid selects several commits (Ctrl, Shift); the view model follows the latest selected.
        HistoryList.SelectionChanged += (_, e) => ViewModel.UpdateSelection(
            [.. HistoryList.SelectedItems?.OfType<CommitListItem>().Select(item => item.Row) ?? []],
            [.. e.AddedItems.OfType<CommitListItem>().Select(item => item.Row)]);

        // Upstream's FileChangesDoubleClick: the commit in upstream's FormCommitDiff.
        HistoryList.DoubleTapped += (_, _) =>
        {
            if (ViewModel.Commits.Selected is { } row && !ArtificialCommits.IsArtificial(row.Hash))
            {
                new CommitDiffWindow(_repositoryPath, row.Hash).Show(this);
            }
        };
        Blame.GridRevision = hash => ViewModel.Commits.VisibleRows.FirstOrDefault(item => item.Row.Hash == hash)?.Row;

        // Upstream's SelectFileInRevision: the grid's row knows the file's name in that commit.
        Blame.CommitRequested += (_, request) => Run(() => ViewModel.GoToCommitAsync(request.Hash));
        Blame.GridChildren = ViewModel.Commits.ChildrenOf;
        Blame.Info.RevisionLinkClicked += (_, linked) => Run(() => ViewModel.GoToLinkedRevisionAsync(linked));
        Blame.OptionsChanged += (_, _) => ViewModel.RefreshOptions();
        Blame.ErrorOccurred += (_, message) => ShowError(message);

        HistoryContextMenu.Opening += (_, _) =>
        {
            ViewModel.UpdateMenu();
            CopyMenus.Fill(CopyToClipboardMenuItem, ViewModel.Selection.Revisions, keep: null, ViewModel.CopyTextCommand);
        };
        WireGridMenus();
        WireBuildServer();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
        Opened += (_, _) =>
        {
            _ = ViewModel.LoadAsync();
            Run(ViewModel.DiffTools.LoadAsync);
        };
    }

    public FileHistoryViewModel ViewModel { get; }

    /// <summary>
    ///  The selected commits, the latest selected first, for tests.
    /// </summary>
    public IReadOnlyList<CommitRow> SelectedRevisions => ViewModel.Selection.Revisions;

    /// <summary>
    ///  The rules, for tests.
    /// </summary>
    public FileHistoryDialog Dialog => ViewModel.Dialog;

    /// <summary>
    ///  The grid's rows, for tests.
    /// </summary>
    public CommitListViewModel Commits => ViewModel.Commits;

    /// <summary>
    ///  Completes when the history is read (or the window opened without reading it), for tests.
    /// </summary>
    public Task HistoryLoaded => ViewModel.Loaded;

    /// <summary>
    ///  The last update of the tabs for the selected commit, for tests.
    /// </summary>
    public Task Shown => ViewModel.Shown;

    private void OnCommitsChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CommitListViewModel.Filter):
                ToolStripFilters.ShowFilterState();
                break;
            case nameof(CommitListViewModel.ErrorMessage) when ViewModel.Commits.ErrorMessage is { } error:
                ShowError(error);
                break;
        }
    }

    // The selected tab's content in its control, as the view model chose it.
    private async Task ShowContentAsync(FileHistoryContent content)
    {
        switch (content.Tab)
        {
            case FileHistoryTab.Blame:
                await Blame.LoadAsync(_repositoryPath, content.Row.Hash, content.FileName);
                break;
            case FileHistoryTab.View:
                await View.ShowFileAsync(_repositoryPath, content.Row.Hash, staged: false, content.FileName);
                break;
            case FileHistoryTab.Diff when content.CompareWith is { } first:
                RevisionDiffFile file = new("M", content.FileName, OldPath: null, IsTracked: true);
                await Diff.ShowDiffAsync(_repositoryPath, content.FileName, content.FileName, options =>
                        RevisionDiff.DiffAsync(_module, ObjectId.Parse(first.Hash), ObjectId.Parse(content.Row.Hash),
                            file,
                            options),
                    FileRevision.Of(content.Row.Hash));
                break;
            case FileHistoryTab.Diff:
                await Diff.ShowAsync(_repositoryPath, content.Hash, content.FileName, content.Staged, content.FileName);
                break;
            case FileHistoryTab.Commit when content.Hash is { } hash:
                await CommitDiffPanel.SetRevisionAsync(_repositoryPath, hash, content.FileName);
                break;
        }
    }

    private async Task<string?> PickSaveFileAsync(string suggestedName, string folderPath)
    {
        IStorageFolder? folder = await StorageProvider.TryGetFolderFromPathAsync(folderPath);
        IStorageFile? target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            DefaultExtension = Path.GetExtension(suggestedName),
            SuggestedStartLocation = folder,
        });
        return target?.TryGetLocalPath();
    }

    private async Task CopyTextAsync(string? text)
    {
        if (text is not null && GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    // The grid as the browse window's View menu and upstream's appearance settings say.
    private void ShowGridColumns()
    {
        RevisionGridColumns.ShowAsSettingsSay(this, HistoryList, _preferences, ViewModel.Commits);
        ShowBuildStatusColumn();
    }

    private void ShowError(string message) => _ = new ErrorWindow(message).ShowDialog(this);

    private void Run(Func<Task> action) => UiActions.Run(action, ex => ShowError(ex.Message));
}
