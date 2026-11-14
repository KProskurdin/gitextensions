using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  A row of the stash window's file list: a group's header or a file.
/// </summary>
public sealed record StashFileRow(string Text, StashFile? File)
{
    public bool IsFile => File is not null;

    public bool IsHeader => File is null;

    /// <summary>
    ///  A group's header cannot be selected.
    /// </summary>
    public bool IsSelectable => IsFile;

    public string? Image => File is null ? null : FileStatusList.ImageFor(File.Change.Kind);
}

/// <summary>
///  Upstream's <c>FormStash</c> in the new shell, bound to <see cref="StashViewModel"/> (rules in
///  <see cref="StashDialog"/>): the working directory's changes and the stashes, the selected one's files and their
///  diffs, a new stash of all or the selected changes, and drop and apply of the selected stash. The file list (its
///  filter, grouping and menu) and the diff stay in the view.
/// </summary>
public partial class StashWindow : Window
{
    // Upstream's splitter distance at 100 %.
    private const double LeftPanelWidth = 280;

    private readonly string _repositoryPath;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;
    private FileTreeRows<StashFileRow> _tree = null!;
    private IReadOnlyList<(string Group, IReadOnlyList<StashFileRow> Files)> _fileGroups = [];

    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public StashWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="manageStashes">Upstream's <c>ManageStashes</c>: show the newest stash first.</param>
    /// <param name="initialStash">The stash to show first ("stash@{n}").</param>
    public StashWindow(string repositoryPath, RepositoryOperationsViewModel actions, IAppPreferences preferences,
        bool manageStashes = true, string? initialStash = null)
    {
        _repositoryPath = repositoryPath;
        _actions = actions;
        _preferences = preferences;
        ViewModel = new StashViewModel(new WindowDialogHost(this), repositoryPath, actions, preferences, manageStashes,
            initialStash, () => ConfirmWindow.AskAsync(this, preferences, Confirmation.StashDrop,
                Confirmations.StashDropQuestion, "Drop", StashDialog.DropConfirmTitle));
        DataContext = ViewModel;
        InitializeComponent();
        InitialFocus.Attach(this);
        WindowPlacementTracker.Attach(this, "Xplat.StashWindow");

        // View concerns: the file list (its rows, tree, filter, grouping and menu), the selected file's diff, the left
        // panel's width and the hotkeys.
        _tree = new FileTreeRows<StashFileRow>(FilesList, preferences, row => row.Text,
            row => row.Image ?? "", row => row.File?.Change.Kind ?? ChangeKind.Modified, ShowFileRows);
        WireFileListToolbar();
        ViewModel.PropertyChanged += OnViewModelChanged;
        FilesList.SelectionChanged += (_, _) => ShowSelectedFile();

        // Upstream's FileViewer_TopScrollReached and FileViewer_BottomScrollReached.
        Diff.TopScrollReached += (_, _) =>
        {
            FileListNodes.SelectNeighbourFile(FilesList, next: false, item => item is StashFileRow { IsFile: true });
            Diff.ScrollToBottom();
        };
        Diff.BottomScrollReached += (_, _) =>
        {
            FileListNodes.SelectNeighbourFile(FilesList, next: true, item => item is StashFileRow { IsFile: true });
            Diff.ScrollToTop();
        };
        Diff.LinesHotkey += (_, command) =>
        {
            if (SelectedFiles is [{ } file])
            {
                Run(() => DiffLinePatching.RunAsync(this, Diff, command,
                    MenuServices ?? new FileListMenuServices(_actions),
                    _repositoryPath, FileStatusList.StatusLetter(file.Change.Kind), () =>
                    {
                        ShowSelectedFile();
                        return Task.CompletedTask;
                    }));
            }
        };
        FilesMenu.Bind(new FileStatusListMenuBinding
        {
            Owner = this,
            RepositoryPath = () => _repositoryPath,
            Selection = MenuSelection,
            DescribeRevision = FileStatusListMenu.DescribeByHash,
            Services = () => MenuServices ?? new FileListMenuServices(_actions),
            Refresh = () => _ = ViewModel.LoadAsync(),
            RunScript = RunScript,
            FocusedPath = () => (FileStatusListMenu.FocusedItem(FilesList) as StashFileRow)?.File?.Change.Path,
            FindFile = new FileListFind(
                () =>
                [
                    .. _fileGroups.SelectMany(group => group.Files).Select(row => row.File)
                        .OfType<StashFile>().Select(file => (file.Change.Path, (string?)null))
                ],
                SelectFile),
            Tree = _tree.MenuTree(),
            SelectFirstGroup = SelectFirstGroupFiles,
            Viewer = () => Diff,
            Submodules = () => Submodules?.Invoke(this),
        });
        FilesMenu.AttachHotkeys(FilesList);
        KeyDown += OnKeyDown;
        Opened += (_, _) =>
        {
            FitLeftPanel();
            _ = ViewModel.LoadAsync();
        };
        Closing += (_, _) => ViewModel.SaveOptions();
    }

