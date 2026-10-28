using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using GitUI.ScriptsEngine;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's browse toolstrip (<c>FormBrowse.Designer</c>'s <c>ToolStripMain</c> and <c>FormBrowse.InitMenusAndToolbars</c>):
///  the layout toggles, the submodules, worktrees, working directory and branch drop-downs, the pull split button with the
///  default pull action, push with the ahead and behind counts, commit with the repository state, and the stash split
///  button.
/// </summary>
public partial class MainWindow
{
    // Upstream's pull button texts (FormBrowse's _pullMerge, _pullRebase, ... translation strings).
    private const string PullMergeText = "Pull - merge";
    private const string PullRebaseText = "Pull - rebase";
    private const string PullFetchText = "Fetch";
    private const string PullFetchAllText = "Fetch all";
    private const string PullFetchPruneAllText = "Fetch and prune all";
    private const string PullOpenDialogText = "Open pull dialog";

    // WorkingDirectoryToolStripSplitButton's texts.
    private const string ConfigureWorkingDirMenuText = "Co&nfigure this menu...";
    private const string RepositorySearchPlaceholderText = "Search repositories...";
    private const string WorkingDirToolTipText = "Change working directory\n"
        + "Left click opens the drop-down menu.\n"
        + "Then hold Ctrl in order to open the selected repository in a new instance.\n"
        + "Right click starts the \"Open repository\" dialog.";

    // ToolStripPushButton's texts.
    private const string PushText = "Push";

    // Upstream's TranslatedStrings.NoBranch, the title's branch until it is known.
    private const string NoBranchText = "no branch";
    private const string AheadCommitsToPushText = "{0} new commit(s) will be pushed";
    private const string BehindCommitsText = "{0} commit(s) should be integrated (or will be lost if force pushed)";

    // The widths the left panel and the commit info had before they were hidden by the layout toggles.
    private GridLength _leftPanelWidth = new(280);
    private GridLength _commitInfoHeight = new(300);

    // Upstream's width of the commit info beside the grid: room for the avatar and the commit hash, plus a scroll bar.
    private const double RevisionInfoWidth = 490 + 17;

    /// <summary>
    ///  True while the window reads or writes the repository; the toolbar's repository actions wait for it.
    /// </summary>
    public bool IsBusy => _commits.IsLoading || _repository.IsLoading || _actions.IsBusy;

    /// <summary>
    ///  The open repository's path, or null on the dashboard.
    /// </summary>
    public string? OpenRepositoryPath => RepositoryPath;

    /// <summary>
    ///  Opens <paramref name="path"/> as upstream's working directory drop-down and Start > Open do.
    /// </summary>
    public void OpenRepository(string path) => Run(() => OpenRepositoryAsync(path.Trim()));

