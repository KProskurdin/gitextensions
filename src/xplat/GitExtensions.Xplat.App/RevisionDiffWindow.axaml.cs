using Avalonia.Controls;
using Avalonia.Media;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using GitExtUtils.GitUI.Theming;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormDiff</c> (rules in <see cref="RevisionDiff"/>): the files changed from a BASE revision to a compared
///  one and the selected file's diff. Either side can be another branch (the compare to branch window) or commit (the
///  choose commit window), the two can be swapped, BASE can be their merge base, and the directory diff tool can open
///  them.
/// </summary>
public partial class RevisionDiffWindow : Window
{
    private readonly string _repositoryPath;
    private readonly IAppPreferences _preferences;
    private readonly GitModule _module;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly RevisionFileList<RevisionDiffFile> _files;
    private ObjectId? _first;
    private ObjectId? _second;
    private string? _firstDisplay;
    private string? _secondDisplay;
    private ObjectId? _mergeBase;
    private int _populate;
    private Task _filesShown = Task.CompletedTask;

    public RevisionDiffWindow()
        : this("", ObjectId.WorkTreeId, ObjectId.IndexId, "", "", AppServices.Preferences)
    {
    }

    /// <param name="repositoryPath">The repository.</param>
    /// <param name="first">The BASE revision.</param>
    /// <param name="second">The revision compared to BASE.</param>
    /// <param name="firstDisplay">What BASE is shown as (a subject or a branch name).</param>
    /// <param name="secondDisplay">What the compared revision is shown as.</param>
    /// <param name="preferences">The choose commit window's grid columns.</param>
    public RevisionDiffWindow(string repositoryPath, ObjectId first, ObjectId second, string firstDisplay,
        string secondDisplay, IAppPreferences preferences)
    {
        _repositoryPath = repositoryPath;
        _preferences = preferences;
        _module = GitModules.Open(repositoryPath);
        _first = first;
        _second = second;
        _firstDisplay = firstDisplay;
        _secondDisplay = secondDisplay;
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormDiff");

        // This window has no settings menu, but upstream's choice holds for every file list.
        FileListToolbar.Attach(DiffFilesByPathButton, preferences);
        _files = new RevisionFileList<RevisionDiffFile>(DiffFiles, DiffFilesFilterBox, DiffFilesByPathButton,
            DiffFilesByExtensionButton, DiffFilesByStatusButton, DiffFilesNoFilesText, preferences, file => file.Path,
            file => file.Status, treeButtons: new FileListTreeButtons(DiffFilesCollapseGroupsButton, DiffFilesAsTreeButton,
                [
                    (DiffFilesPathTreeMenuItem, DiffListSortType.FilePath),
                    (DiffFilesPathFlatMenuItem, DiffListSortType.FilePathFlat),
                    (DiffFilesExtensionTreeMenuItem, DiffListSortType.FileExtension),
                    (DiffFilesExtensionFlatMenuItem, DiffListSortType.FileExtensionFlat),
                    (DiffFilesStatusTreeMenuItem, DiffListSortType.FileStatus),
                    (DiffFilesStatusFlatMenuItem, DiffListSortType.FileStatusFlat),
                ],
                DiffFilesDenseTreeMenuItem, DiffFilesGroupNodesInFlatListMenuItem), oldPath: file => file.OldPath);
        DiffFilesFilterBox.WorkingDirectory = () => _repositoryPath;
        lblFirstCommitBack.Background = Brush(AppColor.AnsiTerminalRedBackNormal);
        lblSecondCommitBack.Background = Brush(AppColor.AnsiTerminalGreenBackNormal);
        ckCompareToMergeBase.IsEnabled = false;
        DiffFiles.SelectionChanged += (_, _) => Run(ShowSelectedFileDiffAsync);

        // Upstream's FileViewer_TopScrollReached and FileViewer_BottomScrollReached.
        DiffText.TopScrollReached += (_, _) =>
        {
            FileListNodes.SelectNeighbourFile(DiffFiles, next: false, item => item is RevisionDiffFile);
            DiffText.ScrollToBottom();
        };
        DiffText.BottomScrollReached += (_, _) =>
        {
            FileListNodes.SelectNeighbourFile(DiffFiles, next: true, item => item is RevisionDiffFile);
            DiffText.ScrollToTop();
        };
        DiffText.LinesHotkey += (_, command) =>
        {
            if (MenuServices is { } services && DiffFiles.SelectedItem is RevisionDiffFile file)
            {
                Run(() => DiffLinePatching.RunAsync(this, DiffText, command, services, _repositoryPath, file.Status,
                    ShowSelectedFileDiffAsync));
            }
        };
        btnSwap.Click += (_, _) =>
        {
            (_second, _first) = (_first, _second);
            (_secondDisplay, _firstDisplay) = (_firstDisplay, _secondDisplay);
            PopulateDiffFiles();
        };
        ckCompareToMergeBase.IsCheckedChanged += (_, _) => PopulateDiffFiles();
        btnCompareDirectoriesWithDiffTool.Click += (_, _) => OpenDirectoryDiff();
        btnAnotherFirstBranch.Click += (_, _) => Run(() => PickAnotherBranchAsync(first: true));
        btnAnotherSecondBranch.Click += (_, _) => Run(() => PickAnotherBranchAsync(first: false));
        btnAnotherFirstCommit.Click += (_, _) => Run(() => PickAnotherCommitAsync(first: true));
        btnAnotherSecondCommit.Click += (_, _) => Run(() => PickAnotherCommitAsync(first: false));
        DiffFilesMenu.Bind(new FileStatusListMenuBinding
        {
            Owner = this,
            RepositoryPath = () => _repositoryPath,
            Selection = MenuSelection,
            DescribeRevision = FileStatusListMenu.DescribeByHash,
            Services = () => MenuServices,
            Refresh = PopulateDiffFiles,
            RunScript = RunScript,
            FocusedPath = () => (FileStatusListMenu.FocusedItem(DiffFiles) as RevisionDiffFile)?.Path,
            FindFile = new FileListFind(() => [.. (_files.Files ?? []).Select(file => (file.Path, file.OldPath))],
                _files.SelectPath),
            Tree = _files.MenuTree,
            SelectFirstGroup = _files.SelectFirstGroupFiles,
            Viewer = () => DiffText,
            Submodules = () => Submodules?.Invoke(this),
        });
        DiffFilesMenu.AttachHotkeys(DiffFiles);
        DiffFiles.DoubleTapped += (_, _) =>
        {
            if (DiffFiles.SelectedItem is RevisionDiffFile)
            {
                DiffFilesMenu.RunDoubleClick();
            }
        };
        Opened += (_, _) => Run(LoadAsync);
    }

