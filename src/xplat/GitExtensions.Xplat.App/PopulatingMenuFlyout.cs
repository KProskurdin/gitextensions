using Avalonia.Controls;
using Microsoft.VisualStudio.Threading;

namespace GitExtensions.Xplat.App;

/// <summary>
///  A menu flyout whose items are built each time it opens, as upstream's <c>DropDownOpening</c> handlers build theirs. An
///  Avalonia 12 <see cref="MenuFlyout"/> whose items change during its <c>Opening</c> event shows as a thin empty line (its
///  presenter keeps the size it had), so the items are built before the flyout starts to open: <see cref="Populating"/>
///  for items at hand, <see cref="PopulateAsync"/> for items that need a read first, after which the flyout opens.
/// </summary>
public class PopulatingMenuFlyout : MenuFlyout
{
    // Set while the flyout opens after its asynchronous build, which is not run again for that opening.
    private bool _populated;

    /// <summary>
    ///  Raised before the flyout opens: the handler sets the items.
    /// </summary>
    public event EventHandler? Populating;

    /// <summary>
    ///  Builds the items before the flyout opens, when they need a read first; the flyout opens when it is done.
    /// </summary>
    public Func<Task>? PopulateAsync { get; set; }

    /// <summary>
    ///  Reports a failed asynchronous build; the flyout does not open then.
    /// </summary>
    public Action<Exception>? ReportError { get; set; }

    /// <summary>
    ///  Completes when the last asynchronous build is done, for tests.
    /// </summary>
    public Task Populated { get; private set; } = Task.CompletedTask;

    protected override bool ShowAtCore(Control placementTarget, bool showAtPointer = false)
    {
        if (_populated)
        {
            _populated = false;
            return base.ShowAtCore(placementTarget, showAtPointer);
        }

        Populating?.Invoke(this, EventArgs.Empty);
        if (PopulateAsync is not { } populate)
        {
            return base.ShowAtCore(placementTarget, showAtPointer);
        }

        JoinableTask build = GitUI.ThreadHelper.JoinableTaskFactory.RunAsync(populate);
        if (build.IsCompleted)
        {
            return base.ShowAtCore(placementTarget, showAtPointer);
        }

        Populated = OpenAfterAsync(build, placementTarget, showAtPointer);
        return false;
    }

    // Opens the flyout once its items are built, unless it was opened meanwhile or its target left the window.
    private async Task OpenAfterAsync(JoinableTask build, Control placementTarget, bool showAtPointer)
    {
        try
        {
            await build;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            ReportError?.Invoke(ex);
            return;
        }

        if (IsOpen || TopLevel.GetTopLevel(placementTarget) is null)
        {
            return;
        }

        _populated = true;
        try
        {
            ShowAt(placementTarget, showAtPointer);
        }
        finally
        {
            _populated = false;
        }
    }
}
