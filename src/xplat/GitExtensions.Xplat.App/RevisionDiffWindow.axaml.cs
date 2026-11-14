using System.ComponentModel;
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
///  Upstream's <c>FormDiff</c>, bound to <see cref="RevisionDiffViewModel"/> (rules in <see cref="RevisionDiff"/>): the
///  files changed from a BASE revision to a compared one and the selected file's diff. Either side can be another branch
///  (the compare to branch window) or commit (the choose commit window), the two can be swapped, BASE can be their merge
///  base, and the directory diff tool can open them. The file list, its menu and the diff stay in the view.
/// </summary>
public partial class RevisionDiffWindow : Window
{
    private readonly string _repositoryPath;
    private readonly GitModule _module;
    private readonly RevisionFileList<RevisionDiffFile> _files;

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
        _module = GitModules.Open(repositoryPath);
        ViewModel = new RevisionDiffViewModel(new WindowDialogHost(this), _module, first, second, firstDisplay,
            secondDisplay)
        {
            PickBranch = async current =>
                await new CompareToBranchWindow(repositoryPath, current ?? default).ShowDialog<string?>(this),
            PickCommit = async current =>
                await new ChooseCommitWindow(repositoryPath, current?.ToString(), preferences, showArtificial: true)
                    .ShowDialog<CommitRow?>(this) is { } row
                    ? (row.Hash, row.Subject)
                    : null,
        };
        InitializeComponent();
        UpstreamTranslation.Apply(this, RevisionDiffViewModel.Category);

        // After the translation pass, which would put upstream's text back on "Compare to merge base".
        DataContext = ViewModel;

        // View concerns: the file list (its filter, grouping and menu), the selected file's diff and the sides' colors.
        // This window has no settings menu, but upstream's choice holds for every file list.
        FileListToolbar.Attach(DiffFilesByPathButton, preferences);
        _files = new RevisionFileList<RevisionDiffFile>(DiffFiles, DiffFilesFilterBox, DiffFilesByPathButton,
            DiffFilesByExtensionButton, DiffFilesByStatusButton, DiffFilesNoFilesText, preferences, file => file.Path,
            file => file.Status, treeButtons: new FileListTreeButtons(DiffFilesCollapseGroupsButton,
                DiffFilesAsTreeButton,
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
        ViewModel.PropertyChanged += OnViewModelChanged;
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
        DiffFilesMenu.Bind(new FileStatusListMenuBinding
        {
            Owner = this,
            RepositoryPath = () => _repositoryPath,
            Selection = MenuSelection,
            DescribeRevision = FileStatusListMenu.DescribeByHash,
            Services = () => MenuServices,
            Refresh = ViewModel.Refresh,
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
        Opened += (_, _) => _ = ViewModel.LoadAsync();
    }

    public RevisionDiffViewModel ViewModel { get; }

    /// <summary>
    ///  Completes when the merge base is known and the first file list is shown, for tests.
    /// </summary>
    public Task DiffLoaded => ViewModel.Loaded;

    /// <summary>
    ///  Completes when the last file list asked for is shown, for tests.
    /// </summary>
    public Task FilesShown => ViewModel.FilesShown;

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

    private static SolidColorBrush Brush(AppColor color)
    {
        System.Drawing.Color value = ThemeBrushes.Current.Colors.Get(color);
        return new SolidColorBrush(Color.FromArgb(value.A, value.R, value.G, value.B));
    }

    // The files show once read, the first selected; while they are read the list and the diff are empty.
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RevisionDiffViewModel.Files))
        {
            return;
        }

        _files.Files = ViewModel.Files;
        if (ViewModel.Files is null)
        {
            DiffText.Clear();
        }
        else
        {
            DiffFiles.SelectedIndex = DiffFiles.ItemCount > 0 ? 0 : -1;
        }
    }

    // Upstream's ShowSelectedFileDiff.
    private Task ShowSelectedFileDiffAsync()
    {
        if (DiffFiles.SelectedItem is not RevisionDiffFile file || ViewModel.ShownFrom is not { } fromId
                                                                || ViewModel.Second is not { } toId)
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

    // The selected files between the shown revisions (BASE or its merge base, and the compared one), for the menu.
    private DiffMenuSelection? MenuSelection()
    {
        ObjectId? from = ViewModel.ShownFrom;
        if (ViewModel.Second is not { } second)
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

        IReadOnlyList<string> hashes = ViewModel.Second is { } second ? [second.ToString()] : [];
        IReadOnlyDictionary<string, IReadOnlyList<string>> options = ScriptFileOptions.For(
            (DiffFiles.SelectedItems ?? Array.Empty<object>()).OfType<RevisionDiffFile>().Select(file => file.Path),
            DiffText.CurrentFileLine);
        Run(() => new ScriptHost(this, services.Actions).RunAsync(script, _repositoryPath, hashes, options));
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}