    /// <summary>
    ///  Completes when the merge base is known and the first file list is shown, for tests.
    /// </summary>
    public Task DiffLoaded => _loaded.Task;

    /// <summary>
    ///  Completes when the last file list asked for is shown, for tests.
    /// </summary>
    public Task FilesShown => _filesShown;

    /// <summary>
    ///  The browse window's git actions and windows for the file list's menu; without them the menu leaves out the items
    ///  that need them.
    /// </summary>
    internal FileListMenuServices? MenuServices { get; init; }

    /// <summary>
    ///  The browse window's submodule commands for the file list's menu, with this window as the owner of their dialogs;
    ///  null leaves out the submodule items.
    /// </summary>
    internal Func<Window, FileListSubmodules>? Submodules { get; init; }

    // Upstream's constructor (the merge base, which later picks do not change) and Load (PopulateDiffFiles).
    private async Task LoadAsync()
    {
        try
        {
            ObjectId? first = _first;
            ObjectId? second = _second;
            _mergeBase = first is { } a && second is { } b
                ? await Task.Run(() => RevisionDiff.MergeBase(_module, a, b))
                : null;
            ckCompareToMergeBase.Content =
                $"{UpstreamTranslation.ByText(RevisionDiff.CompareToMergeBase)} ({_mergeBase?.ToShortString()})";
            ckCompareToMergeBase.IsEnabled = _mergeBase is not null;
            await ShowDiffFilesAsync();
        }
        finally
        {
            _loaded.TrySetResult();
        }
    }

    // Upstream's PopulateDiffFiles: the labels, the directory diff tool (not from the work tree), and the files from BASE
    // (or the merge base) to the compared revision.
    private void PopulateDiffFiles() => _ = ShowDiffFilesAsync();

    // PopulateDiffFiles' work; the task completes when the files are shown, for the first load and the tests.
    private Task ShowDiffFilesAsync()
    {
        lblFirstCommit.Text = _firstDisplay;
        lblSecondCommit.Text = _secondDisplay;
        ToolTip.SetTip(lblFirstCommit, _firstDisplay);
        ToolTip.SetTip(lblSecondCommit, _secondDisplay);
        btnCompareDirectoriesWithDiffTool.IsEnabled = _first is { } first && RevisionDiff.CanOpenDirectoryDiff(first);
        int populate = ++_populate;
        ObjectId? from = ckCompareToMergeBase.IsChecked == true ? _mergeBase : _first;
        ObjectId? to = _second;
        _files.Files = null;
        DiffText.Clear();
        if (from is not { } fromId || to is not { } toId)
        {
            return Task.CompletedTask;
        }

        return _filesShown = ShowFilesAsync(populate, fromId, toId);
    }