    public StashViewModel ViewModel { get; }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public StashDialog Dialog => ViewModel.Dialog;

    /// <summary>
    ///  Completes when the stashes and the first stash's files are shown, for tests.
    /// </summary>
    public Task StashesLoaded => ViewModel.Loaded;

    /// <summary>
    ///  True while the selected stash's files are read, for tests.
    /// </summary>
    public bool IsLoadingFiles => ViewModel.IsLoadingFiles;

    /// <summary>
    ///  The browse window's git actions and windows for the file list's menu; without them the menu uses the window's
    ///  own actions and leaves out the file history.
    /// </summary>
    internal FileListMenuServices? MenuServices { get; init; }

    /// <summary>
    ///  The browse window's submodule commands for the file list's menu, with this window as the owner of their dialogs;
    ///  null leaves out the submodule items.
    /// </summary>
    internal Func<Window, FileListSubmodules>? Submodules { get; init; }

    // Upstream's SelectedItems: a single selected folder or group node stands for the files below it.
    private IReadOnlyList<StashFile> SelectedFiles
        =>
        [
            .. (FileListNodes.SelectedNode(FilesList)?.Files ?? FilesList.SelectedItems?.Cast<object>() ?? [])
            .OfType<StashFileRow>().Select(row => row.File).OfType<StashFile>()
        ];

    // The revision a file's diff shows: its stash commit, else the index or the working directory.
    private static ObjectId SecondOf(StashFile file)
        => file.Commit is { } commit && ObjectId.TryParse(commit, out ObjectId id) ? id
            : file.Change.Staged ? ObjectId.IndexId
            : ObjectId.WorkTreeId;

