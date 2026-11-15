using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace GitExtensions.Xplat.App;

// Upstream FileViewer's continuous scroll (ContinuousScrollEventManager): a wheel turn past the top or the bottom of the
// diff, with Alt held or upstream's AutomaticContinuousScroll on, asks the window for the previous or next file, at most
// once per AutomaticContinuousScrollDelay (the view model's rule; the wheel and the scrolling are the view's).
public partial class DiffView
{
    private bool? _pendingScrollToBottom;

    /// <summary>
    ///  Raised by a wheel turn toward the top while the diff is at its top.
    /// </summary>
    public event EventHandler? TopScrollReached;

    /// <summary>
    ///  Raised by a wheel turn toward the bottom while the diff is at its bottom.
    /// </summary>
    public event EventHandler? BottomScrollReached;

    /// <summary>
    ///  Upstream's <c>EnableAutomaticContinuousScroll</c>: whether the menu has the setting's item (not in the browse
    ///  window's tabs, whose lists do not move with the viewer).
    /// </summary>
    public bool EnableAutomaticContinuousScroll
    {
        get => ViewModel.EnableAutomaticContinuousScroll;
        set => ViewModel.EnableAutomaticContinuousScroll = value;
    }

    /// <summary>
    ///  Upstream's <c>ScrollToTop</c>: the diff shown next starts at its top.
    /// </summary>
    public void ScrollToTop() => ScrollToEnd(bottom: false);

    /// <summary>
    ///  Upstream's <c>ScrollToBottom</c>: the diff shown next starts at its bottom.
    /// </summary>
    public void ScrollToBottom() => ScrollToEnd(bottom: true);

    private void WireContinuousScroll()
    {
        // Before the list scrolls, as upstream reads the scroll bar before the wheel moves it.
        DiffList.AddHandler(PointerWheelChangedEvent, (_, e) =>
        {
            if (DiffList.Scroll is not { } scroll || e.Delta.Y == 0)
            {
                return;
            }

            bool atTop = scroll.Offset.Y <= 0;
            bool atBottom = scroll.Offset.Y + scroll.Viewport.Height >= scroll.Extent.Height - 1;
            if (e.Delta.Y > 0 && atTop)
            {
                RaiseScrollReached(TopScrollReached, e.KeyModifiers);
            }
            else if (e.Delta.Y < 0 && atBottom)
            {
                RaiseScrollReached(BottomScrollReached, e.KeyModifiers);
            }
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void RaiseScrollReached(EventHandler? handler, KeyModifiers modifiers)
    {
        if (handler is not null && ViewModel.TakeScrollReached(altHeld: modifiers == KeyModifiers.Alt))
        {
            handler(this, EventArgs.Empty);
        }
    }

    private void ScrollToEnd(bool bottom)
    {
        _pendingScrollToBottom = bottom;
        ApplyPendingScroll();
    }

    // After the next lines are shown and laid out.
    private void ApplyPendingScroll()
    {
        if (_pendingScrollToBottom is not { } bottom || Lines.Count == 0)
        {
            return;
        }

        _pendingScrollToBottom = null;
        Dispatcher.UIThread.Post(() => DiffList.ScrollIntoView(bottom ? Lines.Count - 1 : 0), DispatcherPriority.Background);
    }
}
