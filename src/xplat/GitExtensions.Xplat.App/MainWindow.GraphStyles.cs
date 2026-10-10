using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using GitExtensions.Xplat.Core.CommitHistory;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's revision graph draw styles in the grid (<c>RevisionGridControl</c>): "Draw non relatives gray", "Highlight
///  selected branch (until refresh)" with its hotkey and Alt+click, and the hover highlight of a ref label's ancestry.
/// </summary>
public partial class MainWindow
{
    private CommitListItem? _hoveredLabelRow;

    private void WireGraphStyles()
    {
        _commits.DrawNonRelativesGray = _preferences.RevisionGraphDrawNonRelativesGray;
        _commits.MaxCommits = _preferences.MaxRevisionGraphCommits;
        DrawNonRelativesGrayMenuItem.IsChecked = _commits.DrawNonRelativesGray;
        DrawNonRelativesGrayMenuItem.Click += (_, _) =>
        {
            _preferences.RevisionGraphDrawNonRelativesGray = !_preferences.RevisionGraphDrawNonRelativesGray;
            _commits.DrawNonRelativesGray = _preferences.RevisionGraphDrawNonRelativesGray;
            DrawNonRelativesGrayMenuItem.IsChecked = _commits.DrawNonRelativesGray;
        };
        HighlightSelectedBranchMenuItem.Click += (_, _) => HighlightSelectedBranch();

        // Upstream's "Show artificial commits": the working directory and index rows above HEAD.
        ShowArtificialCommitsMenuItem.IsChecked = _preferences.RevisionGraphShowArtificialCommits;
        ShowArtificialCommitsMenuItem.Click += (_, _) =>
        {
            _preferences.RevisionGraphShowArtificialCommits = !_preferences.RevisionGraphShowArtificialCommits;
            ShowArtificialCommitsMenuItem.IsChecked = _preferences.RevisionGraphShowArtificialCommits;
            RefreshRepository();
        };
        _commits.GraphAppearanceChanged += (_, _) => RedrawGraph();

        // Upstream: Alt+click on a commit highlights its branch.
        CommitList.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)
                && (e.Source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is
                CommitListItem item)
            {
                _commits.HighlightBranch(item.Row);
            }
        }, handledEventsToo: true);

        // Upstream's hover highlight: while the mouse is on a row's ref labels, that commit's ancestry is drawn in color.
        CommitList.AddHandler(PointerMovedEvent, (_, e) => SetHoveredLabels(
            (e.Source as Control)?.FindAncestorOfType<ItemsControl>(includeSelf: true) is { } labels
            && labels.Classes.Contains("refLabels")
                ? labels.DataContext as CommitListItem
                : null), handledEventsToo: true);
        CommitList.PointerExited += (_, _) => SetHoveredLabels(null);

        // After a scroll or a new page has been laid out.
        CommitList.LayoutUpdated += (_, _) => FitGraphToVisibleRows();
    }

    // Upstream's "Show number of changed files for artificial commits": the working directory's unstaged and untracked
    // files, the index's staged ones.
    private void ShowArtificialChangeCounts()
    {
        if (!_preferences.ShowGitStatusForArtificialCommits)
        {
            _commits.SetArtificialChangeCounts(null, null);
            return;
        }

        _commits.SetArtificialChangeCounts(
            _repository.Changes.Count(change => !change.Staged),
            _repository.Changes.Count(change => change.Staged));
    }

    private void HighlightSelectedBranch()
    {
        if (_commits.Selected is { } row)
        {
            _commits.HighlightBranch(row);
        }
    }

    private void SetHoveredLabels(CommitListItem? item)
    {
        if (ReferenceEquals(item, _hoveredLabelRow))
        {
            return;
        }

        _hoveredLabelRow = item;
        _commits.SetHoverHighlight(item);
    }

    // Upstream's graph column is as wide as the most lanes of the rows on screen, so it narrows where the history is simple.
    private void FitGraphToVisibleRows()
    {
        Rect viewport = new(CommitList.Bounds.Size);
        List<CommitListItem> visible =
        [
            .. CommitList.GetRealizedContainers()
                .Where(container => container.IsVisible
                                    && container.TranslatePoint(new Point(0, 0), CommitList) is { } top
                                    && new Rect(top, container.Bounds.Size).Intersects(viewport))
                .Select(container => container.DataContext)
                .OfType<CommitListItem>()
        ];
        if (visible.Count == 0)
        {
            return;
        }

        CommitGraph graph = visible[0].Graph.Graph;
        int lanes = visible.Max(item => graph.LaneCountAt(item.Graph.Index));
        if (lanes == graph.ShownLaneCount)
        {
            return;
        }

        graph.ShownLaneCount = lanes;
        foreach (GraphCell cell in CommitList.GetVisualDescendants().OfType<GraphCell>())
        {
            cell.InvalidateMeasure();
        }
    }

    // The rows keep their items; only their graph cells draw again.
    private void RedrawGraph()
    {
        foreach (GraphCell cell in CommitList.GetVisualDescendants().OfType<GraphCell>())
        {
            cell.InvalidateVisual();
        }
    }
}
