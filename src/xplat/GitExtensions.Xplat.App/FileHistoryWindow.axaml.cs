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
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormFileHistory</c> in the new shell (rules in <see cref="FileHistoryDialog"/>): the revision grid of the
///  commits that changed a file (following its renames as upstream's settings say), and for the selected one its Commit,
///  Diff, View and Blame tabs; upstream's toolstrip and grid menu.
/// </summary>
public partial class FileHistoryWindow : Window
{
    // Upstream's MessageBoxes.RevisionFilteredInGrid texts.
    public const string RevisionFiltered = "Revision \"{0}\" is not visible in the revision grid. Remove the revision filter.";
    public const string RevisionFilteredCaption = "Cannot find revision";

    private readonly string _repositoryPath;
    private readonly IAppPreferences _preferences;
    private readonly string? _revision;
    private readonly Func<string, string, Task> _cherryPick;
    private readonly Func<string, string, Task> _revert;
    private readonly FileHistoryDialog _dialog;
    private readonly GitModule _module;
    private readonly CommitListViewModel _commits;
    private readonly GridSelection _selection = new();
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task _shown = Task.CompletedTask;
    private bool _selectedOnce;
    private string? _selectedHash;

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
        _revision = revision;
        _cherryPick = cherryPick;
        _revert = revert;
        _module = GitModules.Open(repositoryPath);
        _dialog = new FileHistoryDialog(fileName,
            isSubmodule: GitModule.IsValidGitWorkingDir(Path.Combine(repositoryPath, fileName.ToNativePath())));
        _commits = new CommitListViewModel(new GitCommitHistory(
            () => new CommitDateStyle(preferences.RelativeDate, preferences.ShowAuthorDate),
            () => preferences.RevisionSortOrder, fileHistory: () => preferences.FileHistory,
            gridView: () => preferences.GridViewOptions));
        InitializeComponent();
        WindowPlacementTracker.Attach(this, "Xplat.FileHistoryWindow");
        ShowGridColumns();
        Title = _dialog.Title(PathUtil.GetDisplayPath(repositoryPath));
        Blame.RepositoryHost = repositoryHost;
        if (!_dialog.HasBlame)
        {
            BlameTab.IsVisible = false;
            BlameOptionsButton.IsVisible = false;
        }

        SaveAsMenuItem.IsVisible = !_dialog.IsSubmodule;
        Tabs.SelectedItem = TabFor(_dialog.InitialTab(showBlame));
        ShowOptionChecks();
        WireFilterToolStrip(revision, filterByRevision);

        _commits.PropertyChanged += OnCommitsChanged;

