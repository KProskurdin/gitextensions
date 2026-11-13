using Avalonia;
using Avalonia.Threading;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitUIPluginInterfaces;
using DrawingPoint = System.Drawing.Point;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormBrowse</c> as <see cref="IBrowseRepo"/>, which plugins reach through <c>IGitUICommands.BrowseRepo</c>:
///  the grid's selected commits, the place for a quick selector, going to a ref, and opening another repository. A call from
///  another thread runs on the UI thread, as the grid is there.
/// </summary>
public partial class MainWindow : IBrowseRepo
{
    public GitRevision? GetLatestSelectedRevision()
        => OnUiThread(() => _gridSelection.Latest is { } row ? GitRevisions.Of(row) : null);

    public IReadOnlyList<GitRevision> GetSelectedRevisions()
        => OnUiThread<IReadOnlyList<GitRevision>>(() => [.. SelectedRevisions.Select(GitRevisions.Of)]);

    // Upstream's GetQuickItemSelectorLocation: below the latest selected row, at the grid's left, in screen pixels; the
    // grid's top left without a selected row shown.
    public DrawingPoint GetQuickItemSelectorLocation()
        => OnUiThread(() =>
        {
            PixelPoint point = LatestSelectedItem is { } item && CommitList.ContainerFromItem(item) is { } row
                ? row.PointToScreen(new Point(0, row.Bounds.Height))
                : CommitList.PointToScreen(default);
            return new DrawingPoint(point.X, point.Y);
        });

    public void GoToRef(string refName, bool showNoRevisionMsg, bool toggleSelection = false)
        => Dispatcher.UIThread.Post(() => Run(() => GoToRevisionAsync(refName, showNoRevisionMsg, toggleSelection)));

    // Upstream selects selectedId (and firstId, the first commit of a diff) when the grid has read the repository.
    public void SetWorkingDir(string? path, ObjectId selectedId = default, ObjectId firstId = default)
        => Dispatcher.UIThread.Post(() => Run(async () =>
        {
            await OpenRepositoryAsync(path?.Trim() ?? "");
            if (!firstId.IsZero && !selectedId.IsZero)
            {
                await GoToRevisionAsync(firstId.ToString());
                await GoToRevisionAsync(selectedId.ToString(), toggleSelection: true);
            }
            else if (!selectedId.IsZero)
            {
                await GoToRevisionAsync(selectedId.ToString());
            }
        }));

    private static T OnUiThread<T>(Func<T> read)
        => Dispatcher.UIThread.CheckAccess() ? read() : Dispatcher.UIThread.Invoke(read);
}
