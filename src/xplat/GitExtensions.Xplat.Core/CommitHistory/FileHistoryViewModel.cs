using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  One of the file history window's check items in its toolstrip and grid menu (upstream's <c>FileHistory</c> settings).
/// </summary>
public enum FileHistoryOption
{
    LoadHistoryOnShow,
    LoadBlameOnShow,
    FullHistory,
    SimplifyMerges,
    FollowRenames,
    ExactRenamesOnly,
}

/// <summary>
///  What a file history tab shows for the selected commit: the commit, the file's name in it, the commit to show
///  (<see langword="null"/> for the working directory and index rows) with whether it is the index, and for the Diff tab
///  with several selected commits the earliest selected one, which the diff starts from.
/// </summary>
public sealed record FileHistoryContent(
    FileHistoryTab Tab,
    CommitRow Row,
    string FileName,
    string? Hash,
    bool Staged,
    CommitRow? CompareWith);

/// <summary>
///  What <see cref="FileHistoryViewModel"/> asks of its window and its caller besides <see cref="IDialogHost"/>.
/// </summary>
public sealed record FileHistoryViewModelServices
{
    /// <summary>
    ///  Upstream's "Cherry pick commit": cherry-picks a commit (repository, hash).
    /// </summary>
    public required Func<string, string, Task> CherryPick { get; init; }

    /// <summary>
    ///  Upstream's "Revert commit": reverts a commit (repository, hash).
    /// </summary>
    public required Func<string, string, Task> Revert { get; init; }

    /// <summary>
    ///  Shows a tab's content in its control (the diff, file and blame viewers and the commit panel).
    /// </summary>
    public required Func<FileHistoryContent, Task> ShowContent { get; init; }

    /// <summary>
    ///  Applies changed blame options to the Blame tab, which stores them and blames again.
    /// </summary>
    public required Func<BlameOptions, Task> ChangeBlameOptions { get; init; }

    /// <summary>
    ///  The save picker: the suggested file name and the folder it starts in; the path chosen, or null.
    /// </summary>
    public required Func<string, string, Task<string?>> PickSaveFile { get; init; }

    /// <summary>
    ///  Called after each read of the grid's revisions (the build server is chosen again).
    /// </summary>
    public Action? GridReloaded { get; init; }

    /// <summary>
    ///  The grid's commits; null reads them with git as the settings say.
    /// </summary>
    public ICommitHistory? History { get; init; }

    /// <summary>
    ///  Puts a text on the clipboard.
    /// </summary>
    public Func<string, Task>? CopyText { get; init; }
}

/// <summary>
///  The view model of upstream's <c>FormFileHistory</c> (rules in <see cref="FileHistoryDialog"/>): the commits that changed
///  a file (following its renames as the settings say), and for the latest selected one the title, the tabs it has and
///  their content; the toolstrip's and menu's options, Save as, the difftool, cherry pick and revert. The grid's navigation,
///  quick search, View menu and build column are the window's, as the browse window's grid.
/// </summary>
public sealed partial class FileHistoryViewModel : DialogViewModel
{
    // Upstream's MessageBoxes.RevisionFilteredInGrid texts.
    public const string RevisionFiltered = "Revision \"{0}\" is not visible in the revision grid. Remove the revision filter.";
    public const string RevisionFilteredCaption = "Cannot find revision";

    /// <summary>
    ///  The Build Report tab's place after the four tabs of <see cref="FileHistoryTab"/>.
    /// </summary>
    public const int BuildReportTabIndex = 4;

    private readonly IGitModule _module;
    private readonly string _repositoryPath;
    private readonly IAppPreferences _preferences;
    private readonly string? _revision;
    private readonly FileHistoryViewModelServices _services;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _selectedOnce;
    private string? _selectedHash;

