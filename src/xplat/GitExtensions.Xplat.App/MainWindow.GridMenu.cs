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

        Run(() => StartSpecialCommitAsync(path, kind, id, row.Subject));
        return true;
    }

    // Upstream's StartFixupCommitDialog, StartSquashCommitDialog and StartAmendCommitDialog: the commit window making that
    // commit for a commit; false when no commit window could open.
    private async Task<bool> StartSpecialCommitAsync(string path, CommitKind kind, ObjectId id, string subject)
    {
        GitRevision? revision = await Task.Run(() => GitModules.Open(path).GetRevision(id, shortFormat: false));
        if (OpenCommitWindow() is not { } window)
        {
            return false;
        }

        window.StartSpecialCommit(kind, revision?.Subject ?? subject, revision?.Body ?? subject);
        return true;
    }

    // Upstream's EditCommitWithFixupToolStripMenuItemClick: the commit window making a fixup commit for the commit; once it
    // is made, the interactive autosquash rebase onto the commit's first parent with git's output shown. Upstream's
    // commit window is modal; the new shell's is not, so the rebase follows the window's next commit.
    private async Task<bool> EditCommitWithFixupAsync(string path, string revision, ObjectId firstParent)
    {
        GitRevision? commit = await Task.Run(() => GitModules.Open(path).GetRevision(ObjectId.Parse(revision), shortFormat: false));
        if (OpenCommitWindow() is not { } window)
        {
            return false;
        }

        window.StartSpecialCommit(CommitKind.Fixup, commit?.Subject ?? revision, commit?.Body ?? commit?.Subject ?? revision);
        window.ViewModel.CommitCreated += OnFixupCommitCreated;
        window.Closed += (_, _) => window.ViewModel.CommitCreated -= OnFixupCommitCreated;
        return true;

        void OnFixupCommitCreated(object? sender, EventArgs e)
        {
            window.ViewModel.CommitCreated -= OnFixupCommitCreated;
            Run(() => _actions.RebaseWithOutputAsync(path,
                RewriteCommit.FixupArguments(firstParent, GitVersion.Current.SupportRebaseMerges)));
        }
    }

    // Upstream's ResetAnotherBranchToHereToolStripMenuItemClick: FormResetAnotherBranch on the commit, its update-ref with
    // the output shown (which reads the repository again), then the checkout window on the branch when asked.
    private Task<bool> ShowResetAnotherBranchAsync(string path, string revision)
        => new ResetAnotherBranchWindow(path, revision, _preferences,
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

        HashSet<string> localBranches = [.. _repository.Branches.Where(branch => !branch.IsRemote).Select(branch => branch.Name)];
        GridRefMenus menus = GridMenus.For(row, _repository.CurrentBranch,
            new HashSet<string>(_repository.Tags.Where(localBranches.Contains)), keep,
            clicked is null ? null : GridMenus.OnlyOrTracking(clicked));

        FillCopyMenu(CopyToClipboardMenuItem, SelectedRevisions, keep);
        GridRefMenuEntries entries = Toolbar.GridRefEntries(menus);
        MenuEntries.Fill(GridCheckoutBranchMenuItem.Items, entries.Checkout, this);
        MenuEntries.Fill(GridPushBranchMenuItem.Items, entries.Push, this);
        MenuEntries.Fill(GridMergeBranchMenuItem.Items, entries.Merge, this);
        MenuEntries.Fill(GridRenameBranchMenuItem.Items, entries.Rename, this);
        MenuEntries.Fill(GridDeleteBranchMenuItem.Items, entries.Delete, this);
        MenuEntries.Fill(GridDeleteTagMenuItem.Items, entries.DeleteTags, this);
        MenuEntries.Fill(GridSelectInLeftPanelMenuItem.Items, entries.SelectInLeftPanel, this);
        FillFromMenu(GridNavigateMenuItem, NavigateMenu);
        GridViewMenu.Refresh();
        FillFromMenu(GridViewMenuItem, ViewMenu);

        // Upstream's SetEnabled calls: an item that does not apply is hidden (the view model's state, bound in XAML), then
        // the separators left alone.
        Toolbar.GridMenu.Refresh(row, menus, editorAvailable: _actions.EditorCommand is not null,
            hasBuildReport: SelectedBuildStatus()?.Url is not null,
            hasPullRequest: SelectedBuildStatus()?.PullRequestUrl is not null, CompareState());
        ShowCompareMenu();
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
        => CopyMenus.Fill(host, rows, keep, Toolbar.CopyTextCommand);

    private async Task CopyTextAsync(string? text)
    {
        if (text is not null && GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    // Upstream's FormBrowse.SelectInLeftPanel: the left panel shown, then the ref's node selected.
    private void SelectInLeftPanel(string name, bool isRemote)
    {
        if (ToggleLeftPanelButton.IsChecked != true)
        {
            ToggleLeftPanel();
        }

        RepoObjectNode? node = _repoObjects.SelectMany(root => root.Descendants()).FirstOrDefault(node =>
            node.Kind == RepoObjectKind.Tag
                ? node.Item as string == name
                : node.Branch is { } branch && branch.Name == name && branch.IsRemote == isRemote);
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

    // Upstream's grid Navigate and View submenus have the main menus' commands (its FillMenuFromMenuCommands): copies of the
    // main menus' items, showing their state and running their commands (the group headers have none).
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

                // An item bound to a view model's command runs it from the copy too.
                Command = source.Command,
                CommandParameter = source.CommandParameter,
                Icon = source.Icon is Image { Source: { } picture } ? new Image { Source = picture, Width = 16, Height = 16 } : null,
            };
            item.Classes.AddRange(source.Classes.Where(name => !name.StartsWith(':')));
            ToolTip.SetTip(item, ToolTip.GetTip(source));
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

    // An item that does not apply is hidden, as upstream's SetEnabled; also for the left panel's submodule menu.
    private static void Show(MenuItem item, bool shown)
    {
        item.IsVisible = shown;
        item.IsEnabled = shown;
    }
}
