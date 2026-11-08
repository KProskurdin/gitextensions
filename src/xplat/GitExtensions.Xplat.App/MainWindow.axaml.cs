using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GitCommands;
using GitCommands.Git;
using GitCommands.Remotes;
using GitCommands.Settings;
using GitCommands.Utils;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Plugins;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;
using GitUI.ScriptsEngine;
using GitUIPluginInterfaces;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The browse window: the new shell's version of upstream <c>FormBrowse</c>.
/// </summary>
public partial class MainWindow : Window
{
    // Developer aid: when set, the window loads XPLAT_BENCHMARK_COMMITS commits (default 5000), scrolls through them, writes
    // frame times to this file, and the app exits (ScrollBenchmark).
    private const string BenchmarkEnvironmentVariable = "XPLAT_BENCHMARK";
    private const string BenchmarkCommitsEnvironmentVariable = "XPLAT_BENCHMARK_COMMITS";
    private const int DefaultBenchmarkCommits = 5000;
    private const string RemoteName = "origin";

    // Upstream's FormBisect._bisectStart.
    private const string BisectStartRange = "Mark selected revisions as start bisect range?";

    // Activation refreshes the working-tree status at most this often, so switching windows back and forth stays cheap.
    private static readonly TimeSpan _activationRefreshInterval = TimeSpan.FromSeconds(2);

    private readonly GitDiscoveryResult? _git;
    private readonly GitCommitHistory _history;
    private readonly CommitListViewModel _commits;
    private readonly NavigationHistory _navigationHistory = new();
    private readonly GitRepositoryService _repositoryService;
    private readonly RepositoryViewModel _repository;

    private readonly RepositoryOperationsViewModel _actions = new(new GitOperations(AppServices.AskPassExecutable,
        () => AppServices.Preferences.ShowErrorsWhenStagingFiles));

    private readonly RecentRepositoriesViewModel _recent = new(AppServices.RecentRepositories,
        path => AppServices.Preferences.ShowRepoCurrentBranch ? RecentRepositoryBranches.CurrentBranch(path) : null);

    private readonly ICommitMessageStore _commitMessages = AppServices.CommitMessages;
    private Task _commitDraftSaved = Task.CompletedTask;
    private readonly IAppPreferences _preferences = AppServices.Preferences;
    private readonly IProcessLauncher _launcher = new SystemProcessLauncher();
    private readonly IFileManager _fileManager;
    private CommitWindow? _commitWindow;
    private ProcessWindow? _processWindow;
    private GitActionBanner? _actionBanner;

    private ScriptHost? _scriptHost;
    private readonly PluginHost _plugins;
    private IReadOnlyList<IGitPlugin>? _loadedPlugins;

    // The repository the plugins are registered with; empty for the dashboard, null before the first registration.
    private string? _pluginRepository;

    // The commit whose tree the File tree tab shows, so the tree is read again only when the selection moves.
    private string? _fileTreeHash;

    // The repository openings not finished yet (IsBusy).
    private int _openings;
    private int _fileTreeVersion;
    private DateTime _lastActivationRefresh = DateTime.MinValue;

    public MainWindow(GitDiscoveryResult? git)
    {
        _git = git;

        // Upstream's FileViewer settings button opens the settings dialog on its page.
        DiffView.ShowSettings = (owner, page) => ShowSettingsAsync(owner: owner, page: page);
        _history = new GitCommitHistory(
            () => new CommitDateStyle(_preferences.RelativeDate, _preferences.ShowAuthorDate),
            () => _preferences.RevisionSortOrder, AppServices.RevisionLinks,
            showArtificialCommits: () => _preferences.RevisionGraphShowArtificialCommits,
            showStashes: () => _preferences.ShowStashes, gridView: () => _preferences.GridViewOptions);
        _repositoryService = new GitRepositoryService(RefSorting);
        _commits = new CommitListViewModel(_history);
        _repository = new RepositoryViewModel(_repositoryService);
        _actions.EditorCommand = AppServices.EditorCommand;
        _fileManager = new SystemFileManager(_launcher, HostPlatform.Current);
        _plugins = new PluginHost(this, _actions, plugin => Run(() => ShowSettingsAsync(plugin, pluginsPage: true)),
            ShowCommitWindow);
        InitializeComponent();
        WindowPlacementTracker.Attach(this, "Xplat.MainWindow");

        ShowSortMenus();
        ShowGridTooltips();
        ShowAvatarSettings();
        _ = _commits.ApplyFilterAsync(StoredRevisionFilter.Initial(_preferences));
        WireToolbar();
        WireMenus();
        WireLeftPanel();
        WireCommitList();
        WireGridView();

        _commits.PropertyChanged += (_, e) => OnCommitsChanged(e.PropertyName);
        _repository.PropertyChanged += (_, e) => OnRepositoryChanged(e.PropertyName);
        _actions.PropertyChanged += (_, e) => OnActionsChanged(e.PropertyName);
        _actions.RepositoryChanged += (_, e) => OnActionChangedRepository(e.RepositoryPath);
        _actions.RemoteOperationStarted += (_, _) => ShowProcessWindow();
        _recent.PropertyChanged += (_, e) => OnRecentChanged(e.PropertyName);
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        Opened += (_, _) => Run(OnOpenedAsync);
        Activated += (_, _) => RefreshStatusOnActivation();
        Closing += (_, _) => _preferences.Save();
        Closed += (_, _) =>
        {
            _plugins.Unregister();
            StopBuildServer();
        };
        WireBuildStatus();
        WireRepositoryHosts();
        _plugins.PostRepositoryChanged += (_, _) => RefreshRepository();
        ShowGitProblem();
        Hotkeys.Load(_preferences.SerializedHotkeys);
        ShowHotkeys();
        ShowRepositoryPanels();
        ShowUserScripts();
        ShowGridScripts();
        UpdateBusyState();
    }

    private string? RepositoryPath => _commits.RepositoryPath;

    // The branch of the node selected in the left panel; null for a group or folder node.
    private BranchInfo? SelectedBranch => SelectedRepoObject?.Branch;

    private void WireToolbar()
    {
        RefreshButton.Click += (_, _) => RefreshRepository();
        CommitDialogButton.Click += (_, _) => ShowCommitWindow();
        PullButton.Click += (_, _) => Pull();
        PushButton.Click += (_, _) => Push();
        OpenFolderButton.Click += (_, _) => RunOnRepositoryFolder(path => _fileManager.OpenFolder(path));
        WireMainToolStrip();
        TrackSplitters();
        WireFilterToolStrip();
        LoadMoreButton.Click += (_, _) => Run(_commits.LoadMoreAsync);
        ResolveConflictsButton.Click += (_, _) => ShowConflicts();
        ContinueActionButton.Click += (_, _) => ContinueAction();
        AbortActionButton.Click += (_, _) => AbortAction();
        MoreActionButton.Click += (_, _) =>
        {
            // Upstream's More... of a rebase opens FormRebase (StartTheContinueRebaseDialog), of a patch FormApplyPatch
            // (StartApplyPatchDialog), which shows the stopped git am's patches with Skip, Resolved and Abort.
            switch (_actionBanner?.Action)
            {
                case GitAction.Rebase:
                    RunOnRepository(path => ShowRebaseAsync(path, onto: null));
                    break;
                case GitAction.Patch:
                    RunOnRepositoryFolder(ShowApplyPatch);
                    break;
            }
        };
        BisectMoreButton.Click += (_, _) => Run(ShowBisectAsync);
    }

    private void WireMenus()
    {
        OpenMenuItem.Click += (_, _) => Run(PickAndOpenRepositoryAsync);
        CloneMenuItem.Click += (_, _) => Run(CloneAsync);
        InitMenuItem.Click += (_, _) => Run(PickAndInitRepositoryAsync);
        CloseRepositoryMenuItem.Click += (_, _) => CloseRepository();
        ExitMenuItem.Click += (_, _) => Close();
        RefreshMenuItem.Click += (_, _) => RefreshRepository();
        RemoteRepositoriesMenuItem.Click += (_, _) => Run(() => ShowRemotesAsync(this));
        FileExplorerMenuItem.Click += (_, _) => RunOnRepositoryFolder(path => _fileManager.OpenFolder(path));
        TerminalMenuItem.Click += (_, _) => OpenTerminal(TerminalButton.Tag as TerminalChoice);
        FilesMenuItem.Click += (_, _) => ShowFiles(_commits.Selected?.Hash ?? "HEAD");
        ReflogMenuItem.Click += (_, _) => Run(ShowReflogAsync);
        GitIgnoreMenuItem.Click += (_, _) => Run(() => EditGitIgnoreAsync(localExclude: false));
        GitInfoExcludeMenuItem.Click += (_, _) => Run(() => EditGitIgnoreAsync(localExclude: true));
        UpdateAllSubmodulesMenuItem.Click += (_, _) => RunOnRepository(path => UpdateSubmodulesAsync(path, null));
        SyncAllSubmodulesMenuItem.Click += (_, _) =>
            RunOnRepository(path => _actions.SyncSubmodulesAsync(path, path: null));
        WorktreesMenuItem.Click += (_, _) => Run(ShowWorktreesAsync);
        CommitMenuItem.Click += (_, _) => ShowCommitWindow();
        PullDialogMenuItem.Click += (_, _) => Run(ShowPullDialogAsync);
        PushDialogMenuItem.Click += (_, _) => Run(() => ShowPushDialogAsync());
        ManageStashesCommandMenuItem.Click += (_, _) => Run(() => ShowStashWindowAsync(this));
        ResetChangesMenuItem.Click += (_, _) => RunOnRepository(ResetChangesAsync);
        CreateBranchMenuItem.Click += (_, _) => Run(() => PromptCreateBranchAsync(_commits.Selected?.Hash));
        CheckoutBranchMenuItem.Click += (_, _) => RunOnRepository(path => StartCheckoutBranchAsync(path));
        CheckoutRevisionMenuItem.Click +=
            (_, _) => RunOnRepository(path => ShowCheckoutRevisionAsync(path, revision: null));
        MergeMenuItem.Click += (_, _) => RunOnRepository(path => ShowMergeAsync(path, branch: null));
        RebaseMenuItem.Click += (_, _) => ShowRebaseOnSelectedCommit();
        CreateTagMenuItem.Click += (_, _) => Run(() => ShowCreateTagAsync(_commits.Selected?.Hash ?? "HEAD"));
        BisectMenuItem.Click += (_, _) => Run(ShowBisectAsync);
        ResolveConflictsMenuItem.Click += (_, _) => ShowConflicts();
        CommandLogMenuItem.Click += (_, _) => CommandLogWindow.ShowFor(this);
        SettingsMenuItem.Click += (_, _) => Run(() => ShowSettingsAsync());
        WireStartMenu();
        WireDeleteRefs();
        WireCommandsMenu();
        foreach ((MenuItem item, GridCommand command) in NavigateMenuCommands())
        {
            item.Click += (_, _) => RunGridHotkey(command);
        }

        PluginSettingsMenuItem.Click += (_, _) => Run(() => ShowSettingsAsync(pluginsPage: true));

        // Upstream's View menu toggles for the grid's date column.
        ShowAuthorDateMenuItem.IsChecked = _preferences.ShowAuthorDate;
        ShowRelativeDateMenuItem.IsChecked = _preferences.RelativeDate;
        ShowAuthorDateMenuItem.Click += (_, _) =>
        {
            _preferences.ShowAuthorDate = !_preferences.ShowAuthorDate;
            ShowAuthorDateMenuItem.IsChecked = _preferences.ShowAuthorDate;
            RefreshRepository();
        };
        ShowRelativeDateMenuItem.Click += (_, _) =>
        {
            _preferences.RelativeDate = !_preferences.RelativeDate;
            ShowRelativeDateMenuItem.IsChecked = _preferences.RelativeDate;
            RefreshRepository();
        };
        WireGraphStyles();
        WireQuickSearch();
        WireMouseNavigation();
        AboutMenuItem.Click += (_, _) =>
            _ = new AboutWindow(_git?.Version?.ToString()).ShowDialog(this);
    }

    private void WireLeftPanel()
    {
        WireRepoObjectsTree();
        WireDashboard();
    }