    /// <param name="fileName">The file in the repository, relative to its working directory.</param>
    /// <param name="revision">The commit to select at first; null for the first one.</param>
    /// <param name="showBlame">Open on the Blame tab instead of Diff.</param>
    public FileHistoryViewModel(IDialogHost host, IGitModule module, string repositoryPath, IAppPreferences preferences,
        string fileName, string? revision, bool showBlame, FileHistoryViewModelServices services)
        : base(host)
    {
        _module = module;
        _repositoryPath = repositoryPath;
        _preferences = preferences;
        _revision = revision;
        _services = services;
        Dialog = new FileHistoryDialog(fileName,
            isSubmodule: GitModule.IsValidGitWorkingDir(Path.Combine(repositoryPath, fileName.ToNativePath())));
        Commits = new CommitListViewModel(services.History ?? new GitCommitHistory(
            () => new CommitDateStyle(preferences.RelativeDate, preferences.ShowAuthorDate),
            () => preferences.RevisionSortOrder, fileHistory: () => preferences.FileHistory,
            gridView: () => preferences.GridViewOptions, showSessionRefs: () => preferences.ShowSessionRefs));
        Title = Dialog.Title(PathUtil.GetDisplayPath(repositoryPath));
        IsBlameTabVisible = Dialog.HasBlame;
        CommitTabText = host.Translate(FileHistoryDialog.CommitTabText);
        SelectedTabIndex = (int)Dialog.InitialTab(showBlame);
        _ = Commits.ApplyFilterAsync(StoredRevisionFilter.Initial(preferences));
    }

    /// <summary>
    ///  The rules.
    /// </summary>
    public FileHistoryDialog Dialog { get; }

    /// <summary>
    ///  The grid's rows.
    /// </summary>
    public CommitListViewModel Commits { get; }

    /// <summary>
    ///  The selected commits; the window passes the grid's selection in with <see cref="UpdateSelection"/>.
    /// </summary>
    public GridSelection Selection { get; } = new();

    /// <summary>
    ///  Asks the window to select a row of the grid (null for none) and show it.
    /// </summary>
    public event EventHandler<CommitListItem?>? SelectRowRequested;

    [ObservableProperty]
    public partial string Title { get; private set; }

    /// <summary>
    ///  Upstream hides the grid until the history is read.
    /// </summary>
    [ObservableProperty]
    public partial bool IsHistoryVisible { get; private set; }

    [ObservableProperty]
    public partial string CommitTabText { get; private set; }

    [ObservableProperty]
    public partial bool IsCommitTabVisible { get; private set; } = true;

    [ObservableProperty]
    public partial bool IsDiffTabVisible { get; private set; } = true;

    [ObservableProperty]
    public partial bool IsViewTabVisible { get; private set; } = true;

    [ObservableProperty]
    public partial bool IsBlameTabVisible { get; private set; }

    /// <summary>
    ///  The selected tab: a <see cref="FileHistoryTab"/>, or <see cref="BuildReportTabIndex"/>.
    /// </summary>
    [ObservableProperty]
    public partial int SelectedTabIndex { get; set; }

    /// <summary>
    ///  Which items of the grid's menu apply; the window updates it when the menu opens (<see cref="UpdateMenu"/>).
    /// </summary>
    [ObservableProperty]
    public partial FileHistoryMenu Menu { get; private set; } = new(true, true, true, true, true);

    /// <summary>
    ///  Upstream hides Save as for a submodule.
    /// </summary>
    public bool CanSaveFile => !Dialog.IsSubmodule;

    public bool LoadHistoryOnShow => _preferences.FileHistory.LoadHistoryOnShow;

    public bool LoadBlameOnShow => _preferences.FileHistory.LoadBlameOnShow && Dialog.HasBlame;

    public bool FullHistory => _preferences.FileHistory.FullHistory;

    public bool SimplifyMerges => _preferences.FileHistory.SimplifyMerges;

    public bool FollowRenames => _preferences.FileHistory.FollowRenames;

    public bool ExactRenamesOnly => _preferences.FileHistory.ExactRenamesOnly;

    public BlameOptions BlameOptions => _preferences.BlameOptions;

    /// <summary>
    ///  Completes when the history is read (or the window opened without reading it), for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  The last update of the tabs for the selected commit, for tests.
    /// </summary>
    public Task Shown { get; private set; } = Task.CompletedTask;

    private FileHistoryTab SelectedTab
        => SelectedTabIndex is >= 0 and < BuildReportTabIndex ? (FileHistoryTab)SelectedTabIndex : FileHistoryTab.Diff;

