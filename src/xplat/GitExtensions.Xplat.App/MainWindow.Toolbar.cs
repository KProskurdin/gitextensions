using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Presentation;
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
    // WorkingDirectoryToolStripSplitButton's texts.
    private const string RepositorySearchPlaceholderText = "Search repositories...";
    private static readonly string WorkingDirToolTipText = "Change working directory\n"
        + "Left click opens the drop-down menu.\n"
        + $"Then hold {Hotkeys.CommandModifierName} in order to open the selected repository in a new instance.\n"
        + "Right click starts the \"Open repository\" dialog.";

    // The widths the left panel and the commit info had before they were hidden by the layout toggles.
    private GridLength _leftPanelWidth = new(280);
    private GridLength _commitInfoHeight = new(300);

    // Upstream's width of the commit info beside the grid: room for the avatar and the commit hash, plus a scroll bar.
    private const double RevisionInfoWidth = 490 + 17;

    /// <summary>
    ///  True while the window reads or writes the repository, also until opening a repository has finished (the recent
    ///  list, the settings and the plugins follow the reading); the toolbar's repository actions wait for it.
    /// </summary>
    public bool IsBusy => _commits.IsLoading || _repository.IsLoading || _actions.IsBusy || _openings > 0;

    /// <summary>
    ///  The open repository's path, or null on the dashboard.
    /// </summary>
    public string? OpenRepositoryPath => RepositoryPath;

    /// <summary>
    ///  Shows the About window, for the Help menu and the macOS application menu (the toolbar view model's About command).
    /// </summary>
    public void ShowAbout() => _ = new AboutWindow(_git?.Version?.ToString()).ShowDialog(this);

    /// <summary>
    ///  Opens <paramref name="path"/> as upstream's working directory drop-down and Start > Open do.
    /// </summary>
    public void OpenRepository(string path) => Run(() => OpenRepositoryAsync(path.Trim()));

    private void WireMainToolStrip()
    {
        // The buttons' and the fixed items' actions are the toolbar view model's commands (bound in XAML); the flyouts'
        // items get its data context here, as a flyout's items do not inherit it.
        foreach (SplitButton button in (SplitButton[])[CommitInfoPositionButton, StashToolButton])
        {
            if (button.Flyout is MenuFlyout menu)
            {
                foreach (Control item in menu.Items.OfType<Control>())
                {
                    item.DataContext = Toolbar;
                }
            }
        }

        LayoutRevisionInfo();
        ShowSplitViewLayout();
        FillTerminals();

        // The menus are built before they open: Avalonia shows a menu whose items change while it opens as a thin line.
        ((PopulatingMenuFlyout)SubmodulesButton.Flyout!).Populating += (_, _) => ShowSubmodulesMenu();
        PopulatingMenuFlyout worktrees = (PopulatingMenuFlyout)WorktreesButton.Flyout!;
        worktrees.PopulateAsync = ShowWorktreesMenuAsync;
        worktrees.ReportError = ex => ShowError(ex.Message);
        ((PopulatingMenuFlyout)WorkingDirButton.Flyout!).Populating += (_, _) => ShowWorkingDirMenu();
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
        PopulatingMenuFlyout branches = (PopulatingMenuFlyout)BranchSelectButton.Flyout!;
        branches.PopulateAsync = ShowBranchSelectMenuAsync;
        branches.ReportError = ex => ShowError(ex.Message);

        FillSetDefaultPullActionMenu();
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

                // A flyout's items get their data context only when it opens (MVVM.md, Flyouts).
                DataContext = Toolbar,
                Command = Toolbar.SetDefaultPullActionCommand,
                CommandParameter = action,
            };

            // Checked while the action is the default (MVVM.md, Menu check items: one way, with the command).
            CompiledBinding isDefault = CompiledBinding.Create<BrowseToolbarViewModel, GitPullAction>(
                toolbar => toolbar.DefaultPullAction, mode: BindingMode.OneWay);
            isDefault.Converter = EnumEqualsConverter.Instance;
            isDefault.ConverterParameter = action;
            item.Bind(MenuItem.IsCheckedProperty, isDefault);
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
                RunOnCurrentBranch(async (path, branch) => await LoadPuttyKeysAsync(path, TrackingRemote())
                    && await WithScriptsAsync(path, ScriptEvent.BeforePull, ScriptEvent.AfterPull,
                        () => PullAndUpdateSubmodulesAsync(path,
                            () => _actions.PullAsync(path, TrackingRemote(), branch, action == GitPullAction.Rebase),
                            TrackingRemote())));
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
        MenuEntries.Fill(((MenuFlyout)TerminalButton.Flyout!).Items, Toolbar.TerminalMenu(choices), this);
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

    // Upstream's submodule drop-down (PopulateToolbar, from the structure the left panel's submodule tree reads), with
    // "Loading..." until it is read.
    private void ShowSubmodulesMenu()
        => MenuEntries.Fill(((MenuFlyout)SubmodulesButton.Flyout!).Items,
            Toolbar.SubmodulesMenu(RepositoryPath is { } path ? ShownSubmoduleStructure(path) : null), this);

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
        MenuEntries.Fill(flyout.Items, Toolbar.WorktreesMenu(worktrees, path), this);
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
        IReadOnlyList<MenuEntry> entries = Toolbar.WorkingDirMenu(_menuHistory, (string)FavouritesMenu.Header!,
            (string)OpenMenuItem.Header!, OpenMenuItem.InputGesture, (string)CloseRepositoryMenuItem.Header!,
            CloseRepositoryMenuItem.InputGesture);
        MenuFlyout flyout = (MenuFlyout)WorkingDirButton.Flyout!;
        MenuEntries.Fill(flyout.Items, entries, this, TrackNewInstanceClick);
        flyout.Items.Insert(0, new Separator());
        flyout.Items.Insert(0, new MenuItem { Header = filter, StaysOpenOnClick = true });

        // Upstream's filter: the repositories whose text has the search text, case ignored; the separators stay.
        filter.TextChanged += (_, _) => BrowseToolbarViewModel.FilterRepositories(entries, filter.Text);
    }

    // Upstream's _tsmiRecentReposSettings_Click: the settings window, then the menus and the button's caption again.
    private async Task ConfigureRecentRepositoriesAsync()
    {
        await new RecentRepositoriesSettingsWindow(AppServices.RecentRepositories, _preferences).ShowDialog<bool>(this);
        await _recent.LoadAsync();
        UpdateBusyState();
    }

    // Upstream's CurrentBranchDropDownOpening: "Checkout branch..." with its hotkey, then the local branches read from git
    // as the menu opens; a branch whose commit the grid does not show has the closed eye and gray text. A click checks the
    // branch out.
    private async Task ShowBranchSelectMenuAsync()
    {
        MenuFlyout flyout = (MenuFlyout)BranchSelectButton.Flyout!;
        flyout.Items.Clear();
        if (RepositoryPath is not { } path)
        {
            return;
        }

        IReadOnlyList<(string Name, string Hash)> branches =
            await GitRepositoryService.GetBranchMenuBranchesAsync(path, RefSorting());
        MenuEntries.Fill(flyout.Items, Toolbar.BranchesMenu(branches, (string)CheckoutBranchMenuItem.Header!,
            Hotkeys.GestureFor(BrowseCommand.CheckoutBranch)), this);
    }

    private static Image? UpstreamIcon(string name)
        => UpstreamImages.Named(name) is { } picture ? ToolStripContent.Icon(picture) : null;
}