    private void WireCommitList()
    {
        WireDiffFileList();
        DiffFilesFilterBox.WorkingDirectory = () => RepositoryPath;
        WireDiffGitGrep();
        WireFileTreeGitGrep();
        WireDiffFileSettings();
        DetailTabs.SelectionChanged += (_, _) =>
        {
            ShowGitGrepBoxOfTab();
            ShowFileTreeIfVisible();
        };
        WireCommitInfo();
        FileTreeView.SelectionChanged += (_, _) => Run(ShowSelectedTreeFileAsync);
        CommitList.SelectionChanged += (_, e) =>
        {
            TrackGridSelection(e);
            CommitDiff.Clear();

            // As upstream's OnGridViewSelectionChanged: the history follows a single selected commit.
            if (LatestSelectedItem is { } selected && SelectedRevisions.Count == 1)
            {
                _navigationHistory.Push(selected.Row.Hash);
            }

            Run(() => _commits.SelectAsync(LatestSelectedItem?.Row));
            ShowSelectionDiff();
            ShowMergedBranchesOfSelection();
        };
        CommitList.DoubleTapped += (_, _) => ViewSelectedRevision();
        CommitContextMenu.Opening += (_, _) => ShowGridScripts();
        WireGridMenu();
        WireDiffFileMenu();
        WireFileTreeMenu();
        WireDiffTabBlame();
        WireDiffLinePatching();
        CommitFilesList.SelectionChanged += (_, _) => ShowSelectedCommitFileDiff();

        // Upstream's DiffFiles_DoubleClick: the file's history at the selected commit.
        CommitFilesList.DoubleTapped += (_, _) =>
        {
            if (_preferences.OpenSubmoduleDiffInSeparateWindow
                && CommitFilesList.SelectedItem is CommitFile file &&
                (file.IsSubmodule || file.DiffFile?.IsSubmodule == true))
            {
                CommitFilesContextMenu.RunDoubleClick();
            }
            else
            {
                RunBrowseDiffHotkey(BrowseDiffCommand.ShowHistory);
            }
        };
        ShowCommitDiffButton.Click += (_, _) => ShowCommitDiff();
        CommitDiffToolButton.Click += (_, _) => Run(OpenCommitFileInDiffToolAsync);
        DiffToolChoices.Attach(CommitDiffToolChoiceButton, tool => OpenCommitFileInDiffToolAsync(tool),
            message => ShowError(message));
        CreateBranchHereMenuItem.Click += (_, _) => Run(() => PromptCreateBranchAsync(_commits.Selected?.Hash));
        CreateTagHereMenuItem.Click += (_, _) => Run(() => ShowCreateTagAsync(_commits.Selected?.Hash ?? "HEAD"));
        BrowseFilesHereMenuItem.Click += (_, _) => ShowFiles(_commits.Selected?.Hash ?? "HEAD");
        CheckoutCommitMenuItem.Click +=
            (_, _) => RunOnSelectedCommit((path, row) => ShowCheckoutRevisionAsync(path, row.Hash));
        CherryPickMenuItem.Click += (_, _) => CherryPickSelectedRevision();
        RevertMenuItem.Click += (_, _) => RevertSelectedRevision();
        ResetCurrentBranchMenuItem.Click += (_, _) =>
            RunOnSelectedCommit((path, row) => ShowResetCurrentBranchAsync(this, path, row.Hash, ResetMode.Soft));
        RebaseOnCommitMenuItem.Click += (_, _) => Run(() => RebaseOnSelectedCommitAsync(interactive: false));
        RebaseInteractiveMenuItem.Click += (_, _) => Run(() => RebaseOnSelectedCommitAsync(interactive: true));
        RebaseAdvancedMenuItem.Click += (_, _) =>
            RunOnSelectedCommit((path, row) => ShowRebaseAsync(path, row.Hash, from: RebaseRangeStart()));
        MarkBadMenuItem.Click += (_, _) => MarkSelectedForBisect(GitBisectOption.Bad);
        MarkGoodMenuItem.Click += (_, _) => MarkSelectedForBisect(GitBisectOption.Good);
        BisectSkipMenuItem.Click += (_, _) => MarkSelectedForBisect(GitBisectOption.Skip);
        StopBisectMenuItem.Click += (_, _) => RunOnRepository(path => _actions.StopBisectAsync(path));
    }

    // The File tree tab follows the selected commit (HEAD when none is selected), and is read only while it is on show.
    private void ShowFileTreeIfVisible()
    {
        string hash = _commits.Selected?.Hash ?? "HEAD";
        if (DetailTabs.SelectedItem != FileTreeTab || RepositoryPath is not { } path || hash == _fileTreeHash)
        {
            return;
        }

        _fileTreeHash = hash;
        int version = ++_fileTreeVersion;
        FileContentView.Clear();
        ShowFileTreeBlame(false);
        Task load = LoadFileTreeAsync();
        _fileTreeLoad = load;
        Run(() => load);
        return;

        async Task LoadFileTreeAsync()
        {
            // Upstream's file tree mode with a git-grep search lists the files it matched, expanded while they are few.
            string grep = _fileTreeGrep;
            IReadOnlyList<string> files = grep.Length > 0
                ? await GitGrep.FilesAsync(GitModules.Open(path), hash, grep)
                : await _history.LoadTreeAsync(path, hash);
            IReadOnlySet<string> submodules = await _history.LoadTreeSubmodulesAsync(path, hash);
            if (version == _fileTreeVersion)
            {
                _fileTreeSubmodules = submodules;
                _fileTreeFiles = files;
                ShowFileTreeFiles();
            }
        }
    }

    private async Task ShowSelectedTreeFileAsync()
    {
        // Upstream's ShowSelectedFolderAsync, before blame: a selected folder shows the files below it.
        if (FileTreeView.SelectedItems is { Count: 1 } &&
            FileTreeView.SelectedItem is FileTreeNode { IsFolder: true } folder)
        {
            ShowFileTreeBlame(false);
            (string title, string description) = FileStatusList.FolderDescription(folder.Path,
                [.. folder.Files().Select(node => node.Path)]);
            FileContentView.ShowText(title, description);
            return;
        }

        if (RepositoryPath is not { } path || _fileTreeHash is not { } hash ||
            FileTreeView.SelectedItem is not FileTreeNode { IsFolder: false } file)
        {
            return;
        }

        ShowFileTreeBlame(_fileTreeBlame);
        if (_fileTreeBlame)
        {
            await FileTreeBlame.LoadAsync(path, hash, file.Path);
            return;
        }

        // Upstream's viewer shows the file's matches while the list has a git-grep search (no forceFileView then).
        if (_fileTreeGrep is { Length: > 0 } grep)
        {
            await FileContentView.ShowGrepAsync(file.Path,
                options => TreeFileGrepAsync(path, hash, file.Path, grep, options));
            return;
        }

        await FileContentView.ShowFileAsync(path, hash, staged: false, file.Path);
    }

    private void ShowGitProblem()
    {
        GitProblemText.Text = _git?.Status switch
        {
            GitDiscoveryStatus.NotFound => OperatingSystem.IsMacOS()
                ? "git was not found. Install the Command Line Tools with 'xcode-select --install', then restart."
                : "git was not found. Install git, then restart.",
            GitDiscoveryStatus.TooOld =>
                $"git {_git.Version} is older than the supported minimum. Install a newer git, then restart.",
            _ => "",
        };
        GitProblemText.IsVisible = GitProblemText.Text.Length > 0;
    }