    private async Task ShowFilesAsync(int populate, ObjectId from, ObjectId to)
    {
        try
        {
            IReadOnlyList<RevisionDiffFile> files = await Task.Run(() => RevisionDiff.Files(_module, from, to));
            if (populate == _populate)
            {
                _files.Files = files;
                DiffFiles.SelectedIndex = DiffFiles.ItemCount > 0 ? 0 : -1;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (populate == _populate)
            {
                _ = new ErrorWindow(ex.Message).ShowDialog(this);
            }
        }
    }

    // Upstream's ShowSelectedFileDiff.
    private Task ShowSelectedFileDiffAsync()
    {
        ObjectId? from = ckCompareToMergeBase.IsChecked == true ? _mergeBase : _first;
        if (DiffFiles.SelectedItem is not RevisionDiffFile file || from is not { } fromId || _second is not { } toId)
        {
            DiffText.Clear();
            return Task.CompletedTask;
        }

        // Upstream's viewer applies a commit's lines; between a commit and the working directory or the index the
        // lines are not the index's own, so they are not staged here.
        DiffText.LineAction = MenuServices is null || toId.IsArtificial
            ? DiffLineAction.None
            : DiffLinePatching.For(_repositoryPath, file.Path, file.Status, toId);
        return DiffText.ShowDiffAsync(_repositoryPath, file.Path, file.Path,
            options => RevisionDiff.DiffAsync(_module, fromId, toId, file, options), FileRevision.Of(toId));
    }

    // Upstream's btnCompareDirectoriesWithDiffTool_Clicked.
    private void OpenDirectoryDiff()
    {
        ObjectId? from = ckCompareToMergeBase.IsChecked == true ? _mergeBase : _first;
        if (from is not { } fromId || _second is not { } toId)
        {
            return;
        }

        Run(() => Task.Run(() =>
            _module.OpenWithDifftoolDirDiff(fromId.ToString(), toId.ToString(), customTool: null)));
    }

    // Upstream's PickAnotherBranch: the compare to branch window from the side's commit; a name git does not know leaves
    // the side empty.
    private async Task PickAnotherBranchAsync(bool first)
    {
        ObjectId? current = first ? _first : _second;
        if (await new CompareToBranchWindow(_repositoryPath, current ?? default).ShowDialog<string?>(this) is not
            { } branch)
        {
            return;
        }

        ObjectId id = await Task.Run(() => _module.RevParse(branch));
        SetSide(first, id.IsZero ? null : id, branch);
    }

    // Upstream's PickAnotherCommit: the choose commit window from the side's commit.
    private async Task PickAnotherCommitAsync(bool first)
    {
        ObjectId? current = first ? _first : _second;
        if (await new ChooseCommitWindow(_repositoryPath, current?.ToString(), _preferences, showArtificial: true)
                .ShowDialog<CommitRow?>(this) is not { } row)
        {
            return;
        }

        SetSide(first, ObjectId.TryParse(row.Hash, out ObjectId id) ? id : null, row.Subject);
    }

    private void SetSide(bool first, ObjectId? id, string display)
    {
        if (first)
        {
            _first = id;
            _firstDisplay = display;
        }
        else
        {
            _second = id;
            _secondDisplay = display;
        }

        PopulateDiffFiles();
    }

    // The selected files between the shown revisions (BASE or its merge base, and the compared one), for the menu.
    private DiffMenuSelection? MenuSelection()
    {
        ObjectId? from = ckCompareToMergeBase.IsChecked == true ? _mergeBase : _first;
        if (_second is not { } second)
        {
            return null;
        }

        // Upstream's SelectedItems: a single selected folder node stands for the files below it.
        IFileListNode? node = FileListNodes.SelectedNode(DiffFiles);
        return new DiffMenuSelection(from, second, from is { } parent ? [parent] : [],
        [
            .. (node?.Files ?? DiffFiles.SelectedItems?.Cast<object>() ?? []).OfType<RevisionDiffFile>()
            .Select(file => new DiffMenuFile(file.Status, file.Path, file.OldPath, file.IsTracked)
            {
                IsSubmodule = file.IsSubmodule
            })
        ], node?.Folder);
    }

    // Upstream's file list scripts: with the compared revision and the selected files.
    private void RunScript(ScriptDefinition script)
    {
        if (MenuServices is not { } services)
        {
            return;
        }

        IReadOnlyList<string> hashes = _second is { } second ? [second.ToString()] : [];
        IReadOnlyDictionary<string, IReadOnlyList<string>> options = ScriptFileOptions.For(
            (DiffFiles.SelectedItems ?? Array.Empty<object>()).OfType<RevisionDiffFile>().Select(file => file.Path),
            DiffText.CurrentFileLine);
        Run(() => new ScriptHost(this, services.Actions).RunAsync(script, _repositoryPath, hashes, options));
    }

    private static SolidColorBrush Brush(AppColor color)
    {
        System.Drawing.Color value = ThemeBrushes.Current.Colors.Get(color);
        return new SolidColorBrush(Color.FromArgb(value.A, value.R, value.G, value.B));
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}
