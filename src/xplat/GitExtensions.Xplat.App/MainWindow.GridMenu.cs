using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's revision grid context menu (<c>RevisionGridControl.ContextMenuOpening</c>; rules in <see cref="GridMenus"/>):
///  when it opens, the items take upstream's run-time order (for a right-clicked ref label the "advanced" ones go under
///  "Other actions" and the ref submenus offer that ref only), the copy, branch and tag submenus are filled for the
///  selected commit, items that do not apply are hidden, and separators with nothing above them are hidden too.
/// </summary>
public partial class MainWindow
{
    // Upstream's _contextMenuItems: the menu's order at run time, and the items a focused menu moves under "Other actions".
    private (Control Item, bool Advanced)[] _gridMenuLayout = [];

    // Upstream's _rightClickedHitInfo: the ref label the menu was opened on, and the keys held then.
    private RefLabel? _rightClickedRef;
    private KeyModifiers _rightClickModifiers;

    private void WireGridMenu()
    {
        const bool top = false;
        const bool advanced = true;
        _gridMenuLayout =
        [
            (GridResetChangesMenuItem, top),
            (GridCommitMenuItem, top),
            (MarkBadMenuItem, top),
            (MarkGoodMenuItem, top),
            (BisectSkipMenuItem, top),
            (StopBisectMenuItem, top),
            (BisectSeparator, top),
            (CopyToClipboardMenuItem, top),
            (CopySeparator, top),
            (ApplyStashMenuItem, top),
            (PopStashMenuItem, top),
            (GridDropStashMenuItem, top),
            (StashSeparator, top),
            (GridCheckoutBranchMenuItem, top),
            (GridPushBranchMenuItem, top),
            (GridMergeBranchMenuItem, top),
            (GridRebaseOnMenuItem, top),
            (ResetCurrentBranchMenuItem, top),
            (BranchSeparator, top),
            (GridSelectInLeftPanelMenuItem, top),
            (CreateBranchHereMenuItem, top),
            (ResetAnotherBranchMenuItem, advanced),
            (GridRenameBranchMenuItem, top),
            (GridDeleteBranchMenuItem, top),
            (BranchModificationSeparator, top),
            (CreateTagHereMenuItem, advanced),
            (GridDeleteTagMenuItem, top),
            (CommitSeparator, advanced),
            (CheckoutCommitMenuItem, advanced),
            (RevertMenuItem, advanced),
            (CherryPickMenuItem, advanced),
            (ArchiveCommitMenuItem, advanced),
            (GridAdvancedMenuItem, advanced),
            (CompareSeparator, top),
            (GridCompareMenuItem, top),
            (NavigateSeparator, advanced),
            (GridNavigateMenuItem, advanced),
            (GridViewMenuItem, advanced),
            (RunScriptMenuItem, top),
            (OpenBuildReportMenuItem, advanced),
            (OpenPullRequestPageMenuItem, advanced),
            (BrowseFilesHereMenuItem, advanced),
            (OtherActionsMenuItem, top),
        ];
        ArrangeGridMenu(focused: false);
        CommitList.AddHandler(PointerPressedEvent, OnCommitListPointerPressed, RoutingStrategies.Tunnel);
        CommitContextMenu.Opening += (_, e) => e.Cancel = !ShowGridMenu();
        GridResetChangesMenuItem.Click += (_, _) => RunOnRepository(ResetChangesAsync);
        GridCommitMenuItem.Click += (_, _) => ShowCommitWindow();
        ResetAnotherBranchMenuItem.Click += (_, _) => RunOnSelectedCommit(ShowResetAnotherBranchAsync);
        WireCompareMenu();

        // Upstream's ApplyStashToolStripMenuItemClick (by the stash's commit), PopStashToolStripMenuItemClick and
        // DropStashToolStripMenuItemClick (by its selector, the drop after upstream's suppressible question).
        ApplyStashMenuItem.Click += (_, _) => RunOnSelectedCommit((path, row) => _actions.ApplyStashAsync(path, row.Hash));
        PopStashMenuItem.Click += (_, _) => RunOnSelectedCommit((path, row) => row.StashLabel is { } stash
            ? _actions.PopStashAsync(path, stash)
            : Task.FromResult(false));
        GridDropStashMenuItem.Click += (_, _) => RunOnSelectedCommit(async (path, row) =>
            row.StashLabel is { } stash
            && await ConfirmAsync(Confirmation.StashDrop, Confirmations.StashDropQuestion, "Drop", "Drop Stash Confirmation")
            && await _actions.DropStashAsync(path, stash));
        EditCommitMenuItem.Click += (_, _) => RewriteSelectedCommit(RewriteCommit.Edit);
        RewordCommitMenuItem.Click += (_, _) => RewriteSelectedCommit(RewriteCommit.Reword);
        FixupCommitMenuItem.Click += (_, _) => StartSpecialCommit(CommitKind.Fixup);
        SquashCommitMenuItem.Click += (_, _) => StartSpecialCommit(CommitKind.Squash);
        AmendCommitMenuItem.Click += (_, _) => StartSpecialCommit(CommitKind.Amend);
        AdvancedHelpMenuItem.Click += (_, _) => OsShellUtil.OpenUrlInDefaultBrowser(
            GitUI.UserManual.UserManual.UrlFor(RewriteCommit.HelpSubfolder, RewriteCommit.HelpAnchor));
    }