    // The selected stash's files, once read: their rows grouped, the first file selected; while they are read the list
    // and the diff are empty.
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(StashViewModel.Files))
        {
            return;
        }

        if (ViewModel.Files is not { } files)
        {
            Diff.Clear();
            FilesList.ItemsSource = null;
            return;
        }

        _fileGroups =
        [
            .. files.GroupBy(file => file.Group).Select(group =>
                (group.Key,
                    (IReadOnlyList<StashFileRow>)[.. group.Select(file => new StashFileRow(file.Change.Path, file))]))
        ];
        _tree.Reset();
        ShowFileRows();
        if (FilesList.Items.OfType<StashFileRow>().FirstOrDefault(row => row.IsFile) is { } first)
        {
            FilesList.SelectedItem = first;
        }
    }

    // Upstream's SelectFirstGroupChanges hotkey (FirstGroupItems): the first group's shown files (the index's for the
    // working directory, else all), their folders expanded since the list holds only the rows shown.
    private void SelectFirstGroupFiles()
    {
        if (_fileGroups is not [var (group, files), ..])
        {
            return;
        }

        List<StashFileRow> matching = [.. files.Where(file => FilesFilterBox.IsMatch(file.Text))];
        _tree.Expand(_tree.NodeKeys(matching, $"{group}\n"));
        ShowFileRows();
        _tree.Select(matching);
    }

    // Upstream's SelectedGitItem setter after "Find file": the first shown row of the file is the only one selected.
    private void SelectFile(string path)
    {
        if (FilesList.Items.OfType<StashFileRow>().FirstOrDefault(row => row.File?.Change.Path == path) is { } row)
        {
            FilesList.SelectedItem = row;
            FilesList.ScrollIntoView(row);
        }
    }

    // As upstream: the left panel keeps its 280 pixels unless the options and buttons need more (a larger font).
    private void FitLeftPanel()
    {
        OptionsGrid.Measure(Avalonia.Size.Infinity);
        SplitGrid.ColumnDefinitions[0].Width = new GridLength(Math.Max(LeftPanelWidth, OptionsGrid.DesiredSize.Width));
    }

    // Upstream's Stashed list: each group's header (the commit index and the working directory for the working
    // directory's changes), then its files in upstream's tree (FileTreeRows).
    private void ShowFileRows()
    {
        HashSet<StashFileRow> selected = [.. FilesList.SelectedItems?.OfType<StashFileRow>() ?? []];
        List<object> rows = [];
        int shown = 0;
        _tree.BeginFill();
        foreach ((string group, IReadOnlyList<StashFileRow> files) in _fileGroups)
        {
            if (group.Length > 0)
            {
                rows.Add(new StashFileRow(UpstreamTranslation.ByText(group), null));
            }

            List<StashFileRow> matching = [.. files.Where(file => FilesFilterBox.IsMatch(file.Text))];
            shown += matching.Count;
            _tree.Add(rows, matching, depth: group.Length > 0 ? 1 : 0, keyPrefix: $"{group}\n");
        }

        FilesList.ItemsSource = rows;

        // Avalonia keeps a selected row that the new list has an equal one of (the rows are records); adding that one too
        // would list the file twice in SelectedItems.
        foreach (StashFileRow row in rows.OfType<StashFileRow>().Where(selected.Contains))
        {
            if (FilesList.SelectedItems is { } selectedItems && !selectedItems.Contains(row))
            {
                selectedItems.Add(row);
            }
        }

        _tree.EndFill();
        FilesFilterBox.Shown(shown);
    }

    // Upstream FileStatusList's toolbar over the Stashed list: the filter box, the group-by buttons, the tree button and
    // its menu, and the collapse button (for the extension and status groups; the commit index and working directory
    // headers stay).
    private void WireFileListToolbar()
    {
        // This window has no settings menu, but upstream's choice holds for every file list.
        FileListToolbar.Attach(FilesByExtensionButton, _preferences);
        FileListTreeButtons buttons = new(FilesCollapseGroupsButton, FilesAsTreeButton,
            [
                (FilesPathTreeMenuItem, DiffListSortType.FilePath),
                (FilesPathFlatMenuItem, DiffListSortType.FilePathFlat),
                (FilesExtensionTreeMenuItem, DiffListSortType.FileExtension),
                (FilesExtensionFlatMenuItem, DiffListSortType.FileExtensionFlat),
                (FilesStatusTreeMenuItem, DiffListSortType.FileStatus),
                (FilesStatusFlatMenuItem, DiffListSortType.FileStatusFlat),
            ],
            FilesDenseTreeMenuItem, FilesGroupNodesInFlatListMenuItem);
        buttons.Wire(_preferences, ShowGrouping);
        buttons.CollapseGroups?.Click += (_, _) =>
        {
            _tree.CollapseGroups([.. FilesList.ItemsSource?.Cast<object>() ?? []]);
            ShowFileRows();
        };
        FilesFilterBox.WorkingDirectory = () => _repositoryPath;
        FilesFilterBox.FilterChanged += (_, _) => ShowFileRows();
        FilesByPathButton.Click += (_, _) => SetGroupBy(DiffListSortType.FilePath);
        FilesByExtensionButton.Click += (_, _) => SetGroupBy(DiffListSortType.FileExtension);
        FilesByStatusButton.Click += (_, _) => SetGroupBy(DiffListSortType.FileStatus);
        ShowGroupByButtons();
        return;

        void SetGroupBy(DiffListSortType grouping)
        {
            _preferences.DiffListSorting = FileListTreeButtons.GroupBy(_preferences.DiffListSorting, grouping);
            _preferences.Save();
            ShowGrouping();
        }

        void ShowGrouping()
        {
            ShowGroupByButtons();
            ShowFileRows();
        }

        void ShowGroupByButtons()
        {
            DiffListSortType sorting = _preferences.DiffListSorting;
            FilesByPathButton.IsChecked = sorting is DiffListSortType.FilePath or DiffListSortType.FilePathFlat;
            FilesByExtensionButton.IsChecked =
                sorting is DiffListSortType.FileExtension or DiffListSortType.FileExtensionFlat;
            FilesByStatusButton.IsChecked = sorting is DiffListSortType.FileStatus or DiffListSortType.FileStatusFlat;
            buttons.Show(_preferences);
        }
    }

    // The selected files go to the view model ("Stash selected changes"); a single one shows its diff.
    private void ShowSelectedFile()
    {
        IReadOnlyList<StashFile> files = SelectedFiles;
        ViewModel.SelectedFiles = files;
        if (files is [{ } file])
        {
            Diff.LineAction = DiffLinePatching.For(_repositoryPath, file.Change.Path,
                FileStatusList.StatusLetter(file.Change.Kind),
                SecondOf(file));
            Run(() => Diff.ShowAsync(_repositoryPath, file.Commit, file.Change.Path, file.Change.Staged,
                file.Change.Path));
        }
    }

    // Upstream's Stash hotkeys (FormStash.Command) and Escape, which closes the window.
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            Close();
            e.Handled = true;
            return;
        }

        StashCommand? command = Hotkeys.Stash.Match(e);
        switch (command)
        {
            case StashCommand.NextStash or StashCommand.PreviousStash:
                ViewModel.SelectNeighbour(next: command == StashCommand.NextStash);
                e.Handled = true;
                break;

            case StashCommand.Refresh:
                ViewModel.RefreshWorkingDirectory();
                e.Handled = true;
                break;
        }
    }

    // Upstream's LoadGitItemStatuses as the file list's menu sees it: the working directory's staged files between HEAD
    // and the index and its other files between the index and the working directory; a stash's files between the commit
    // it was made on and the stash.
    private DiffMenuSelection? MenuSelection()
    {
        if (ViewModel.Revisions is not { } revisions || SelectedFiles is not [_, ..] files)
        {
            return null;
        }

        List<DiffMenuFile> menuFiles =
        [
            .. files.Select(file => new DiffMenuFile(FileStatusList.StatusLetter(file.Change.Kind), file.Change.Path,
                OldPath: null,
                IsTracked: file.Change.Kind != ChangeKind.Untracked, revisions.Of(file.Change))
            {
                IsSubmodule = file.Change.IsSubmodule,
                IsSkipWorktree = file.Change.IsSkipWorktree,
                IsAssumeUnchanged = file.Change.IsAssumeUnchanged
            })
        ];
        DiffMenuRevisions first = menuFiles[0].Revisions!;
        return new DiffMenuSelection(first.First, first.Second, first.SecondParents, menuFiles,
            FileListNodes.SelectedNode(FilesList)?.Folder);
    }

    // Upstream's file list scripts: with the shown stash and the selected files.
    private void RunScript(ScriptDefinition script)
    {
        IReadOnlyList<string> hashes = ViewModel.Revisions is { IsWorkingDirectory: false } revisions
            ? [revisions.Stash.ToString()]
            : [];
        IReadOnlyDictionary<string, IReadOnlyList<string>> options = ScriptFileOptions.For(
            SelectedFiles.Select(file => file.Change.Path), Diff.CurrentFileLine);
        Run(() => new ScriptHost(this, _actions).RunAsync(script, _repositoryPath, hashes, options));
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}