        // Upstream's grid selects several commits (Ctrl, Shift); the latest selected is the one the tabs show, and the Diff
        // tab compares the earliest selected with it.
        HistoryList.SelectionChanged += (_, e) =>
        {
            _selection.Update([.. HistoryList.SelectedItems?.OfType<CommitListItem>().Select(item => item.Row) ?? []],
                [.. e.AddedItems.OfType<CommitListItem>().Select(item => item.Row)]);
            if (_selection.Latest is not { } latest)
            {
                return;
            }

            if (!ReferenceEquals(latest, _commits.Selected))
            {
                Run(() => SelectAsync(latest));
            }
            else
            {
                Run(() => _shown = ShowSelectedAsync());
            }
        };
        Tabs.SelectionChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Source, Tabs))
            {
                Run(ShowSelectedAsync);
            }
        };

        // Upstream's FileChangesDoubleClick: the commit in upstream's FormCommitDiff.
        HistoryList.DoubleTapped += (_, _) =>
        {
            if (_commits.Selected is { } row && !ArtificialCommits.IsArtificial(row.Hash))
            {
                new CommitDiffWindow(_repositoryPath, row.Hash).Show(this);
            }
        };
        Blame.GridRevision = hash => _commits.VisibleRows.FirstOrDefault(item => item.Row.Hash == hash)?.Row;

        // Upstream's SelectFileInRevision: the grid's row knows the file's name in that commit.
        Blame.CommitRequested += (_, request) => Run(() => GoToCommitAsync(request.Hash));
        Blame.GridChildren = _commits.ChildrenOf;
        Blame.Info.RevisionLinkClicked += (_, revision) => Run(() => GoToLinkedRevisionAsync(revision));
        Blame.OptionsChanged += (_, _) => ShowOptionChecks();
        Blame.ErrorOccurred += (_, message) => ShowError(message);
        WireToolbar();
        WireMenu();
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
            Run(LoadAsync);
            Run(LoadCustomDifftoolsAsync);
        };
    }

    /// <summary>
    ///  The selected commits, the latest selected first, for tests.
    /// </summary>
    public IReadOnlyList<CommitRow> SelectedRevisions => _selection.Revisions;

    /// <summary>
    ///  The rules, for tests.
    /// </summary>
    public FileHistoryDialog Dialog => _dialog;

    /// <summary>
    ///  The grid's rows, for tests.
    /// </summary>
    public CommitListViewModel Commits => _commits;

    /// <summary>
    ///  Completes when the history is read (or the window opened without reading it), for tests.
    /// </summary>
    public Task HistoryLoaded => _loaded.Task;

    /// <summary>
    ///  The last update of the tabs for the selected commit, for tests.
    /// </summary>
    public Task Shown => _shown;

    // Upstream's OnRuntimeLoad: the history is read unless the settings say to wait for Load.
    private async Task LoadAsync()
    {
        if (FileHistoryDialog.LoadsOnShow(SelectedTab(), _preferences.FileHistory))
        {
            await LoadFileHistoryAsync();
        }

        _loaded.TrySetResult();
    }

    // Upstream's LoadFileHistory: the grid shows, filtered to the file, with the full history as the settings say.
    private async Task LoadFileHistoryAsync()
    {
        HistoryList.IsVisible = true;
        if (_dialog.FileName.Length == 0)
        {
            return;
        }

        // Upstream's SetAndApplyPathFilter keeps the grid's other filters.
        FileHistoryOptions options = _preferences.FileHistory;
        await ApplyGridFilterAsync(_commits.Filter with
        {
            PathFilter = _dialog.FileName,
            FullHistory = options.FullHistory,
            SimplifyMerges = options.SimplifyMerges,
        });
    }

    private void OnCommitsChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CommitListViewModel.VisibleRows):
                HistoryList.ItemsSource = _commits.VisibleRows;
                break;
            case nameof(CommitListViewModel.Filter):
                ToolStripFilters.ShowFilterState();
                break;
            case nameof(CommitListViewModel.ErrorMessage) when _commits.ErrorMessage is { } error:
                ShowError(error);
                break;
        }
    }

    private Task SelectAsync(CommitRow row)
    {
        _selectedHash = row.Hash;
        _shown = SelectAndShowAsync(row);
        return _shown;
    }

    private async Task SelectAndShowAsync(CommitRow row)
    {
        await _commits.SelectAsync(row);
        await ShowSelectedAsync();
    }

    // Upstream's UpdateSelectedFileViewers: the file's name in the commit, the title, the tabs the commit has, and the
    // selected tab's content.
    private async Task ShowSelectedAsync()
    {
        if (_commits.Selected is not { } row)
        {
            return;
        }

        string fileName = _commits.FilePaths.TryGetValue(row.Hash, out string? name) ? name : _dialog.FileName;
        bool isArtificial = ArtificialCommits.IsArtificial(row.Hash);
        bool isFolder = fileName.EndsWith('/');
        bool fileAvailable = !isFolder && await Task.Run(() => isArtificial
            ? File.Exists(Path.Combine(_repositoryPath, fileName.ToNativePath()))
            : !_module.GetFileBlobHash(fileName, ObjectId.Parse(row.Hash)).IsZero);
        if (!ReferenceEquals(row, _commits.Selected))
        {
            return;
        }

        Title = _dialog.Title(PathUtil.GetDisplayPath(_repositoryPath), fileName);
        FileHistoryTabs tabs = _dialog.Tabs(isArtificial, fileAvailable, isFolder, fileName);
        CommitTabText.Text = tabs.CommitCaption.StartsWith(FileHistoryDialog.CommitTabText, StringComparison.Ordinal)
            ? UpstreamTranslation.ByText(FileHistoryDialog.CommitTabText)
              + tabs.CommitCaption[FileHistoryDialog.CommitTabText.Length..]
            : tabs.CommitCaption;
        foreach (FileHistoryTab tab in Enum.GetValues<FileHistoryTab>())
        {
            TabFor(tab).IsVisible = tabs.Visible.Contains(tab);
        }

        // The Build Report tab shows the build's report, not the file.
        if (Tabs.SelectedItem == BuildReportTab)
        {
            return;
        }

        if (!tabs.Visible.Contains(SelectedTab()) && tabs.Preferred is { } preferred)
        {
            Tabs.SelectedItem = TabFor(preferred);
        }

        (string? hash, bool staged) = row.Hash == ArtificialCommits.WorkTreeHash ? (null, false)
            : row.Hash == ArtificialCommits.IndexHash ? (null, true)
            : (row.Hash, false);
        switch (SelectedTab())
        {
            case FileHistoryTab.Blame when tabs.Visible.Contains(FileHistoryTab.Blame):
                await Blame.LoadAsync(_repositoryPath, row.Hash, fileName);
                break;
            case FileHistoryTab.View when tabs.Visible.Contains(FileHistoryTab.View):
                await View.ShowFileAsync(_repositoryPath, row.Hash, staged: false, fileName);
                break;
            case FileHistoryTab.Diff when tabs.Visible.Contains(FileHistoryTab.Diff)
                                          && _selection.Revisions is [_, .., { } first]:
                // Upstream's FileStatusItem(firstRev: the earliest selected, secondRev: the latest selected).
                RevisionDiffFile file = new("M", fileName, OldPath: null, IsTracked: true);
                await Diff.ShowDiffAsync(_repositoryPath, fileName, fileName, options =>
                    RevisionDiff.DiffAsync(_module, ObjectId.Parse(first.Hash), ObjectId.Parse(row.Hash), file, options),
                    FileRevision.Of(row.Hash));
                break;
            case FileHistoryTab.Diff when tabs.Visible.Contains(FileHistoryTab.Diff):
                await Diff.ShowAsync(_repositoryPath, hash, fileName, staged, fileName);
                break;
            case FileHistoryTab.Commit when hash is not null:
                await CommitDiffPanel.SetRevisionAsync(_repositoryPath, hash, fileName);
                break;
        }
    }

    // Upstream's Blame_CommandClick "gotocommit": the commit is selected in the grid, or upstream's message says it is
    // filtered out.
    private async Task GoToCommitAsync(string hash)
    {
        if (_commits.VisibleRows.FirstOrDefault(item => item.Row.Hash == hash) is { } item)
        {
            HistoryList.SelectedItem = item;
            HistoryList.ScrollIntoView(item);
            return;
        }

        string shortHash = ObjectId.TryParse(hash, out ObjectId id) ? id.ToShortString() : hash;
        await new MessageBoxWindow(string.Format(UpstreamTranslation.ByText(RevisionFiltered), shortHash),
            UpstreamTranslation.ByText(RevisionFilteredCaption), MessageBoxButtons.OK).ShowDialog(this);
    }

    // Upstream's Blame_CommandClick for "gotocommit", "gotobranch" and "gototag" of the Blame tab's commit information.
    private async Task GoToLinkedRevisionAsync(string revision)
    {
        string? hash = ObjectId.TryParse(revision, out ObjectId id)
            ? id.ToString()
            : await RepositoryScriptContext.ResolveCommitAsync(_repositoryPath, revision);
        if (hash is not null)
        {
            await GoToCommitAsync(hash);
        }
    }

    private void WireToolbar()
    {
        LoadButton.Click += (_, _) => Run(LoadFileHistoryAsync);
        LoadHistoryOnShowMenuItem.Click += (_, _) => ChangeOptions(options =>
            options with { LoadHistoryOnShow = !options.LoadHistoryOnShow }, reload: false);
        LoadBlameOnShowMenuItem.Click += (_, _) => ChangeOptions(options =>
            options with { LoadBlameOnShow = !options.LoadBlameOnShow }, reload: false);
        ShowFullHistoryMenuItem.Click += (_, _) => ChangeOptions(options =>
            options with { FullHistory = !options.FullHistory }, reload: true);

        // Upstream reloads after a merge simplification change only while the full history is shown.
        SimplifyMergesMenuItem.Click += (_, _) => ChangeOptions(options =>
            options with { SimplifyMerges = !options.SimplifyMerges }, reload: _preferences.FileHistory.FullHistory);
        CommandLogButton.Click += (_, _) => CommandLogWindow.ShowFor(this);

        BlameOption(IgnoreWhitespaceMenuItem, options => options with { IgnoreWhitespace = !options.IgnoreWhitespace });
        BlameOption(DetectMoveInFileMenuItem, options => options with { DetectMoveInFile = !options.DetectMoveInFile });
        BlameOption(DetectMoveInAllFilesMenuItem,
            options => options with { DetectMoveInAllFiles = !options.DetectMoveInAllFiles });
        BlameOption(DisplayAuthorFirstMenuItem,
            options => options with { DisplayAuthorFirst = !options.DisplayAuthorFirst });
        BlameOption(ShowAuthorMenuItem, options => options.ToggleShowAuthor());
        BlameOption(ShowAuthorDateMenuItem, options => options.ToggleShowAuthorDate());
        BlameOption(ShowAuthorTimeMenuItem, options => options with { ShowAuthorTime = !options.ShowAuthorTime });
        BlameOption(ShowLineNumbersMenuItem, options => options with { ShowLineNumbers = !options.ShowLineNumbers });
        BlameOption(ShowOriginalFilePathMenuItem,
            options => options with { ShowOriginalFilePath = !options.ShowOriginalFilePath });
        BlameOption(ShowAuthorAvatarMenuItem, options => options with { ShowAuthorAvatar = !options.ShowAuthorAvatar });
    }

    private void WireMenu()
    {
        HistoryContextMenu.Opening += (_, _) =>
        {
            IReadOnlyList<CommitRow> selected = _selection.Revisions;
            CommitRow? row = selected.Count > 0 ? selected[0] : null;
            FileHistoryMenu menu = FileHistoryDialog.Menu(selected.Count,
                row is not null && ArtificialCommits.IsArtificial(row.Hash), row?.Hash == ArtificialCommits.WorkTreeHash,
                File.Exists(Path.Combine(_repositoryPath, _dialog.FileName.ToNativePath())));
            OpenWithDifftoolMenuItem.IsEnabled = menu.OpenWithDifftool;
            DiffToolRemoteLocalMenuItem.IsEnabled = menu.DiffToolRemoteLocal;
            CopyMenus.Fill(CopyToClipboardMenuItem, selected, keep: null, value => Run(() => CopyTextAsync(value)));
            ManipulateCommitMenuItem.IsEnabled = menu.ManipulateCommit;
            SaveAsMenuItem.IsEnabled = menu.SaveAs;
            CopyToClipboardMenuItem.IsEnabled = menu.CopyToClipboard;
        };
        CustomDiffTools.OnClick(OpenWithDifftoolMenuItem,
            tool => Run(() => OpenWithDifftoolAsync(RevisionDiffKind.DiffAB, tool)));
        CustomDiffTools.OnClick(DiffToolRemoteLocalMenuItem,
            tool => Run(() => OpenWithDifftoolAsync(RevisionDiffKind.DiffBLocal, tool)));
        SaveAsMenuItem.Click += (_, _) => Run(SaveAsAsync);
        CherryPickMenuItem.Click += (_, _) => RunOnSelected(_cherryPick);
        RevertCommitMenuItem.Click += (_, _) => RunOnSelected(_revert);
        FollowRenamesMenuItem.Click += (_, _) => ChangeOptions(options =>
            options with { FollowRenames = !options.FollowRenames }, reload: true);
        FollowExactRenamesMenuItem.Click += (_, _) => ChangeOptions(options =>
            options with { ExactRenamesOnly = !options.ExactRenamesOnly }, reload: true);
    }

    private void ChangeOptions(Func<FileHistoryOptions, FileHistoryOptions> change, bool reload)
    {
        _preferences.FileHistory = change(_preferences.FileHistory);
        _preferences.Save();
        ShowOptionChecks();
        if (reload)
        {
            Run(LoadFileHistoryAsync);
        }
    }

    private void BlameOption(MenuItem item, Func<BlameOptions, BlameOptions> change)
        => item.Click += (_, _) => Run(() => Blame.ChangeOptionsAsync(change(_preferences.BlameOptions)));

    private void ShowOptionChecks()
    {
        FileHistoryOptions history = _preferences.FileHistory;
        LoadHistoryOnShowMenuItem.IsChecked = history.LoadHistoryOnShow;
        LoadBlameOnShowMenuItem.IsChecked = history.LoadBlameOnShow && _dialog.HasBlame;
        ShowFullHistoryMenuItem.IsChecked = history.FullHistory;
        SimplifyMergesMenuItem.IsChecked = history.SimplifyMerges;
        SimplifyMergesMenuItem.IsEnabled = history.FullHistory;
        FollowRenamesMenuItem.IsChecked = history.FollowRenames;
        FollowExactRenamesMenuItem.IsChecked = history.ExactRenamesOnly;
        FollowExactRenamesMenuItem.IsEnabled = history.FollowRenames;

        BlameOptions blame = _preferences.BlameOptions;
        IgnoreWhitespaceMenuItem.IsChecked = blame.IgnoreWhitespace;
        DetectMoveInFileMenuItem.IsChecked = blame.DetectMoveInFile;
        DetectMoveInAllFilesMenuItem.IsChecked = blame.DetectMoveInAllFiles;
        DisplayAuthorFirstMenuItem.IsChecked = blame.DisplayAuthorFirst;
        ShowAuthorMenuItem.IsChecked = blame.ShowAuthor;
        ShowAuthorDateMenuItem.IsChecked = blame.ShowAuthorDate;
        ShowAuthorTimeMenuItem.IsChecked = blame.ShowAuthorTime;
        ShowAuthorTimeMenuItem.IsEnabled = blame.ShowAuthorDate;
        ShowLineNumbersMenuItem.IsChecked = blame.ShowLineNumbers;
        ShowOriginalFilePathMenuItem.IsChecked = blame.ShowOriginalFilePath;
        ShowAuthorAvatarMenuItem.IsChecked = blame.ShowAuthorAvatar;
    }

    private async Task CopyTextAsync(string? text)
    {
        if (text is not null && GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    // Upstream's LoadCustomDifftools: the diff tools under both difftool items.
    private Task LoadCustomDifftoolsAsync()
        => CustomDiffTools.LoadAsync(
        [
            (OpenWithDifftoolMenuItem, tool => Run(() => OpenWithDifftoolAsync(RevisionDiffKind.DiffAB, tool))),
            (DiffToolRemoteLocalMenuItem, tool => Run(() => OpenWithDifftoolAsync(RevisionDiffKind.DiffBLocal, tool))),
        ], diff: true, _preferences);

    // Upstream's OpenFilesWithDiffTool: "Open with difftool" (DiffAB: a commit against its parent, or the two selected
    // against each other) and "Difftool selected < - > local" (DiffBLocal), with the default tool or the chosen one.
    private async Task OpenWithDifftoolAsync(RevisionDiffKind kind, string? tool)
    {
        IReadOnlyList<CommitRow> selected = _selection.Revisions;
        if (selected.Count == 0)
        {
            return;
        }

        string? oldFileName = _commits.FilePaths.TryGetValue(selected[0].Hash, out string? name) ? name : null;
        string fileName = _dialog.FileName;
        if (await Task.Run(() => FileHistoryDialog.OpenWithDifftool(_module, selected, fileName, oldFileName, kind,
                tool)) is { } error)
        {
            await new ErrorWindow(error).ShowDialog(this);
        }
    }

    // Upstream's saveAsToolStripMenuItem_Click: the file as of the commit, saved where the user chooses.
    private async Task SaveAsAsync()
    {
        if (_commits.Selected is not { } row)
        {
            return;
        }

        string fileName = _commits.FilePaths.TryGetValue(row.Hash, out string? name) ? name : _dialog.FileName;
        string fullName = Path.Combine(_repositoryPath, fileName.ToNativePath());
        IStorageFolder? folder = await StorageProvider.TryGetFolderFromPathAsync(
            Path.GetDirectoryName(fullName) ?? _repositoryPath);
        IStorageFile? target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = Path.GetFileName(fullName),
            DefaultExtension = Path.GetExtension(fullName),
            SuggestedStartLocation = folder,
        });
        if (target?.TryGetLocalPath() is { } path)
        {
            await Task.Run(() => _module.SaveBlobAs(path, $"{row.Hash}:\"{fileName}\""));
        }
    }

    private void RunOnSelected(Func<string, string, Task> action)
    {
        if (_selection.Revisions is [{ } row] && !ArtificialCommits.IsArtificial(row.Hash))
        {
            Run(() => action(_repositoryPath, row.Hash));
        }
    }

    private FileHistoryTab SelectedTab()
        => Tabs.SelectedItem == CommitTab ? FileHistoryTab.Commit
            : Tabs.SelectedItem == ViewTab ? FileHistoryTab.View
            : Tabs.SelectedItem == BlameTab ? FileHistoryTab.Blame
            : FileHistoryTab.Diff;

    private TabItem TabFor(FileHistoryTab tab)
        => tab switch
        {
            FileHistoryTab.Commit => CommitTab,
            FileHistoryTab.View => ViewTab,
            FileHistoryTab.Blame => BlameTab,
            _ => DiffTab,
        };

    // The grid as the browse window's View menu and upstream's appearance settings say.
    private void ShowGridColumns()
    {
        RevisionGridColumns.ShowAsSettingsSay(this, HistoryList, _preferences, _commits);
        ShowBuildStatusColumn();
    }

    private void ShowError(string message) => _ = new ErrorWindow(message).ShowDialog(this);

    private void Run(Func<Task> action) => UiActions.Run(action, ex => ShowError(ex.Message));
}