    // Upstream's FixupCommitToolStripMenuItemClick, SquashCommitToolStripMenuItemClick and
    // AmendCommitToolStripMenuItemClick (and their hotkeys): the commit window making that commit for the selected one.
    // Upstream's commit window is modal; the new shell's is not, so an open one takes the special commit, its message
    // replaced.
    private bool StartSpecialCommit(CommitKind kind)
    {
        if (RepositoryPath is not { } path || _commits.Selected is not { } row || ArtificialCommits.IsArtificial(row.Hash)
            || !ObjectId.TryParse(row.Hash, out ObjectId id))
        {
            return false;
        }

        Run(async () =>
        {
            GitRevision? revision = await Task.Run(() => GitModules.Open(path).GetRevision(id, shortFormat: false));
            OpenCommitWindow()?.StartSpecialCommit(kind, revision?.Subject ?? row.Subject, revision?.Body ?? row.Subject);
        });
        return true;
    }

    // Upstream's editCommitToolStripMenuItem_Click and rewordCommitToolStripMenuItem_Click (LaunchRebase).
    private void RewriteSelectedCommit(string command) => RunOnSelectedCommit((path, row) =>
        _actions.RewriteCommitAsync(path,
            RewriteCommit.Arguments(
                row.ParentHashes is [{ } first, ..] && ObjectId.TryParse(first, out ObjectId parent) ? parent : null,
                GitVersion.Current.SupportRebaseMerges),
            command));

    // Upstream's ResetAnotherBranchToHereToolStripMenuItemClick: FormResetAnotherBranch on the commit, its update-ref with
    // the output shown (which reads the repository again), then the checkout window on the branch when asked.
    private Task<bool> ShowResetAnotherBranchAsync(string path, CommitRow row)
        => new ResetAnotherBranchWindow(path, row.Hash, _preferences,
                (_, arguments) => RunGitWithOutputAsync(path, arguments.ToString()),
                (owner, branch) => CheckoutFlow().StartAsync(owner, path, branch, remote: false))
            .ShowDialog<bool>(this);

