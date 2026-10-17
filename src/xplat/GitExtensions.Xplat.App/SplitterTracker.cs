using Avalonia.Controls;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>SplitterManager</c> for the new shell's grids: each splitter is a grid of three columns or rows (panel
///  1, the splitter, panel 2) stored under upstream's name, restored the first time its grid is laid out (some start
///  hidden) and saved when the window closes. Positions are stored in pixels with the DPI, as upstream stores them, so
///  both apps share them.
/// </summary>
public sealed class SplitterTracker
{
    // WinForms' SplitContainer defaults, which upstream's designers keep unless they set their own.
    private const int DefaultSplitterWidth = 4;
    private const int DefaultPanelMinSize = 25;

    private const double DefaultDpi = 96;

    private readonly ISplitterStore _store;
    private readonly string _form;
    private readonly List<Splitter> _splitters = [];

    /// <param name="form">Upstream's settings path of the form, e.g. <c>FormBrowse</c>.</param>
    public SplitterTracker(Window window, ISplitterStore store, string form)
    {
        _store = store;
        _form = form;
        window.Closing += (_, _) => Save();
    }

    /// <summary>
    ///  Tracks <paramref name="grid"/>'s splitter as upstream's <paramref name="name"/>.
    /// </summary>
    /// <param name="hidden">For a panel the window hides by shrinking it to nothing (the left panel, the tabs below the
    ///  grid): whether it is hidden, and the distance it keeps meanwhile, read and set in device-independent pixels.</param>
    /// <param name="panel1Collapsed">For a panel 1 upstream stores as collapsed (the left panel): whether it is, and how
    ///  to collapse or show it.</param>
    public void Add(string name, Grid grid, bool rows, SplitterFixedPanel fixedPanel,
        int splitterWidth = DefaultSplitterWidth, int panel1MinSize = DefaultPanelMinSize,
        int panel2MinSize = DefaultPanelMinSize, HiddenPanel? hidden = null,
        (Func<bool> Get, Action<bool> Set)? panel1Collapsed = null)
    {
        Splitter splitter = new(name, grid, rows, fixedPanel, splitterWidth, panel1MinSize, panel2MinSize, hidden,
            panel1Collapsed);
        _splitters.Add(splitter);
        grid.SizeChanged += (_, _) =>
        {
            if (!splitter.Restored && Length(splitter) > 0)
            {
                splitter.Restored = true;
                Restore(splitter);
            }
        };
    }

    // Upstream's SaveSplitters, for the splitters that were laid out: the others still hold the stored values.
    private void Save()
    {
        foreach (Splitter splitter in _splitters.Where(splitter => splitter.Restored))
        {
            double scale = Scale(splitter.Grid);
            double distance = splitter.Hidden is { } hidden && hidden.IsHidden()
                ? hidden.Distance(Length(splitter))
                : Distance(splitter);
            _store.Save(_form, splitter.Name, Pixels(Length(splitter), scale), Pixels(DefaultDpi, scale),
                Pixels(distance, scale), splitter.Panel1Collapsed?.Get() ?? false, panel2Collapsed: false);
        }
    }

    // Upstream's RestoreFromSettings: the distance for the size and DPI now, then the collapsed state.
    private void Restore(Splitter splitter)
    {
        SplitterSettings stored = _store.Load(_form, splitter.Name);
        double scale = Scale(splitter.Grid);
        if (SplitterLayout.RestoredDistance(stored, Pixels(Length(splitter), scale), Pixels(DefaultDpi, scale),
                splitter.FixedPanel, splitter.SplitterWidth, splitter.Panel1MinSize, splitter.Panel2MinSize) is
            { } pixels)
        {
            SetDistance(splitter, pixels / scale);
        }

        if (splitter.Panel1Collapsed is { } collapsed && stored.Panel1Collapsed is { } wasCollapsed
                                                      && collapsed.Get() != wasCollapsed)
        {
            collapsed.Set(wasCollapsed);
        }
    }

    private static double Length(Splitter splitter)
        => splitter.Rows ? splitter.Grid.Bounds.Height : splitter.Grid.Bounds.Width;

    private static double Distance(Splitter splitter)
        => splitter.Rows
            ? splitter.Grid.RowDefinitions[0].ActualHeight
            : splitter.Grid.ColumnDefinitions[0].ActualWidth;

    // The panel the grid sizes absolutely takes the distance (panel 1) or the rest (panel 2); with both in proportion,
    // the two shares are set. A hidden panel keeps it for when it is shown.
    private static void SetDistance(Splitter splitter, double distance)
    {
        if (splitter.Hidden is { } hidden && hidden.IsHidden())
        {
            hidden.SetDistance(Length(splitter), distance);
            return;
        }

        double between = splitter.Rows
            ? splitter.Grid.RowDefinitions[1].ActualHeight
            : splitter.Grid.ColumnDefinitions[1].ActualWidth;
        double rest = Math.Max(0, Length(splitter) - distance - between);
        (GridLength first, GridLength second) = Definition(splitter, 0).IsAbsolute
            ? (new GridLength(distance), Definition(splitter, 2))
            : Definition(splitter, 2).IsAbsolute
                ? (Definition(splitter, 0), new GridLength(rest))
                : (new GridLength(distance, GridUnitType.Star), new GridLength(rest, GridUnitType.Star));
        if (splitter.Rows)
        {
            splitter.Grid.RowDefinitions[0].Height = first;
            splitter.Grid.RowDefinitions[2].Height = second;
        }
        else
        {
            splitter.Grid.ColumnDefinitions[0].Width = first;
            splitter.Grid.ColumnDefinitions[2].Width = second;
        }
    }

    private static GridLength Definition(Splitter splitter, int index)
        => splitter.Rows ? splitter.Grid.RowDefinitions[index].Height : splitter.Grid.ColumnDefinitions[index].Width;

    private static double Scale(Control control) => TopLevel.GetTopLevel(control)?.RenderScaling ?? 1;

    private static int Pixels(double value, double scale) => (int)Math.Round(value * scale);

    private sealed record Splitter(
        string Name,
        Grid Grid,
        bool Rows,
        SplitterFixedPanel FixedPanel,
        int SplitterWidth,
        int Panel1MinSize,
        int Panel2MinSize,
        HiddenPanel? Hidden,
        (Func<bool> Get, Action<bool> Set)? Panel1Collapsed)
    {
        public bool Restored { get; set; }
    }
}

/// <summary>
///  A panel the window hides by shrinking it to nothing: whether it is hidden, and the splitter distance it keeps for
///  when it is shown, read and set from the grid's length.
/// </summary>
public sealed record HiddenPanel(
    Func<bool> IsHidden,
    Func<double, double> Distance,
    Action<double, double> SetDistance);
