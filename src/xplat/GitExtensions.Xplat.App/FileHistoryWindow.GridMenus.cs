using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormBrowseMenus.InsertRevisionGridMainMenuItems</c> in FormFileHistory: the grid's Navigate and View
///  menus (<c>RevisionGridMenuCommands</c>) in its context menu, acting on the file's grid, with the grid's hotkeys, its
///  navigation history and its quick search, as in the browse window.
/// </summary>
public partial class FileHistoryWindow
{
    // Upstream RevisionGridMenuCommands' _quickSearchQuickHelp, shown by Navigate > Quick search.
    private const string QuickSearchHelpText = "Start typing in revision grid to start quick search.";

    private readonly NavigationHistory _navigationHistory = new();
    private readonly QuickSearch _quickSearch = new();
    private DispatcherTimer? _quickSearchTimer;

    private CommitListItem? LatestSelectedItem
        => ViewModel.Selection.Latest is { } latest
            ? ViewModel.Commits.VisibleRows.FirstOrDefault(item => item.Row.Hash == latest.Hash)
            : null;

    private int LatestSelectedIndex
        => LatestSelectedItem is { } item ? IndexOf(ViewModel.Commits.VisibleRows, item) : -1;

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

    private void WireGridMenus()
    {
        foreach ((MenuItem item, GridCommand command) in NavigateMenuCommands())
        {
            item.InputGesture = Hotkeys.Grid.GestureFor(command);
            item.Click += (_, _) => RunGridCommand(command);
        }

        HighlightSelectedBranchMenuItem.InputGesture = Hotkeys.Grid.GestureFor(GridCommand.ToggleHighlightSelectedBranch);
        QuickSearchMenuItem.Click += (_, _) => Run(() => new MessageBoxWindow(
            UpstreamTranslation.ByText(QuickSearchHelpText), "Information", MessageBoxButtons.OK).ShowDialog(this));

        // As upstream's OnGridViewSelectionChanged: the history follows a single selected commit.
        HistoryList.SelectionChanged += (_, _) =>
        {
            if (ViewModel.Selection.Revisions is [{ } only])
            {
                _navigationHistory.Push(only.Hash);
            }
        };

        // Upstream's grid hotkeys act while it has the focus.
        HistoryList.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (!e.Handled && Hotkeys.Grid.Match(e) is { } command)
            {
                e.Handled = RunGridCommand(command);
            }
        });
        WireQuickSearch();
        WireViewMenu();
        ViewModel.Commits.DrawNonRelativesGray = _preferences.RevisionGraphDrawNonRelativesGray;
        ViewModel.Commits.GraphAppearanceChanged += (_, _) =>
        {
            foreach (GraphCell cell in HistoryList.GetVisualDescendants().OfType<GraphCell>())
            {
                cell.InvalidateVisual();
            }
        };
    }

    // The commands of upstream RevisionGridControl's hotkeys and Navigate menu; false when one cannot run now.
    private bool RunGridCommand(GridCommand command)
    {
        CommitRow? selected = ViewModel.Selection.Latest;
        switch (command)
        {
            case GridCommand.ToggleBetweenArtificialAndHeadCommits:
                return SelectGridRow(ViewModel.Commits.IndexOfNextArtificialOrHead(selected));
            case GridCommand.SelectCurrentRevision:
                return SelectGridRow(ViewModel.Commits.IndexOfHead());
            case GridCommand.GoToCommit:
                Run(GoToCommitAsync);
                return true;
            case GridCommand.GoToChild when selected is not null:
                return SelectGridRow(ViewModel.Commits.IndexOfChild(selected));
            case GridCommand.GoToParent or GridCommand.GoToFirstParent when selected is not null:
                return SelectGridRow(ViewModel.Commits.IndexOfParent(selected));
            case GridCommand.GoToLastParent when selected is not null:
                return SelectGridRow(ViewModel.Commits.IndexOfLastParent(selected));
            case GridCommand.GoToMergeBase when selected is not null:
                Run(GoToMergeBaseAsync);
                return true;
            case GridCommand.NavigateBackward:
                return Navigate(_navigationHistory.NavigateBackward());
            case GridCommand.NavigateForward:
                return Navigate(_navigationHistory.NavigateForward());
            case GridCommand.NextQuickSearch:
                return QuickSearchNext(down: true);
            case GridCommand.PrevQuickSearch:
                return QuickSearchNext(down: false);
            case GridCommand.ToggleHighlightSelectedBranch when selected is not null:
                ViewModel.Commits.HighlightBranch(selected);
                return true;
            case GridCommand.ShowAllBranches:
                ToolStripFilters.ShowBranches(currentOnly: false, filtered: false);
                return true;
            case GridCommand.ShowCurrentBranchOnly:
                ToolStripFilters.ShowBranches(currentOnly: true, filtered: false);
                return true;
            case GridCommand.RevisionFilter:
                Run(((IFilterToolBarGrid)this).ShowRevisionFilterDialogAsync);
                return true;
            case GridCommand.ResetRevisionFilter:
                ApplyFilter(ViewModel.Commits.Filter.ResetAll());
                return true;
            case GridCommand.ToggleHideMergeCommits:
                ApplyFilter(ViewModel.Commits.Filter with { NoMerges = !ViewModel.Commits.Filter.NoMerges });
                return true;
            case GridCommand.ShowFirstParent:
                ApplyFilter(ViewModel.Commits.Filter with { FirstParent = !ViewModel.Commits.Filter.FirstParent });
                return true;
            default:
                return false;
        }
    }

    private bool SelectGridRow(int? index)
    {
        if (index is not { } row || row < 0 || row >= ViewModel.Commits.VisibleRows.Count)
        {
            return false;
        }

        HistoryList.SelectedItem = ViewModel.Commits.VisibleRows[row];
        HistoryList.ScrollIntoView(row);
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

        Run(() => ViewModel.GoToCommitAsync(hash));
        return true;
    }

    // Upstream's GotoCommitExecute: the commit FormGoToCommit names, selected in the grid, or upstream's messages.
    private async Task GoToCommitAsync()
    {
        if (await new GoToCommitWindow(_repositoryPath).ShowDialog<ObjectId?>(this) is not { } commit)
        {
            return;
        }

        if (commit.IsZero)
        {
            ShowError(UpstreamTranslation.ByText(GoToCommit.NoRevisionFound));
            return;
        }

        await ViewModel.GoToCommitAsync(commit.ToString());
    }

    // Upstream's GoToMergeBase: the common ancestor of the selected commits (with HEAD for one).
    private async Task GoToMergeBaseAsync()
    {
        string[] hashes = [.. ViewModel.Selection.Revisions.Select(row => row.Hash)];
        MergeBaseResult result = await Task.Run(() => GoToCommit.MergeBase(_module, hashes));
        if (result.NoCommonAncestor)
        {
            ShowError(UpstreamTranslation.ByText(GoToCommit.NoMergeBaseCommit));
        }
        else if (result.Hash is { } hash)
        {
            await ViewModel.GoToCommitAsync(hash);
        }
    }

    // Upstream's quick search of the revision grid: typing into the grid selects the next commit that matches, Alt+Down and
    // Alt+Up find the next and previous match, and the search ends after upstream's timeout, with Escape or another key.
    private void WireQuickSearch()
    {
        HistoryList.AddHandler(TextInputEvent, (_, e) =>
        {
            if (e.Text is { Length: > 0 } text && !text.Any(char.IsControl))
            {
                QuickSearchTo(_quickSearch.Type(text, ShownRows(), LatestSelectedIndex));
                e.Handled = true;
            }
        });
        HistoryList.AddHandler(KeyDownEvent, (_, e) => Run(() => OnQuickSearchKeyAsync(e)), RoutingStrategies.Tunnel);
    }

    private async Task OnQuickSearchKeyAsync(KeyEventArgs e)
    {
        if (!_quickSearch.IsActive)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Back:
                QuickSearchTo(_quickSearch.Backspace(ShownRows(), LatestSelectedIndex));
                e.Handled = true;
                break;
            case Key.V when e.KeyModifiers == Hotkeys.CommandModifier:
                e.Handled = true;
                if (await (Clipboard?.TryGetTextAsync() ?? Task.FromResult<string?>(null)) is { Length: > 0 } text)
                {
                    QuickSearchTo(_quickSearch.Type(text, ShownRows(), LatestSelectedIndex));
                }

                break;
            case Key.Escape:
                // Escape ends the search, not the window.
                e.Handled = true;
                EndQuickSearch();
                break;
            case Key.Enter or Key.Tab:
                EndQuickSearch();
                break;
        }
    }

    private bool QuickSearchNext(bool down)
    {
        QuickSearchTo(_quickSearch.Next(ShownRows(), LatestSelectedIndex, down));
        return true;
    }

    private IReadOnlyList<CommitRow> ShownRows() => [.. ViewModel.Commits.VisibleRows.Select(item => item.Row)];

    private void QuickSearchTo(int? index)
    {
        if (index is { } row && LatestSelectedIndex != row)
        {
            SelectGridRow(row);
        }

        QuickSearchText.Text = _quickSearch.Text;
        QuickSearchText.Foreground = _quickSearch.Found ? null : ThemeBrushes.Current.Warning;
        QuickSearchPanel.IsVisible = true;
        _quickSearchTimer ??= new DispatcherTimer();
        _quickSearchTimer.Stop();
        _quickSearchTimer.Interval =
            TimeSpan.FromMilliseconds(Math.Max(1, _preferences.RevisionGridQuickSearchTimeout));
        _quickSearchTimer.Tick -= OnQuickSearchTimeout;
        _quickSearchTimer.Tick += OnQuickSearchTimeout;
        _quickSearchTimer.Start();
    }

    private void OnQuickSearchTimeout(object? sender, EventArgs e) => EndQuickSearch();

    private void EndQuickSearch()
    {
        _quickSearchTimer?.Stop();
        _quickSearch.End();
        QuickSearchPanel.IsVisible = false;
    }

    // Upstream's View menu (RevisionGridMenuCommands.GetViewMenuCommands) for this grid, with the items the browse window's
    // has; its settings are the browse window's, so a change shows there on its next refresh.
    private void WireViewMenu()
    {
        ViewMenuItem.DataContext = new GridViewMenuViewModel(_preferences, ViewModel.Commits, new GridViewMenuServices
        {
            ShowBranches = (currentOnly, filtered) => ToolStripFilters.ShowBranches(currentOnly, filtered),
            ApplyFilter = ApplyFilter,
            EditFilter = ((IFilterToolBarGrid)this).ShowRevisionFilterDialogAsync,
            RefreshRepository = ReloadGrid,
            ShowGridView = ShowGridColumns,
            ShowAvatarColumn = ShowGridColumns,
            ShowBuildStatusColumn = () => { },
            HighlightSelectedBranch = () => RunGridCommand(GridCommand.ToggleHighlightSelectedBranch),
            ShowError = ex => ShowError(ex.Message),
        });

        ViewMenuItem.SubmenuOpened += (_, _) => ((GridViewMenuViewModel)ViewMenuItem.DataContext!).Refresh();
    }

    // The grid reads its rows again with the changed settings, once it shows.
    private void ReloadGrid()
    {
        if (HistoryList.IsVisible)
        {
            Run(() => ViewModel.ApplyGridFilterAsync(ViewModel.Commits.Filter));
        }
    }

    private void ApplyFilter(RevisionFilter filter) => Run(() => ((IFilterToolBarGrid)this).ApplyFilterAsync(filter));

    private static int IndexOf(IReadOnlyList<CommitListItem> items, CommitListItem item)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], item))
            {
                return i;
            }
        }

        return -1;
    }
}