    private void WireMainToolStrip()
    {
        ToggleLeftPanelButton.Click += (_, _) => ToggleLeftPanel();
        ToggleSplitViewButton.Click += (_, _) =>
        {
            _preferences.ShowSplitViewLayout = !_preferences.ShowSplitViewLayout;
            ShowSplitViewLayout();
        };

        // Upstream's CommitInfoPositionClick: the button moves to the next position, the menu to the chosen one.
        CommitInfoPositionButton.Click += (_, _) => SetCommitInfoPosition(
            (CommitInfoPosition)(((int)_preferences.CommitInfoPosition + 1) %
                                 Enum.GetValues<CommitInfoPosition>().Length));
        CommitInfoBelowMenuItem.Click += (_, _) => SetCommitInfoPosition(CommitInfoPosition.BelowList);
        CommitInfoLeftwardMenuItem.Click += (_, _) => SetCommitInfoPosition(CommitInfoPosition.LeftwardFromList);
        CommitInfoRightwardMenuItem.Click += (_, _) => SetCommitInfoPosition(CommitInfoPosition.RightwardFromList);
        LayoutRevisionInfo();
        ShowSplitViewLayout();
        FillTerminals();
        TerminalButton.Click += (_, _) => OpenTerminal(TerminalButton.Tag as TerminalChoice);
        EditSettingsButton.Click += (_, _) => Run(() => ShowSettingsAsync());
        ((MenuFlyout)SubmodulesButton.Flyout!).Opening += (_, _) => ShowSubmodulesMenu();
        ((MenuFlyout)WorktreesButton.Flyout!).Opening += (_, _) => Run(ShowWorktreesMenuAsync);
        ((MenuFlyout)WorkingDirButton.Flyout!).Opening += (_, _) => ShowWorkingDirMenu();
        ToolTip.SetTip(WorkingDirButton, UpstreamTranslation.ByText(WorkingDirToolTipText));

        // Upstream's MouseUpHandler: a right click starts "Open repository".
        WorkingDirButton.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Right)
            {
                e.Handled = true;
                Run(PickAndOpenRepositoryAsync);
            }
        }, RoutingStrategies.Tunnel);
        ((MenuFlyout)BranchSelectButton.Flyout!).Opening += (_, _) => ShowBranchSelectMenu();

        PullMergeMenuItem.Click += (_, _) => PullWith(GitPullAction.Merge);
        PullRebaseMenuItem.Click += (_, _) => PullWith(GitPullAction.Rebase);
        PullFetchMenuItem.Click += (_, _) => PullWith(GitPullAction.Fetch);
        PullOpenDialogMenuItem.Click += (_, _) => PullWith(GitPullAction.None);
        PullFetchAllMenuItem.Click += (_, _) => PullWith(GitPullAction.FetchAll);
        PullFetchPruneAllMenuItem.Click += (_, _) => PullWith(GitPullAction.FetchPruneAll);
        FillSetDefaultPullActionMenu();
        ShowDefaultPullAction();

        // Upstream's stash split button opens its stash window; the new shell has none, so the button stashes.
        StashToolButton.Click += (_, _) => Run(() => ShowStashWindowAsync(this));
        StashChangesMenuItem.Click += (_, _) => Run(() => StashAsync());
        StashStagedMenuItem.Click += (_, _) => RunOnRepository(path => _actions.StashStagedAsync(path));
        StashPopToolMenuItem.Click += (_, _) => PopTopStash();
        ManageStashesMenuItem.Click += (_, _) => Run(() => ShowStashWindowAsync(this));
        CreateStashMenuItem.Click += (_, _) => Run(() => ShowStashWindowAsync(this, manageStashes: false));
    }

    // The toolstrip's state after each read or write, as upstream's FormBrowse refreshes it.
    private void UpdateToolStrip(bool busy, bool open)
    {
        bool hasBranch = _repository.CurrentBranch.Length > 0;
        ToggleLeftPanelButton.IsEnabled = open;
        ToggleSplitViewButton.IsEnabled = open;
        SubmodulesButton.IsEnabled = open;
        WorktreesButton.IsEnabled = open;
        WorkingDirButton.IsEnabled = !busy && _git?.Status != GitDiscoveryStatus.NotFound;
        BranchSelectButton.IsEnabled = open;
        StashToolButton.IsEnabled = open;
        PullMergeMenuItem.IsEnabled = PullRebaseMenuItem.IsEnabled = open && hasBranch;
        PullFetchMenuItem.IsEnabled = PullFetchAllMenuItem.IsEnabled = PullFetchPruneAllMenuItem.IsEnabled = open;
        PullOpenDialogMenuItem.IsEnabled = open && _repository.Remotes.Count > 0;

        // Upstream's UpdateFetchAllVisibility: "Fetch all" only with more than one remote.
        PullFetchAllMenuItem.IsVisible = _repository.Remotes.Count > 1;

        // Upstream's RefreshContent: the caption among the recent repositories, at least RecentReposComboMinWidth wide.
        WorkingDirButton.Text = RepositoryPath is { } path
            ? RecentRepositoryPaths.Caption(path, [.. _recent.Items.Select(item => item.Path)],
                _preferences.RecentRepositoryMenu.Splitter())
            : "";
        WorkingDirButton.MinWidth = _preferences.RecentRepositoryMenu.ComboMinWidth;
        BranchSelectButton.Text = RepositoryPath is null ? "" : CurrentBranchName;
        ShowPushAheadBehind();
        UpdateTitle();
    }

    // The branch as upstream's branch drop-down and title show it: "(no branch)" on a detached head.
    private string CurrentBranchName
        => _repository.CurrentBranch.Length > 0 ? _repository.CurrentBranch : DetachedHeadParser.DetachedBranch;

    // Upstream's FormBrowse title (AppTitleGenerator), refreshed with the revision grid.
    private void UpdateTitle()
        => Title = BrowseWindowTitle.Of(RepositoryPath, RepositoryPath is null ? null : CurrentBranchName,
            UpstreamTranslation.ByText(NoBranchText), _commits.Filter.PathFilter);

    // Upstream's ToolStripPushButton.DisplayAheadBehindInformation: the counts as the button's text, and the unstage
    // image when the branch is behind.
    private void ShowPushAheadBehind()
    {
        if (!_preferences.ShowAheadBehindData || _repository.CurrentBranch.Length == 0 ||
            _repository.Sync is not { } sync)
        {
            PushButton.Text = null;
            PushButton.Image = "Push";
            ToolTip.SetTip(PushButton, PushText);
            return;
        }

        AheadBehindData data = new(_repository.CurrentBranch, "",
            sync.Ahead.ToString(System.Globalization.CultureInfo.InvariantCulture),
            sync.Behind > 0 ? sync.Behind.ToString(System.Globalization.CultureInfo.InvariantCulture) : "");
        PushButton.Text = data.ToDisplay();
        PushButton.Image = sync.Behind > 0 ? "Unstage" : "Push";
        List<string> tips = [];
        if (sync.Ahead > 0)
        {
            tips.Add(string.Format(AheadCommitsToPushText, sync.Ahead));
        }

        if (sync.Behind > 0)
        {
            tips.Add(string.Format(BehindCommitsText, sync.Behind));
        }

        ToolTip.SetTip(PushButton, tips.Count > 0 ? string.Join(Environment.NewLine, tips) : PushText);
    }

    // Upstream's RefreshDefaultPullAction: the pull button shows the default action's image and text.
    private void ShowDefaultPullAction()
    {
        GitPullAction action = _preferences.DefaultPullAction;
        (string image, string tip) = action switch
        {
            GitPullAction.Fetch => ("PullFetch", PullFetchText),
            GitPullAction.FetchAll => ("PullFetchAll", PullFetchAllText),
            GitPullAction.FetchPruneAll => ("PullFetchPruneAll", PullFetchPruneAllText),
            GitPullAction.Merge => ("PullMerge", PullMergeText),
            GitPullAction.Rebase => ("PullRebase", PullRebaseText),
            _ => ("Pull", PullOpenDialogText),
        };
        PullButton.Image = image;
        ToolTip.SetTip(PullButton, tip);
        foreach (MenuItem item in SetDefaultPullActionMenuItem.Items.OfType<MenuItem>())
        {
            item.IsChecked = item.Tag is GitPullAction tagged && tagged == action;
        }
    }

    // Upstream's FillNextPullActionAsDefaultToolStripMenuItems: the pull menu's actions again, as check items.
    private void FillSetDefaultPullActionMenu()
    {
        MenuItem[] sources =
        [
            PullMergeMenuItem, PullRebaseMenuItem, PullFetchMenuItem, PullOpenDialogMenuItem, PullFetchAllMenuItem,
            PullFetchPruneAllMenuItem,
        ];
        foreach (MenuItem source in sources)
        {
            GitPullAction action = ActionOf(source);
            MenuItem item = new()
            {
                Header = source.Header,
                ToggleType = MenuItemToggleType.CheckBox,
                Tag = action,
                Icon = source.Icon is Image icon
                    ? ToolStripContent.Icon((Avalonia.Media.Imaging.Bitmap)icon.Source!)
                    : null,
            };
            item.Click += (_, _) =>
            {
                _preferences.DefaultPullAction = action;
                ShowDefaultPullAction();
            };
            SetDefaultPullActionMenuItem.Items.Add(item);
        }

        GitPullAction ActionOf(MenuItem item)
            => item == PullMergeMenuItem ? GitPullAction.Merge
                : item == PullRebaseMenuItem ? GitPullAction.Rebase
                : item == PullFetchMenuItem ? GitPullAction.Fetch
                : item == PullFetchAllMenuItem ? GitPullAction.FetchAll
                : item == PullFetchPruneAllMenuItem ? GitPullAction.FetchPruneAll
                : GitPullAction.None;
    }

    // Upstream's pull menu items run their action directly, whatever the default is.
    private void PullWith(GitPullAction action)
    {
        switch (action)
        {
            case GitPullAction.None or GitPullAction.Default:
                Run(ShowPullDialogAsync);
                break;
            case GitPullAction.Fetch:
                Fetch(remote: TrackingRemote(), prune: false);
                break;
            case GitPullAction.FetchAll:
                Fetch(remote: "--all", prune: false);
                break;
            case GitPullAction.FetchPruneAll:
                Fetch(remote: "--all", prune: true);
                break;
            default:
                RunOnCurrentBranch((path, branch) => WithScriptsAsync(path, ScriptEvent.BeforePull,
                    ScriptEvent.AfterPull,
                    () => _actions.PullAsync(path, TrackingRemote(), branch, action == GitPullAction.Rebase)));
                break;
        }
    }

    // Upstream's toggleLeftPanel: the left panel and its splitter collapse, and come back at their width.
    private void ToggleLeftPanel()
    {
        ColumnDefinition panel = MainSplitGrid.ColumnDefinitions[0];
        ColumnDefinition splitter = MainSplitGrid.ColumnDefinitions[1];
        if (panel.Width.Value > 0)
        {
            _leftPanelWidth = panel.Width;
            panel.Width = new GridLength(0);
            splitter.Width = new GridLength(0);
        }
        else
        {
            panel.Width = _leftPanelWidth;
            splitter.Width = new GridLength(5);
        }

        ToggleLeftPanelButton.IsChecked = panel.Width.Value > 0;
    }

    // Upstream's RefreshSplitViewLayout: the tabs below the grid collapse, and come back at their height.
    private void ShowSplitViewLayout()
    {
        bool show = _preferences.ShowSplitViewLayout;
        RowDefinition splitter = BrowsePanel.RowDefinitions[1];
        RowDefinition tabs = BrowsePanel.RowDefinitions[2];
        if (!show && tabs.Height.Value > 0)
        {
            _commitInfoHeight = tabs.Height;
        }

        tabs.Height = show ? _commitInfoHeight : new GridLength(0);
        splitter.Height = new GridLength(show ? 5 : 0);
        DetailTabs.IsVisible = show;
        ToggleSplitViewButton.IsChecked = show;
    }

    // Upstream's SetSplitterPositions and its SplitterManager ("FormBrowse"): the left panel (MainSplitContainer, which
    // also stores whether the left panel is hidden), the grid above the tabs (RightSplitContainer) and the file lists of
    // the Diff and File tree tabs (RevisionDiffControl's DiffSplitContainer, one per tab). Upstream resets the commit
    // info's width beside the grid on every layout, so RevisionsSplitContainer is not kept.
    private void TrackSplitters()
    {
        const int MainSplitterWidth = 6;
        const int MainPanel1MinSize = 192;
        const int DiffSplitterWidth = 7;
        const double TabsSplitterHeight = 5;
        SplitterTracker splitters = new(this, AppServices.Splitters, "FormBrowse");
        splitters.Add("MainSplitContainer", MainSplitGrid, rows: false, SplitterFixedPanel.Panel1, MainSplitterWidth,
            MainPanel1MinSize,
            hidden: new HiddenPanel(LeftPanelHidden, _ => _leftPanelWidth.Value,
                (_, distance) => _leftPanelWidth = new GridLength(distance)),
            panel1Collapsed: (LeftPanelHidden, collapse =>
            {
                if (collapse != LeftPanelHidden())
                {
                    ToggleLeftPanel();
                }
            }));
        splitters.Add("RightSplitContainer", BrowsePanel, rows: true, SplitterFixedPanel.None, MainSplitterWidth,
            panel2MinSize: 0,
            hidden: new HiddenPanel(() => !_preferences.ShowSplitViewLayout,
                length => length - TabsSplitterHeight - _commitInfoHeight.Value,
                (length, distance) =>
                    _commitInfoHeight = new GridLength(Math.Max(0, length - TabsSplitterHeight - distance))));
        splitters.Add("revisionDiff.DiffSplitContainer", DiffSplitGrid, rows: false, SplitterFixedPanel.Panel1,
            DiffSplitterWidth);
        splitters.Add("fileTree.DiffSplitContainer", FileTreeSplitGrid, rows: false, SplitterFixedPanel.Panel1,
            DiffSplitterWidth);

        bool LeftPanelHidden() => MainSplitGrid.ColumnDefinitions[0].Width.Value == 0;
    }

    private void SetCommitInfoPosition(CommitInfoPosition position)
    {
        _preferences.CommitInfoPosition = position;
        LayoutRevisionInfo();
    }

    // Upstream's LayoutRevisionInfo: the commit info is the first tab below the grid, or sits left or right of the grid
    // at a fixed width while the grid takes the rest.
    private void LayoutRevisionInfo()
    {
        CommitInfoPosition position = _preferences.CommitInfoPosition;
        ColumnDefinitions columns = RevisionsSplitGrid.ColumnDefinitions;
        CommitInfoTab.Content = null;
        RevisionInfoHost.Child = null;
        bool below = position == CommitInfoPosition.BelowList;
        if (below)
        {
            CommitInfoTab.Content = RevisionInfo;
            CommitInfoTab.IsVisible = true;
            DetailTabs.SelectedItem = CommitInfoTab;
            columns[0].Width = GridLength.Star;
            columns[1].Width = columns[2].Width = new GridLength(0);
            Grid.SetColumn(RevisionGridContainer, 0);
        }
        else
        {
            CommitInfoTab.IsVisible = false;
            if (DetailTabs.SelectedItem == CommitInfoTab)
            {
                DetailTabs.SelectedItem = DiffTab;
            }

            RevisionInfoHost.Child = RevisionInfo;
            bool right = position == CommitInfoPosition.RightwardFromList;
            GridLength info = new(RevisionInfoWidth);
            columns[0].Width = right ? GridLength.Star : info;
            columns[1].Width = new GridLength(5);
            columns[2].Width = right ? info : GridLength.Star;
            Grid.SetColumn(RevisionGridContainer, right ? 0 : 2);
            Grid.SetColumn(RevisionInfoHost, right ? 2 : 0);
        }

        RevisionInfoSplitter.IsVisible = RevisionInfoHost.IsVisible = !below;

        // Upstream's RefreshLayoutToggleButtonStates: the button shows the chosen position's image and text.
        MenuItem chosen = position switch
        {
            CommitInfoPosition.LeftwardFromList => CommitInfoLeftwardMenuItem,
            CommitInfoPosition.RightwardFromList => CommitInfoRightwardMenuItem,
            _ => CommitInfoBelowMenuItem,
        };
        CommitInfoPositionButton.Image = position switch
        {
            CommitInfoPosition.LeftwardFromList => "LayoutSidebarTopLeft",
            CommitInfoPosition.RightwardFromList => "LayoutSidebarTopRight",
            _ => "LayoutFooterTab",
        };
        ToolTip.SetTip(CommitInfoPositionButton, ((string)chosen.Header!).Replace("_", "", StringComparison.Ordinal));
    }

    // Upstream's FillUserShells: the installed terminals in the drop-down; the button opens the default one.
    private void FillTerminals()
    {
        IReadOnlyList<TerminalChoice> choices = TerminalChoices.For(HostPlatform.Current,
            Environment.GetEnvironmentVariable("TERMINAL"), PathUtil.TryFindShellPath);
        MenuFlyout flyout = (MenuFlyout)TerminalButton.Flyout!;
        flyout.Items.Clear();
        foreach (TerminalChoice choice in choices)
        {
            MenuItem item = new() { Header = new TextBlock { Text = choice.Name }, Icon = UpstreamIcon(choice.Image) };
            ToolTip.SetTip(item, choice.Name);
            item.Click += (_, _) => OpenTerminal(choice);
            flyout.Items.Add(item);
        }

        if (TerminalChoices.Default(choices, TerminalChoices.DefaultName) is { } terminal)
        {
            TerminalButton.Image = terminal.Image;
            ToolTip.SetTip(TerminalButton, terminal.Name);
            TerminalButton.Tag = terminal;
        }

        TerminalButton.IsVisible = choices.Count > 0;
    }

    // Upstream's userShell_Click: the terminal opens in the working directory.
    private void OpenTerminal(TerminalChoice? terminal)
    {
        if (terminal is not null)
        {
            RunOnRepositoryFolder(path => new SystemTerminalLauncher(_launcher, terminal.Command).OpenTerminal(path));
        }
    }

    // Upstream's submodule drop-down: the submodules to open, then updating or synchronizing all of them.
    private void ShowSubmodulesMenu()
    {
        MenuFlyout flyout = (MenuFlyout)SubmodulesButton.Flyout!;
        flyout.Items.Clear();
        if (RepositoryPath is not { } path)
        {
            return;
        }

        foreach (SubmoduleInfo submodule in _repository.Submodules)
        {
            MenuItem item = new() { Header = new TextBlock { Text = submodule.Display } };
            string submodulePath = Path.Combine(path, submodule.Path);
            item.Click += (_, _) => Run(() => OpenRepositoryAsync(submodulePath));
            flyout.Items.Add(item);
        }

        if (flyout.Items.Count > 0)
        {
            flyout.Items.Add(new Separator());
        }

        MenuItem update = new() { Header = "_Update all submodules", Icon = UpstreamIcon("SubmodulesUpdate") };
        update.Click += (_, _) => RunOnRepository(repository => UpdateSubmodulesAsync(repository, null));
        MenuItem sync = new() { Header = "_Synchronize all submodules", Icon = UpstreamIcon("SubmodulesSync") };
        sync.Click += (_, _) => RunOnRepository(repository => _actions.SyncSubmodulesAsync(repository, path: null));
        update.IsEnabled = sync.IsEnabled = _repository.Submodules.Count > 0;
        flyout.Items.Add(update);
        flyout.Items.Add(sync);
    }

    // Upstream's toolStripWorktrees_DropDownOpening: every worktree by its folder's name (the open one checked, those
    // whose folders are gone disabled), then Create worktree..., Prune worktrees and Manage worktrees....
    private async Task ShowWorktreesMenuAsync()
    {
        MenuFlyout flyout = (MenuFlyout)WorktreesButton.Flyout!;
        flyout.Items.Clear();
        if (RepositoryPath is not { } path)
        {
            return;
        }

        IReadOnlyList<WorktreeInfo> worktrees = await _repositoryService.GetWorktreesAsync(path);
        foreach (WorktreeInfo worktree in worktrees)
        {
            bool current = ManageWorktreeDialog.IsCurrent(worktree, path);
            MenuItem item = new()
            {
                Header = new TextBlock { Text = ManageWorktreeDialog.MenuText(worktree) },
                Icon = UpstreamIcon("WorkTree"),
                ToggleType = current ? MenuItemToggleType.CheckBox : MenuItemToggleType.None,
                IsChecked = current,
                IsEnabled = !current && !worktree.IsDeleted,
            };
            item.Click += (_, _) => Run(() => OpenWorktreeFolderAsync(worktree.Path));
            flyout.Items.Add(item);
        }

        flyout.Items.Add(new Separator());
        MenuItem create = new()
        {
            Header = UpstreamTranslation.ByText(ManageWorktreeDialog.CreateWorktree), Icon = UpstreamIcon("WorkTree"),
        };
        create.Click += (_, _) => Run(CreateWorktreeAsync);
        MenuItem prune = new() { Header = UpstreamTranslation.ByText(ManageWorktreeDialog.PruneWorktrees) };
        prune.Click += (_, _) => RunOnRepository(repository => WorktreeFlow().PruneAsync(repository));
        MenuItem manage = new() { Header = UpstreamTranslation.ByText(ManageWorktreeDialog.ManageWorktrees) };
        manage.Click += (_, _) => Run(ShowWorktreesAsync);
        flyout.Items.Add(create);
        flyout.Items.Add(prune);
        flyout.Items.Add(manage);
    }

    // Upstream's WorktreeToolStripMenuItem_Click: opens the worktree at once, or says its folder is gone.
    private async Task OpenWorktreeFolderAsync(string worktreePath)
    {
        if (!Directory.Exists(worktreePath))
        {
            await new MessageBoxWindow(
                string.Format(UpstreamTranslation.ByText(ManageWorktreeDialog.DirectoryNotFound), worktreePath),
                UpstreamTranslation.ByText(ManageWorktreeDialog.Error), MessageBoxButtons.OK).ShowDialog(this);
            return;
        }

        await OpenRepositoryAsync(Path.GetFullPath(worktreePath));
    }

    // Upstream's WorkingDirectoryToolStripSplitButton.FillDropDown: the search box, the favourites, the recent
    // repositories, Open and Close as in the menus, and "Configure this menu...".
    private void ShowWorkingDirMenu()
    {
        TextBox filter = new()
        {
            Name = "WorkingDirFilterBox", PlaceholderText = UpstreamTranslation.ByText(RepositorySearchPlaceholderText),
        };
        List<Control> recent = RecentRepositoryItems(_menuHistory.Recent);
        List<Control> items = [new MenuItem { Header = filter, StaysOpenOnClick = true }, new Separator()];
        List<MenuItem> favourites = FavouriteRepositoryItems(_menuHistory.Favourites);
        if (favourites.Count > 0)
        {
            items.Add(new MenuItem
            {
                Header = FavouritesMenu.Header, Icon = UpstreamIcon("Star"), ItemsSource = favourites
            });
        }

        items.AddRange(recent);
        MenuItem open = new()
        {
            Header = OpenMenuItem.Header, Icon = UpstreamIcon("RepoOpen"), InputGesture = OpenMenuItem.InputGesture,
        };
        open.Click += (_, _) => Run(PickAndOpenRepositoryAsync);
        MenuItem close = new()
        {
            Header = CloseRepositoryMenuItem.Header, Icon = UpstreamIcon("DashboardFolderGit"),
            InputGesture = CloseRepositoryMenuItem.InputGesture, IsEnabled = CloseRepositoryMenuItem.IsEnabled,
        };
        close.Click += (_, _) => CloseRepository();
        MenuItem configure = new()
        {
            Name = "ConfigureWorkingDirMenuItem",
            Header = UpstreamTranslation.WithAccessKey(UpstreamTranslation.ByText(ConfigureWorkingDirMenuText)),
        };
        configure.Click += (_, _) => Run(ConfigureRecentRepositoriesAsync);
        items.AddRange([new Separator(), open, close, new Separator(), configure]);

        // Upstream's filter: the repositories whose text has the search text, case ignored; the separators stay.
        filter.TextChanged += (_, _) =>
        {
            foreach (MenuItem item in recent.OfType<MenuItem>())
            {
                item.IsVisible = string.IsNullOrWhiteSpace(filter.Text)
                                 || (item.Tag as string)?.Contains(filter.Text,
                                     StringComparison.CurrentCultureIgnoreCase) is true;
            }
        };
        MenuFlyout flyout = (MenuFlyout)WorkingDirButton.Flyout!;
        flyout.Items.Clear();
        foreach (Control item in items)
        {
            flyout.Items.Add(item);
        }
    }

    // Upstream's _tsmiRecentReposSettings_Click: the settings window, then the menus and the button's caption again.
    private async Task ConfigureRecentRepositoriesAsync()
    {
        await new RecentRepositoriesSettingsWindow(AppServices.RecentRepositories, _preferences).ShowDialog<bool>(this);
        await _recent.LoadAsync();
        UpdateBusyState();
    }

    // Upstream's branch drop-down: the local branches, the current one checked; another one is checked out.
    private void ShowBranchSelectMenu()
    {
        MenuFlyout flyout = (MenuFlyout)BranchSelectButton.Flyout!;
        flyout.Items.Clear();
        foreach (BranchInfo branch in _repository.Branches.Where(branch => !branch.IsRemote))
        {
            MenuItem item = new()
            {
                Header = new TextBlock { Text = branch.Name },
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = branch.IsCurrent,
            };
            string name = branch.Name;
            item.Click += (_, _) =>
            {
                if (!branch.IsCurrent)
                {
                    RunOnRepository(path => StartCheckoutBranchAsync(path, name));
                }
            };
            flyout.Items.Add(item);
        }
    }

    // Upstream's Commit button image (RepoStateVisualiser) and its count of changed files.
    private void UpdateCommitButton()
    {
        int count = _repository.Changes.Select(change => change.Path).Distinct(StringComparer.Ordinal).Count();
        string commit = GitExtensions.Xplat.Ui.UpstreamTranslation.ByText("Commit");
        CommitDialogButton.Text =
            count == 0 || !_preferences.ShowGitStatusInBrowseToolbar ? commit : $"{commit} ({count})";
        CommitDialogButton.Image =
            RepositoryPath is null ? "RepoStateClean" : RepoStates.Of(_repository.Changes).ToString();
    }

    private static Image? UpstreamIcon(string name)
        => UpstreamImages.Named(name) is { } picture ? ToolStripContent.Icon(picture) : null;
}
