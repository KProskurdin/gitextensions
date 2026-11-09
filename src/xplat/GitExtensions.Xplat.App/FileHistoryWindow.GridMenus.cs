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
        => _selection.Latest is { } latest
            ? _commits.VisibleRows.FirstOrDefault(item => item.Row.Hash == latest.Hash)
            : null;

    private int LatestSelectedIndex
        => LatestSelectedItem is { } item ? IndexOf(_commits.VisibleRows, item) : -1;

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
            if (_selection.Revisions is [{ } only])
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
        _commits.DrawNonRelativesGray = _preferences.RevisionGraphDrawNonRelativesGray;
        _commits.GraphAppearanceChanged += (_, _) =>
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
        CommitRow? selected = _selection.Latest;
        switch (command)
        {
            case GridCommand.ToggleBetweenArtificialAndHeadCommits:
                return SelectGridRow(_commits.IndexOfNextArtificialOrHead(selected));
            case GridCommand.SelectCurrentRevision:
                return SelectGridRow(_commits.IndexOfHead());
            case GridCommand.GoToCommit:
                Run(GoToCommitAsync);
                return true;
            case GridCommand.GoToChild when selected is not null:
                return SelectGridRow(_commits.IndexOfChild(selected));
            case GridCommand.GoToParent or GridCommand.GoToFirstParent when selected is not null:
                return SelectGridRow(_commits.IndexOfParent(selected));
            case GridCommand.GoToLastParent when selected is not null:
                return SelectGridRow(_commits.IndexOfLastParent(selected));
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
                _commits.HighlightBranch(selected);
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
                ApplyFilter(_commits.Filter.ResetAll());
                return true;
            case GridCommand.ToggleHideMergeCommits:
                ApplyFilter(_commits.Filter with { NoMerges = !_commits.Filter.NoMerges });
                return true;
            case GridCommand.ShowFirstParent:
                ApplyFilter(_commits.Filter with { FirstParent = !_commits.Filter.FirstParent });
                return true;
            default:
                return false;
        }
    }

    private bool SelectGridRow(int? index)
    {
        if (index is not { } row || row < 0 || row >= _commits.VisibleRows.Count)
        {
            return false;
        }

        HistoryList.SelectedItem = _commits.VisibleRows[row];
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

        Run(() => GoToCommitAsync(hash));
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

        await GoToCommitAsync(commit.ToString());
    }

    // Upstream's GoToMergeBase: the common ancestor of the selected commits (with HEAD for one).
    private async Task GoToMergeBaseAsync()
    {
        string[] hashes = [.. _selection.Revisions.Select(row => row.Hash)];
        MergeBaseResult result = await Task.Run(() => GoToCommit.MergeBase(_module, hashes));
        if (result.NoCommonAncestor)
        {
            ShowError(UpstreamTranslation.ByText(GoToCommit.NoMergeBaseCommit));
        }
        else if (result.Hash is { } hash)
        {
            await GoToCommitAsync(hash);
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

    private IReadOnlyList<CommitRow> ShownRows() => [.. _commits.VisibleRows.Select(item => item.Row)];

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
        ShowAllBranchesMenuItem.Click += (_, _) => ToolStripFilters.ShowBranches(currentOnly: false, filtered: false);
        ShowCurrentBranchOnlyMenuItem.Click += (_, _) => ToolStripFilters.ShowBranches(currentOnly: true, filtered: false);
        ShowFilteredBranchesMenuItem.Click += (_, _) => ToolStripFilters.ShowBranches(currentOnly: false, filtered: true);
        ShowReflogReferencesMenuItem.Click += (_, _) =>
            ApplyFilter(_commits.Filter with { ShowReflog = !_commits.Filter.ShowReflog });
        AdvancedFilterViewMenuItem.Click += (_, _) => Run(((IFilterToolBarGrid)this).ShowRevisionFilterDialogAsync);
        DrawNonRelativesGrayMenuItem.Click += (_, _) =>
        {
            _preferences.RevisionGraphDrawNonRelativesGray = !_preferences.RevisionGraphDrawNonRelativesGray;
            _commits.DrawNonRelativesGray = _preferences.RevisionGraphDrawNonRelativesGray;
        };
        HighlightSelectedBranchMenuItem.Click += (_, _) => RunGridCommand(GridCommand.ToggleHighlightSelectedBranch);
        ShowArtificialCommitsMenuItem.Click += (_, _) => ChangePreference(() =>
            _preferences.RevisionGraphShowArtificialCommits = !_preferences.RevisionGraphShowArtificialCommits);
        ShowStashesMenuItem.Click += (_, _) => ChangePreference(() => _preferences.ShowStashes = !_preferences.ShowStashes);
        ShowSessionCheckpointsMenuItem.Click += (_, _) =>
            ChangePreference(() => _preferences.ShowSessionRefs = !_preferences.ShowSessionRefs);
        ShowRemoteBranchesMenuItem.Click += (_, _) => SetGridViewOptions(_preferences.GridViewOptions with
        {
            ShowRemoteBranches = !_preferences.GridViewOptions.ShowRemoteBranches,
        });
        ShowTagsMenuItem.Click += (_, _) => SetGridViewOptions(_preferences.GridViewOptions with
        {
            ShowTags = !_preferences.GridViewOptions.ShowTags,
        });
        ShowCommitBodyMenuItem.Click += (_, _) => SetGridViewOptions(_preferences.GridViewOptions with
        {
            ShowCommitBody = !_preferences.GridViewOptions.ShowCommitBody,
        });
        GridShowAuthorDateMenuItem.Click += (_, _) =>
            ChangePreference(() => _preferences.ShowAuthorDate = !_preferences.ShowAuthorDate);
        ShowRelativeDateMenuItem.Click += (_, _) =>
        {
            _preferences.RelativeDate = !_preferences.RelativeDate;
            ShowGridColumns();
            ReloadGrid();
        };
        ShowGraphColumnMenuItem.Click += (_, _) => SetGridColumns(_preferences.GridColumns with
        {
            Graph = !_preferences.GridColumns.Graph,
        });
        ShowAvatarColumnMenuItem.Click += (_, _) =>
        {
            _preferences.ShowAuthorAvatarColumn = !_preferences.ShowAuthorAvatarColumn;
            ShowGridColumns();
        };
        ShowAuthorNameColumnMenuItem.Click += (_, _) => SetGridColumns(_preferences.GridColumns with
        {
            AuthorName = !_preferences.GridColumns.AuthorName,
        });
        ShowDateColumnMenuItem.Click += (_, _) => SetGridColumns(_preferences.GridColumns with
        {
            Date = !_preferences.GridColumns.Date,
        });
        ShowObjectIdColumnMenuItem.Click += (_, _) => SetGridColumns(_preferences.GridColumns with
        {
            ObjectId = !_preferences.GridColumns.ObjectId,
        });

        // Upstream's ToggleAuthorDateSort and ToggleTopoOrder: each turns its order on, or back to git's default.
        AuthorDateSortMenuItem.Click += (_, _) => SetRevisionSortOrder(RevisionSortOrder.AuthorDate);
        TopoOrderMenuItem.Click += (_, _) => SetRevisionSortOrder(RevisionSortOrder.Topology);
        ViewMenuItem.SubmenuOpened += (_, _) => ShowViewMenuChecks();
        ShowViewMenuChecks();
    }

    private void ShowViewMenuChecks()
    {
        RevisionFilter filter = _commits.Filter;
        ShowAllBranchesMenuItem.IsChecked = !filter.CurrentBranchOnly && !filter.ShowFilteredBranches;
        ShowCurrentBranchOnlyMenuItem.IsChecked = filter.CurrentBranchOnly;
        ShowFilteredBranchesMenuItem.IsChecked = !filter.CurrentBranchOnly && filter.ShowFilteredBranches;
        ShowReflogReferencesMenuItem.IsChecked = filter.ShowReflog;
        DrawNonRelativesGrayMenuItem.IsChecked = _preferences.RevisionGraphDrawNonRelativesGray;
        ShowArtificialCommitsMenuItem.IsChecked = _preferences.RevisionGraphShowArtificialCommits;
        ShowStashesMenuItem.IsChecked = _preferences.ShowStashes;
        ShowSessionCheckpointsMenuItem.IsChecked = _preferences.ShowSessionRefs;
        GridViewOptions options = _preferences.GridViewOptions;
        ShowRemoteBranchesMenuItem.IsChecked = options.ShowRemoteBranches;
        ShowTagsMenuItem.IsChecked = options.ShowTags;
        ShowCommitBodyMenuItem.IsChecked = options.ShowCommitBody;
        GridShowAuthorDateMenuItem.IsChecked = _preferences.ShowAuthorDate;
        ShowRelativeDateMenuItem.IsChecked = _preferences.RelativeDate;
        GridColumns columns = _preferences.GridColumns;
        ShowGraphColumnMenuItem.IsChecked = columns.Graph;
        ShowAvatarColumnMenuItem.IsChecked = _preferences.ShowAuthorAvatarColumn;
        ShowAuthorNameColumnMenuItem.IsChecked = columns.AuthorName;
        ShowDateColumnMenuItem.IsChecked = columns.Date;
        ShowObjectIdColumnMenuItem.IsChecked = columns.ObjectId;
        AuthorDateSortMenuItem.IsChecked = _preferences.RevisionSortOrder == RevisionSortOrder.AuthorDate;
        TopoOrderMenuItem.IsChecked = _preferences.RevisionSortOrder == RevisionSortOrder.Topology;
    }

    private void ChangePreference(Action change)
    {
        change();
        ShowViewMenuChecks();
        ReloadGrid();
    }

    private void SetGridViewOptions(GridViewOptions options)
    {
        _preferences.GridViewOptions = options;
        ShowGridColumns();
        ShowViewMenuChecks();
    }

    private void SetGridColumns(GridColumns columns)
    {
        _preferences.GridColumns = columns;
        ShowGridColumns();
        ShowViewMenuChecks();
    }

    private void SetRevisionSortOrder(RevisionSortOrder order)
    {
        _preferences.RevisionSortOrder = _preferences.RevisionSortOrder == order ? RevisionSortOrder.GitDefault : order;
        ShowViewMenuChecks();
        ReloadGrid();
    }

    // The grid reads its rows again with the changed settings, once it shows.
    private void ReloadGrid()
    {
        if (HistoryList.IsVisible)
        {
            Run(() => ApplyGridFilterAsync(_commits.Filter));
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