    private bool ShowGridMenu()
    {
        if (RepositoryPath is not { } path || _commits.Selected is not { } row)
        {
            return false;
        }

        // Upstream's ContextMenuOpening: a right-clicked label gets the focused menu, its "advanced" items under "Other
        // actions", unless Shift or "always show advanced options" asks for the whole menu (Ctrl overrides both, and Cmd
        // on macOS, where Ctrl with a click already is the right-click); either
        // way the ref submenus offer only that ref.
        RefLabel? clicked = _rightClickedRef;
        _rightClickedRef = null;
        ArrangeGridMenu(GridMenus.IsFocused(clicked is not null, _rightClickModifiers.HasFlag(Hotkeys.CommandModifier),
            _rightClickModifiers.HasFlag(KeyModifiers.Shift), _preferences.AlwaysShowAdvOpt));
        Func<RefLabel, bool>? keep = clicked is null ? null : GridMenus.Only(clicked);

        bool artificial = ArtificialCommits.IsArtificial(row.Hash);
        HashSet<string> localBranches = [.. _repository.Branches.Where(branch => !branch.IsRemote).Select(branch => branch.Name)];
        GridRefMenus menus = GridMenus.For(row, _repository.CurrentBranch,
            new HashSet<string>(_repository.Tags.Where(localBranches.Contains)), keep);

        FillCopyMenu(CopyToClipboardMenuItem, SelectedRevisions, keep);
        Fill(GridCheckoutBranchMenuItem, menus.Checkout,
            branch => Run(() => StartCheckoutBranchAsync(path, branch.Name, branch.IsRemote)));
        Fill(GridPushBranchMenuItem, menus.Push, branch => Run(() => ShowPushDialogAsync(branch.Name)));
        Fill(GridMergeBranchMenuItem, menus.Merge, branch => Run(() => ShowMergeAsync(path, branch.Target)));
        Fill(GridRenameBranchMenuItem, menus.Rename, branch => Run(() => RenameBranchAsync(branch.Name)));
        Fill(GridDeleteBranchMenuItem, menus.Delete, branch => Run(() => branch.IsRemote
            ? ShowDeleteRemoteBranchAsync(path, branch.Name)
            : ShowDeleteBranchAsync(path, [branch.Name])));
        Fill(GridDeleteTagMenuItem, menus.DeleteTags, tag => Run(() => ShowDeleteTagAsync(path, tag.Name)));
        FillSelectInLeftPanel(menus.SelectInLeftPanel);
        FillFromMenu(GridNavigateMenuItem, NavigateMenu);
        ShowViewMenuChecks();
        FillFromMenu(GridViewMenuItem, ViewMenu);

        // Upstream's SetEnabled calls: an item that does not apply is hidden.
        Show(CopyToClipboardMenuItem, !artificial);
        Show(GridCheckoutBranchMenuItem, !artificial && HasItems(GridCheckoutBranchMenuItem));
        Show(GridMergeBranchMenuItem, !artificial && HasItems(GridMergeBranchMenuItem));
        Show(GridPushBranchMenuItem, !artificial && HasItems(GridPushBranchMenuItem));
        Show(GridRebaseOnMenuItem, !artificial);
        RebaseOnCommitMenuItem.IsEnabled = menus.RebaseOnTopOf is not null;
        RebaseInteractiveMenuItem.IsEnabled = menus.RebaseOnTopOf is not null && _actions.EditorCommand is not null;
        RebaseAdvancedMenuItem.IsEnabled = menus.RebaseOnTopOf is not null;
        Show(GridRenameBranchMenuItem, HasItems(GridRenameBranchMenuItem));
        Show(GridDeleteBranchMenuItem, HasItems(GridDeleteBranchMenuItem) || menus.IsHeadOfCurrentBranch);
        GridDeleteBranchMenuItem.IsEnabled = HasItems(GridDeleteBranchMenuItem);
        Show(GridDeleteTagMenuItem, HasItems(GridDeleteTagMenuItem));
        foreach (MenuItem item in new[]
                 {
                     CheckoutCommitMenuItem, RevertMenuItem, CherryPickMenuItem, CreateBranchHereMenuItem,
                     ResetCurrentBranchMenuItem, ResetAnotherBranchMenuItem,
                 })
        {
            Show(item, !artificial);
        }

        Show(ArchiveCommitMenuItem, !artificial);
        Show(GridAdvancedMenuItem, !artificial);

        // Upstream's showStash: the stash items for a stash row.
        Show(ApplyStashMenuItem, !artificial && row.IsStash);
        Show(PopStashMenuItem, !artificial && row.IsStash);
        Show(GridDropStashMenuItem, !artificial && row.IsStash);
        ShowCompareMenu(row);
        Show(AmendCommitMenuItem, !artificial && GitVersion.Current.SupportAmendCommits);
        Show(CreateTagHereMenuItem, !artificial);
        Show(GridResetChangesMenuItem, artificial);
        Show(GridCommitMenuItem, artificial);
        Show(GridSelectInLeftPanelMenuItem, menus.SelectInLeftPanel.Count > 0);
        Show(OpenBuildReportMenuItem, SelectedBuildStatus()?.Url is not null);
        Show(OpenPullRequestPageMenuItem, SelectedBuildStatus()?.PullRequestUrl is not null);
        UpdateGridMenuSeparators();
        return true;
    }

    // Upstream's _contextMenuItems loop in ContextMenuOpening: the items in their order, the "advanced" ones of a focused
    // menu under "Other actions".
    private void ArrangeGridMenu(bool focused)
    {
        CommitContextMenu.Items.Clear();
        OtherActionsMenuItem.Items.Clear();
        foreach ((Control item, bool isAdvanced) in _gridMenuLayout)
        {
            (focused && isAdvanced ? OtherActionsMenuItem.Items : CommitContextMenu.Items).Add(item);
        }

        OtherActionsMenuItem.IsVisible = focused;
    }

    // Upstream's OnGridViewCellMouseDown: the ref label under a right-click, kept for the menu it opens.
    private void OnCommitListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _rightClickedRef = null;
        if (!e.GetCurrentPoint(CommitList).Properties.IsRightButtonPressed)
        {
            return;
        }

        _rightClickModifiers = e.KeyModifiers;