    private void ShowHotkeys()
    {
        OpenMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.OpenRepository);
        CloseRepositoryMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.CloseRepository);
        RefreshMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.Refresh);
        TerminalMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.Terminal);
        CommitMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.Commit);
        PullDialogMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.Pull);
        PushDialogMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.Push);
        StashChangesMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.Stash);
        StashPopToolMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.StashPop);
        CreateBranchMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.CreateBranch);
        CheckoutBranchMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.CheckoutBranch);
        MergeMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.Merge);
        RebaseMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.Rebase);
        CreateTagMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.CreateTag);
        SettingsMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.Settings);
        RevisionInfo.AddNotesGesture = Hotkeys.GestureFor(BrowseCommand.AddNotes);
        foreach ((MenuItem item, GridCommand command) in NavigateMenuCommands())
        {
            item.InputGesture = Hotkeys.Grid.GestureFor(command);
        }

        HighlightSelectedBranchMenuItem.InputGesture =
            Hotkeys.Grid.GestureFor(GridCommand.ToggleHighlightSelectedBranch);
        FixupCommitMenuItem.InputGesture = Hotkeys.Grid.GestureFor(GridCommand.CreateFixupCommit);
        SquashCommitMenuItem.InputGesture = Hotkeys.Grid.GestureFor(GridCommand.CreateSquashCommit);
        AmendCommitMenuItem.InputGesture = Hotkeys.Grid.GestureFor(GridCommand.CreateAmendCommit);
        SelectAsBaseMenuItem.InputGesture = Hotkeys.Grid.GestureFor(GridCommand.SelectAsBaseToCompare);
        OpenCommitsWithDiffToolMenuItem.InputGesture = Hotkeys.Grid.GestureFor(GridCommand.OpenCommitsWithDifftool);
        CompareToBaseMenuItem.InputGesture = Hotkeys.Grid.GestureFor(GridCommand.CompareToBase);
        CompareToWorkingDirectoryMenuItem.InputGesture = Hotkeys.Grid.GestureFor(GridCommand.CompareToWorkingDirectory);
        CompareSelectedCommitsMenuItem.InputGesture = Hotkeys.Grid.GestureFor(GridCommand.CompareSelectedCommits);
    }

    // Event handlers start their async work through this, so an exception that escapes it is shown, not fatal.
    private void Run(Func<Task> action) => UiActions.Run(action, ex => ShowError(ex.Message));

    private async Task OnOpenedAsync()
    {
        // As upstream's Program at startup: without a language, the user chooses one first (FormChooseTranslation), and
        // the window takes it.
        if (string.IsNullOrEmpty(_preferences.Translation))
        {
            await new ChooseTranslationWindow(_preferences).ShowDialog(this);
            GitExtensions.Xplat.Ui.UpstreamTranslation.ApplyByText(this);
        }

        Run(ShowPluginsAsync);
        await _recent.LoadAsync();

        // As upstream's Program: without a repository argument, the one used last when the setting asks for it.
        string? initial = Program.InitialRepository ??
                          (_preferences.StartWithRecentWorkingDir &&
                           _preferences.RecentWorkingDir is { Length: > 0 } recent &&
                           GitModule.IsValidGitWorkingDir(recent)
                              ? recent
                              : null);
        if (initial is null || _git?.Status == GitDiscoveryStatus.NotFound)
        {
            // The dashboard, after the history and the plugins' links show.
            if (Screenshot.RequestedFile is { } dashboardScreenshot)
            {
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                await Task.Delay(TimeSpan.FromMilliseconds(1000));
                await Screenshot.SaveAsync(this, dashboardScreenshot);
                Close();
            }

            return;
        }

        await OpenRepositoryAsync(initial);

        if (Environment.GetEnvironmentVariable(BenchmarkEnvironmentVariable) is { Length: > 0 } benchmark)
        {
            await RunBenchmarkAsync(benchmark);
            return;
        }

        if (Screenshot.RequestedFile is { } screenshot)
        {
            // XPLAT_SCREENSHOT_WINDOW=commit: the commit window instead of the browse window.
            if (Screenshot.RequestedWindow == Screenshot.CommitWindow)
            {
                ShowCommitWindow();
                await Task.Delay(TimeSpan.FromMilliseconds(1500));
                if (_commitWindow is { } commit)
                {
                    await Screenshot.SaveAsync(commit, screenshot);
                    commit.Close();
                }

                Close();
                return;
            }

            // XPLAT_SCREENSHOT_WINDOW=overflow: the browse window narrow enough for its toolstrip to overflow.
            if (Screenshot.RequestedWindow == Screenshot.OverflowWindow)
            {
                WindowState = WindowState.Normal;
                Width = 700;
                await Task.Delay(TimeSpan.FromMilliseconds(1500));
                await Screenshot.SaveAsync(this, screenshot);
                Close();
                return;
            }

            // XPLAT_SCREENSHOT_WINDOW=<dialog>: one of upstream's dialogs on the open repository.
            if (await ScreenshotDialogAsync(Screenshot.RequestedWindow) is { } screenshotDialog)
            {
                screenshotDialog.Show(this);
                await Task.Delay(TimeSpan.FromMilliseconds(1500));

                // git fsck takes longer than the delay in a large repository.
                if (screenshotDialog is VerifyDatabaseWindow verify)
                {
                    while (!verify.ObjectsLoaded.IsCompleted)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(100));
                    }
                }

                await Screenshot.SaveAsync(screenshotDialog, screenshot);
                screenshotDialog.Close();
                Close();
                return;
            }

            // XPLAT_SCREENSHOT_WINDOW=settings[:search]: the settings window, with the text typed into its search box.
            if (Screenshot.RequestedWindow?.Split(':', 2) is [Screenshot.SettingsWindow, .. var search])
            {
                SettingsWindow settings = new(_preferences, RepositoryPath, await LoadPluginsAsync());
                settings.Show(this);
                if (search is [{ } text] && settings.FindControl<TextBox>("SettingsSearchBox") is { } searchBox)
                {
                    searchBox.Text = text;
                    searchBox.RaiseEvent(new KeyEventArgs { RoutedEvent = KeyUpEvent, Key = Key.Enter });
                }

                await Task.Delay(TimeSpan.FromMilliseconds(1500));

                // The Checklist's checks ask git, which can take a few seconds.
                for (int wait = 0;
                     wait < 100 && settings.SelectedPage?.PageTitle == "Checklist" &&
                     settings.ChecklistEntries.Count == 0;
                     wait++)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }

                await Screenshot.SaveAsync(settings, screenshot);
                settings.Close();
                Close();
                return;
            }

            if (CommitList.ItemCount > 0)
            {
                CommitList.SelectedIndex = 0;
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }

            await Screenshot.SaveAsync(this, screenshot);
            Close();
        }
    }

    private async Task RunBenchmarkAsync(string outputFile)
    {
        int commits = int.TryParse(Environment.GetEnvironmentVariable(BenchmarkCommitsEnvironmentVariable),
            out int requested)
            ? requested
            : DefaultBenchmarkCommits;
        System.Diagnostics.Stopwatch load = System.Diagnostics.Stopwatch.StartNew();
        await _commits.LoadAtLeastAsync(commits);
        load.Stop();
        string result = await ScrollBenchmark.RunAsync(this, CommitList, _commits.Rows.Count, load.Elapsed);
        await File.WriteAllTextAsync(outputFile, result + Environment.NewLine);
        Close();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Upstream's FileViewer handles its hotkeys before the controls around it.
        if (DiffView.Focused(e) is { } viewer && viewer.RunHotkey(e))
        {
            e.Handled = true;
            return;
        }

        if (RunTabHotkey(e))
        {
            e.Handled = true;
            return;
        }

        // As upstream, the grid's and the left panel's own hotkeys act while they have the focus.
        if (IsWithin(e.Source, CommitList) && Hotkeys.Grid.Match(e) is { } gridCommand)
        {
            e.Handled = RunGridHotkey(gridCommand);
            return;
        }

        // Upstream's RepoObjectsTree.ProcessHotkey: anywhere in the left panel, in its search box except on the keys that
        // edit the text.
        if (IsWithin(e.Source, LeftPanel) && Hotkeys.LeftPanel.Match(e) is { } leftPanelCommand
                                          && !(IsWithin(e.Source, LeftPanelSearchBox)
                                               && Hotkeys.LeftPanel.GestureFor(leftPanelCommand) is { } gesture
                                               && Hotkeys.IsTextEditKey(Hotkeys.ToWinFormsKeys(gesture))))
        {
            RunLeftPanelHotkey(leftPanelCommand, e.KeyModifiers);
            e.Handled = true;
            return;
        }

        if (Hotkeys.Match(e) is not { } command)
        {
            // As upstream's GitModuleForm, the scripts' hotkeys come after the window's own.
            if (ScriptHost.MatchHotkey(e) is { } script)
            {
                e.Handled = true;
                RunOnRepository(path => Scripts.RunAsync(script, path, SelectedHashes, FileScriptOptions()));
            }

            return;
        }

        // Typing in a text box keeps its own keys (Ctrl+E, Ctrl+B and Ctrl+T are editing keys there too), except F5.
        if (e.Source is TextBox && command != BrowseCommand.Refresh)
        {
            return;
        }

        e.Handled = true;
        switch (command)
        {
            case BrowseCommand.OpenRepository:
                Run(PickAndOpenRepositoryAsync);
                break;
            case BrowseCommand.CloseRepository:
                CloseRepository();
                break;
            case BrowseCommand.Commit:
                ShowCommitWindow();
                break;
            case BrowseCommand.Refresh:
                RefreshRepository();
                break;
            case BrowseCommand.Pull:
                Run(ShowPullDialogAsync);
                break;
            case BrowseCommand.Push:
                Run(() => ShowPushDialogAsync());
                break;
            case BrowseCommand.QuickPull:
                Pull();
                break;
            case BrowseCommand.QuickPush:
                Push();
                break;
            case BrowseCommand.QuickFetch:
                Fetch();
                break;
            case BrowseCommand.Terminal:
                OpenTerminal(TerminalButton.Tag as TerminalChoice);
                break;
            case BrowseCommand.Settings:
                Run(() => ShowSettingsAsync());
                break;
            case BrowseCommand.FocusFilter:
                FilterToolStrip.FocusFilter();
                break;
            case BrowseCommand.FocusRevisionGrid:
                CommitList.Focus();
                break;
            case BrowseCommand.FocusLeftPanel:
                BranchTreeView.Focus();
                break;
            case BrowseCommand.CreateBranch:
                Run(() => PromptCreateBranchAsync(_commits.Selected?.Hash));
                break;
            case BrowseCommand.CheckoutBranch:
                RunOnRepository(path => StartCheckoutBranchAsync(path));
                break;
            case BrowseCommand.CreateTag:
                Run(() => ShowCreateTagAsync(_commits.Selected?.Hash ?? "HEAD"));
                break;
            case BrowseCommand.Merge:
                RunOnRepository(path => ShowMergeAsync(path, branch: null));
                break;
            case BrowseCommand.Rebase:
                ShowRebaseOnSelectedCommit();
                break;
            case BrowseCommand.Stash:
                Run(() => StashAsync());
                break;
            case BrowseCommand.StashPop:
                PopTopStash();
                break;
            case BrowseCommand.FindFileInSelectedCommit:
                Run(FindFileInSelectedCommitAsync);
                break;
            case BrowseCommand.AddNotes:
                AddNotes();
                break;
        }
    }

    // Upstream's Navigate menu items and the grid commands they run.
    private IEnumerable<(MenuItem Item, GridCommand Command)> NavigateMenuCommands() =>
    [
        (ToggleArtificialAndHeadMenuItem, GridCommand.ToggleBetweenArtificialAndHeadCommits),
        (GoToCurrentRevisionMenuItem, GridCommand.SelectCurrentRevision),
        (GoToCommitMenuItem, GridCommand.GoToCommit),
        (GoToChildMenuItem, GridCommand.GoToChild),
        (GoToParentMenuItem, GridCommand.GoToParent),
        (GoToFirstParentMenuItem, GridCommand.GoToFirstParent),
        (GoToLastParentMenuItem, GridCommand.GoToLastParent),
        (GoToMergeBaseMenuItem, GridCommand.GoToMergeBase),
        (NavigateBackwardMenuItem, GridCommand.NavigateBackward),
        (NavigateForwardMenuItem, GridCommand.NavigateForward),
        (PrevQuickSearchMenuItem, GridCommand.PrevQuickSearch),
        (NextQuickSearchMenuItem, GridCommand.NextQuickSearch),
    ];

    // The commands of upstream RevisionGridControl's hotkeys that the grid has; false when one cannot run now.
    private bool RunGridHotkey(GridCommand command)
    {
        CommitRow? selected = _commits.Selected;
        switch (command)
        {
            case GridCommand.GoToParent:
                return SelectGridRow(selected is null ? null : _commits.IndexOfParent(selected));
            case GridCommand.GoToChild:
                return SelectGridRow(selected is null ? null : _commits.IndexOfChild(selected));
            case GridCommand.SelectCurrentRevision:
                return SelectGridRow(_commits.IndexOfHead());
            case GridCommand.RevisionFilter:
                Run(EditRevisionFilterAsync);
                return true;
            case GridCommand.ResetRevisionFilter:
                ApplyFilter(_commits.Filter.ResetAll());
                return true;
            case GridCommand.ResetRevisionPathFilter:
                ApplyFilter(_commits.Filter with { PathFilter = "" });
                return true;
            case GridCommand.ShowAllBranches:
                ShowBranches(currentOnly: false, filtered: false);
                return true;
            case GridCommand.ShowCurrentBranchOnly:
                ShowBranches(currentOnly: true, filtered: false);
                return true;
            case GridCommand.ToggleHideMergeCommits:
                Run(() => ApplyRevisionFilterAsync(_commits.Filter with { NoMerges = !_commits.Filter.NoMerges }));
                return true;
            case GridCommand.ShowFirstParent:
                Run(() => ApplyRevisionFilterAsync(_commits.Filter with
                {
                    FirstParent = !_commits.Filter.FirstParent
                }));
                return true;
            case GridCommand.ToggleHighlightSelectedBranch when selected is not null:
                _commits.HighlightBranch(selected);
                return true;
            case GridCommand.NextQuickSearch:
                return QuickSearchNext(down: true);
            case GridCommand.PrevQuickSearch:
                return QuickSearchNext(down: false);
            case GridCommand.GoToFirstParent:
                return SelectGridRow(selected is null ? null : _commits.IndexOfParent(selected));
            case GridCommand.GoToLastParent:
                return SelectGridRow(selected is null ? null : _commits.IndexOfLastParent(selected));
            case GridCommand.ToggleBetweenArtificialAndHeadCommits:
                return SelectGridRow(_commits.IndexOfNextArtificialOrHead(selected));
            case GridCommand.NavigateBackward:
                return Navigate(_navigationHistory.NavigateBackward());
            case GridCommand.NavigateForward:
                return Navigate(_navigationHistory.NavigateForward());
            case GridCommand.GoToCommit when RepositoryPath is not null:
                Run(GoToCommitAsync);
                return true;
            case GridCommand.GoToMergeBase when selected is not null:
                Run(GoToMergeBaseAsync);
                return true;
            case GridCommand.CreateFixupCommit:
                return StartSpecialCommit(CommitKind.Fixup);
            case GridCommand.CreateSquashCommit:
                return StartSpecialCommit(CommitKind.Squash);
            case GridCommand.CreateAmendCommit:
                return StartSpecialCommit(CommitKind.Amend);
            case GridCommand.SelectAsBaseToCompare:
                return SelectAsBaseToCompare();
            case GridCommand.CompareToBase:
                return CompareToBase();
            case GridCommand.CompareToWorkingDirectory:
                return CompareToWorkingDirectory();
            case GridCommand.CompareToCurrentBranch:
                return CompareWithCurrentBranch();
            case GridCommand.CompareToBranch:
                return CompareToBranch();
            case GridCommand.CompareSelectedCommits:
                return CompareSelectedCommits();
            case GridCommand.OpenCommitsWithDifftool:
                return DiffSelectedCommitsWithDifftool(tool: null);
            default:
                return false;
        }
    }

    private bool SelectGridRow(int? index)
    {
        if (index is not { } row)
        {
            return false;
        }

        CommitList.SelectedIndex = row;
        CommitList.ScrollIntoView(row);
        return true;
    }

    // Upstream's NavigateBackward and NavigateForward: the commit selected before or after, or upstream's message when the
    // filters hide it.
    private bool Navigate(string? hash)
    {
        if (hash is null)
        {
            return false;
        }

        if (!SelectGridRow(_commits.IndexOfHash(hash)))
        {
            ShowRevisionFiltered(hash);
        }

        return true;
    }

    private static bool IsWithin(object? source, Visual container)
        => source is Visual visual && (visual == container || container.IsVisualAncestorOf(visual));

    private void DeleteSelectedBranch() => RunOnSelectedBranch(DeleteSelectedBranchAsync);

    private void RefreshRepository()
    {
        if (RepositoryPath is { } path)
        {
            Run(() => OpenRepositoryAsync(path));
        }
    }

    private async Task EditRevisionFilterAsync()
    {
        if (await new FilterWindow(_commits.Filter, _preferences.MaxRevisionGraphCommits)
                .ShowDialog<RevisionFilter?>(this)
            is { } filter)
        {
            StoredRevisionFilter.StoreDialogChoice(filter, _preferences);
            await ApplyRevisionFilterAsync(filter);
        }
    }

    // Upstream's FilterInfo keeps its options in settings: hide merge commits, first parent and simplify by decoration are
    // stored on each change; the full history and merge simplification are the settings the file history shares, which
    // only the filter window changes from here.
    private Task ApplyRevisionFilterAsync(RevisionFilter filter)
    {
        StoredRevisionFilter.Store(filter, _preferences);
        return _commits.ApplyFilterAsync(StoredRevisionFilter.WithStoredHistory(filter, _preferences));
    }

    private async Task OpenRepositoryAsync(string path)
    {
        // A reload can move HEAD, so the tree is read again; another repository starts a new navigation history.
        _fileTreeHash = null;
        if (!string.Equals(path, RepositoryPath, StringComparison.Ordinal))
        {
            _navigationHistory.Clear();
        }

        _openings++;
        try
        {
            await _commits.OpenAsync(path);
            if (RepositoryPath is { } repositoryPath)
            {
                ShowRepositoryPanels();
                await _repository.RefreshAsync(repositoryPath);
                await _recent.AddAsync(repositoryPath);
                if (_showUntrackedReadFor != repositoryPath)
                {
                    _showUntrackedReadFor = repositoryPath;
                    await ReadShowUntrackedFilesAsync(repositoryPath);
                }

                // Upstream's RecentWorkingDir, which the next start can open.
                _preferences.RecentWorkingDir = repositoryPath;
            }

            RegisterPlugins();
        }
        finally
        {
            _openings--;
        }

        await LaunchBuildServerAsync();
    }

    private void CloseRepository()
    {
        if (RepositoryPath is null)
        {
            return;
        }

        _commitWindow?.Close();
        _commits.Close();
        _repository.Clear();
        CommitDiff.Clear();
        _fileTreeHash = null;
        FileTreeView.ItemsSource = null;
        FileContentView.Clear();
        ShowRepositoryPanels();
        UpdateBusyState();
        RegisterPlugins();
        StopBuildServer();
    }

    // The dashboard of recent repositories stands in for the grid while no repository is open.
    private void ShowRepositoryPanels()
    {
        bool open = RepositoryPath is not null;
        BrowsePanel.IsVisible = open;
        DashboardPanel.IsVisible = !open;
        LeftPanel.IsVisible = open;

        // Upstream's menus for the dashboard: its Dashboard menu instead of the repository's menus.
        DashboardMenu.IsVisible = !open;
        RepositoryMainMenu.IsVisible = NavigateMenu.IsVisible = ViewMenu.IsVisible = open;
        CommandsMenu.IsVisible = PluginsMenu.IsVisible = open;
    }

    private void OnRecentChanged(string? propertyName)
    {
        if (propertyName == nameof(RecentRepositoriesViewModel.Items))
        {
            ShowRecentMenu();
        }
    }

    private async Task PickAndOpenRepositoryAsync()
    {
        if (await PickFolderAsync("Open repository") is { } folder)
        {
            await OpenRepositoryAsync(folder);
        }
    }

    private async Task PickAndInitRepositoryAsync()
    {
        if (await PickFolderAsync("Create a repository in this folder") is { } folder)
        {
            await _actions.InitAsync(folder);
        }
    }

    private async Task<string?> PickFolderAsync(string title)
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    // Launching a file manager or terminal is a local action; a failure is shown like any other error.
    private void RunOnRepositoryFolder(Action<string> action)
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        try
        {
            action(path);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    // Upstream's StartCloneDialog from the browse window: the clone window asks whether to open the clone here.
    private Task CloneAsync() => NewCloneWindow().ShowDialog<bool>(this);

    private CloneWindow NewCloneWindow()
        => new(RepositoryPath, _actions, _preferences, [.. _recent.Items.Select(item => item.Path)], _recent.AddAsync,
            OpenRepositoryAsync);

    // Pull and push use the remote the branch tracks, falling back to origin for a branch without one.
    private string TrackingRemote() => _repository.TrackingRemote ?? RemoteName;

    // Upstream's toolbar pull runs the default pull action (Settings > General): the pull dialog, a merge or rebase pull, or
    // a fetch of the branch's remote or of all remotes (with prune). The Rebase box is the merge or rebase choice.
    private void Pull()
    {
        GitPullAction action = _preferences.DefaultPullAction;
        if (action is GitPullAction.None or GitPullAction.Default)
        {
            Run(ShowPullDialogAsync);
            return;
        }

        if (action is GitPullAction.Fetch or GitPullAction.FetchAll or GitPullAction.FetchPruneAll)
        {
            string remote = action == GitPullAction.Fetch ? TrackingRemote() : "--all";
            RunOnRepository(async path => await LoadPuttyKeysAsync(path, remote)
                                          && await WithScriptsAsync(path, ScriptEvent.BeforeFetch,
                                              ScriptEvent.AfterFetch,
                                              () => _actions.FetchAsync(path, remote,
                                                  prune: action == GitPullAction.FetchPruneAll)));
            return;
        }

        RunOnCurrentBranch(async (path, branch) => await LoadPuttyKeysAsync(path, TrackingRemote())
                                                   && await WithScriptsAsync(path, ScriptEvent.BeforePull,
                                                       ScriptEvent.AfterPull,
                                                       () => PullAndUpdateSubmodulesAsync(path,
                                                           () => _actions.PullAsync(path, TrackingRemote(), branch,
                                                               action == GitPullAction.Rebase),
                                                           TrackingRemote())));
    }

    private void Push() => RunOnCurrentBranch(async (path, branch) =>
        await ConfirmPushAsync(TrackingRemote(), branch)
        && await LoadPuttyKeysAsync(path, TrackingRemote())
        && await WithScriptsAsync(path, ScriptEvent.BeforePush, ScriptEvent.AfterPush,
            () => PushFlow.PushAsync(ProcessOwner, path,
                new PushRequest(TrackingRemote(), branch, "", ForcePushOptions.DoNotForce, Track: true),
                new PushOrigin(branch, branch, _repository.TrackingRemote))));

    // Upstream's quick fetch (Fetch in the pull menu), and its fetch-all and prune variants; a prune asks first.
    private void Fetch(string remote = RemoteName, bool prune = false) =>
        RunOnRepository(async path =>
            (!prune || await ConfirmPruneAsync(remote))
            && await LoadPuttyKeysAsync(path, remote)
            && await WithScriptsAsync(path, ScriptEvent.BeforeFetch, ScriptEvent.AfterFetch,
                () => _actions.FetchAsync(path, remote, prune)));

    // As upstream's FormPush: a branch the remote does not have yet (as far as the fetched remote branches show) is pushed
    // after a question.
    private async Task<bool> ConfirmPushAsync(string remote, string remoteBranch)
        => _repository.Branches.Any(known => known.IsRemote && known.Name == $"{remote}/{remoteBranch}")
           || await ConfirmAsync(Confirmation.PushNewBranch, Confirmations.PushNewBranchQuestion, "Push", "Push");

    // As upstream's pull dialog: a fetch that prunes remote-tracking branches asks first.
    private Task<bool> ConfirmPruneAsync(string remote)
        => ConfirmAsync(Confirmation.FetchAndPrune, Confirmations.FetchAndPruneQuestion, "Fetch and prune",
            $"Prune remote branches from {remote}");

    private ScriptHost Scripts =>
        _scriptHost ??= new ScriptHost(this, _actions, SelectRefAsync, _plugins.ExecuteByName);

    // The commit selected in the grid, for the scripts' {s...} options.
    private IReadOnlyList<string> SelectedHashes => _commits.Selected is { } row ? [row.Hash] : [];

    // As upstream's forms: the "before" scripts run first and can stop the operation; the "after" scripts run once it
    // succeeded. Around them, as upstream's DoActionOnRepo, a plugin can cancel the action, and hears how it ended.
    private async Task<bool> WithScriptsAsync(string path, ScriptEvent before, ScriptEvent after,
        Func<Task<bool>> operation, PluginEvent? pluginEvent = null)
    {
        if (pluginEvent is { } cancellable && !_plugins.RaisePre(cancellable))
        {
            return false;
        }

        bool done = false;
        try
        {
            if (!await Scripts.RunEventAsync(before, path, SelectedHashes))
            {
                return false;
            }

            done = await operation();
            if (done)
            {
                await Scripts.RunEventAsync(after, path, SelectedHashes);
            }

            return done;
        }
        finally
        {
            if (pluginEvent is { } finished)
            {
                _plugins.RaisePost(finished, done);
            }
        }
    }

    // Upstream's user menu bar: one button per enabled script whose event is "show in user menu bar".
    // Upstream's ShowAuthorAvatarColumn and ShowAuthorAvatarInCommitInfo; upstream needs a restart, here they apply when the
    // settings are saved.
    private void ShowAvatarSettings()
    {
        Resources["ShowAuthorAvatarColumn"] = _preferences.ShowAuthorAvatarColumn;
        RevisionInfo.ShowAvatar();
    }

    // Upstream's ShowRevisionGridTooltips; upstream needs a restart, here it applies when the settings are saved.
    private void ShowGridTooltips()
    {
        ToolTip.SetServiceEnabled(CommitList, _preferences.ShowRevisionGridTooltips);
        RefLabelView.SetShowRevisionTooltips(CommitList, _preferences.ShowRevisionGridTooltips);
    }

    // Upstream's branch sort (RefsSortBy, RefsSortOrder, done by git) and priorities, read at each refresh.
    private RefSorting RefSorting() => CommitInfoView.UserRefSorting(_preferences);

    // Upstream's left panel menus list every choice by its description and check the current one; a choice applies at once.
    private void ShowSortMenus()
    {
        foreach (GitRefsSortBy sortBy in Enum.GetValues<GitRefsSortBy>())
        {
            SortByMenuItem.Items.Add(SortMenuItem(sortBy.GetDescription(), () => _preferences.RefsSortBy == sortBy,
                () => _preferences.RefsSortBy = sortBy));
        }

        foreach (GitRefsSortOrder order in Enum.GetValues<GitRefsSortOrder>())
        {
            SortOrderMenuItem.Items.Add(SortMenuItem(order.GetDescription(), () => _preferences.RefsSortOrder == order,
                () => _preferences.RefsSortOrder = order));
        }
    }

    private MenuItem SortMenuItem(string header, Func<bool> isCurrent, Action choose)
    {
        MenuItem item = new() { Header = header, ToggleType = MenuItemToggleType.Radio, IsChecked = isCurrent() };
        BranchTreeContextMenu.Opening += (_, _) => item.IsChecked = isCurrent();
        item.Click += (_, _) =>
        {
            choose();
            _preferences.Save();
            RefreshRepository();
        };
        return item;
    }

    private void ShowUserScripts()
    {
        UserScriptsPanel.Children.Clear();
        foreach (ScriptDefinition script in ScriptHost.ScriptsFor(ScriptEvent.ShowInUserMenuBar))
        {
            // As upstream's user menu bar: the script's icon before its name, when it has one.
            Button button = new() { Content = script.DisplayName };
            if (ScriptIcons.For(script) is { } icon)
            {
                button.Content = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 4,
                    Children =
                    {
                        new Image { Source = icon, Width = 16, Height = 16 },
                        new TextBlock { Text = script.DisplayName }
                    },
                };
            }

            button.Click += (_, _) =>
                RunOnRepository(path => Scripts.RunAsync(script, path, SelectedHashes, FileScriptOptions()));
            UserScriptsPanel.Children.Add(button);
        }

        UserScriptsPanel.IsVisible = UserScriptsPanel.Children.Count > 0;
    }

    // As upstream's FormBrowse.GetScriptOptionsProvider: the files of the tab on show, the File tree's or the Diff tab's, and
    // the line selected in its viewer; none on the other tabs.
    private IReadOnlyDictionary<string, IReadOnlyList<string>>? FileScriptOptions()
    {
        if (DetailTabs.SelectedItem == FileTreeTab)
        {
            return ScriptFileOptions.For(
                [.. FileTreeView.SelectedItems?.OfType<FileTreeNode>().Select(node => node.Path) ?? []],
                FileContentView.CurrentFileLine);
        }

        if (DetailTabs.SelectedItem == DiffTab)
        {
            return ScriptFileOptions.For(
                CommitFilesList.SelectedItems?.OfType<CommitFile>().Select(file => file.Path) ?? [],
                CommitDiff.CurrentFileLine);
        }

        return null;
    }

    // As upstream's grid menu, read when the menu opens: the scripts marked for the grid are items of the menu, every other
    // enabled script is under "Run script".
    private void ShowGridScripts()
        => ScriptMenus.Fill(CommitContextMenu, RunScriptMenuItem, script => script.AddToRevisionGridContextMenu,
            script => RunOnSelectedCommit((path, _) =>
                Scripts.RunAsync(script, path, SelectedHashes, FileScriptOptions())));

    // A script's navigateTo: output names a commit or ref; it is selected when it is among the loaded commits.
    private async Task SelectRefAsync(string target)
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        string? hash = await RepositoryScriptContext.ResolveCommitAsync(path, target);
        int index = _commits.VisibleRows.ToList().FindIndex(item => item.Row.Hash == hash);
        SelectGridRow(index < 0 ? null : index);
    }

    // Upstream's Ctrl+Up opens the push dialog; the toolbar's Push and Ctrl+Shift+Up push at once.
    // The grid's "Push branch..." opens it on that local branch (upstream's StartPushDialog with a branch).
    private async Task ShowPushDialogAsync(string? branch = null)
    {
        if (RepositoryPath is not { } path || !PushButton.IsEnabled)
        {
            return;
        }

        PushWindow window = await NewPushWindowAsync(path, branch);
        PushRequest? request = await window.ShowDialog<PushRequest?>(this);
        if (request is not null
            && (window.Dialog.ToUrl || await LoadPuttyKeysAsync(path, request.Remote))
            && await WithScriptsAsync(path, ScriptEvent.BeforePush, ScriptEvent.AfterPush,
                () => PushFlow.PushAsync(ProcessOwner, path, request,
                    new PushOrigin(window.Dialog.Branch, _repository.CurrentBranch, _repository.TrackingRemote,
                        window.Dialog.ToUrl)))
            && window.CreatePullRequestAfterPush)
        {
            // As upstream: the repository host's pull request window after the push.
            _plugins.StartCreatePullRequest(_plugins.Owner);
        }
    }

    // Upstream's StartPushDialog: the dialog on the open repository; its Pull button opens the pull dialog over it.
    private async Task<PushWindow> NewPushWindowAsync(string path, string? branch = null)
        => new(new PushDialog(await PushRepository.LoadAsync(path), branch), _preferences,
            path.EnsureTrailingPathSeparator(),
            _plugins.HasRepositoryHost, owner => ShowPullDialogAsync(owner),
            async (owner, remote) =>
            {
                await ShowRemotesAsync(owner, remote);
                return await PushRepository.LoadAsync(path);
            },
            remote => LoadPuttyKeysAsync(path, remote),
            await Task.Run(() => DetailedSettings.GetRemoteBranchesDirectlyFromRemote.ValueOrDefault(
                GitModules.Open(path).GetEffectiveSettings()))
                ? remote => ReadRemoteHeadsAsync(path, remote)
                : null);

    // Upstream's LoadMultiBranchViewData with "Get remote branches directly from the remote": Pageant for the remote's
    // key, git's ls-remote in the process window, and its refs; null after an error.
    private async Task<IReadOnlyList<IGitRef>?> ReadRemoteHeadsAsync(string path, string remote)
    {
        if (!await LoadPuttyKeysAsync(path, remote)
            || !await _actions.RunProgramAsync("git ls-remote", AppSettings.GitCommand,
                PushDialog.LsRemoteArguments(remote), path))
        {
            return null;
        }

        IReadOnlyList<string> output = _actions.OutputLines;
        return await Task.Run(() => PushDialog.RemoteHeadsFromLsRemote(GitModules.Open(path), output));
    }

    private async Task ShowPullDialogAsync() => await ShowPullDialogAsync(this);

    private async Task ShowPullDialogAsync(Window owner)
    {
        if (RepositoryPath is not { } path || !RefreshMenuItem.IsEnabled)
        {
            return;
        }

        PullRequest? request = await (await NewPullWindowAsync(path)).ShowDialog<PullRequest?>(owner);

        // As upstream's dialog, a fetch with prune chosen there runs without the question the quick fetch and prune asks.
        if (request is not null && await LoadPuttyKeysAsync(path, request.Remote))
        {
            // As upstream's pull dialog, "fetch only" runs the fetch scripts and a pull the pull scripts.
            bool fetchOnly = request.Action == PullAction.FetchOnly;
            await WithScriptsAsync(path, fetchOnly ? ScriptEvent.BeforeFetch : ScriptEvent.BeforePull,
                fetchOnly ? ScriptEvent.AfterFetch : ScriptEvent.AfterPull,
                () => fetchOnly
                    ? _actions.PullAsync(path, request)
                    : PullAndUpdateSubmodulesAsync(path, () => _actions.PullAsync(path, request), request.Remote));
        }
    }

    private async Task<Window?> ScreenshotDialogAsync(string? name)
        => (name, RepositoryPath) switch
        {
            (Screenshot.PullWindow, { } path) => await NewPullWindowAsync(path),
            (Screenshot.PushWindow, { } path) => await NewPushWindowAsync(path),
            (Screenshot.RemotesWindow, { } path) => new RemotesWindow(path, _preferences),
            (Screenshot.StashWindow, { } path) => new StashWindow(path, _actions, _preferences),
            (Screenshot.CreateBranchWindow, { } path) => new CreateBranchWindow(path, _actions, _preferences),
            (Screenshot.CheckoutBranchWindow, { } path) => await CheckoutFlow().NewWindowAsync(path),
            (Screenshot.CheckoutRevisionWindow, { } path) => NewCheckoutRevisionWindow(path, "HEAD~1"),
            (Screenshot.MergeBranchWindow, { } path) => NewMergeWindow(path, branch: null),
            (Screenshot.CloneWindow, _) => NewCloneWindow(),
            (Screenshot.ReflogWindow, { } path) => NewReflogWindow(path),
            (Screenshot.ConflictsWindow, { } path) => NewConflictsWindow(path),
            (Screenshot.MergeSubmoduleWindow, { } path) => new MergeSubmoduleWindow(path,
                Environment.GetEnvironmentVariable(Screenshot.FileEnvironmentVariable) ?? ""),
            (Screenshot.WorktreesWindow, { } path) => new WorktreesWindow(path, _repositoryService, WorktreeFlow()),
            (Screenshot.CreateWorktreeWindow, { } path) => new CreateWorktreeWindow(path,
                ManageWorktreeDialog.MainWorktreePath(_repository.Worktrees, path), _actions, _preferences),
            (Screenshot.FileHistoryWindow, { } path) => NewFileHistoryWindow(path,
                Environment.GetEnvironmentVariable(Screenshot.FileEnvironmentVariable) ?? "", revision: null,
                showBlame: false),
            (Screenshot.CommitDiffWindow, { } path) => new CommitDiffWindow(path, _commits.Selected?.Hash ?? "HEAD"),
            (Screenshot.BlameWindow, { } path) => new BlameWindow(path, "HEAD",
                Environment.GetEnvironmentVariable(Screenshot.FileEnvironmentVariable) ?? ""),
            (Screenshot.RecentRepositoriesSettingsWindow, _) => new RecentRepositoriesSettingsWindow(),
            (Screenshot.GoToCommitWindow, { } path) => new GoToCommitWindow(path),
            (Screenshot.DeleteBranchWindow, { } path) => new DeleteBranchWindow(
                [.. _repository.Branches.Where(branch => !branch.IsRemote).Select(branch => branch.Name)],
                _repository.CurrentBranch, [], (_, _) => Task.FromResult(false)),
            (Screenshot.ArchiveWindow, { } path) => new ArchiveWindow(path, "HEAD", "HEAD~1", null, _preferences, null),
            (Screenshot.FormatPatchWindow, { } path) => new FormatPatchWindow(path, _preferences),
            (Screenshot.ApplyPatchWindow, { } path) => new ApplyPatchWindow(path, _preferences, null, null, null),
            (Screenshot.ViewPatchWindow, { } path) => new ViewPatchWindow(path, null),
            (Screenshot.GitIgnoreWindow, { } path) => new GitIgnoreWindow(path),
            (Screenshot.CommandLogWindow, _) => new CommandLogWindow(),
            (Screenshot.AboutWindow, _) => new AboutWindow(_git?.Version?.ToString()),
            (Screenshot.ContributorsWindow, _) => new ContributorsWindow(),
            (Screenshot.ChooseTranslationWindow, _) => new ChooseTranslationWindow(new InMemoryAppPreferences()),
            (Screenshot.AddToGitIgnoreWindow, { } path) =>
                new AddToGitIgnoreWindow(path, localExclude: false, ["*.txt"]),
            (Screenshot.CherryPickWindow, { } path) => new CherryPickWindow(path, "HEAD", _preferences, null),
            (Screenshot.ChooseCommitWindow, { } path) => new ChooseCommitWindow(path, "HEAD~1", _preferences),
            (Screenshot.SearchWindow, { } path) => await NewScreenshotSearchWindowAsync(path),
            (Screenshot.CleanupRepositoryWindow, { } path) => new CleanupRepositoryWindow(path, null),
            (Screenshot.MailMapWindow, { } path) => new RepositoryTextFileWindow(path, RepositoryTextFiles.MailMap),
            (Screenshot.VerifyDatabaseWindow, { } path) => NewVerifyDatabaseWindow(path),
            (Screenshot.SubmodulesWindow, { } path) => NewSubmodulesWindow(path),
            (Screenshot.AddSubmoduleWindow, { } path) => new AddSubmoduleWindow(path, _ => Task.FromResult(false)),
            (Screenshot.SparseWorkingCopyWindow, { } path) =>
                new SparseWorkingCopyWindow(path, _ => Task.FromResult(false)),
            (Screenshot.ChangeLogWindow, _) => new ChangeLogWindow(),
            (Screenshot.DonateWindow, _) => new DonateWindow(OpenUrl),
            (Screenshot.RevisionDiffWindow, { } path) => new RevisionDiffWindow(path,
                GitModules.Open(path).RevParse("HEAD~1"), GitModules.Open(path).RevParse("HEAD"), "HEAD~1", "HEAD",
                _preferences),
            (Screenshot.CompareToBranchWindow, { } path) => new CompareToBranchWindow(path, default),
            (Screenshot.ResetAnotherBranchWindow, { } path) => new ResetAnotherBranchWindow(path, "HEAD", _preferences,
                (_, _) => Task.FromResult(false), (_, _) => Task.CompletedTask),
            (Screenshot.RevertCommitWindow, { } path) =>
                new RevertCommitWindow(path, "HEAD", (_, _, _) => Task.CompletedTask),
            (Screenshot.CreateTagWindow, { } path) => new CreateTagWindow(path, "HEAD", _ => Task.FromResult(false),
                (_, _) => Task.FromResult(false)),
            (Screenshot.DeleteTagWindow, { } path) => new DeleteTagWindow(null, _repository.Tags, _repository.Remotes,
                _repository.TrackingRemote ?? "", (_, _) => Task.CompletedTask),
            (Screenshot.ResetCurrentBranchWindow, { } path) => NewResetCurrentBranchWindow(path, "HEAD~1",
                ResetMode.Soft),
            (Screenshot.RebaseWindow, { } path) => NewRebaseWindow(path, "HEAD~1", interactive: false,
                immediately: false),
            _ => null,
        };

    // Upstream's StartPullDialog: the dialog on the open repository, its default action from Settings > General.
    private async Task<PullWindow> NewPullWindowAsync(string path)
    {
        PullDialog dialog = new(_repository.Remotes, _repository.CurrentBranch, GitPullAction.None,
            _preferences.DefaultPullAction, defaultRemote: null, _repository.TrackingRemote);
        PullWindowGit git = new(
            async branch => await AppServices.GitConfig.GetAsync(ConfigScope.Local, $"branch.{branch}.remote", path) is
                { Length: > 0 } remote
                ? remote
                : null,
            (remote, remoteBranch, currentBranch) =>
                _actions.RebasesMergeCommitAsync(path, remote, remoteBranch, currentBranch),
            async (owner, remote) =>
            {
                await ShowRemotesAsync(owner, remote);
                return await Task.Run(() => new ConfigFileRemoteSettingsManager(() => GitModules.Open(path))
                    .LoadRemotes(false).Select(loaded => loaded.Name ?? "").ToList());
            },
            owner => CheckoutFlow().StartAsync(owner, path));
        return new PullWindow(dialog, _preferences, PathUtil.GetDisplayPath(path.EnsureTrailingPathSeparator()),
            [.. _repository.Branches.Where(branch => branch.IsRemote).Select(branch => branch.Name)],
            [.. _repository.Branches.Where(branch => !branch.IsRemote).Select(branch => branch.Name)],
            await Task.Run(() => PullDialog.IsShallowRepository(path)), git, ShowConflicts,
            owner => ShowStashWindowAsync(owner));
    }

    // One commit window at a time, as upstream; asking again brings the open one forward.
    private void ShowCommitWindow() => OpenCommitWindow();

    // The commit window, brought to the front when it is open; null when none can be opened.
    private CommitWindow? OpenCommitWindow()
    {
        if (RepositoryPath is not { } path || !CommitDialogButton.IsEnabled)
        {
            return null;
        }

        if (_commitWindow is not null)
        {
            _commitWindow.Activate();
            return _commitWindow;
        }

        // As upstream's StartCommitDialog, the plugin events wrap the window, not each commit made in it.
        if (!_plugins.RaisePre(PluginEvent.Commit))
        {
            return null;
        }

        CommitWindow window = new(path, _repository, _actions, _repositoryService, _preferences,
            () => OpenRepositoryAsync(path),
            _commitMessages, _plugins.ExecuteByName,
            (owner, page) => ShowSettingsAsync(owner: owner, page: page))
        {
            Submodules = DiffFileSubmodules(() => _commitWindow ?? (Window)this)
        };
        _commitWindow = window;
        window.Closed += (_, _) =>
        {
            _commitDraftSaved = window.SavedDraft;
            _commitWindow = null;
            _plugins.RaisePost(PluginEvent.Commit, actionDone: true);
        };

        // The window reads the draft the previous one saved, so it waits for that save.
        Task previousSave = _commitDraftSaved;
        Run(async () =>
        {
            await previousSave;
            window.Show(this);
        });
        return window;
    }

    // Upstream's handling of a rejected push, with its questions over the process window that shows git's output.
    private PushRejectedFlow PushFlow => new(_actions, _preferences);

    private Window ProcessOwner() => _processWindow ?? (Window)this;

    // One output window, reused for the next remote operation, like upstream's process dialog.
    private void ShowProcessWindow()
    {
        if (_processWindow is not null)
        {
            _processWindow.Activate();
            return;
        }

        _processWindow = new ProcessWindow(_actions, _preferences);
        _processWindow.Closed += (_, _) => _processWindow = null;
        _processWindow.Show(this);
    }

    // Upstream's StartResolveConflictsDialog: the conflicts window, modal as upstream's.
    private void ShowConflicts()
    {
        if (RepositoryPath is { } path)
        {
            _ = NewConflictsWindow(path).ShowDialog(this);
        }
    }

    private ConflictsWindow NewConflictsWindow(string path)
        => new(path, _repository, _actions, _preferences,
            (owner, file) => NewFileHistoryWindow(path, file, revision: null, showBlame: false).Show(owner),
            _ => ShowCommitWindow(), UpdateSubmodulesAfterCheckoutAsync,
            checkoutSubmoduleBranch: (owner, submodulePath, commits) =>
                CheckoutFlow().StartAsync(owner, submodulePath, containObjectIds: commits));

    // As upstream's settings dialog, the plugins hear about the change (PostSettings) whether or not it was saved. A
    // window other than this one can open it over itself on one of upstream's pages (StartSettingsDialog with a page).
    private async Task ShowSettingsAsync(IGitPlugin? plugin = null, bool pluginsPage = false, Window? owner = null,
        string? page = null)
    {
        SettingsWindow settings = new(_preferences, RepositoryPath, await LoadPluginsAsync(), plugin, pluginsPage);
        if (page is not null)
        {
            settings.GoToPage(page);
        }

        bool saved = await settings.ShowDialog<bool>(owner ?? this);
        if (saved)
        {
            ThemeApplier.Apply(_preferences);
            FontApplier.Apply(_preferences);

            // Upstream's Colors page: the revision graph group; the Browse repository window page: the git-grep box and
            // the GPG tab.
            ShowGridView();
            ShowGitGrepBoxOfTab();
            ShowGpgTab();
            FilePathTrimming.Apply(_preferences);
            ShowGridTooltips();
            ShowAvatarSettings();
            AppServices.Avatars.Reload();
            DiffToolChoices.Show(CommitDiffToolChoiceButton);

            // Upstream's FileStatusDiffCalculator reloads its groups after "Show file differences for all parents".
            ShowSelectionDiff();
            ShowHotkeys();
            ShowUserScripts();
            ShowGridScripts();
            await _recent.LoadAsync();

            // The date and commit button settings show on the next read.
            ShowAuthorDateMenuItem.IsChecked = _preferences.ShowAuthorDate;
            ShowRelativeDateMenuItem.IsChecked = _preferences.RelativeDate;
            ShowDefaultPullAction();
            UpdateBranchText();
            _commits.MaxCommits = _preferences.MaxRevisionGraphCommits;
            ShowArtificialChangeCounts();
            UpdateCommitButton();
            ShowBuildStatusColumn();
            RefreshRepository();
        }

        _plugins.RaisePost(PluginEvent.Settings, saved);
    }

    // Loaded once, off the UI thread: scanning the plugin assemblies takes a while, as upstream's FormBrowse notes.
    private async Task<IReadOnlyList<IGitPlugin>> LoadPluginsAsync()
        => _loadedPlugins ??= await Task.Run(AppServices.Plugins.Load);

    // Upstream's Plugins menu: the plugins by name above the separator, "Plugins settings..." below it.
    private async Task ShowPluginsAsync()
    {
        IReadOnlyList<IGitPlugin> plugins = await LoadPluginsAsync();
        PluginsMenu.Items.Remove(PluginsLoadingMenuItem);
        ShowRepositoryHostsMenu(plugins);
        int index = 0;
        foreach (IGitPlugin plugin in plugins)
        {
            MenuItem item = new() { Header = plugin.Name, Tag = plugin };
            if (PluginHost.LoadIcon(plugin) is { } icon)
            {
                item.Icon = new Image { Source = icon, Width = 16, Height = 16 };
            }

            item.Click += (_, _) => ExecutePlugin(plugin);
            PluginsMenu.Items.Insert(index++, item);
        }

        RegisterPlugins();
        _plugins.RaisePostBrowseInitialize(_plugins.Owner);
    }

    // As upstream's SetGitModule: the plugins move to the repository now shown, or to the dashboard.
    private void RegisterPlugins()
    {
        string repository = RepositoryPath ?? "";
        if (_loadedPlugins is null || _pluginRepository == repository)
        {
            return;
        }

        _pluginRepository = repository;
        _plugins.Register(_loadedPlugins, RepositoryPath);

        // Plugins that work on a repository are off on the dashboard, as upstream's UpdatePluginMenu turns them off.
        foreach (MenuItem item in PluginsMenu.Items.OfType<MenuItem>())
        {
            if (item.Tag is IGitPlugin plugin)
            {
                item.IsEnabled = plugin is not IGitPluginForRepository || RepositoryPath is not null;
            }
        }
    }

    private void ExecutePlugin(IGitPlugin plugin)
    {
        if (plugin is FailedPlugin failed)
        {
            ShowError($"Fail to load a plugin. Error:{Environment.NewLine}{Environment.NewLine}{failed.Error}");
            return;
        }

        try
        {
            if (_plugins.Execute(plugin))
            {
                RefreshRepository();
            }
        }
        catch (Exception ex)
        {
            // A plugin is someone else's code; its failure is shown, not fatal.
            ShowError(ex.Message);
        }
    }

    // Upstream's ConfirmSuppressible: asks unless the user turned the question off (Settings > Confirmations).
    private Task<bool> ConfirmAsync(Confirmation confirmation, string message, string confirmText, string caption)
        => ConfirmWindow.AskAsync(this, _preferences, confirmation, message, confirmText, caption);

    // As upstream's left panel: asks first only when upstream's ConfirmBranchCheckout is on (it is off by default).
    private void CheckoutSelectedBranch() => RunOnSelectedBranch(async (path, branch) =>
        await ConfirmAsync(Confirmation.BranchCheckout,
            string.Format(Confirmations.BranchCheckoutQuestion, branch.Name),
            "Checkout", "Confirm checkout")
        && await StartCheckoutBranchAsync(path, branch.Name, branch.IsRemote));

    // Upstream's StartCheckoutBranch: the checkout window (or the checkout at once when it is not needed), between the
    // checkout scripts and plugin events, followed by the submodule update.
    private Task<bool> StartCheckoutBranchAsync(string path, string branch = "", bool remote = false)
        => CheckoutFlow().StartAsync(this, path, branch, remote);

    private CheckoutBranchFlow CheckoutFlow()
        => new(_actions, _preferences,
            (repository, checkout) => WithScriptsAsync(repository, ScriptEvent.BeforeCheckout,
                ScriptEvent.AfterCheckout,
                checkout, PluginEvent.CheckoutBranch),
            UpdateSubmodulesAfterCheckoutAsync);

    // Upstream's StartCheckoutRevisionDialog: the checkout revision window proposing <revision> (none from the menu).
    private async Task<bool> ShowCheckoutRevisionAsync(string path, string? revision)
        => await NewCheckoutRevisionWindow(path, revision).ShowDialog<bool>(this);

    private CheckoutRevisionWindow NewCheckoutRevisionWindow(string path, string? revision)
        => new(path, _actions, revision,
            (repository, checkout) => WithScriptsAsync(repository, ScriptEvent.BeforeCheckout,
                ScriptEvent.AfterCheckout,
                checkout, PluginEvent.CheckoutRevision),
            UpdateSubmodulesAfterCheckoutAsync);

    // Upstream's StartMergeBranchDialog: the merge window proposing <branch> (the tracked branch when null).
    private async Task<bool> ShowMergeAsync(string path, string? branch)
        => await NewMergeWindow(path, branch).ShowDialog<bool>(this);

    private MergeBranchWindow NewMergeWindow(string path, string? branch)
        => new(path, _actions, _preferences, branch,
            (repository, merge) => WithScriptsAsync(repository, ScriptEvent.BeforeMerge, ScriptEvent.AfterMerge, merge),
            ShowConflicts);

    // Upstream's left panel "Rebase current branch on this branch...": the rebase window on it (StartRebaseDialog).
    private void RebaseOnSelectedBranch() =>
        RunOnSelectedBranch((path, branch) => ShowRebaseAsync(path, branch.Name));

    // Upstream's Commands > Rebase...: the rebase window on the selected commit, which must not be an artificial one.
    private void ShowRebaseOnSelectedCommit()
    {
        if (_commits.Selected is { } row && !ArtificialCommits.IsArtificial(row.Hash))
        {
            RunOnRepository(path => ShowRebaseAsync(path, row.Hash));
        }
    }

    // Upstream's StartRebaseDialog: the rebase window on the open repository.
    private Task<bool> ShowRebaseAsync(string path, string? onto, bool interactive = false, bool immediately = false,
        string from = "")
        => NewRebaseWindow(path, onto, interactive, immediately, from).ShowDialog<bool>(this);

    private RebaseWindow NewRebaseWindow(string path, string? onto, bool interactive, bool immediately,
        string from = "")
        => new(path, _actions, _preferences, from, to: null, onto, interactive, immediately, _ => ShowCommitWindow(),
            owner => _ = NewConflictsWindow(path).ShowDialog(owner))
        {
            RunGit = arguments => RunGitWithOutputAsync(path, arguments),
        };

    // Upstream's OnRebaseWithAdvOptionsClicked: with two commits selected, the other one is the range's start (excluded).
    private string RebaseRangeStart()
        => SelectedRevisions is [_, var other] ? ObjectId.Parse(other.Hash).ToShortString() : "";

    private void PopTopStash()
    {
        if (RepositoryPath is { } path && _repository.Stashes.Count > 0)
        {
            Run(() => _actions.PopStashAsync(path, _repository.Stashes[0].Name));
        }
    }

    // Upstream's StartCreateBranchDialog: the create branch window at the commit (HEAD when none or an artificial one), with
    // a proposed name; as upstream, the submodules are updated when the new branch is checked out at another commit.
    private async Task PromptCreateBranchAsync(string? startPoint, string? namePrefix = null)
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        CreateBranchWindow window = new(path, _actions, _preferences, startPoint, namePrefix);
        if (await window.ShowDialog<bool>(this) && window.CheckedOutOtherCommit)
        {
            await UpdateSubmodulesAfterCheckoutAsync(path);
        }
    }

    // Upstream's StartCreateTagDialog: FormCreateTag on the commit; its push runs between the push scripts, as upstream's
    // PushTag.
    private async Task<bool> ShowCreateTagAsync(string commit, Window? owner = null)
    {
        if (RepositoryPath is not { } path)
        {
            return false;
        }

        return await new CreateTagWindow(path, commit, arguments => RunGitWithOutputAsync(path, arguments),
                (remote, tag) => WithScriptsAsync(path, ScriptEvent.BeforePush, ScriptEvent.AfterPush,
                    () => _actions.PushAsync(path,
                        new PushRequest(remote, "", "", ForcePushOptions.DoNotForce, Track: false, PushKind.Tag, tag))))
            .ShowDialog<bool>(owner ?? this);
    }

    // Deleting a tag on the remote removes it for everyone who fetches, so it is confirmed first; the local tag stays.
    private async Task<bool> DeleteRemoteTagAfterConfirmAsync(string path, string tag)
    {
        string remote = TrackingRemote();
        string message = $"Delete the tag {tag} on {remote}? Everyone who fetches will lose it. The local tag is kept.";
        return await new ConfirmWindow(message, "Delete on remote").ShowDialog<bool>(this) &&
               await _actions.DeleteRemoteTagAsync(path, remote, tag);
    }

    // Upstream's StartFileHistoryDialog: the file history window on <file>, selecting <revision>, on its Blame tab for
    // blame. Upstream starts it in a new process; here it is a window of this one.
    private FileHistoryWindow NewFileHistoryWindow(string path, string file, string? revision, bool showBlame)
        => new(path, _preferences, file, revision, showBlame,
            (repository, hash) => _actions.CherryPickAsync(repository, hash),
            (repository, hash) => _actions.RevertAsync(repository, hash), RelevantRepositoryHost)
        {
            ShowSettings = () => ShowSettingsAsync(),
        };

    // Upstream's StartReflogDialog: its create branch and reset windows open over it.
    private async Task ShowReflogAsync()
    {
        if (RepositoryPath is { } path)
        {
            await NewReflogWindow(path).ShowDialog(this);
        }
    }

    private ReflogWindow NewReflogWindow(string path)
        => new(path,
            (owner, id) => new CreateBranchWindow(path, _actions, _preferences, id.ToString(),
                checkoutAfterCreation: false,
                userAbleToChangeRevision: false, couldBeOrphan: false).ShowDialog<bool>(owner),
            (owner, id, mode) => ShowResetCurrentBranchAsync(owner, path, id.ToString(), mode));

    // Upstream's FormResetCurrentBranch from the grid, the left panel and the reflog.
    private Task<bool> ShowResetCurrentBranchAsync(Window owner, string path, string target, ResetMode mode)
        => NewResetCurrentBranchWindow(path, target, mode).ShowDialog<bool>(owner);

    private ResetCurrentBranchWindow NewResetCurrentBranchWindow(string path, string target, ResetMode mode)
        => new(path, _actions, _preferences, target, mode, UpdateSubmodulesAfterCheckoutAsync);

    private void RunOnRepository(Func<string, Task<bool>> action)
    {
        if (RepositoryPath is { } path)
        {
            Run(() => action(path));
        }
    }

    private void RunOnCurrentBranch(Func<string, string, Task<bool>> action)
    {
        if (RepositoryPath is { } path && _repository.CurrentBranch is { Length: > 0 } branch)
        {
            Run(() => action(path, branch));
        }
    }

    // Checkout and delete never act on the checked-out branch; delete never acts on a remote branch.
    private void RunOnSelectedBranch(Func<string, BranchInfo, Task<bool>> action)
    {
        if (RepositoryPath is { } path && SelectedBranch is BranchInfo { IsCurrent: false } branch)
        {
            Run(() => action(path, branch));
        }
    }

    // Cherry-pick, revert and reset act on the commit selected in the list.
    private void RunOnSelectedCommit(Func<string, CommitRow, Task<bool>> action)
    {
        if (RepositoryPath is { } path && _commits.Selected is { } row)
        {
            Run(() => action(path, row));
        }
    }

    // Upstream's LocalBranchNode.Delete and RemoteBranchNode.Delete: upstream's delete branch or delete remote branch
    // window with the branch.
    private async Task<bool> DeleteSelectedBranchAsync(string path, BranchInfo branch)
    {
        if (!branch.IsRemote)
        {
            return await ShowDeleteBranchAsync(path, [branch.Name]);
        }

        return await ShowDeleteRemoteBranchAsync(path, branch.Name);
    }

    private void OnActionsChanged(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(RepositoryOperationsViewModel.IsBusy):
                OperationStatusText.Text = _actions.IsBusy ? "Working..." : _actions.StatusMessage;
                UpdateBusyState();
                break;
            case nameof(RepositoryOperationsViewModel.StatusMessage):
                OperationStatusText.Text = _actions.StatusMessage;
                break;
            case nameof(RepositoryOperationsViewModel.ErrorMessage):
                if (_actions.ErrorMessage is { } message)
                {
                    _actions.ClearError();
                    ShowError(message);
                }

                break;
        }
    }

    private void OnCommitsChanged(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(CommitListViewModel.Selected):
                ShowFileTreeIfVisible();
                UpdateBusyState();
                break;
            case nameof(CommitListViewModel.CommitFiles):
                // While the Diff tab shows upstream's diff groups (several commits, a merge), it lists their files instead.
                if (_diffGroups is null)
                {
                    _diffFiles?.Files = SelectedCommitFiles();
                }

                break;
            case nameof(CommitListViewModel.VisibleRows):
                ShowGridRows();
                break;
            case nameof(CommitListViewModel.Status):
                StatusText.Text = _commits.Status;
                break;
            case nameof(CommitListViewModel.Filter):
                ShowFilterState();
                break;
            case nameof(CommitListViewModel.RepositoryName):
                UpdateTitle();
                break;
            case nameof(CommitListViewModel.HasMore):
                LoadMoreButton.IsVisible = _commits.HasMore;
                break;
            case nameof(CommitListViewModel.IsLoading):
                UpdateBusyState();
                ShowHiddenRepoObjects();
                break;
            case nameof(CommitListViewModel.Rows):
                ShowHiddenRepoObjects();
                break;
            case nameof(CommitListViewModel.Details):
            case nameof(CommitListViewModel.DetailsError):
                ShowDetails();
                break;
            case nameof(CommitListViewModel.ErrorMessage):
                if (_commits.ErrorMessage is { } message)
                {
                    _commits.ClearError();
                    ShowError(message);
                }

                break;
        }
    }

    private void OnRepositoryChanged(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(RepositoryViewModel.CurrentBranch):
            case nameof(RepositoryViewModel.IsMerging):
            case nameof(RepositoryViewModel.IsRebasing):
            case nameof(RepositoryViewModel.IsApplyingPatch):
            case nameof(RepositoryViewModel.IsBisecting):
            case nameof(RepositoryViewModel.Sync):
                UpdateBranchText();
                UpdateStateBanner();
                UpdateBusyState();
                break;
            case nameof(RepositoryViewModel.Changes):
            case nameof(RepositoryViewModel.ChangesRead):
                UpdateCommitButton();
                UpdateStateBanner();
                ShowArtificialChangeCounts();
                break;
            case nameof(RepositoryViewModel.Stashes):
                UpdateBranchText();
                UpdateBusyState();
                break;
            case nameof(RepositoryViewModel.Branches):
            case nameof(RepositoryViewModel.Submodules):
            case nameof(RepositoryViewModel.Remotes):
                UpdateBusyState();
                break;
            case nameof(RepositoryViewModel.RemoteColors):
                RefLabelView.SetRemoteColors(CommitList, _repository.RemoteColors);
                break;
            case nameof(RepositoryViewModel.IsLoading):
                // The left panel is built once the whole snapshot is read (upstream refills its trees after a refresh).
                if (!_repository.IsLoading)
                {
                    ShowRepoObjects();
                    LoadSubmoduleStructure();
                }

                UpdateBusyState();
                break;
            case nameof(RepositoryViewModel.ErrorMessage):
                if (_repository.ErrorMessage is { } message)
                {
                    _repository.ClearError();
                    ShowError(message);
                }

                break;
        }
    }

    // Upstream's FormBrowse refreshes both notification bars with the repository (RefreshBisect, RefreshGitAction).
    private void UpdateStateBanner()
    {
        bool conflicts = _repository.Changes.Any(change => change.Kind == ChangeKind.Conflict);
        _actionBanner = GitActionBanner.ForRepository(_repository.IsRebasing, _repository.IsMerging,
            _repository.IsApplyingPatch, conflicts);
        ShowBanner(StateBanner, StateBannerText, _actionBanner);
        IReadOnlyList<GitActionButton> buttons = _actionBanner?.Buttons ?? [];
        ResolveConflictsButton.IsVisible = buttons.Contains(GitActionButton.Resolve);
        ContinueActionButton.IsVisible = buttons.Contains(GitActionButton.Continue);
        AbortActionButton.IsVisible = buttons.Contains(GitActionButton.Abort);
        MoreActionButton.IsVisible = buttons.Contains(GitActionButton.More);

        ShowBanner(BisectBanner, BisectBannerText, GitActionBanner.ForBisect(_repository.IsBisecting));
        foreach (Control item in new Control[]
                 {
                     MarkBadMenuItem, MarkGoodMenuItem, BisectSkipMenuItem, StopBisectMenuItem, BisectSeparator,
                 })
        {
            item.IsVisible = _repository.IsBisecting;
        }
    }

    private static void ShowBanner(Border banner, TextBlock text, GitActionBanner? state)
    {
        banner.IsVisible = state is not null;
        if (state is null)
        {
            return;
        }

        text.Text = state.Text;
        text.Foreground = ThemeBrushes.Current.Context;
        banner.Background = state.HasConflicts ? ThemeBrushes.Current.ConflictBanner : ThemeBrushes.Current.InfoBanner;
    }

    // Upstream's ContinueButton_Click: the action's own continue command.
    private void ContinueAction()
    {
        switch (_actionBanner?.Action)
        {
            case GitAction.Rebase:
                RunOnRepository(path => _actions.ContinueRebaseAsync(path));
                break;
            case GitAction.Merge:
                RunOnRepository(path => _actions.ContinueMergeAsync(path));
                break;
            case GitAction.Patch:
                RunOnRepository(path => _actions.ContinuePatchAsync(path));
                break;
        }
    }

    // Upstream's AbortButton_Click: the command runs at once.
    private void AbortAction()
    {
        switch (_actionBanner?.Action)
        {
            case GitAction.Rebase:
                RunOnRepository(_actions.AbortRebaseAsync);
                break;
            case GitAction.Merge:
                RunOnRepository(_actions.AbortMergeAsync);
                break;
            case GitAction.Patch:
                RunOnRepository(_actions.AbortPatchAsync);
                break;
        }
    }

    // Upstream's FormBisect, from Commands > Bisect... or the bar's More...: the window starts the bisect itself and returns
    // the step to run next.
    private async Task ShowBisectAsync()
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        BisectWindow? window = null;
        window = new BisectWindow(_repository.IsBisecting, async () =>
        {
            // Upstream's Start_Click: with several commits selected, after a question the latest selected is marked good
            // and the earliest bad, and the window closes.
            IReadOnlyList<CommitRow> revisions = SelectedRevisions;
            bool started = await _actions.StartBisectAsync(path);
            if (started && revisions.Count > 1 && window is not null)
            {
                MessageBoxWindow question = new(Ui.UpstreamTranslation.ByText(BisectStartRange),
                    Ui.UpstreamTranslation.ByText(window.Title ?? ""), MessageBoxButtons.YesNo);
                await question.ShowDialog(window);
                if (question.Result == DialogResult.Yes)
                {
                    if (await _actions.MarkBisectAsync(path, GitBisectOption.Good, revisions[0].Hash))
                    {
                        await _actions.MarkBisectAsync(path, GitBisectOption.Bad, revisions[^1].Hash);
                    }

                    window.Close(null);
                }
            }

            return started;
        });
        BisectStep? step = await window.ShowDialog<BisectStep?>(this);
        if (step == BisectStep.Stop)
        {
            await _actions.StopBisectAsync(path);
        }
        else if (step is { } mark && BisectWindow.OptionFor(mark) is { } option)
        {
            await _actions.MarkBisectAsync(path, option);
        }
    }

    // Upstream's grid bisect items mark the selected commit, not the checked-out one.
    private void MarkSelectedForBisect(GitBisectOption option)
    {
        if (_commits.Selected is { } row)
        {
            RunOnRepository(path => _actions.MarkBisectAsync(path, option, row.Hash));
        }
    }

    // Each control is enabled only when its action can run: nothing while a read or write is in progress, and
    // the repository actions only when a repository is open. Clone and Open need no repository, only git.
    private void UpdateBusyState()
    {
        bool busy = _commits.IsLoading || _repository.IsLoading || _actions.IsBusy;
        bool open = !busy && RepositoryPath is not null;
        bool gitAvailable = _git?.Status != GitDiscoveryStatus.NotFound;
        bool hasBranch = _repository.CurrentBranch.Length > 0;

        // Upstream's commit actions do not apply to the artificial working directory and index rows.
        bool commitSelected = open && _commits.Selected is { } selectedRow &&
                              !ArtificialCommits.IsArtificial(selectedRow.Hash);

        OpenMenuItem.IsEnabled = !busy && gitAvailable;
        InitMenuItem.IsEnabled = !busy && gitAvailable;
        CloneMenuItem.IsEnabled = !busy && gitAvailable;
        DashboardGroups.IsEnabled = !busy && gitAvailable;
        CloseRepositoryMenuItem.IsEnabled = open;
        RefreshButton.IsEnabled = open;
        FilterToolStrip.IsEnabled = open;
        RefreshMenuItem.IsEnabled = open;
        LoadMoreButton.IsEnabled = !busy;
        CommitDialogButton.IsEnabled = open;
        CommitMenuItem.IsEnabled = open;
        PullDialogMenuItem.IsEnabled = open && _repository.Remotes.Count > 0;
        PushDialogMenuItem.IsEnabled = open && _repository.Remotes.Count > 0 &&
                                       _repository.Branches.Any(branch => !branch.IsRemote);
        PullButton.IsEnabled = open && hasBranch;
        PushButton.IsEnabled = open && hasBranch;
        OpenFolderButton.IsEnabled = open;
        FileExplorerMenuItem.IsEnabled = open;
        TerminalButton.IsEnabled = open;
        TerminalMenuItem.IsEnabled = open;
        GitGuiMenuItem.IsEnabled = GitKMenuItem.IsEnabled = UndoLastCommitMenuItem.IsEnabled = open;
        GitAttributesMenuItem.IsEnabled = MailMapMenuItem.IsEnabled = CleanupMenuItem.IsEnabled = open;
        GitMaintenanceMenuItem.IsEnabled =
            ManageSubmodulesMenuItem.IsEnabled = SparseWorkingCopyMenuItem.IsEnabled = open;
        RepositorySettingsMenuItem.IsEnabled = open;
        FilesMenuItem.IsEnabled = open;
        UpdateAllSubmodulesMenuItem.IsEnabled = SyncAllSubmodulesMenuItem.IsEnabled = open;
        GitInfoExcludeMenuItem.IsEnabled = ResetChangesMenuItem.IsEnabled = open;
        ReflogMenuItem.IsEnabled = open;
        GitIgnoreMenuItem.IsEnabled = open;
        WorktreesMenuItem.IsEnabled = open;
        ResolveConflictsMenuItem.IsEnabled = open;
        ResolveConflictsButton.IsEnabled = open;
        UpdateToolStrip(busy, open);

        CreateBranchMenuItem.IsEnabled = open;
        MergeMenuItem.IsEnabled = open;
        RebaseMenuItem.IsEnabled = open;
        ContinueActionButton.IsEnabled = open;
        AbortActionButton.IsEnabled = open;
        MoreActionButton.IsEnabled = open;
        BisectMoreButton.IsEnabled = open;
        BisectMenuItem.IsEnabled = commitSelected;
        MarkBadMenuItem.IsEnabled = commitSelected;
        MarkGoodMenuItem.IsEnabled = commitSelected;
        BisectSkipMenuItem.IsEnabled = commitSelected;
        StopBisectMenuItem.IsEnabled = open;

        ManageStashesCommandMenuItem.IsEnabled = open;
        CheckoutBranchMenuItem.IsEnabled = open;
        CheckoutRevisionMenuItem.IsEnabled = open;
        CreateTagMenuItem.IsEnabled = open;

        CherryPickMenuItem.IsEnabled = commitSelected;
        RevertMenuItem.IsEnabled = commitSelected;
        ArchiveMenuItem.IsEnabled = ArchiveCommitMenuItem.IsEnabled = commitSelected;
        CherryPickCommandMenuItem.IsEnabled = commitSelected;
        FormatPatchMenuItem.IsEnabled = ApplyPatchMenuItem.IsEnabled = ViewPatchMenuItem.IsEnabled = open;
        ResetCurrentBranchMenuItem.IsEnabled = commitSelected;
        RebaseOnCommitMenuItem.IsEnabled = commitSelected;
        RebaseInteractiveMenuItem.IsEnabled = commitSelected && _actions.EditorCommand is not null;
        CreateBranchHereMenuItem.IsEnabled = commitSelected;
        CreateTagHereMenuItem.IsEnabled = commitSelected;
        BrowseFilesHereMenuItem.IsEnabled = commitSelected;
        CheckoutCommitMenuItem.IsEnabled = commitSelected;
    }

    // Upstream's StashSave from the toolbar, menu and left panel: no message, untracked files as upstream's
    // includeUntrackedFilesInManualStash says.
    private async Task StashAsync()
    {
        if (RepositoryPath is { } path)
        {
            await _actions.StashAsync(path, "", _preferences.IncludeUntrackedFilesInManualStash, keepIndex: false);
        }
    }

    // As upstream's grid menu "Rebase current branch on": asks first unless upstream's DontConfirmRebase is set. The
    // interactive rebase opens this app's editor for the todo list (a separate process that git starts and waits for).
    private async Task RebaseOnSelectedCommitAsync(bool interactive)
    {
        if (RepositoryPath is not { } path || _commits.Selected is not { } row)
        {
            return;
        }

        if (!await ConfirmAsync(Confirmation.Rebase, Confirmations.RebaseQuestion,
                interactive ? "Rebase interactively" : "Rebase", "Rebase Confirmation"))
        {
            return;
        }

        await ShowRebaseAsync(path, row.Hash, interactive, immediately: true);
    }

    // Like upstream, coming back to the window picks up files edited in other programs: the status is read again (the
    // commit list is not, as it changes only through git).
    private void RefreshStatusOnActivation()
    {
        bool busy = _commits.IsLoading || _repository.IsLoading || _actions.IsBusy;
        if (RepositoryPath is not { } path || busy ||
            DateTime.UtcNow - _lastActivationRefresh < _activationRefreshInterval)
        {
            return;
        }

        _lastActivationRefresh = DateTime.UtcNow;
        Run(() => _repository.RefreshAsync(path));
    }

    // Upstream's StartRemotesDialog: the remotes window on the open repository; a saved remote is fetched, and the
    // repository is read again when the window closes.
    private async Task ShowRemotesAsync(Window owner, string? preselectRemote = null, string? preselectLocal = null)
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        await new RemotesWindow(path, _preferences, remote => _actions.FetchAsync(path, remote, prune: false),
            preselectRemote, preselectLocal).ShowDialog(owner);
        RefreshRepository();
    }

    // Upstream's StartStashDialog: the stash window on the open repository; the repository is read again after it.
    private async Task ShowStashWindowAsync(Window owner, bool manageStashes = true, string? initialStash = null)
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        await new StashWindow(path, _actions, _preferences, manageStashes, initialStash)
        {
            MenuServices = FileListMenuServices, Submodules = stashWindow => DiffFileSubmodules(() => stashWindow),
        }.ShowDialog(owner);
        RefreshRepository();
    }

    // Upstream's manageWorktreeToolStripMenuItem_Click: the worktree window, which opens a worktree in this window; the
    // repository is read again when a worktree was created there.
    private async Task ShowWorktreesAsync()
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        WorktreesWindow window = new(path, _repositoryService, WorktreeFlow());
        await window.ShowDialog(this);
        if (window.ShouldRefresh)
        {
            RefreshRepository();
        }
    }

    // Upstream's WorktreeTree.CreateWorktree and the drop-down's Create worktree...: starts at the main worktree.
    private async Task CreateWorktreeAsync()
    {
        if (RepositoryPath is { } path &&
            await WorktreeFlow().CreateAsync(this, path,
                ManageWorktreeDialog.MainWorktreePath(_repository.Worktrees, path)))
        {
            RefreshRepository();
        }
    }

    private WorktreeFlow WorktreeFlow() => new(_actions, _preferences, OpenRepositoryAsync);

    // Upstream's ResetToolStripMenuItem_Click: StartResetChangesDialog with the work tree's files, all changes reset.
    private async Task<bool> ResetChangesAsync(string path)
    {
        List<FileChange> workTree = [.. _repository.Changes.Where(change => !change.Staged)];
        ResetChangesWindow.ResetAction action = await ResetChangesWindow.ShowResetDialogAsync(this,
            hasExistingFiles: workTree.Any(change => !IsNew(change)), hasNewFiles: workTree.Any(IsNew));
        return action != ResetChangesWindow.ResetAction.Cancel
               && await _actions.ResetAllChangesAsync(path,
                   clean: action == ResetChangesWindow.ResetAction.ResetAndDelete,
                   onlyWorkTree: false);

        static bool IsNew(FileChange change) => change.Kind is ChangeKind.Untracked or ChangeKind.Added;
    }

    // Upstream's StartEditGitIgnoreDialog, for .gitignore or .git/info/exclude.
    private async Task EditGitIgnoreAsync(bool localExclude)
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        bool saved = await GitIgnoreWindow.ShowAsync(this, path, localExclude);
        _plugins.RaisePost(PluginEvent.EditGitIgnore, saved);
        if (saved)
        {
            await OpenRepositoryAsync(path);
        }
    }

    // Upstream's GitUICommands.UpdateSubmodules after a checkout: with submodules, they are updated as Settings > General's
    // "Update submodules on checkout" says, or after upstream's question, whose remembered answer sets that setting.
    private async Task<bool> UpdateSubmodulesAfterCheckoutAsync(string repositoryPath)
    {
        if (_repository.Submodules.Count == 0)
        {
            return true;
        }

        bool update;
        if (_preferences.UpdateSubmodulesOnCheckout is { } setting)
        {
            update = setting;
        }
        else
        {
            ConfirmWindow question = new(UpdateSubmodulesQuestion, "Yes", "Update submodules", offerDontShowAgain: true,
                cancelText: "No", dontShowAgainText: "Remember choice");
            update = await question.ShowDialog<bool>(this);
            if (question.DontShowAgain)
            {
                _preferences.UpdateSubmodulesOnCheckout = update;
            }
        }

        return !update || await UpdateSubmodulesAsync(repositoryPath, null);
    }

    // Upstream's MessageBoxes texts of that question.
    private const string UpdateSubmodulesQuestion =
        "Update submodules on checkout?\n\nSince this repository has submodules, it's necessary to update them on every "
        + "checkout.\n\nThis will just checkout on the submodule the commit determined by the superproject.";

    // Upstream's submodule dialogs raise PostUpdateSubmodules (the auto compile plugin builds after it).
    private async Task<bool> UpdateSubmodulesAsync(string repositoryPath, string? submodulePath)
    {
        bool done = await _actions.UpdateSubmodulesAsync(repositoryPath, submodulePath);
        _plugins.RaisePost(PluginEvent.UpdateSubmodules, done);
        return done;
    }

    private async Task OpenCommitFileInDiffToolAsync() => await OpenCommitFileInDiffToolAsync(tool: null);

    private async Task OpenCommitFileInDiffToolAsync(string? tool)
    {
        if (RepositoryPath is { } path && CommitFilesList.SelectedItem is CommitFile groupItem
                                       && GroupFile(groupItem) is { } grouped)
        {
            if (!grouped.Group.IsCombinedDiff)
            {
                await OpenGroupFileInDiffToolAsync(path, grouped.Group, grouped.File, tool);
            }

            return;
        }

        if (RepositoryPath is { } repositoryPath && _commits.Selected is { } row &&
            CommitFilesList.SelectedItem is CommitFile file)
        {
            (string? hash, bool staged) = DiffSource(row);
            await _actions.RunDiffToolAsync(repositoryPath, file.Path, hash, staged, tool);
        }
    }

    // The diff of a commit file: the working tree's unstaged or staged change for the artificial rows, as upstream's grid.
    private static (string? Hash, bool Staged) DiffSource(CommitRow row)
        => row.Hash == ArtificialCommits.WorkTreeHash ? (null, false)
            : row.Hash == ArtificialCommits.IndexHash ? (null, true)
            : (row.Hash, false);

    private void ShowCommitDiff()
    {
        if (RepositoryPath is { } repositoryPath && CommitFilesList.SelectedItem is CommitFile groupItem
                                                 && GroupFile(groupItem) is { } grouped)
        {
            new DiffWindow(grouped.File.Path,
                view => ShowGroupFileDiffAsync(view, repositoryPath, grouped.Group, grouped.File)).Show(this);
            return;
        }

        if (RepositoryPath is { } path && _commits.Selected is { } row &&
            CommitFilesList.SelectedItem is CommitFile file)
        {
            (string? hash, bool staged) = DiffSource(row);
            new DiffWindow(path, hash, file.Path, staged).Show(this);
        }
    }

    private void ShowSelectedCommitFileDiff()
    {
        CommitDiff.LineAction = SelectedFileLineAction();
        if (ShowDiffTabBlame())
        {
            return;
        }

        ShowCommitDiffButton.IsEnabled = CommitFilesList.SelectedItem is CommitFile;
        // Upstream has no difftool for the combined diff of a merge or the range diff item.
        CommitDiffToolButton.IsEnabled = CommitFilesList.SelectedItem is CommitFile
        {
            Group: not ({ IsCombinedDiff: true } or { IsRangeDiff: true })
        };

        // Upstream's ShowSelectedFolderAsync: a selected folder or group node shows the files below it.
        if (FileListNodes.SelectedNode(CommitFilesList) is { } node)
        {
            (string folder, string description) = FileStatusList.FolderDescription(node.Folder,
                [.. node.Files.OfType<CommitFile>().Select(folderFile => folderFile.Path)]);
            CommitDiff.ShowText(folder, description);
            return;
        }

        if (RepositoryPath is { } repositoryPath && CommitFilesList.SelectedItem is CommitFile groupItem
                                                 && GroupFile(groupItem) is { } grouped)
        {
            Run(() => ShowGroupFileDiffAsync(CommitDiff, repositoryPath, grouped.Group, grouped.File));
            return;
        }

        if (RepositoryPath is { } path && _commits.Selected is { } row &&
            CommitFilesList.SelectedItem is CommitFile file)
        {
            (string? hash, bool staged) = DiffSource(row);
            Run(() => CommitDiff.ShowAsync(path, hash, file.Path, staged, file.Path));
        }
    }

    private void UpdateBranchText()
    {
        string branch = _repository.CurrentBranch;
        string state = _repository.IsRebasing ? " (rebase in progress)" :
            _repository.IsMerging ? " (merge in progress)" : "";

        // Upstream's "Show ahead and behind information on status bar in browse window".
        string sync = _preferences.ShowAheadBehindData && _repository.Sync is { } status
            ? $" [ahead {status.Ahead}, behind {status.Behind}]"
            : "";
        BranchText.Text = branch.Length == 0 ? "" : $"Branch: {branch}{state}{sync}";
        StashCountText.IsVisible = _preferences.ShowStashCount && RepositoryPath is not null;
        StashCountText.Text = $"Stashes: {_repository.Stashes.Count}";
    }

    private void ShowFiles(string hash)
    {
        if (RepositoryPath is { } path)
        {
            new FileBrowserWindow(path, hash, RelevantRepositoryHost).Show(this);
        }
    }

    // Over the window that is active, as upstream shows an error over the form that ran the command: a modal window
    // (the stash window) runs operations whose errors arrive here, and an error owned by this window would open behind it.
    private void ShowError(string message)
    {
        _ = new ErrorWindow(message).ShowDialog(OwnedWindows.FirstOrDefault(window => window.IsActive) ?? this);
    }
}
