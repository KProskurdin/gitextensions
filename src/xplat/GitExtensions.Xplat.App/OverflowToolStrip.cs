using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>ToolStrip</c> overflow (items' <c>Overflow</c> is <c>AsNeeded</c>): the items lie in a row; those that do not
///  fit, from the end, are left out and an overflow button ("»") at the strip's end shows them in a drop-down. As upstream's
///  overflow drop-down hosts the items themselves, the left-out items move into the drop-down while it is open, so their
///  clicks, menus and state work there, and move back when it closes.
/// </summary>
public sealed class OverflowToolStrip : Panel
{
    // Upstream's ToolStripOverflowButton shows a chevron; its drop-down closes after an item's click.
    private const string OverflowText = "»";

    private readonly ToolButton _overflowButton;
    private readonly Popup _popup;
    private readonly StackPanel _overflowItems;

    // The items in the drop-down, with their places in the strip to go back to.
    private readonly List<(Control Item, int Index)> _moved = [];

    // How many of the visible items the row shows; the others are in the overflow.
    private int _shownCount = int.MaxValue;

    // The overflow button shows while items are left out (it is arranged out of sight otherwise).
    private bool _overflowShown;
    private TopLevel? _topLevel;

    public OverflowToolStrip()
    {
        ClipToBounds = true;
        _overflowButton = new ToolButton { Name = "OverflowButton", Text = OverflowText };
        _overflowButton.Click += (_, _) => ToggleOverflow();
        _overflowItems = new StackPanel { Classes = { "toolStripOverflow" } };
        _overflowItems.AddHandler(Button.ClickEvent, (_, _) => CloseOverflowAfterClick(), RoutingStrategies.Bubble,
            handledEventsToo: true);
        _popup = new Popup
        {
            Child = new Border { Classes = { "toolStripOverflow" }, Child = _overflowItems },
            PlacementTarget = _overflowButton,
            Placement = PlacementMode.BottomEdgeAlignedRight,
            IsLightDismissEnabled = false,
        };
        _popup.Closed += (_, _) => MoveItemsBack();
        VisualChildren.Add(_overflowButton);
        LogicalChildren.Add(_overflowButton);
        VisualChildren.Add(_popup);
        LogicalChildren.Add(_popup);
    }

    /// <summary>
    ///  True while the overflow drop-down is open, for tests.
    /// </summary>
    public bool IsOverflowOpen => _popup.IsOpen;

    /// <summary>
    ///  True while items are left out of the row and the overflow button shows.
    /// </summary>
    public bool HasOverflow => _overflowShown;

    /// <summary>
    ///  The items left out of the row, in their order.
    /// </summary>
    public IReadOnlyList<Control> OverflowItems
        => _popup.IsOpen
            ? [.. _moved.Select(moved => moved.Item)]
            : [.. Children.Where(child => child.IsVisible).Skip(_shownCount)];

    /// <summary>
    ///  Opens the overflow drop-down with the items left out of the row, or closes it.
    /// </summary>
    public void ToggleOverflow()
    {
        if (_popup.IsOpen)
        {
            _popup.IsOpen = false;
            return;
        }

        List<Control> left = [.. OverflowItems];
        if (left.Count == 0)
        {
            return;
        }

        // The places are taken before any item leaves, so putting them back in this order restores the row.
        List<(Control Item, int Index)> places = [.. left.Select(item => (item, Children.IndexOf(item)))];
        foreach ((Control item, int index) in places)
        {
            Children.Remove(item);
            _moved.Add((item, index));
            _overflowItems.Children.Add(item);
            if (item is Button { Flyout: { } flyout })
            {
                flyout.Closed += OnItemFlyoutClosed;
            }
        }

        _popup.IsOpen = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        _topLevel?.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(PointerPressedEvent, OnTopLevelPointerPressed);
        _topLevel?.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
        _topLevel = null;
        _popup.IsOpen = false;
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        List<Control> items = [.. Children.Where(child => child.IsVisible)];
        double height = 0;
        double total = 0;
        foreach (Control item in items)
        {
            item.Measure(Size.Infinity);
            total += item.DesiredSize.Width;
            height = Math.Max(height, item.DesiredSize.Height);
        }

        _overflowShown = _popup.IsOpen || total > availableSize.Width;
        _overflowButton.Measure(Size.Infinity);
        if (!_overflowShown)
        {
            _shownCount = items.Count;
            return new Size(total, height);
        }

        height = Math.Max(height, _overflowButton.DesiredSize.Height);
        double room = availableSize.Width - _overflowButton.DesiredSize.Width;
        double width = 0;
        int shown = 0;
        while (shown < items.Count && width + items[shown].DesiredSize.Width <= room)
        {
            width += items[shown].DesiredSize.Width;
            shown++;
        }

        // A separator does not end the row, as upstream's layout leaves it out there.
        while (shown > 0 && items[shown - 1] is Rectangle)
        {
            shown--;
            width -= items[shown].DesiredSize.Width;
        }

        _shownCount = shown;
        return new Size(width + _overflowButton.DesiredSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        int index = 0;
        foreach (Control item in Children.Where(child => child.IsVisible))
        {
            if (index++ < _shownCount)
            {
                item.Arrange(new Rect(x, (finalSize.Height - item.DesiredSize.Height) / 2, item.DesiredSize.Width,
                    item.DesiredSize.Height));
                x += item.DesiredSize.Width;
            }
            else
            {
                // Out of sight (the strip clips its bounds) until the drop-down shows it.
                item.Arrange(new Rect(-item.DesiredSize.Width - finalSize.Width, 0, item.DesiredSize.Width,
                    item.DesiredSize.Height));
            }
        }

        Size button = _overflowButton.DesiredSize;
        _overflowButton.Arrange(_overflowShown
            ? new Rect(finalSize.Width - button.Width, (finalSize.Height - button.Height) / 2, button.Width, button.Height)
            : new Rect(finalSize.Width + button.Width, 0, button.Width, button.Height));

        return finalSize;
    }

    // Upstream's overflow drop-down closes after an item's click, but stays while that item's own menu is open.
    private void CloseOverflowAfterClick()
    {
        if (!_moved.Any(moved => moved.Item is Button { Flyout.IsOpen: true }))
        {
            _popup.IsOpen = false;
        }
    }

    private void OnItemFlyoutClosed(object? sender, EventArgs e) => _popup.IsOpen = false;

    // A press outside the drop-down, its items' menus and the overflow button closes it, as a light dismiss would; a
    // light dismiss would also close it when an item's own menu is pressed.
    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_popup.IsOpen || _moved.Any(moved => moved.Item is Button { Flyout.IsOpen: true }))
        {
            return;
        }

        // With overlay popups the drop-down's own presses come through the window too.
        if (e.Source is Visual source && (_overflowButton.IsVisualAncestorOf(source) || ReferenceEquals(source, _overflowButton)
                                          || (_popup.Child?.IsVisualAncestorOf(source) ?? false)
                                          || ReferenceEquals(source, _popup.Child)))
        {
            return;
        }

        _popup.IsOpen = false;
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (_popup.IsOpen && e.Key == Key.Escape)
        {
            _popup.IsOpen = false;
            e.Handled = true;
        }
    }

    private void MoveItemsBack()
    {
        foreach ((Control item, int index) in _moved)
        {
            if (item is Button { Flyout: { } flyout })
            {
                flyout.Closed -= OnItemFlyoutClosed;
            }

            _overflowItems.Children.Remove(item);
            Children.Insert(Math.Min(index, Children.Count), item);
        }

        _moved.Clear();
        InvalidateMeasure();
    }
}