        // The labels draw only their outlines, so the row's labels are asked rather than the hit-tested control.
        if (e.Source is Visual source && source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is { } rowItem)
        {
            _rightClickedRef = rowItem.GetVisualDescendants().OfType<RefLabelView>()
                .Select(view => view.LabelAt(e.GetPosition(view)))
                .FirstOrDefault(label => label is not null)?.Ref;
        }
    }

    // Upstream's CopyContextMenuItem.OnDropDownOpening, for the grid's menu and the left panel's.
    private void FillCopyMenu(MenuItem host, IReadOnlyList<CommitRow> rows, Func<RefLabel, bool>? keep)
        => CopyMenus.Fill(host, rows, keep, value => Run(() => CopyTextAsync(value)));

    private async Task CopyTextAsync(string? text)
    {
        if (text is not null && GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    // Upstream's AddBranchMenuItem for each entry; a null entry is a separator.
    private static void Fill(MenuItem parent, IReadOnlyList<GridMenuRef?> refs, Action<GridMenuRef> click)
    {
        parent.Items.Clear();
        foreach (GridMenuRef? gitRef in refs)
        {
            if (gitRef is null)
            {
                parent.Items.Add(new Separator());
                continue;
            }

            MenuItem item = new() { Header = new TextBlock { Text = gitRef.Name }, Icon = UpstreamIcon(gitRef.Image) };
            item.Click += (_, _) => click(gitRef);
            parent.Items.Add(item);
        }
    }

    // Upstream's tsmiSelectInLeftPanel: one ref selects it at once, several are a submenu.
    private void FillSelectInLeftPanel(IReadOnlyList<GridMenuRef> refs)
    {
        GridSelectInLeftPanelMenuItem.Items.Clear();
        GridSelectInLeftPanelMenuItem.Click -= OnSelectSingleInLeftPanel;
        _selectInLeftPanel = refs.Count == 1 ? refs[0] : null;
        if (refs.Count == 1)
        {
            GridSelectInLeftPanelMenuItem.Click += OnSelectSingleInLeftPanel;
            return;
        }

        Fill(GridSelectInLeftPanelMenuItem, refs, SelectInLeftPanel);
    }

    private GridMenuRef? _selectInLeftPanel;

    private void OnSelectSingleInLeftPanel(object? sender, RoutedEventArgs e)
    {
        if (e.Source == GridSelectInLeftPanelMenuItem && _selectInLeftPanel is { } gitRef)
        {
            SelectInLeftPanel(gitRef);
        }
    }

    // Upstream's FormBrowse.SelectInLeftPanel: the left panel shown, then the ref's node selected.
    private void SelectInLeftPanel(GridMenuRef gitRef)
    {
        if (ToggleLeftPanelButton.IsChecked != true)
        {
            ToggleLeftPanel();
        }

        RepoObjectNode? node = _repoObjects.SelectMany(root => root.Descendants()).FirstOrDefault(node =>
            node.Kind == RepoObjectKind.Tag
                ? node.Item as string == gitRef.Name
                : node.Branch is { } branch && branch.Name == gitRef.Name && branch.IsRemote == gitRef.IsRemote);
        if (node is null)
        {
            return;
        }

        foreach (RepoObjectNode parent in RepoObjectsTree.PathTo(_repoObjects, node).SkipLast(1))
        {
            parent.IsExpanded = true;
        }

        Dispatcher.UIThread.Post(() =>
        {
            BranchTreeView.SelectedItem = node;
            BranchTreeView.ScrollIntoView(node);
            BranchTreeView.Focus();
        }, DispatcherPriority.Loaded);
    }

    // Upstream's grid Navigate and View submenus have the main menus' commands (its FillMenuFromMenuCommands); here they
    // forward to the main menus' items, showing their state.
    private static void FillFromMenu(MenuItem target, MenuItem menu)
    {
        target.Items.Clear();
        foreach (object? entry in menu.Items)
        {
            if (entry is not MenuItem source)
            {
                target.Items.Add(new Separator());
                continue;
            }

            MenuItem item = new()
            {
                Header = source.Header,
                InputGesture = source.InputGesture,
                IsEnabled = source.IsEnabled,
                IsVisible = source.IsVisible,
                ToggleType = source.ToggleType,
                IsChecked = source.IsChecked,
                Icon = source.Icon is Image { Source: { } picture } ? new Image { Source = picture, Width = 16, Height = 16 } : null,
            };
            item.Classes.AddRange(source.Classes.Where(name => !name.StartsWith(':')));
            ToolTip.SetTip(item, ToolTip.GetTip(source));
            item.Click += (_, _) => source.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            target.Items.Add(item);
        }
    }

    // Upstream's UpdateSeparators: a separator shows only after a shown item since the previous one.
    private void UpdateGridMenuSeparators()
    {
        UpdateSeparators(CommitContextMenu.Items);
        UpdateSeparators(OtherActionsMenuItem.Items);
    }

    private static void UpdateSeparators(ItemCollection items)
    {
        bool seenItem = false;
        foreach (object? entry in items)
        {
            if (entry is Separator separator)
            {
                separator.IsVisible = seenItem;
                seenItem = false;
            }
            else if (entry is MenuItem { IsVisible: true })
            {
                seenItem = true;
            }
        }
    }

    private static bool HasItems(MenuItem item) => item.Items.OfType<MenuItem>().Any(child => child.IsEnabled);

    private static void Show(MenuItem item, bool shown)
    {
        item.IsVisible = shown;
        item.IsEnabled = shown;
    }
}