    /// <summary>
    ///  Upstream's <c>OnRuntimeLoad</c>: the history is read unless the settings say to wait for Load. The window calls it
    ///  when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        try
        {
            if (FileHistoryDialog.LoadsOnShow(SelectedTab, _preferences.FileHistory))
            {
                await LoadFileHistoryAsync();
            }
        }
        finally
        {
            _loaded.TrySetResult();
        }
    });

    /// <summary>
    ///  Upstream's grid selects several commits (Ctrl, Shift); the latest selected is the one the tabs show, and the Diff
    ///  tab compares the earliest selected with it.
    /// </summary>
    /// <param name="current">The grid's selected rows.</param>
    /// <param name="added">The rows this change selected.</param>
    public void UpdateSelection(IReadOnlyList<CommitRow> current, IReadOnlyList<CommitRow> added)
    {
        Selection.Update(current, added);
        if (Selection.Latest is not { } latest)
        {
            return;
        }

        if (!ReferenceEquals(latest, Commits.Selected))
        {
            _selectedHash = latest.Hash;
            Shown = GuardAsync(async () =>
            {
                await Commits.SelectAsync(latest);
                await ShowSelectedAsync();
            });
        }
        else
        {
            Shown = GuardAsync(ShowSelectedAsync);
        }
    }

    /// <summary>
    ///  Reads the options again after something else changed them (the Blame tab's menu, the filter window).
    /// </summary>
    public void RefreshOptions()
    {
        foreach (string name in new[]
                 {
                     nameof(LoadHistoryOnShow), nameof(LoadBlameOnShow), nameof(FullHistory), nameof(SimplifyMerges),
                     nameof(FollowRenames), nameof(ExactRenamesOnly), nameof(BlameOptions),
                 })
        {
            OnPropertyChanged(name);
        }
    }

    /// <summary>
    ///  Upstream's <c>FileHistoryContextMenuOpening</c>: which items apply to the selected commits.
    /// </summary>
    public void UpdateMenu()
    {
        IReadOnlyList<CommitRow> selected = Selection.Revisions;
        CommitRow? row = selected.Count > 0 ? selected[0] : null;
        Menu = FileHistoryDialog.Menu(selected.Count, row is not null && ArtificialCommits.IsArtificial(row.Hash),
            row?.Hash == ArtificialCommits.WorkTreeHash,
            File.Exists(Path.Combine(_repositoryPath, Dialog.FileName.ToNativePath())));
    }

    /// <summary>
    ///  The grid reads the filter once it shows, and selects the caller's commit at first, then the one selected before
    ///  (upstream's grid keeps its selection), else the newest.
    /// </summary>
    public async Task ApplyGridFilterAsync(RevisionFilter filter)
    {
        await Commits.ApplyFilterAsync(filter);
        if (!IsHistoryVisible)
        {
            return;
        }

        if (Commits.RepositoryPath is null)
        {
            await Commits.OpenAsync(_repositoryPath);
        }

        _services.GridReloaded?.Invoke();
        string? select = _selectedOnce ? _selectedHash : _revision;
        _selectedOnce = true;
        CommitListItem? selected = select is null
            ? null
            : Commits.VisibleRows.FirstOrDefault(item =>
                item.Row.Hash.StartsWith(select, StringComparison.OrdinalIgnoreCase));
        SelectRowRequested?.Invoke(this, selected ?? Commits.VisibleRows.FirstOrDefault());
    }

    /// <summary>
    ///  Upstream's Blame_CommandClick "gotocommit": the commit is selected in the grid, or upstream's message says it is
    ///  filtered out.
    /// </summary>
    public Task GoToCommitAsync(string hash) => GuardAsync(() => SelectCommitAsync(hash));

    /// <summary>
    ///  Upstream's Blame_CommandClick for "gotocommit", "gotobranch" and "gototag" of the Blame tab's commit information.
    /// </summary>
    public Task GoToLinkedRevisionAsync(string revision) => GuardAsync(async () =>
    {
        string? hash = ObjectId.TryParse(revision, out ObjectId id)
            ? id.ToString()
            : await RepositoryScriptContext.ResolveCommitAsync(_repositoryPath, revision);
        if (hash is not null)
        {
            await SelectCommitAsync(hash);
        }
    });

    /// <summary>
    ///  Upstream's <c>OpenFilesWithDiffTool</c>: "Open with difftool" (DiffAB: a commit against its parent, or the two
    ///  selected against each other) and "Difftool selected &lt; - &gt; local" (DiffBLocal), with the default tool or the
    ///  chosen one.
    /// </summary>
    public Task OpenWithDifftoolAsync(RevisionDiffKind kind, string? tool) => GuardAsync(async () =>
    {
        IReadOnlyList<CommitRow> selected = Selection.Revisions;
        if (selected.Count == 0)
        {
            return;
        }

        string? oldFileName = Commits.FilePaths.TryGetValue(selected[0].Hash, out string? name) ? name : null;
        string fileName = Dialog.FileName;
        if (await Task.Run(() => FileHistoryDialog.OpenWithDifftool(_module, selected, fileName, oldFileName, kind,
                tool)) is { } error)
        {
            Host.ShowError(new InvalidOperationException(error));
        }
    });

    partial void OnSelectedTabIndexChanged(int value) => Run(ShowSelectedAsync);

    // Upstream's LoadFileHistory: the grid shows, filtered to the file, with the full history as the settings say.
    [RelayCommand]
    private Task LoadHistoryAsync() => GuardAsync(LoadFileHistoryAsync);

    [RelayCommand]
    private void ToggleOption(FileHistoryOption option)
    {
        FileHistoryOptions options = _preferences.FileHistory;
        (FileHistoryOptions changed, bool reload) = option switch
        {
            FileHistoryOption.LoadHistoryOnShow => (options with { LoadHistoryOnShow = !options.LoadHistoryOnShow }, false),
            FileHistoryOption.LoadBlameOnShow => (options with { LoadBlameOnShow = !options.LoadBlameOnShow }, false),
            FileHistoryOption.FullHistory => (options with { FullHistory = !options.FullHistory }, true),

            // Upstream reloads after a merge simplification change only while the full history is shown.
            FileHistoryOption.SimplifyMerges => (options with { SimplifyMerges = !options.SimplifyMerges },
                options.FullHistory),
            FileHistoryOption.FollowRenames => (options with { FollowRenames = !options.FollowRenames }, true),
            _ => (options with { ExactRenamesOnly = !options.ExactRenamesOnly }, true),
        };
        _preferences.FileHistory = changed;
        _preferences.Save();
        RefreshOptions();
        if (reload)
        {
            Run(LoadFileHistoryAsync);
        }
    }

    [RelayCommand]
    private Task ToggleBlameOptionAsync(BlameOption option)
        => GuardAsync(() => _services.ChangeBlameOptions(_preferences.BlameOptions.Toggle(option)));

    // Upstream's saveAsToolStripMenuItem_Click: the file as of the commit, saved where the user chooses.
    [RelayCommand]
    private Task SaveAsAsync() => GuardAsync(async () =>
    {
        if (Commits.Selected is not { } row)
        {
            return;
        }

        string fileName = FileNameIn(row);
        string fullName = Path.Combine(_repositoryPath, fileName.ToNativePath());
        if (await _services.PickSaveFile(Path.GetFileName(fullName), Path.GetDirectoryName(fullName) ?? _repositoryPath)
            is { } path)
        {
            await Task.Run(() => _module.SaveBlobAs(path, $"{row.Hash}:\"{fileName}\""));
        }
    });

    [RelayCommand]
    private Task CherryPickAsync() => RunOnSelectedAsync(_services.CherryPick);

    [RelayCommand]
    private Task RevertAsync() => RunOnSelectedAsync(_services.Revert);

    private Task RunOnSelectedAsync(Func<string, string, Task> action) => GuardAsync(async () =>
    {
        if (Selection.Revisions is [{ } row] && !ArtificialCommits.IsArtificial(row.Hash))
        {
            await action(_repositoryPath, row.Hash);
        }
    });

    private async Task LoadFileHistoryAsync()
    {
        IsHistoryVisible = true;
        if (Dialog.FileName.Length == 0)
        {
            return;
        }

        // Upstream's SetAndApplyPathFilter keeps the grid's other filters.
        FileHistoryOptions options = _preferences.FileHistory;
        await ApplyGridFilterAsync(Commits.Filter with
        {
            PathFilter = Dialog.FileName,
            FullHistory = options.FullHistory,
            SimplifyMerges = options.SimplifyMerges,
        });
    }

    // Upstream's UpdateSelectedFileViewers: the file's name in the commit, the title, the tabs the commit has, and the
    // selected tab's content.
    private async Task ShowSelectedAsync()
    {
        if (Commits.Selected is not { } row)
        {
            return;
        }

        string fileName = FileNameIn(row);
        bool isArtificial = ArtificialCommits.IsArtificial(row.Hash);
        bool isFolder = fileName.EndsWith('/');
        bool fileAvailable = !isFolder && await Task.Run(() => isArtificial
            ? File.Exists(Path.Combine(_repositoryPath, fileName.ToNativePath()))
            : !_module.GetFileBlobHash(fileName, ObjectId.Parse(row.Hash)).IsZero);
        if (!ReferenceEquals(row, Commits.Selected))
        {
            return;
        }

        Title = Dialog.Title(PathUtil.GetDisplayPath(_repositoryPath), fileName);
        FileHistoryTabs tabs = Dialog.Tabs(isArtificial, fileAvailable, isFolder, fileName);
        CommitTabText = tabs.CommitCaption.StartsWith(FileHistoryDialog.CommitTabText, StringComparison.Ordinal)
            ? Host.Translate(FileHistoryDialog.CommitTabText) + tabs.CommitCaption[FileHistoryDialog.CommitTabText.Length..]
            : tabs.CommitCaption;
        IsCommitTabVisible = tabs.Visible.Contains(FileHistoryTab.Commit);
        IsDiffTabVisible = tabs.Visible.Contains(FileHistoryTab.Diff);
        IsViewTabVisible = tabs.Visible.Contains(FileHistoryTab.View);
        IsBlameTabVisible = tabs.Visible.Contains(FileHistoryTab.Blame);

        // The Build Report tab shows the build's report, not the file.
        if (SelectedTabIndex == BuildReportTabIndex)
        {
            return;
        }

        if (!tabs.Visible.Contains(SelectedTab) && tabs.Preferred is { } preferred)
        {
            SelectedTabIndex = (int)preferred;
        }

        (string? hash, bool staged) = row.Hash == ArtificialCommits.WorkTreeHash ? (null, false)
            : row.Hash == ArtificialCommits.IndexHash ? (null, true)
            : (row.Hash, false);
        FileHistoryTab tab = SelectedTab;
        if (tab == FileHistoryTab.Commit ? hash is not null : tabs.Visible.Contains(tab))
        {
            // Upstream's FileStatusItem(firstRev: the earliest selected, secondRev: the latest selected).
            CommitRow? compareWith = tab == FileHistoryTab.Diff && Selection.Revisions is [_, .., { } first] ? first : null;
            await _services.ShowContent(new FileHistoryContent(tab, row, fileName, hash, staged, compareWith));
        }
    }

    private async Task SelectCommitAsync(string hash)
    {
        if (Commits.VisibleRows.FirstOrDefault(row => row.Row.Hash == hash) is { } item)
        {
            SelectRowRequested?.Invoke(this, item);
            return;
        }

        string shortHash = ObjectId.TryParse(hash, out ObjectId id) ? id.ToShortString() : hash;
        await Host.ShowMessageAsync(string.Format(Host.Translate(RevisionFiltered), shortHash),
            Host.Translate(RevisionFilteredCaption));
    }

    private string FileNameIn(CommitRow row)
        => Commits.FilePaths.TryGetValue(row.Hash, out string? name) ? name : Dialog.FileName;

    // The copy submenu's entries (upstream's CopyContextMenuItem): an entry's value on the clipboard.
    [RelayCommand]
    private Task CopyTextAsync(string? text)
        => text is not null && _services.CopyText is { } copy ? copy(text) : Task.CompletedTask;
}
