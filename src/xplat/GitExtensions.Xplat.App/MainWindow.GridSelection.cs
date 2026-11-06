using Avalonia.Controls;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's grid selection (<c>MultiSelect</c>): Ctrl and Shift select several commits; the commit selected last is the
///  one the commit info, the menu and the hotkeys act on (<c>LatestSelectedRevision</c>), and the selection's diff compares
///  the earliest selected with it (<c>GetFirstAndSelected</c>). Rules in <see cref="GridSelection"/>.
/// </summary>
public partial class MainWindow
{
    private readonly GridSelection _gridSelection = new();

    // The repository whose rows the grid shows, and the count of row replacements, for ShowGridRows.
    private string? _gridRowsRepository;
    private int _gridRowsVersion;

    /// <summary>
    ///  Upstream's <c>GetSelectedRevisions</c>: the selected commits, the latest selected first.
    /// </summary>
    public IReadOnlyList<CommitRow> SelectedRevisions => _gridSelection.Revisions;

    // The grid item of upstream's LatestSelectedRevision.
    private CommitListItem? LatestSelectedItem
        => _gridSelection.Latest is { } latest
            ? CommitList.SelectedItems?.OfType<CommitListItem>().FirstOrDefault(item => item.Row.Hash == latest.Hash)
            : null;

    private int LatestSelectedIndex => LatestSelectedItem is { } item ? CommitList.Items.IndexOf(item) : -1;

    // Upstream's GetSelectedRevisions(SortDirection): the selected commits in the grid's order, or from the bottom up.
    private IReadOnlyList<CommitRow> SelectedRevisionsInGridOrder(bool descending)
    {
        List<string> order = [.. _commits.VisibleRows.Select(item => item.Row.Hash)];
        IEnumerable<CommitRow> rows = SelectedRevisions.OrderBy(row => order.IndexOf(row.Hash));
        return [.. descending ? rows.Reverse() : rows];
    }

    // A refresh, a new page, a filter or the quick filter replaces the grid's rows. As upstream's ToBeSelectedObjectIds,
    // the selected commits stay selected; with none, HEAD is; a commit no longer shown hands over to its nearest shown
    // ancestor (GridSelection.ToBeSelectedAsync). Another repository starts with HEAD.
    private void ShowGridRows()
    {
        string? path = RepositoryPath;
        IReadOnlyList<string> previous = string.Equals(path, _gridRowsRepository, StringComparison.Ordinal)
            ? [.. SelectedRevisions.Reverse().Select(row => row.Hash)]
            : [];
        _gridRowsRepository = path;
        IReadOnlyList<CommitListItem> items = _commits.VisibleRows;
        CommitList.ItemsSource = items;
        int version = ++_gridRowsVersion;
        if (path is null)
        {
            return;
        }

        Run(async () =>
        {
            IReadOnlyList<string> select = await GridSelection.ToBeSelectedAsync(previous,
                [.. items.Select(item => item.Row)],
                hash => Task.Run(() => GridSelection.Ancestors(GitModules.Open(path), hash)));
            if (version != _gridRowsVersion || select.Count == 0)
            {
                return;
            }

            List<CommitListItem> rows =
            [
                .. select.Select(hash => items.FirstOrDefault(item =>
                        string.Equals(item.Row.Hash, hash, StringComparison.OrdinalIgnoreCase)))
                    .OfType<CommitListItem>()
            ];
            if (rows.Count == 1)
            {
                CommitList.SelectedItem = rows[0];
            }
            else
            {
                // As the file lists: an equal row Avalonia kept selected is not added twice.
                foreach (CommitListItem row in rows)
                {
                    if (CommitList.SelectedItems is { } selectedItems && !selectedItems.Contains(row))
                    {
                        selectedItems.Add(row);
                    }
                }
            }

            if (rows.Count > 0)
            {
                CommitList.ScrollIntoView(rows[^1]);
            }
        });
    }

    // Called first on each selection change, so the other handlers see the new order.
    private void TrackGridSelection(SelectionChangedEventArgs e)
        => _gridSelection.Update(
            [.. (CommitList.SelectedItems ?? Array.Empty<object>()).OfType<CommitListItem>().Select(item => item.Row)],
            [.. e.AddedItems.OfType<CommitListItem>().Select(item => item.Row)]);
}
