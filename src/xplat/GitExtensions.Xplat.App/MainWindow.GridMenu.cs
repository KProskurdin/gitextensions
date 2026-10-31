using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's revision grid context menu (<c>RevisionGridControl.ContextMenuOpening</c>; rules in <see cref="GridMenus"/>):
///  when it opens, the copy, branch and tag submenus are filled for the selected commit, items that do not apply are
///  hidden, and separators with nothing above them are hidden too.
/// </summary>
public partial class MainWindow
{
    private void WireGridMenu()
    {
        CommitContextMenu.Opening += (_, e) => e.Cancel = !ShowGridMenu();
        GridResetChangesMenuItem.Click += (_, _) => RunOnRepository(ResetChangesAsync);
        GridCommitMenuItem.Click += (_, _) => ShowCommitWindow();
    }

    private bool ShowGridMenu()
    {
        if (RepositoryPath is not { } path || _commits.Selected is not { } row)
        {
            return false;
        }

        bool artificial = ArtificialCommits.IsArtificial(row.Hash);
        HashSet<string> localBranches = [.. _repository.Branches.Where(branch => !branch.IsRemote).Select(branch => branch.Name)];
        GridRefMenus menus = GridMenus.For(row, _repository.CurrentBranch,
            new HashSet<string>(_repository.Tags.Where(localBranches.Contains)));

        FillCopyMenu(row);
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
        FillNavigateMenu();

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
                     ResetCurrentBranchMenuItem,
                 })
        {
            Show(item, !artificial);
        }

        Show(ArchiveCommitMenuItem, !artificial);
        Show(CreateTagHereMenuItem, !artificial);
        Show(GridResetChangesMenuItem, artificial);
        Show(GridCommitMenuItem, artificial);
        Show(GridSelectInLeftPanelMenuItem, menus.SelectInLeftPanel.Count > 0);
        Show(OpenBuildReportMenuItem, SelectedBuildStatus()?.Url is not null);
        Show(OpenPullRequestPageMenuItem, SelectedBuildStatus()?.PullRequestUrl is not null);
        UpdateGridMenuSeparators();
        return true;
    }

    // Upstream's CopyContextMenuItem.OnDropDownOpening.
    private void FillCopyMenu(CommitRow row)
    {
        CopyToClipboardMenuItem.Items.Clear();
        IReadOnlyList<GridCopyItem?> items = GridMenus.CopyItems(row,
            Ui.UpstreamTranslation.Text("TranslatedStrings", "_branchesText", "Branches"),
            Ui.UpstreamTranslation.Text("TranslatedStrings", "_tagsText", "Tags"));
        foreach (GridCopyItem? item in items)
        {
            if (item is null)
            {
                CopyToClipboardMenuItem.Items.Add(new Separator());
                continue;
            }

            MenuItem menuItem = new()
            {
                Header = item.Text,
                Icon = item.Image is null ? null : UpstreamIcon(item.Image),
                IsEnabled = item.Value is not null,
                FontWeight = item.Value is null ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal,
            };
            string? value = item.Value;
            menuItem.Click += (_, _) => Run(() => CopyTextAsync(value));
            CopyToClipboardMenuItem.Items.Add(menuItem);
        }
    }

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

    // Upstream's grid Navigate submenu has the Navigate menu's commands; here they forward to the menu's items.
    private void FillNavigateMenu()
    {
        GridNavigateMenuItem.Items.Clear();
        foreach (object? entry in NavigateMenu.Items)
        {
            if (entry is not MenuItem source)
            {
                GridNavigateMenuItem.Items.Add(new Separator());
                continue;
            }

            MenuItem item = new()
            {
                Header = source.Header,
                InputGesture = source.InputGesture,
                IsEnabled = source.IsEnabled,
                Icon = source.Icon is Image { Source: { } picture } ? new Image { Source = picture, Width = 16, Height = 16 } : null,
            };
            item.Click += (_, _) => source.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            GridNavigateMenuItem.Items.Add(item);
        }
    }

    // Upstream's UpdateSeparators: a separator shows only after a shown item since the previous one.
    private void UpdateGridMenuSeparators()
    {
        bool seenItem = false;
        foreach (object? entry in CommitContextMenu.Items)
        {
            if (entry is Separator separator)
            {
                if (separator != BisectSeparator)
                {
                    separator.IsVisible = seenItem;
                }

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
