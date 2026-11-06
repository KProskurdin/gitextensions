using System.Text.RegularExpressions;
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
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.App;

/// <summary>
///  An entry of the stash window's "Show:" box: upstream's <c>GitStash.Summary</c>.
/// </summary>
public sealed record StashChoice(GitStash Stash, string Text);

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
///  Upstream's <c>FormStash</c> in the new shell (rules in <see cref="StashDialog"/>): the working directory's changes and
///  the stashes, the selected one's files and their diffs, a new stash of all or the selected changes, and drop and apply
///  of the selected stash. "Keep index" and "Include untracked files" are upstream's settings, kept on close.
/// </summary>
public partial class StashWindow : Window
{
    // Upstream's splitter distance at 100 %.
    private const double LeftPanelWidth = 280;

    private readonly string _repositoryPath;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;
    private readonly StashDialog _dialog;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _showing;
    private int _filesVersion;
    private StashRevisions? _revisions;
    // Upstream FileStatusList's title of an invalid filter's error.


    private FileTreeRows<StashFileRow> _tree = null!;
    private IReadOnlyList<(string Group, IReadOnlyList<StashFileRow> Files)> _fileGroups = [];

    /// <param name="manageStashes">Upstream's <c>ManageStashes</c>: show the newest stash first.</param>
    /// <param name="initialStash">The stash to show first ("stash@{n}").</param>
    public StashWindow(string repositoryPath, RepositoryOperationsViewModel actions, IAppPreferences preferences,
        bool manageStashes = true, string? initialStash = null)
    {
        _repositoryPath = repositoryPath;
        _actions = actions;
        _preferences = preferences;
        _dialog = new StashDialog(manageStashes, initialStash);
        InitializeComponent();
        InitialFocus.Attach(this);
        WindowPlacementTracker.Attach(this, "Xplat.StashWindow");
        _tree = new FileTreeRows<StashFileRow>(FilesList, preferences, row => row.Text,
            row => row.Image ?? "", row => row.File?.Change.Kind ?? ChangeKind.Modified, ShowFileRows);
        WireFileListToolbar();

        KeepIndexCheck.IsChecked = preferences.StashKeepIndex;
        IncludeUntrackedCheck.IsChecked = preferences.IncludeUntrackedFilesInManualStash;
        StashesBox.SelectionChanged += (_, _) =>
        {
            if (!_showing && StashesBox.SelectedIndex >= 0)
            {
                _dialog.Select(StashesBox.SelectedIndex);
                Run(ShowSelectedStashAsync);
            }
        };
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
                Run(() => DiffLinePatching.RunAsync(this, Diff, command, MenuServices ?? new FileListMenuServices(_actions),
                    _repositoryPath, StatusLetter(file.Change.Kind), () =>
                    {
                        ShowSelectedFile();
                        return Task.CompletedTask;
                    }));
            }
        };
        StashAllButton.Click += (_, _) => Run(() => StashAsync(selectedOnly: false));
        StashSelectedButton.Click += (_, _) => Run(() => StashAsync(selectedOnly: true));
        DropButton.Click += (_, _) => Run(DropAsync);
        ApplyButton.Click += (_, _) => Run(ApplyAsync);
        FilesMenu.Bind(new FileStatusListMenuBinding
        {
            Owner = this,
            RepositoryPath = () => _repositoryPath,
            Selection = MenuSelection,
            DescribeRevision = FileStatusListMenu.DescribeByHash,
            Services = () => MenuServices ?? new FileListMenuServices(_actions),
            Refresh = () => Run(LoadAsync),
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
            Viewer = () => Diff,
        });
        KeyDown += OnKeyDown;
        Opened += (_, _) =>
        {
            FitLeftPanel();
            Run(LoadAsync);
        };
        Closing += (_, _) =>
        {
            _preferences.StashKeepIndex = KeepIndexCheck.IsChecked == true;
            _preferences.IncludeUntrackedFilesInManualStash = IncludeUntrackedCheck.IsChecked == true;
            _preferences.Save();
        };
    }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public StashDialog Dialog => _dialog;

    /// <summary>
    ///  Completes when the stashes and the first stash's files are shown, for tests.
    /// </summary>
    public Task StashesLoaded => _loaded.Task;

    /// <summary>
    ///  True while the selected stash's files are read, for tests.
    /// </summary>
    public bool IsLoadingFiles { get; private set; }

    /// <summary>
    ///  The browse window's git actions and windows for the file list's menu; without them the menu uses the window's
    ///  own actions and leaves out the file history.
    /// </summary>
    internal FileListMenuServices? MenuServices { get; init; }

    // Upstream's SelectedItems: a single selected folder or group node stands for the files below it.
    private IReadOnlyList<StashFile> SelectedFiles
        =>
        [
            .. (FileListNodes.SelectedNode(FilesList)?.Files ?? FilesList.SelectedItems?.Cast<object>() ?? [])
            .OfType<StashFileRow>().Select(row => row.File).OfType<StashFile>()
        ];

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

    // Upstream's Initialize: the stashes read again, then the chosen one shown.
    private async Task LoadAsync()
    {
        IReadOnlyList<GitStash> stashes =
            await Task.Run(() => GitModules.Open(_repositoryPath).GetStashes(noLocks: false));
        _dialog.Load(stashes);
        _showing = true;
        StashesBox.ItemsSource = _dialog.Items
            .Select(stash =>
                new StashChoice(stash, stash.Index == -1 ? UpstreamTranslation.ByText(stash.Message) : stash.Summary))
            .ToList();
        StashesBox.SelectedIndex = _dialog.SelectedIndex;
        _showing = false;
        await ShowSelectedStashAsync();
        _loaded.TrySetResult();
    }

    // Upstream's StashesSelectedIndexChanged and InitializeSoft: the message, the buttons and the files of the selection.
    private async Task ShowSelectedStashAsync()
    {
        if (_dialog.Selected is not { } stash)
        {
            return;
        }

        int version = ++_filesVersion;
        MessageBox.Text = UpstreamTranslation.ByText(_dialog.Message);
        MessageBox.IsReadOnly = _dialog.IsMessageReadOnly;
        DropButton.IsEnabled = ApplyButton.IsEnabled = _dialog.CanApplyOrDrop;
        Diff.Clear();
        FilesList.ItemsSource = null;
        IsLoadingFiles = true;
        StashesBox.IsEnabled = false;
        try
        {
            bool workingDirectory = _dialog.IsWorkingDirectorySelected;
            (IReadOnlyList<StashFile> files, StashRevisions revisions) = await Task.Run(() =>
            {
                GitModule module = GitModules.Open(_repositoryPath);
                return (_dialog.Files(module, stash), StashRevisions.Read(module, stash, workingDirectory));
            });
            if (version != _filesVersion)
            {
                return;
            }

            _revisions = revisions;

            _fileGroups =
            [
                .. files.GroupBy(file => file.Group).Select(group =>
                    (group.Key, (IReadOnlyList<StashFileRow>)[.. group.Select(file => new StashFileRow(file.Change.Path, file))]))
            ];
            _tree.Reset();
            ShowFileRows();
            if (FilesList.Items.OfType<StashFileRow>().FirstOrDefault(row => row.IsFile) is { } first)
            {
                FilesList.SelectedItem = first;
            }
        }
        finally
        {
            if (version == _filesVersion)
            {
                IsLoadingFiles = false;
                StashesBox.IsEnabled = true;
                UpdateStashSelectedButton();
            }
        }
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
        FileListTreeButtons buttons = new(FilesCollapseGroupsButton, FilesAsTreeButton,
            [
                (FilesPathTreeMenuItem, DiffListSortType.FilePath), (FilesPathFlatMenuItem, DiffListSortType.FilePathFlat),
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
            FilesByExtensionButton.IsChecked = sorting is DiffListSortType.FileExtension or DiffListSortType.FileExtensionFlat;
            FilesByStatusButton.IsChecked = sorting is DiffListSortType.FileStatus or DiffListSortType.FileStatusFlat;
            buttons.Show(_preferences);
        }
    }

    private void ShowSelectedFile()
    {
        UpdateStashSelectedButton();
        if (SelectedFiles is [{ } file])
        {
            Diff.LineAction = DiffLinePatching.For(_repositoryPath, file.Change.Path, StatusLetter(file.Change.Kind),
                SecondOf(file));
            Run(() => Diff.ShowAsync(_repositoryPath, file.Commit, file.Change.Path, file.Change.Staged,
                file.Change.Path));
        }
    }

    // The revision a file's diff shows: its stash commit, else the index or the working directory.
    private static ObjectId SecondOf(StashFile file)
        => file.Commit is { } commit && ObjectId.TryParse(commit, out ObjectId id) ? id
            : file.Change.Staged ? ObjectId.IndexId
            : ObjectId.WorkTreeId;

    private void UpdateStashSelectedButton()
        => StashSelectedButton.IsEnabled = _dialog.CanStashSelected(SelectedFiles.Count);

    // Upstream's StashClick and StashSelectedFiles_Click: a new stash, then the list read again.
    private async Task StashAsync(bool selectedOnly)
    {
        IReadOnlyList<string>? paths =
            selectedOnly ? [.. SelectedFiles.Select(file => file.Change.Path).Distinct()] : null;
        if (selectedOnly && paths!.Count == 0)
        {
            return;
        }

        await _actions.StashAsync(_repositoryPath, StashDialog.MessageArgument(MessageBox.Text),
            IncludeUntrackedCheck.IsChecked == true, KeepIndexCheck.IsChecked == true, paths);
        await LoadAsync();
    }

    // Upstream's ClearClick: asks first (unless "Don't show me this message again" was chosen), and shows the stash that
    // takes the dropped one's place.
    private async Task DropAsync()
    {
        if (_dialog.Selected is not { } stash || !_dialog.CanApplyOrDrop
                                              || !await ConfirmWindow.AskAsync(this, _preferences,
                                                  Confirmation.StashDrop, Confirmations.StashDropQuestion,
                                                  "Drop", StashDialog.DropConfirmTitle))
        {
            return;
        }

        _dialog.Dropping();
        await _actions.DropStashAsync(_repositoryPath, stash.Name);
        await LoadAsync();
    }

    private async Task ApplyAsync()
    {
        if (_dialog.Selected is { } stash && _dialog.CanApplyOrDrop)
        {
            await _actions.ApplyStashAsync(_repositoryPath, stash.Name);
            await LoadAsync();
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
            case StashCommand.NextStash or StashCommand.PreviousStash when !IsLoadingFiles:
                if (_dialog.SelectNeighbour(next: command == StashCommand.NextStash))
                {
                    StashesBox.SelectedIndex = _dialog.SelectedIndex;
                }

                e.Handled = true;
                break;

            // As upstream's RefreshAll: only the working directory's changes can have changed.
            case StashCommand.Refresh:
                if (_dialog.IsWorkingDirectorySelected)
                {
                    Run(LoadAsync);
                }

                e.Handled = true;
                break;
        }
    }

    // Upstream's LoadGitItemStatuses as the file list's menu sees it: the working directory's staged files between HEAD
    // and the index and its other files between the index and the working directory; a stash's files between the commit
    // it was made on and the stash.
    private DiffMenuSelection? MenuSelection()
    {
        if (_revisions is not { } revisions || SelectedFiles is not [_, ..] files)
        {
            return null;
        }

        List<DiffMenuFile> menuFiles =
        [
            .. files.Select(file => new DiffMenuFile(StatusLetter(file.Change.Kind), file.Change.Path, OldPath: null,
                IsTracked: file.Change.Kind != ChangeKind.Untracked, revisions.Of(file.Change)))
        ];
        DiffMenuRevisions first = menuFiles[0].Revisions!;
        return new DiffMenuSelection(first.First, first.Second, first.SecondParents, menuFiles,
            FileListNodes.SelectedNode(FilesList)?.Folder);
    }

    // Git's status letter for a change, as the menu's rules read it.
    private static string StatusLetter(ChangeKind kind)
        => kind switch
        {
            ChangeKind.Added => "A",
            ChangeKind.Untracked => "?",
            ChangeKind.Deleted => "D",
            ChangeKind.Renamed => "R",
            ChangeKind.Conflict => "U",
            _ => "M"
        };

    // Upstream's file list scripts: with the shown stash and the selected files.
    private void RunScript(ScriptDefinition script)
    {
        IReadOnlyList<string> hashes = _revisions is { IsWorkingDirectory: false } revisions
            ? [revisions.Stash.ToString()]
            : [];
        IReadOnlyDictionary<string, IReadOnlyList<string>> options = ScriptFileOptions.For(
            SelectedFiles.Select(file => file.Change.Path), Diff.CurrentFileLine);
        Run(() => new ScriptHost(this, _actions).RunAsync(script, _repositoryPath, hashes, options));
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));

    // The revisions upstream's LoadGitItemStatuses gives the shown files: HEAD for the working directory, the stash and
    // the commit it was made on for a stash.
    private sealed record StashRevisions(bool IsWorkingDirectory, ObjectId Head, ObjectId StashParent, ObjectId Stash)
    {
        public static StashRevisions Read(GitModule module, GitStash stash, bool workingDirectory)
            => workingDirectory
                ? new StashRevisions(true, module.RevParse("HEAD"), default, default)
                : new StashRevisions(false, default, module.RevParse(stash.Name + "^"), module.RevParse(stash.Name));

        public DiffMenuRevisions Of(FileChange change)
        {
            if (!IsWorkingDirectory)
            {
                return new DiffMenuRevisions(StashParent.IsZero ? null : StashParent, Stash,
                    StashParent.IsZero ? [] : [StashParent]);
            }

            // Without HEAD (an unborn branch) upstream lists every file against the working directory.
            return change.Staged && !Head.IsZero
                ? new DiffMenuRevisions(Head, ObjectId.IndexId, [Head])
                : new DiffMenuRevisions(Head.IsZero ? null : ObjectId.IndexId, ObjectId.WorkTreeId, [ObjectId.IndexId]);
        }
    }
}
