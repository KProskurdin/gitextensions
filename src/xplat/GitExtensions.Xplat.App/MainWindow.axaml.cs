using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GitCommands;
using GitCommands.Git;
using GitCommands.Utils;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Plugins;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;
using GitUI.ScriptsEngine;
using GitUIPluginInterfaces;

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

    // Activation refreshes the working-tree status at most this often, so switching windows back and forth stays cheap.
    private static readonly TimeSpan _activationRefreshInterval = TimeSpan.FromSeconds(2);

    private readonly GitDiscoveryResult? _git;
    private readonly GitCommitHistory _history;
    private readonly CommitListViewModel _commits;
    private readonly GitRepositoryService _repositoryService;
    private readonly RepositoryViewModel _repository;
    private readonly RepositoryOperationsViewModel _actions = new(new GitOperations(AppServices.AskPassExecutable));
    private readonly RecentRepositoriesViewModel _recent = new(AppServices.RecentRepositories);
    private readonly ICommitMessageStore _commitMessages = AppServices.CommitMessages;
    private Task _commitDraftSaved = Task.CompletedTask;
    private readonly IAppPreferences _preferences = AppServices.Preferences;
    private readonly IProcessLauncher _launcher = new SystemProcessLauncher();
    private readonly IFileManager _fileManager;
    private readonly ITerminalLauncher _terminal;
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
    private int _fileTreeVersion;
    private DateTime _lastActivationRefresh = DateTime.MinValue;

    public MainWindow(GitDiscoveryResult? git)
    {
        _git = git;
        _history = new GitCommitHistory(
            () => new CommitDateStyle(_preferences.RelativeDate, _preferences.ShowAuthorDate),
            () => _preferences.RevisionSortOrder, AppServices.RevisionLinks);
        _repositoryService = new GitRepositoryService(RefSorting);
        _commits = new CommitListViewModel(_history);
        _repository = new RepositoryViewModel(_repositoryService);
        _actions.EditorCommand = AppServices.EditorCommand;
        _fileManager = new SystemFileManager(_launcher, HostPlatform.Current);
        _terminal = new SystemTerminalLauncher(_launcher,
            TerminalCommand.Default(HostPlatform.Current, Environment.GetEnvironmentVariable("TERMINAL")));
        _plugins = new PluginHost(this, _actions, plugin => Run(() => ShowSettingsAsync(plugin, pluginsPage: true)),
            ShowCommitWindow);
        InitializeComponent();
        WindowPlacementTracker.Attach(this, "Xplat.MainWindow");
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://GitExtensions/Assets/git-extensions-logo-256px.png")));

        // The branch choice is upstream'"'"'s ShowCurrentBranchOnly; it is set before the toolbar reacts to changes.
        BranchScopeBox.SelectedIndex = _preferences.ShowCurrentBranchOnly ? 1 : 0;
        ShowSortMenus();
        ShowGridTooltips();
        _ = _commits.ApplyFilterAsync(new RevisionFilter(CurrentBranchOnly: _preferences.ShowCurrentBranchOnly));
        WireToolbar();
        WireMenus();
        WireLeftPanel();
        WireCommitList();

        _commits.PropertyChanged += (_, e) => OnCommitsChanged(e.PropertyName);
        _repository.PropertyChanged += (_, e) => OnRepositoryChanged(e.PropertyName);
        _actions.PropertyChanged += (_, e) => OnActionsChanged(e.PropertyName);
        _actions.RepositoryChanged += (_, e) => Run(() => OpenRepositoryAsync(e.RepositoryPath));
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
    private BranchInfo? SelectedBranch => (BranchTreeView.SelectedItem as BranchTreeNode)?.Branch;

    private void WireToolbar()
    {
        OpenButton.Click += (_, _) => Run(() => OpenRepositoryAsync(PathBox.Text?.Trim() ?? ""));
        RefreshButton.Click += (_, _) => RefreshRepository();

        // Creates a repository in the path box, or in a new folder at that path; the window then opens it.
        InitButton.Click += (_, _) => Run(() => _actions.InitAsync(PathBox.Text ?? ""));
        CommitDialogButton.Click += (_, _) => ShowCommitWindow();
        PullButton.Click += (_, _) => Pull();
        PushButton.Click += (_, _) => Push();
        PushTagsButton.Click += (_, _) => RunOnRepository(path =>
            WithScriptsAsync(path, ScriptEvent.BeforePush, ScriptEvent.AfterPush,
                () => _actions.PushTagsAsync(path, RemoteName)));
        FetchButton.Click += (_, _) => Fetch();
        CloneButton.Click += (_, _) => Run(CloneAsync);
        OpenFolderButton.Click += (_, _) => RunOnRepositoryFolder(path => _fileManager.OpenFolder(path));
        TerminalButton.Click += (_, _) => RunOnRepositoryFolder(path => _terminal.OpenTerminal(path));
        FilesButton.Click += (_, _) => ShowFiles(_commits.Selected?.Hash ?? "HEAD");
        ReflogButton.Click += (_, _) => Run(ShowReflogAsync);
        FilterBox.TextChanged += (_, _) => _commits.FilterText = FilterBox.Text ?? "";
        SearchBox.KeyDown += OnSearchKeyDown;
        LoadMoreButton.Click += (_, _) => Run(_commits.LoadMoreAsync);
        BranchScopeBox.SelectionChanged += (_, _) => Run(ApplyBranchScopeAsync);
        RevisionFilterButton.Click += (_, _) => Run(EditRevisionFilterAsync);
        ResolveConflictsButton.Click += (_, _) => ShowConflicts();
        ContinueActionButton.Click += (_, _) => ContinueAction();
        AbortActionButton.Click += (_, _) => AbortAction();
        SkipRebaseMenuItem.Click += (_, _) => RunOnRepository(path => _actions.SkipRebaseAsync(path));
        EditRebaseTodoMenuItem.Click += (_, _) => RunOnRepository(path => _actions.EditRebaseTodoAsync(path));
        SkipPatchMenuItem.Click += (_, _) => RunOnRepository(path => _actions.SkipPatchAsync(path));
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
        FileExplorerMenuItem.Click += (_, _) => RunOnRepositoryFolder(path => _fileManager.OpenFolder(path));
        TerminalMenuItem.Click += (_, _) => RunOnRepositoryFolder(path => _terminal.OpenTerminal(path));
        FilesMenuItem.Click += (_, _) => ShowFiles(_commits.Selected?.Hash ?? "HEAD");
        ReflogMenuItem.Click += (_, _) => Run(ShowReflogAsync);
        GitIgnoreMenuItem.Click += (_, _) => Run(EditGitIgnoreAsync);
        WorktreesMenuItem.Click += (_, _) => Run(ShowWorktreesAsync);
        CommitMenuItem.Click += (_, _) => ShowCommitWindow();
        PullDialogMenuItem.Click += (_, _) => Run(ShowPullDialogAsync);
        PushDialogMenuItem.Click += (_, _) => Run(ShowPushDialogAsync);
        PullMenuItem.Click += (_, _) => Pull();
        PushMenuItem.Click += (_, _) => Push();
        FetchMenuItem.Click += (_, _) => Fetch();
        StashMenuItem.Click += (_, _) => Run(() => StashAsync());
        StashPopMenuItem.Click += (_, _) => PopTopStash();
        CreateBranchMenuItem.Click += (_, _) => Run(() => PromptCreateBranchAsync(startPoint: null));
        MergeMenuItem.Click += (_, _) => MergeSelectedBranch();
        RebaseMenuItem.Click += (_, _) => RebaseOnSelectedBranch();
        CreateTagMenuItem.Click += (_, _) => Run(() => PromptCreateTagAsync(_commits.Selected?.Hash ?? "HEAD"));
        BisectMenuItem.Click += (_, _) => Run(ShowBisectAsync);
        ResolveConflictsMenuItem.Click += (_, _) => ShowConflicts();
        CommandLogMenuItem.Click += (_, _) => CommandLogWindow.ShowFor(this);
        SettingsMenuItem.Click += (_, _) => Run(() => ShowSettingsAsync());
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
        AboutMenuItem.Click += (_, _) =>
            _ = new AboutWindow(_git?.Version?.ToString() is { } version ? $"git {version}" : "git").ShowDialog(this);
    }

    private void WireLeftPanel()
    {
        BranchTreeView.SelectionChanged += (_, _) => UpdateBusyState();
        BranchTreeView.DoubleTapped += (_, _) => CheckoutSelectedBranch();

        // As upstream's branch dialogs, the name is fixed up when the box is left.
        NewBranchBox.LostFocus += (_, _) => NewBranchBox.Text = NormaliseBranchName(NewBranchBox.Text ?? "");
        CheckoutButton.Click += (_, _) => CheckoutSelectedBranch();
        MergeButton.Click += (_, _) => MergeSelectedBranch();
        RebaseButton.Click += (_, _) => RebaseOnSelectedBranch();
        DeleteBranchButton.Click += (_, _) => DeleteSelectedBranch();
        ForceDeleteBranchButton.Click += (_, _) => Run(ForceDeleteBranchAsync);
        CreateBranchButton.Click += (_, _) => Run(CreateBranchAsync);
        CreateBranchAtCommitButton.Click += (_, _) => Run(CreateBranchAtCommitAsync);
        RenameBranchButton.Click += (_, _) => Run(RenameBranchAsync);
        TagList.SelectionChanged += (_, _) => UpdateBusyState();
        CreateTagButton.Click += (_, _) => Run(CreateTagAsync);
        DeleteTagButton.Click += (_, _) => RunOnSelectedTag((path, tag) => _actions.DeleteTagAsync(path, tag));
        StashList.SelectionChanged += (_, _) => UpdateBusyState();
        StashButton.Click += (_, _) => Run(() => StashAsync());
        ApplyStashButton.Click += (_, _) =>
            RunOnSelectedStash((path, stash) => _actions.ApplyStashAsync(path, stash.Name));
        PopStashButton.Click += (_, _) => RunOnSelectedStash((path, stash) => _actions.PopStashAsync(path, stash.Name));
        DropStashButton.Click += (_, _) => RunOnSelectedStash(async (path, stash) =>
            await ConfirmAsync(Confirmation.StashDrop, Confirmations.StashDropQuestion, "Drop",
                "Drop Stash Confirmation")
            && await _actions.DropStashAsync(path, stash.Name));
        StashDiffButton.Click += (_, _) => ShowStashDiff();
        RemoteList.SelectionChanged += (_, _) => UpdateBusyState();
        SubmoduleList.SelectionChanged += (_, _) => UpdateBusyState();
        OpenSubmoduleButton.Click += (_, _) => OpenSelectedSubmodule();
        UpdateSubmoduleButton.Click += (_, _) =>
            RunOnSelectedSubmodule((path, submodule) => UpdateSubmodulesAsync(path, submodule.Path));
        UpdateSubmodulesButton.Click += (_, _) => RunOnRepository(path => UpdateSubmodulesAsync(path, null));
        SyncSubmodulesButton.Click += (_, _) => RunOnRepository(path => _actions.SyncSubmodulesAsync(path, path: null));
        AddRemoteButton.Click += (_, _) => Run(AddRemoteAsync);
        RemoveRemoteButton.Click += (_, _) => RunOnSelectedRemote(RemoveRemoteAfterConfirmAsync);
        RenameRemoteButton.Click += (_, _) => RunOnSelectedRemote(RenameRemoteAsync);
        SetRemoteUrlButton.Click += (_, _) => RunOnSelectedRemote(SetRemoteUrlAsync);
        DeleteRemoteTagButton.Click += (_, _) => RunOnSelectedTag(DeleteRemoteTagAfterConfirmAsync);
        RecentList.DoubleTapped += (_, _) => OpenSelectedRecent();
        RecentList.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                OpenSelectedRecent();
            }
        };
        RecentList.SelectionChanged += (_, _) => RemoveRecentButton.IsEnabled = RecentList.SelectedItem is not null;
        RemoveRecentButton.Click += (_, _) =>
        {
            if (RecentList.SelectedItem is RecentRepository recent)
            {
                Run(() => _recent.RemoveAsync(recent.Path));
            }
        };
    }

    private void WireCommitList()
    {
        DetailTabs.SelectionChanged += (_, _) => ShowFileTreeIfVisible();
        FileTreeView.SelectionChanged += (_, _) => Run(ShowSelectedTreeFileAsync);
        CommitList.SelectionChanged += (_, _) =>
        {
            CommitDiff.Clear();
            Run(() => _commits.SelectAsync((CommitList.SelectedItem as CommitListItem)?.Row));
        };
        CommitList.DoubleTapped += (_, _) => DetailTabs.SelectedItem = DiffTab;
        CommitContextMenu.Opening += (_, _) => ShowGridScripts();
        CommitFilesContextMenu.Opening += (_, _) => ShowFileListScripts();
        CommitFilesList.SelectionChanged += (_, _) => ShowSelectedCommitFileDiff();
        CopyHashButton.Click += (_, _) => Run(() => CopyDetailAsync(details => details.Hash));
        CopyMessageButton.Click += (_, _) => Run(() => CopyDetailAsync(details => details.Message));
        CopyHashMenuItem.Click += (_, _) => Run(() => CopyDetailAsync(details => details.Hash));
        CopyMessageMenuItem.Click += (_, _) => Run(() => CopyDetailAsync(details => details.Message));
        ShowCommitDiffButton.Click += (_, _) => ShowCommitDiff();
        CommitDiffToolButton.Click += (_, _) => Run(OpenCommitFileInDiffToolAsync);
        CreateBranchHereMenuItem.Click += (_, _) => Run(() => PromptCreateBranchAsync(_commits.Selected?.Hash));
        CreateTagHereMenuItem.Click += (_, _) => Run(() => PromptCreateTagAsync(_commits.Selected?.Hash ?? "HEAD"));
        BrowseFilesHereMenuItem.Click += (_, _) => ShowFiles(_commits.Selected?.Hash ?? "HEAD");
        CheckoutCommitMenuItem.Click +=
            (_, _) => RunOnSelectedCommit((path, row) =>
                WithScriptsAsync(path, ScriptEvent.BeforeCheckout, ScriptEvent.AfterCheckout,
                    () => CheckoutLocalBranchAsync(path, row.Hash), PluginEvent.CheckoutRevision));
        CherryPickMenuItem.Click +=
            (_, _) => RunOnSelectedCommit((path, row) => _actions.CherryPickAsync(path, row.Hash));
        RevertMenuItem.Click += (_, _) => RunOnSelectedCommit((path, row) => _actions.RevertAsync(path, row.Hash));
        ResetSoftMenuItem.Click += (_, _) =>
            RunOnSelectedCommit((path, row) => _actions.ResetAsync(path, row.Hash, ResetMode.Soft));
        ResetMixedMenuItem.Click += (_, _) =>
            RunOnSelectedCommit((path, row) => _actions.ResetAsync(path, row.Hash, ResetMode.Mixed));
        ResetHardMenuItem.Click += (_, _) => Run(ResetHardAsync);
        RebaseOnCommitMenuItem.Click += (_, _) => Run(() => RebaseOnSelectedCommitAsync(interactive: false));
        RebaseInteractiveMenuItem.Click += (_, _) => Run(() => RebaseOnSelectedCommitAsync(interactive: true));
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
        Run(async () =>
        {
            IReadOnlyList<string> files = await _history.LoadTreeAsync(path, hash);
            if (version == _fileTreeVersion)
            {
                FileTreeView.ItemsSource = FileTree.Build(files);
            }
        });
    }

    private async Task ShowSelectedTreeFileAsync()
    {
        if (RepositoryPath is not { } path || _fileTreeHash is not { } hash ||
            FileTreeView.SelectedItem is not FileTreeNode { IsFolder: false } file)
        {
            return;
        }

        string? content = await _history.LoadFileTextAsync(path, hash, file.Path);
        if (FileTreeView.SelectedItem == file)
        {
            FileContentView.ShowText(file.Path, content);
        }
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
        PullMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.QuickPull);
        PushMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.QuickPush);
        FetchMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.QuickFetch);
        StashMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.Stash);
        StashPopMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.StashPop);
        CreateBranchMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.CreateBranch);
        MergeMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.Merge);
        RebaseMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.Rebase);
        CreateTagMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.CreateTag);
        SettingsMenuItem.InputGesture = Hotkeys.GestureFor(BrowseCommand.Settings);
    }

    // Event handlers start their async work through this, so an exception that escapes it is shown, not fatal.
    private void Run(Func<Task> action) => UiActions.Run(action, ex => ShowError(ex.Message));

    private async Task OnOpenedAsync()
    {
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
            return;
        }

        PathBox.Text = initial;
        await OpenRepositoryAsync(initial);

        if (Environment.GetEnvironmentVariable(BenchmarkEnvironmentVariable) is { Length: > 0 } benchmark)
        {
            await RunBenchmarkAsync(benchmark);
            return;
        }

        if (Screenshot.RequestedFile is { } screenshot)
        {
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
        // As upstream, the grid's and the left panel's own hotkeys act while they have the focus.
        if (IsWithin(e.Source, CommitList) && Hotkeys.Grid.Match(e) is { } gridCommand)
        {
            e.Handled = RunGridHotkey(gridCommand);
            return;
        }

        if (IsWithin(e.Source, BranchTreeView) && Hotkeys.LeftPanel.Match(e) is LeftPanelCommand.Delete)
        {
            DeleteSelectedBranch();
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
                Run(ShowPushDialogAsync);
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
                RunOnRepositoryFolder(path => _terminal.OpenTerminal(path));
                break;
            case BrowseCommand.Settings:
                Run(() => ShowSettingsAsync());
                break;
            case BrowseCommand.FocusFilter:
                FilterBox.Focus();
                break;
            case BrowseCommand.FocusRevisionGrid:
                CommitList.Focus();
                break;
            case BrowseCommand.FocusLeftPanel:
                BranchTreeView.Focus();
                break;
            case BrowseCommand.CreateBranch:
                Run(() => PromptCreateBranchAsync(startPoint: null));
                break;
            case BrowseCommand.CreateTag:
                Run(() => PromptCreateTagAsync(_commits.Selected?.Hash ?? "HEAD"));
                break;
            case BrowseCommand.Merge:
                MergeSelectedBranch();
                break;
            case BrowseCommand.Rebase:
                RebaseOnSelectedBranch();
                break;
            case BrowseCommand.Stash:
                Run(() => StashAsync());
                break;
            case BrowseCommand.StashPop:
                PopTopStash();
                break;
        }
    }

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
                Run(() => ApplyRevisionFilterAsync(
                    new RevisionFilter(CurrentBranchOnly: _commits.Filter.CurrentBranchOnly)));
                return true;
            case GridCommand.ShowAllBranches:
                BranchScopeBox.SelectedIndex = 0;
                return true;
            case GridCommand.ShowCurrentBranchOnly:
                BranchScopeBox.SelectedIndex = 1;
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

    private async Task ApplyBranchScopeAsync()
    {
        bool currentOnly = BranchScopeBox.SelectedIndex == 1;
        if (currentOnly == _commits.Filter.CurrentBranchOnly)
        {
            return;
        }

        _preferences.ShowCurrentBranchOnly = currentOnly;
        await _commits.ApplyFilterAsync(_commits.Filter with { CurrentBranchOnly = currentOnly });
    }

    private async Task EditRevisionFilterAsync()
    {
        if (await new FilterWindow(_commits.Filter).ShowDialog<RevisionFilter?>(this) is { } filter)
        {
            await ApplyRevisionFilterAsync(filter);
        }
    }

    private async Task ApplyRevisionFilterAsync(RevisionFilter filter)
    {
        await _commits.ApplyFilterAsync(filter);
        RevisionFilterButton.Content = filter.IsNarrowed ? "Filter (on)..." : "Filter...";
    }

    // Enter runs the search over the whole history; an empty box shows the first page again.
    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Run(() => _commits.SearchAsync(SearchBox.Text ?? ""));
        }
    }

    private async Task OpenRepositoryAsync(string path)
    {
        // A reload can move HEAD, so the tree is read again.
        _fileTreeHash = null;
        await _commits.OpenAsync(path);
        if (RepositoryPath is { } repositoryPath)
        {
            PathBox.Text = repositoryPath;
            ShowRepositoryPanels();
            await _repository.RefreshAsync(repositoryPath);
            await _recent.AddAsync(repositoryPath);

            // Upstream's RecentWorkingDir, which the next start can open.
            _preferences.RecentWorkingDir = repositoryPath;
        }

        RegisterPlugins();
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
    }

    private void OpenSelectedRecent()
    {
        if (RecentList.SelectedItem is RecentRepository recent && _git?.Status != GitDiscoveryStatus.NotFound)
        {
            PathBox.Text = recent.Path;
            Run(() => OpenRepositoryAsync(recent.Path));
        }
    }

    private void ShowRecentMenu()
    {
        List<MenuItem> items = [];
        foreach (RecentRepository recent in _recent.Items)
        {
            MenuItem item = new() { Header = recent.Display };
            item.Click += (_, _) => Run(() => OpenRepositoryAsync(recent.Path));
            items.Add(item);
        }

        RecentMenu.ItemsSource = items;
        RecentMenu.IsEnabled = items.Count > 0;
        RecentList.ItemsSource = _recent.Items;
        RemoveRecentButton.IsEnabled = RecentList.SelectedItem is not null;
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
            PathBox.Text = folder;
            await OpenRepositoryAsync(folder);
        }
    }

    private async Task PickAndInitRepositoryAsync()
    {
        if (await PickFolderAsync("Create a repository in this folder") is { } folder)
        {
            PathBox.Text = folder;
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

    private async Task CloneAsync()
    {
        CloneRequest? request =
            await new CloneWindow(_preferences.DefaultCloneDestinationPath).ShowDialog<CloneRequest?>(this);
        if (request is not null)
        {
            await _actions.CloneAsync(request.Url, request.TargetPath);
        }
    }

    // Pull and push use the remote the branch tracks, falling back to origin for a branch without one.
    private string TrackingRemote() => _repository.TrackingRemote ?? RemoteName;

    private void Pull() =>
        RunOnCurrentBranch((path, branch) => WithScriptsAsync(path, ScriptEvent.BeforePull, ScriptEvent.AfterPull,
            () => _actions.PullAsync(path, TrackingRemote(), branch, RebaseOnPullCheck.IsChecked == true)));

    private void Push() => RunOnCurrentBranch(async (path, branch) =>
        await ConfirmPushAsync(TrackingRemote(), branch)
        && await WithScriptsAsync(path, ScriptEvent.BeforePush, ScriptEvent.AfterPush,
            () => _actions.PushAsync(path, TrackingRemote(), branch)));

    private void Fetch() =>
        RunOnRepository(async path =>
            (PruneCheck.IsChecked != true || await ConfirmPruneAsync(RemoteName))
            && await WithScriptsAsync(path, ScriptEvent.BeforeFetch, ScriptEvent.AfterFetch,
                () => _actions.FetchAsync(path, RemoteName, PruneCheck.IsChecked == true)));

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
    // Upstream's ShowRevisionGridTooltips; upstream needs a restart, here it applies when the settings are saved.
    private void ShowGridTooltips() => ToolTip.SetServiceEnabled(CommitList, _preferences.ShowRevisionGridTooltips);

    // Upstream's branch sort (RefsSortBy, RefsSortOrder, done by git) and priorities, read at each refresh.
    private RefSorting RefSorting()
        => new(_preferences.RefsSortBy, _preferences.RefsSortOrder, _preferences.PrioritizedBranchNames,
            _preferences.PrioritizedRemoteNames);

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

    // Upstream's file list menu (FileStatusList): scripts shown in file lists are items of the menu, the others under "Run
    // script"; they run with the selected files.
    private void ShowFileListScripts()
        => ScriptMenus.Fill(CommitFilesContextMenu, CommitFilesRunScriptMenuItem,
            script => script.OnEvent == ScriptEvent.ShowInFileList,
            script => RunOnRepository(path => Scripts.RunAsync(script, path, SelectedHashes, FileScriptOptions())));

    // As upstream's FormBrowse.GetScriptOptionsProvider: the files of the tab on show, the File tree's or the Diff tab's, and
    // the line selected in its viewer; none on the other tabs.
    private IReadOnlyDictionary<string, IReadOnlyList<string>>? FileScriptOptions()
    {
        if (DetailTabs.SelectedItem == FileTreeTab)
        {
            return ScriptFileOptions.For(
                FileTreeView.SelectedItem is FileTreeNode node ? [node.Path] : [], FileContentView.CurrentFileLine);
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
    private async Task ShowPushDialogAsync()
    {
        if (RepositoryPath is not { } path || !PushMenuItem.IsEnabled)
        {
            return;
        }

        List<string> localBranches =
            [.. _repository.Branches.Where(branch => !branch.IsRemote).Select(branch => branch.Name)];
        PushRequest? request = await new PushWindow(_repository.Remotes, TrackingRemote(), localBranches,
                _repository.CurrentBranch,
                hasUpstream: _repository.TrackingRemote is not null)
            .ShowDialog<PushRequest?>(this);
        if (request is not null
            && await ConfirmPushAsync(request.Remote,
                request.RemoteBranch is { Length: > 0 } remoteBranch ? remoteBranch : request.LocalBranch))
        {
            await WithScriptsAsync(path, ScriptEvent.BeforePush, ScriptEvent.AfterPush,
                () => _actions.PushAsync(path, request));
        }
    }

    private async Task ShowPullDialogAsync()
    {
        if (RepositoryPath is not { } path || !FetchMenuItem.IsEnabled)
        {
            return;
        }

        PullRequest? request =
            await new PullWindow(_repository.Remotes, TrackingRemote(), RebaseOnPullCheck.IsChecked == true)
                .ShowDialog<PullRequest?>(this);
        if (request is not null && (!request.Prune || await ConfirmPruneAsync(request.Remote)))
        {
            // As upstream's pull dialog, "fetch only" runs the fetch scripts and a pull the pull scripts.
            bool fetchOnly = request.Action == PullAction.FetchOnly;
            await WithScriptsAsync(path, fetchOnly ? ScriptEvent.BeforeFetch : ScriptEvent.BeforePull,
                fetchOnly ? ScriptEvent.AfterFetch : ScriptEvent.AfterPull, () => _actions.PullAsync(path, request));
        }
    }

    // One commit window at a time, as upstream; asking again brings the open one forward.
    private void ShowCommitWindow()
    {
        if (RepositoryPath is not { } path || !CommitDialogButton.IsEnabled)
        {
            return;
        }

        if (_commitWindow is not null)
        {
            _commitWindow.Activate();
            return;
        }

        // As upstream's StartCommitDialog, the plugin events wrap the window, not each commit made in it.
        if (!_plugins.RaisePre(PluginEvent.Commit))
        {
            return;
        }

        CommitWindow window = new(path, _repository, _actions, _repositoryService, _preferences,
            () => OpenRepositoryAsync(path),
            _commitMessages, _plugins.ExecuteByName);
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
    }

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

    private void ShowConflicts()
    {
        if (RepositoryPath is { } path)
        {
            new ConflictsWindow(path, _repository, _actions).Show(this);
        }
    }

    // As upstream's settings dialog, the plugins hear about the change (PostSettings) whether or not it was saved.
    private async Task ShowSettingsAsync(IGitPlugin? plugin = null, bool pluginsPage = false)
    {
        bool saved = await new SettingsWindow(_preferences, RepositoryPath, await LoadPluginsAsync(),
            plugin, pluginsPage).ShowDialog<bool>(this);
        if (saved)
        {
            ThemeApplier.Apply(_preferences);
            FontApplier.Apply(_preferences);
            ShowGridTooltips();
            ShowHotkeys();
            ShowUserScripts();
            ShowGridScripts();
            await _recent.LoadAsync();

            // The date and commit button settings show on the next read.
            ShowAuthorDateMenuItem.IsChecked = _preferences.ShowAuthorDate;
            ShowRelativeDateMenuItem.IsChecked = _preferences.RelativeDate;
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

    // Upstream's branch dialogs fix up the name (AutoNormaliseBranchName); done again where the name is used, as a button
    // can be pressed while the name box still has the focus.
    private string NormaliseBranchName(string name)
        => BranchNames.Normalise(name, _preferences.AutoNormaliseBranchName, _preferences.AutoNormaliseSymbol);

    // Upstream's ConfirmSuppressible: asks unless the user turned the question off (Settings > Confirmations).
    private Task<bool> ConfirmAsync(Confirmation confirmation, string message, string confirmText, string caption)
        => ConfirmWindow.AskAsync(this, _preferences, confirmation, message, confirmText, caption);

    // As upstream's left panel: asks first only when upstream's ConfirmBranchCheckout is on (it is off by default).
    private void CheckoutSelectedBranch() => RunOnSelectedBranch(async (path, branch) =>
        await ConfirmAsync(Confirmation.BranchCheckout,
            string.Format(Confirmations.BranchCheckoutQuestion, branch.Name),
            "Checkout", "Confirm checkout")
        && await WithScriptsAsync(path, ScriptEvent.BeforeCheckout, ScriptEvent.AfterCheckout, () => branch.IsRemote
            ? _actions.CheckoutRemoteAsync(path, branch.Name)
            : CheckoutLocalBranchAsync(path, branch.Name), PluginEvent.CheckoutBranch));

    // Like upstream, local changes to tracked files make the checkout ask what to do with them; the choice becomes the
    // default for next time (upstream's checkoutbranchaction). Untracked files do not block a checkout and are not counted.
    private async Task<bool> CheckoutLocalBranchAsync(string path, string branch)
    {
        int changes = _repository.Changes.Where(change => change.Kind != ChangeKind.Untracked)
            .Select(change => change.Path).Distinct(StringComparer.Ordinal).Count();
        if (changes == 0)
        {
            return await _actions.CheckoutAsync(path, branch);
        }

        // As upstream's FormCheckoutBranch: with UseDefaultCheckoutBranchAction the last choice is used without asking,
        // unless AlwaysShowCheckoutBranchDlg asks every time.
        if (_preferences.UseDefaultCheckoutBranchAction && !_preferences.AlwaysShowCheckoutBranchDlg)
        {
            return await _actions.CheckoutAsync(path, branch, _preferences.CheckoutBranchAction);
        }

        if (await new CheckoutWindow(branch, changes, _preferences.CheckoutBranchAction)
                .ShowDialog<LocalChangesAction?>(this) is not { } action)
        {
            return false;
        }

        _preferences.CheckoutBranchAction = action;
        return await _actions.CheckoutAsync(path, branch, action);
    }

    private void MergeSelectedBranch() => RunOnSelectedBranch((path, branch) =>
        WithScriptsAsync(path, ScriptEvent.BeforeMerge, ScriptEvent.AfterMerge,
            () => _actions.MergeAsync(path, branch.Name)));

    private void RebaseOnSelectedBranch() =>
        RunOnSelectedBranch((path, branch) => _actions.RebaseAsync(path, branch.Name));

    private void PopTopStash()
    {
        if (RepositoryPath is { } path && _repository.Stashes.Count > 0)
        {
            Run(() => _actions.PopStashAsync(path, _repository.Stashes[0].Name));
        }
    }

    private async Task PromptCreateBranchAsync(string? startPoint)
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        string at = startPoint is null ? "HEAD" : startPoint[..Math.Min(8, startPoint.Length)];
        PromptResult? result = await new PromptWindow("Create branch",
                $"Name of the new branch at {at}. It is checked out after it is created.", "Create")
            .ShowDialog<PromptResult?>(this);
        if (result is not null)
        {
            await _actions.CreateBranchAsync(path, NormaliseBranchName(result.Value), checkout: true, startPoint);
        }
    }

    private async Task PromptCreateTagAsync(string commit)
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        PromptResult? result = await new PromptWindow("Create tag",
                $"Name of the new tag at {commit[..Math.Min(8, commit.Length)]}.", "Create",
                secondPlaceholder: "Message (optional: makes an annotated tag)")
            .ShowDialog<PromptResult?>(this);
        if (result is not null)
        {
            await _actions.CreateTagAsync(path, result.Value, commit, result.SecondValue);
        }
    }

    private async Task CreateBranchAtCommitAsync()
    {
        if (RepositoryPath is { } path && _commits.Selected is { } row &&
            await _actions.CreateBranchAsync(path, NormaliseBranchName(NewBranchBox.Text ?? ""), checkout: true,
                row.Hash))
        {
            NewBranchBox.Text = "";
        }
    }

    // Git refuses to delete a branch with commits that are not merged; this deletes it anyway after upstream's question
    // (unless the user turned it off, as in upstream's FormDeleteBranch).
    private async Task ForceDeleteBranchAsync()
    {
        if (RepositoryPath is not { } path || SelectedBranch is not BranchInfo branch || branch.IsRemote ||
            branch.IsCurrent)
        {
            return;
        }

        if (await ConfirmAsync(Confirmation.DeleteUnmergedBranch, Confirmations.DeleteUnmergedBranchQuestion,
                "Force delete", "Delete Confirmation"))
        {
            await _actions.DeleteBranchAsync(path, branch.Name, force: true);
        }
    }

    private async Task AddRemoteAsync()
    {
        if (RepositoryPath is { } path &&
            await _actions.AddRemoteAsync(path, NewRemoteNameBox.Text ?? "", NewRemoteUrlBox.Text ?? ""))
        {
            NewRemoteNameBox.Text = "";
            NewRemoteUrlBox.Text = "";
        }
    }

    // Removing a remote also deletes its remote-tracking branches, so it is confirmed first.
    private async Task<bool> RemoveRemoteAfterConfirmAsync(string path, string name)
    {
        string message = $"Remove the remote {name}? Its remote-tracking branches are deleted locally.";
        return await new ConfirmWindow(message, "Remove").ShowDialog<bool>(this) &&
               await _actions.RemoveRemoteAsync(path, name);
    }

    private async Task<bool> RenameRemoteAsync(string path, string name)
    {
        PromptResult? result = await new PromptWindow("Rename remote",
                $"New name of the remote {name}. Its remote-tracking branches are renamed too.",
                "Rename", initialValue: name)
            .ShowDialog<PromptResult?>(this);
        return result is not null && result.Value != name && await _actions.RenameRemoteAsync(path, name, result.Value);
    }

    // The URL is read from the repository's own config, so the box starts with the current value.
    private async Task<bool> SetRemoteUrlAsync(string path, string name)
    {
        string current = await AppServices.GitConfig.GetAsync(ConfigScope.Local, $"remote.{name}.url", path);
        PromptResult? result = await new PromptWindow("Set remote URL", $"URL or path of the remote {name}.", "Save",
                initialValue: current)
            .ShowDialog<PromptResult?>(this);
        return result is not null && result.Value != current &&
               await _actions.SetRemoteUrlAsync(path, name, result.Value);
    }

    // Deleting a tag on the remote removes it for everyone who fetches, so it is confirmed first; the local tag stays.
    private async Task<bool> DeleteRemoteTagAfterConfirmAsync(string path, string tag)
    {
        string remote = TrackingRemote();
        string message = $"Delete the tag {tag} on {remote}? Everyone who fetches will lose it. The local tag is kept.";
        return await new ConfirmWindow(message, "Delete on remote").ShowDialog<bool>(this) &&
               await _actions.DeleteRemoteTagAsync(path, remote, tag);
    }

    private void RunOnSelectedRemote(Func<string, string, Task<bool>> action)
    {
        if (RepositoryPath is { } path && RemoteList.SelectedItem is string name)
        {
            Run(() => action(path, name));
        }
    }

    // Renames the selected local branch to the name in the box; the checked-out branch can be renamed too.
    private async Task RenameBranchAsync()
    {
        if (RepositoryPath is { } path && SelectedBranch is BranchInfo { IsRemote: false } branch &&
            await _actions.RenameBranchAsync(path, branch.Name, NormaliseBranchName(NewBranchBox.Text ?? "")))
        {
            NewBranchBox.Text = "";
        }
    }

    // Aborting drops whatever was resolved by hand, so it is confirmed first.
    private void AbortAfterConfirm(string operation, Func<string, Task<bool>> abort)
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        string message =
            $"Abort the {operation}? Conflicts you resolved by hand are lost and the branch goes back to before the {operation}.";
        Run(async () =>
        {
            if (await new ConfirmWindow(message, $"Abort {operation}").ShowDialog<bool>(this))
            {
                await abort(path);
            }
        });
    }

    // Reset mixed keeps the working changes; the branch moves to the chosen entry, so earlier commits can be recovered.
    private async Task ShowReflogAsync()
    {
        if (RepositoryPath is { } path && await new ReflogWindow(path).ShowDialog<string?>(this) is { } hash)
        {
            await _actions.ResetAsync(path, hash, ResetMode.Mixed);
        }
    }

    private async Task CreateBranchAsync()
    {
        if (RepositoryPath is { } path &&
            await _actions.CreateBranchAsync(path, NormaliseBranchName(NewBranchBox.Text ?? ""), checkout: true))
        {
            NewBranchBox.Text = "";
        }
    }

    // The whole stash, not one file: a stash is a commit that git shows as a diff.
    private void ShowStashDiff()
    {
        if (RepositoryPath is { } path && StashList.SelectedItem is StashInfo stash)
        {
            new DiffWindow(path, stash.Name, filePath: null, staged: false).Show(this);
        }
    }

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

    // As upstream's left panel, a submodule opens as a repository of its own in the browse window.
    private void OpenSelectedSubmodule()
    {
        if (RepositoryPath is { } path && SubmoduleList.SelectedItem is SubmoduleInfo { IsInitialized: true } submodule)
        {
            string submodulePath = Path.GetFullPath(Path.Combine(path, submodule.Path));
            Run(() => OpenRepositoryAsync(submodulePath));
        }
    }

    private void RunOnSelectedSubmodule(Func<string, SubmoduleInfo, Task<bool>> action)
    {
        if (RepositoryPath is { } path && SubmoduleList.SelectedItem is SubmoduleInfo submodule)
        {
            Run(() => action(path, submodule));
        }
    }

    private void RunOnSelectedStash(Func<string, StashInfo, Task<bool>> action)
    {
        if (RepositoryPath is { } path && StashList.SelectedItem is StashInfo stash)
        {
            Run(() => action(path, stash));
        }
    }

    private void RunOnSelectedTag(Func<string, string, Task<bool>> action)
    {
        if (RepositoryPath is { } path && TagList.SelectedItem is string tag)
        {
            Run(() => action(path, tag));
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

    // A remote branch is deleted on its own remote; origin/HEAD is a symbolic ref and is never deleted.
    // Deleting a remote branch removes it for everyone who fetches, so it is confirmed first.
    private async Task<bool> DeleteSelectedBranchAsync(string path, BranchInfo branch)
    {
        if (!branch.IsRemote)
        {
            return await _actions.DeleteBranchAsync(path, branch.Name, force: false);
        }

        int separator = branch.Name.IndexOf('/');
        if (separator < 0 || branch.Name.EndsWith("/HEAD", StringComparison.Ordinal))
        {
            return false;
        }

        string message =
            $"Delete the branch {branch.Name} on the remote? Everyone who fetches it will lose it. This cannot be undone.";
        if (!await new ConfirmWindow(message, "Delete").ShowDialog<bool>(this))
        {
            return false;
        }

        return await _actions.DeleteRemoteBranchAsync(path, branch.Name[..separator], branch.Name[(separator + 1)..]);
    }

    // A tag goes on the selected commit, or on HEAD when no commit is selected.
    private async Task CreateTagAsync()
    {
        if (RepositoryPath is { } path &&
            await _actions.CreateTagAsync(path, NewTagBox.Text ?? "", _commits.Selected?.Hash ?? "HEAD",
                TagMessageBox.Text ?? ""))
        {
            NewTagBox.Text = "";
            TagMessageBox.Text = "";
        }
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
                CommitFilesList.ItemsSource = _commits.CommitFiles;
                break;
            case nameof(CommitListViewModel.VisibleRows):
                // A new page or the quick filter replaces the items; the selected commit stays selected when it is still shown.
                string? selectedHash = _commits.Selected?.Hash;
                CommitList.ItemsSource = _commits.VisibleRows;
                if (selectedHash is not null
                    && _commits.VisibleRows.FirstOrDefault(item => item.Row.Hash == selectedHash) is { } selectedItem)
                {
                    CommitList.SelectedItem = selectedItem;
                }

                break;
            case nameof(CommitListViewModel.Status):
                StatusText.Text = _commits.Status;
                break;
            case nameof(CommitListViewModel.RepositoryName):
                Title = _commits.RepositoryName.Length == 0
                    ? "Git Extensions"
                    : $"{_commits.RepositoryName} - Git Extensions";
                break;
            case nameof(CommitListViewModel.HasMore):
                LoadMoreButton.IsVisible = _commits.HasMore;
                break;
            case nameof(CommitListViewModel.IsLoading):
                UpdateBusyState();
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
            case nameof(RepositoryViewModel.Branches):
                BranchTreeView.ItemsSource = BranchTree.Build(_repository.Branches, RefSorting());
                UpdateBusyState();
                break;
            case nameof(RepositoryViewModel.Changes):
                UpdateCommitButton();
                UpdateStateBanner();
                break;
            case nameof(RepositoryViewModel.Stashes):
                StashList.ItemsSource = _repository.Stashes;
                UpdateBusyState();
                break;
            case nameof(RepositoryViewModel.Tags):
                TagList.ItemsSource = _repository.Tags;
                break;
            case nameof(RepositoryViewModel.Submodules):
                SubmoduleList.ItemsSource = _repository.Submodules;
                SubmodulesExpander.IsVisible = _repository.Submodules.Count > 0;
                UpdateBusyState();
                break;
            case nameof(RepositoryViewModel.Remotes):
                RemoteList.ItemsSource = _repository.Remotes;
                UpdateBusyState();
                break;
            case nameof(RepositoryViewModel.IsLoading):
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

    // Upstream shows the number of changes on the Commit button, so pending work is visible from the browse window.
    private void UpdateCommitButton()
    {
        int count = _repository.Changes.Select(change => change.Path).Distinct(StringComparer.Ordinal).Count();
        CommitDialogButton.Content = count == 0 || !_preferences.ShowGitStatusInBrowseToolbar
            ? "Commit"
            : $"Commit ({count})";
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
        SkipRebaseMenuItem.IsVisible = _actionBanner?.Action == GitAction.Rebase;
        EditRebaseTodoMenuItem.IsVisible = _actionBanner?.Action == GitAction.Rebase;
        SkipPatchMenuItem.IsVisible = _actionBanner?.Action == GitAction.Patch;

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

    private void AbortAction()
    {
        switch (_actionBanner?.Action)
        {
            case GitAction.Rebase:
                AbortAfterConfirm("rebase", _actions.AbortRebaseAsync);
                break;
            case GitAction.Merge:
                AbortAfterConfirm("merge", _actions.AbortMergeAsync);
                break;
            case GitAction.Patch:
                AbortAfterConfirm("patch", _actions.AbortPatchAsync);
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

        BisectWindow window = new(_repository.IsBisecting, () => _actions.StartBisectAsync(path));
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
        bool commitSelected = open && _commits.Selected is not null;

        OpenButton.IsEnabled = !busy && gitAvailable;
        OpenMenuItem.IsEnabled = !busy && gitAvailable;
        InitButton.IsEnabled = !busy && gitAvailable;
        InitMenuItem.IsEnabled = !busy && gitAvailable;
        CloneButton.IsEnabled = !busy && gitAvailable;
        CloneMenuItem.IsEnabled = !busy && gitAvailable;
        RecentList.IsEnabled = !busy && gitAvailable;
        CloseRepositoryMenuItem.IsEnabled = open;
        RefreshButton.IsEnabled = open;
        RevisionFilterButton.IsEnabled = open;
        BranchScopeBox.IsEnabled = !busy;
        RefreshMenuItem.IsEnabled = open;
        LoadMoreButton.IsEnabled = !busy;
        CommitDialogButton.IsEnabled = open;
        CommitMenuItem.IsEnabled = open;
        FetchButton.IsEnabled = open;
        FetchMenuItem.IsEnabled = open;
        PullDialogMenuItem.IsEnabled = open && _repository.Remotes.Count > 0;
        PushDialogMenuItem.IsEnabled = open && _repository.Remotes.Count > 0 &&
                                       _repository.Branches.Any(branch => !branch.IsRemote);
        PullButton.IsEnabled = open && hasBranch;
        PullMenuItem.IsEnabled = open && hasBranch;
        PushButton.IsEnabled = open && hasBranch;
        PushMenuItem.IsEnabled = open && hasBranch;
        PushTagsButton.IsEnabled = open;
        OpenFolderButton.IsEnabled = open;
        FileExplorerMenuItem.IsEnabled = open;
        TerminalButton.IsEnabled = open;
        TerminalMenuItem.IsEnabled = open;
        FilesButton.IsEnabled = open;
        FilesMenuItem.IsEnabled = open;
        ReflogButton.IsEnabled = open;
        ReflogMenuItem.IsEnabled = open;
        GitIgnoreMenuItem.IsEnabled = open;
        WorktreesMenuItem.IsEnabled = open;
        ResolveConflictsMenuItem.IsEnabled = open;
        ResolveConflictsButton.IsEnabled = open;

        CreateBranchButton.IsEnabled = open;
        CreateBranchMenuItem.IsEnabled = open;
        CreateBranchAtCommitButton.IsEnabled = commitSelected;
        CheckoutButton.IsEnabled = open && SelectedBranch is BranchInfo { IsCurrent: false };
        RenameBranchButton.IsEnabled = open && SelectedBranch is BranchInfo { IsRemote: false };
        DeleteBranchButton.IsEnabled = open && SelectedBranch is BranchInfo { IsCurrent: false };
        ForceDeleteBranchButton.IsEnabled =
            open && SelectedBranch is BranchInfo { IsRemote: false, IsCurrent: false };
        bool branchSelected = open && SelectedBranch is BranchInfo { IsCurrent: false };
        MergeButton.IsEnabled = branchSelected;
        MergeMenuItem.IsEnabled = branchSelected;
        RebaseButton.IsEnabled = branchSelected;
        RebaseMenuItem.IsEnabled = branchSelected;
        ContinueActionButton.IsEnabled = open;
        AbortActionButton.IsEnabled = open;
        MoreActionButton.IsEnabled = open;
        EditRebaseTodoMenuItem.IsEnabled = _actions.EditorCommand is not null;
        BisectMoreButton.IsEnabled = open;
        BisectMenuItem.IsEnabled = commitSelected;
        MarkBadMenuItem.IsEnabled = commitSelected;
        MarkGoodMenuItem.IsEnabled = commitSelected;
        BisectSkipMenuItem.IsEnabled = commitSelected;
        StopBisectMenuItem.IsEnabled = open;

        StashButton.IsEnabled = open;
        StashMenuItem.IsEnabled = open;
        StashPopMenuItem.IsEnabled = open && _repository.Stashes.Count > 0;
        bool stashSelected = open && StashList.SelectedItem is not null;
        ApplyStashButton.IsEnabled = stashSelected;
        PopStashButton.IsEnabled = stashSelected;
        DropStashButton.IsEnabled = stashSelected;
        StashDiffButton.IsEnabled = stashSelected;
        CreateTagButton.IsEnabled = open;
        CreateTagMenuItem.IsEnabled = open;
        DeleteTagButton.IsEnabled = open && TagList.SelectedItem is not null;
        AddRemoteButton.IsEnabled = open;
        RemoveRemoteButton.IsEnabled = open && RemoteList.SelectedItem is string;
        bool submoduleSelected = open && SubmoduleList.SelectedItem is SubmoduleInfo;
        OpenSubmoduleButton.IsEnabled = SubmoduleList.SelectedItem is SubmoduleInfo { IsInitialized: true } && open;
        UpdateSubmoduleButton.IsEnabled = submoduleSelected;
        UpdateSubmodulesButton.IsEnabled = open;
        SyncSubmodulesButton.IsEnabled = open;
        RenameRemoteButton.IsEnabled = open && RemoteList.SelectedItem is string;
        SetRemoteUrlButton.IsEnabled = open && RemoteList.SelectedItem is string;
        DeleteRemoteTagButton.IsEnabled = open && TagList.SelectedItem is not null && _repository.Remotes.Count > 0;

        CherryPickMenuItem.IsEnabled = commitSelected;
        RevertMenuItem.IsEnabled = commitSelected;
        ResetSoftMenuItem.IsEnabled = commitSelected;
        ResetMixedMenuItem.IsEnabled = commitSelected;
        ResetHardMenuItem.IsEnabled = commitSelected;
        RebaseOnCommitMenuItem.IsEnabled = commitSelected;
        RebaseInteractiveMenuItem.IsEnabled = commitSelected && _actions.EditorCommand is not null;
        CreateBranchHereMenuItem.IsEnabled = commitSelected;
        CreateTagHereMenuItem.IsEnabled = commitSelected;
        BrowseFilesHereMenuItem.IsEnabled = commitSelected;
        CheckoutCommitMenuItem.IsEnabled = commitSelected;
        CopyHashMenuItem.IsEnabled = commitSelected;
        CopyMessageMenuItem.IsEnabled = commitSelected;
    }

    private async Task StashAsync()
    {
        if (RepositoryPath is { } path &&
            await _actions.StashAsync(path, StashMessageBox.Text ?? "", IncludeUntrackedCheck.IsChecked == true,
                KeepIndexCheck.IsChecked == true))
        {
            StashMessageBox.Text = "";
        }
    }

    private async Task ResetHardAsync()
    {
        if (RepositoryPath is not { } path || _commits.Selected is not { } row)
        {
            return;
        }

        string message = $"Reset the branch to {row.ShortHash} and discard all local changes? This cannot be undone.";
        if (await new ConfirmWindow(message, "Reset hard").ShowDialog<bool>(this))
        {
            await _actions.ResetAsync(path, row.Hash, ResetMode.Hard);
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

        await (interactive ? _actions.RebaseInteractiveAsync(path, row.Hash) : _actions.RebaseAsync(path, row.Hash));
    }

    private async Task CopyDetailAsync(Func<CommitDetails, string> select)
    {
        if (_commits.Details is { } details && GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(select(details));
        }
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

    // A worktree chosen in the window is opened here, as upstream's FormManageWorktree switches the browse window to it.
    private async Task ShowWorktreesAsync()
    {
        if (RepositoryPath is { } path &&
            await new WorktreesWindow(path, _repositoryService, _actions).ShowDialog<string?>(this) is { } worktree &&
            await ConfirmAsync(Confirmation.SwitchWorktree,
                string.Format(Confirmations.SwitchWorktreeQuestion, worktree),
                "Open", "Open worktree"))
        {
            await OpenRepositoryAsync(worktree);
        }
    }

    private async Task EditGitIgnoreAsync()
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        bool saved = await new GitIgnoreWindow(path).ShowDialog<bool>(this);
        _plugins.RaisePost(PluginEvent.EditGitIgnore, saved);
        if (saved)
        {
            await OpenRepositoryAsync(path);
        }
    }

    // Upstream's submodule dialogs raise PostUpdateSubmodules (the auto compile plugin builds after it).
    private async Task<bool> UpdateSubmodulesAsync(string repositoryPath, string? submodulePath)
    {
        bool done = await _actions.UpdateSubmodulesAsync(repositoryPath, submodulePath);
        _plugins.RaisePost(PluginEvent.UpdateSubmodules, done);
        return done;
    }

    private async Task OpenCommitFileInDiffToolAsync()
    {
        if (RepositoryPath is { } path && _commits.Selected is { } row &&
            CommitFilesList.SelectedItem is CommitFile file)
        {
            await _actions.RunDiffToolAsync(path, file.Path, row.Hash, staged: false);
        }
    }

    private void ShowCommitDiff()
    {
        if (RepositoryPath is { } path && _commits.Selected is { } row &&
            CommitFilesList.SelectedItem is CommitFile file)
        {
            new DiffWindow(path, row.Hash, file.Path, staged: false).Show(this);
        }
    }

    private void ShowSelectedCommitFileDiff()
    {
        ShowCommitDiffButton.IsEnabled = CommitFilesList.SelectedItem is CommitFile;
        CommitDiffToolButton.IsEnabled = CommitFilesList.SelectedItem is CommitFile;
        if (RepositoryPath is { } path && _commits.Selected is { } row &&
            CommitFilesList.SelectedItem is CommitFile file)
        {
            Run(() => CommitDiff.ShowAsync(path, row.Hash, file.Path, staged: false, file.Path));
        }
    }

    private void UpdateBranchText()
    {
        string branch = _repository.CurrentBranch;
        string state = _repository.IsRebasing ? " (rebase in progress)" :
            _repository.IsMerging ? " (merge in progress)" : "";
        string sync = _repository.Sync is { } status ? $" [ahead {status.Ahead}, behind {status.Behind}]" : "";
        BranchText.Text = branch.Length == 0 ? "" : $"Branch: {branch}{state}{sync}";
    }

    private void ShowFiles(string hash)
    {
        if (RepositoryPath is { } path)
        {
            new FileBrowserWindow(path, hash, RelevantRepositoryHost).Show(this);
        }
    }

    private void ShowDetails()
    {
        CommitDetails? details = _commits.Details;
        if (details is null)
        {
            DetailHash.Text = "";
            DetailAuthor.Text = "";
            DetailAuthorDate.Text = "";
            DetailCommitDate.Text = "";
            DetailParents.Text = "";
            DetailMessage.Text = _commits.DetailsError;
            ShowLinks([]);
            CopyHashButton.IsEnabled = false;
            CopyMessageButton.IsEnabled = false;
            return;
        }

        DetailHash.Text = details.Hash;
        DetailAuthor.Text = details.Author;
        DetailAuthorDate.Text = $"Authored: {details.AuthorDate}";
        DetailCommitDate.Text = $"Committed: {details.CommitDate}";
        DetailParents.Text = string.IsNullOrEmpty(details.Parents) ? "No parents" : $"Parents: {details.Parents}";
        DetailMessage.Text = details.Message;
        ShowLinks(details.Links ?? []);
        CopyHashButton.IsEnabled = true;
        CopyMessageButton.IsEnabled = true;
    }

    // Each link opens in the browser, as upstream's commit info opens them.
    private void ShowLinks(IReadOnlyList<RevisionLink> links)
    {
        DetailLinks.Children.Clear();
        foreach (RevisionLink link in links)
        {
            HyperlinkButton button = new() { Content = link.Caption, Margin = new Thickness(0, 0, 12, 0) };
            ToolTip.SetTip(button, link.Uri);
            if (Uri.TryCreate(link.Uri, UriKind.Absolute, out Uri? address))
            {
                button.NavigateUri = address;
            }
            else
            {
                button.IsEnabled = false;
            }

            DetailLinks.Children.Add(button);
        }

        DetailLinksPanel.IsVisible = links.Count > 0;
    }

    private void ShowError(string message)
    {
        _ = new ErrorWindow(message).ShowDialog(this);
    }
}
