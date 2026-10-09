using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using GitExtensions.Extensibility.Git;
using Brushes = Avalonia.Media.Brushes;
using Color = Avalonia.Media.Color;
using Control = Avalonia.Controls.Control;
using Pen = Avalonia.Media.Pen;
using Point = Avalonia.Point;
using Rect = Avalonia.Rect;
using Size = Avalonia.Size;

namespace GitExtensions.Plugins.GitImpact;

/// <summary>
///  The new shell's port of upstream's <c>ImpactControl</c>: one band per author across the weeks, each week's block as high as
///  the author's changed lines (on upstream's log scale), joined to the next week by curves; the changed lines on the blocks
///  that are tall enough, the week under each column, and the author under the pointer outlined. The data comes from
///  upstream's <see cref="ImpactLoader"/>, unchanged. Upstream draws with WinForms' Graphics; this draws the same shapes with
///  Avalonia, and scrolls in the <see cref="ScrollViewer"/> around it instead of its own scroll bar.
/// </summary>
public sealed class ImpactGraph : Control, IDisposable
{
    private const double BlockWidth = 60;
    private const double BlockHalfWidth = BlockWidth / 2;
    private const double TransitionWidth = 50;
    private const double TransitionHalfWidth = TransitionWidth / 2;

    // Upstream's Arial sizes in points, as pixels.
    private const double LinesFontSize = 10 * 96 / 72.0;
    private const double WeekFontSize = 8 * 96 / 72.0;
    private const double SelectedAuthorThickness = 2;

    private static readonly Typeface _labelFont = new("Arial");

    private readonly Lock _dataLock = new();
    private ImpactLoader? _loader;

    // <Author, <Commits, Added Lines, Deleted Lines>>
    private readonly Dictionary<string, ImpactLoader.DataPoint> _authors = [];

    // <First weekday of commit date, <Author, <Commits, Added Lines, Deleted Lines>>>
    private SortedDictionary<DateOnly, Dictionary<string, ImpactLoader.DataPoint>> _impact = [];

    // The drawing order: the author first seen last is drawn first.
    private readonly List<string> _authorStack = [];

    // A copy of the order for the UI thread, taken with the drawing.
    private List<string> _drawOrder = [];

    private readonly Dictionary<string, Geometry> _paths = [];

    // Each author's scaled blocks, for the hit test.
    private readonly Dictionary<string, List<Rect>> _blocks = [];
    private readonly Dictionary<string, IBrush> _brushes = [];
    private readonly List<(Point Point, FormattedText Text)> _lineLabels = [];
    private readonly List<(Point Point, FormattedText Text)> _weekLabels = [];
    private bool _showSubmodules;

    public ImpactGraph()
    {
        ClipToBounds = true;
    }

    /// <summary>
    ///  Raised on the UI thread when the drawing changed: new data, a new size, or another author under the pointer.
    /// </summary>
    public event EventHandler? GraphChanged;

    public string SelectedAuthor { get; private set; } = string.Empty;

    /// <summary>
    ///  The authors in drawing order.
    /// </summary>
    public IReadOnlyList<string> Authors
    {
        get
        {
            lock (_dataLock)
            {
                return [.. _authorStack];
            }
        }
    }

    /// <summary>
    ///  The number of weeks the graph has columns for.
    /// </summary>
    public int WeekCount
    {
        get
        {
            lock (_dataLock)
            {
                return _impact.Count;
            }
        }
    }

    public bool ShowSubmodules
    {
        get => _showSubmodules;
        set
        {
            _showSubmodules = value;
            Stop();
            Clear();
            UpdateData();
        }
    }

    public void Init(IGitModule module)
    {
        // As upstream: the authors as the .mailmap file names them.
        _loader = new ImpactLoader(module) { RespectMailmap = true };
        _loader.CommitLoaded += OnCommitsLoaded;
    }

    public void UpdateData()
    {
        if (_loader is not null)
        {
            _loader.ShowSubmodules = _showSubmodules;
            _loader.Execute();
        }
    }

    public void Stop() => _loader?.Stop();

    public void Dispose() => _loader?.Dispose();

    public ImpactLoader.DataPoint GetAuthorInfo(string author)
    {
        lock (_dataLock)
        {
            return _authors.TryGetValue(author, out ImpactLoader.DataPoint info) ? info : new ImpactLoader.DataPoint(0, 0, 0);
        }
    }

    public IBrush GetAuthorBrush(string author) => _brushes.GetValueOrDefault(author) ?? Brushes.Transparent;

    /// <summary>
    ///  Upstream's <c>TrySetAuthorByScreenPosition</c>: selects the topmost author drawn at <paramref name="position"/>, in
    ///  the graph's coordinates; true when the selection changed.
    /// </summary>
    public bool TrySelectAuthorAt(Point position)
    {
        for (int i = _drawOrder.Count - 1; i >= 0; i--)
        {
            string author = _drawOrder[i];
            if (_blocks.TryGetValue(author, out List<Rect>? blocks) && Contains(blocks, position))
            {
                if (SelectedAuthor == author)
                {
                    return false;
                }

                SelectedAuthor = author;
                InvalidateVisual();
                GraphChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
        }

        return false;
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(GraphWidth(), double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdatePathsAndLabels();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        TrySelectAuthorAt(e.GetPosition(this));
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Background(), new Rect(Bounds.Size));
        if (_paths.Count == 0)
        {
            return;
        }

        // The selected author is drawn last, over the others, with an outline.
        foreach (string author in _drawOrder.Where(author => author != SelectedAuthor))
        {
            DrawAuthor(author);
        }

        DrawAuthor(SelectedAuthor);
        if (_paths.TryGetValue(SelectedAuthor, out Geometry? selected))
        {
            context.DrawGeometry(null, new Pen(Foreground(), SelectedAuthorThickness), selected);
        }

        foreach ((Point point, FormattedText text) in _lineLabels.Concat(_weekLabels))
        {
            context.DrawText(text, point);
        }

        void DrawAuthor(string author)
        {
            if (_paths.TryGetValue(author, out Geometry? path))
            {
                context.DrawGeometry(_brushes.GetValueOrDefault(author), null, path);
            }
        }
    }

    // Upstream's OnImpactUpdate, called by the loader on its own threads; the drawing is updated on the UI thread.
    private void OnCommitsLoaded(IList<ImpactLoader.Commit> commits)
    {
        lock (_dataLock)
        {
            foreach (ImpactLoader.Commit commit in commits)
            {
                if (!_impact.TryGetValue(commit.Week, out Dictionary<string, ImpactLoader.DataPoint>? weekData))
                {
                    _impact.Add(commit.Week, weekData = []);
                }

                weekData[commit.Author] = weekData.TryGetValue(commit.Author, out ImpactLoader.DataPoint authorWeek)
                    ? authorWeek + commit.Data
                    : commit.Data;
                _authors[commit.Author] = _authors.TryGetValue(commit.Author, out ImpactLoader.DataPoint author)
                    ? author + commit.Data
                    : commit.Data;
                if (!_authorStack.Contains(commit.Author))
                {
                    _authorStack.Insert(0, commit.Author);
                }
            }

            // Every author gets every week between their first and last, as upstream.
            ImpactLoader.AddIntermediateEmptyWeeks(ref _impact, _authors.Keys);
        }

        Dispatcher.UIThread.Post(() =>
        {
            InvalidateMeasure();
            UpdatePathsAndLabels();
        });
    }

    private void Clear()
    {
        lock (_dataLock)
        {
            _authors.Clear();
            _impact.Clear();
            _authorStack.Clear();
        }

        SelectedAuthor = string.Empty;
        _drawOrder = [];
        _paths.Clear();
        _blocks.Clear();
        _lineLabels.Clear();
        _weekLabels.Clear();
        InvalidateMeasure();
        InvalidateVisual();
        GraphChanged?.Invoke(this, EventArgs.Empty);
    }

    private double GraphWidth()
    {
        lock (_dataLock)
        {
            return Math.Max(0, (_impact.Count * (BlockWidth + TransitionWidth)) - TransitionWidth);
        }
    }

    // Upstream's UpdatePathsAndLabels: a block per author and week, stacked by changed lines, scaled to the height.
    private void UpdatePathsAndLabels()
    {
        double maxHeight = 0;
        double x = 0;
        Dictionary<string, List<(Rect Block, int ChangedLines)>> blocksByAuthor = [];
        List<(Point Point, string Date)> weeks = [];

        lock (_dataLock)
        {
            foreach ((DateOnly week, Dictionary<string, ImpactLoader.DataPoint> dataByAuthor) in _impact)
            {
                double y = 0;
                foreach ((string author, ImpactLoader.DataPoint data) in dataByAuthor.OrderByDescending(entry => entry.Value.ChangedLines))
                {
                    int changedLines = Math.Max(1, data.ChangedLines);
                    double height = Math.Max(1, (int)Math.Round(Math.Pow(Math.Log(changedLines), 1.5) * 4));
                    if (!blocksByAuthor.TryGetValue(author, out List<(Rect, int)>? blocks))
                    {
                        blocksByAuthor.Add(author, blocks = []);
                    }

                    blocks.Add((new Rect(x, y, BlockWidth, height), data.ChangedLines));
                    if (!_brushes.ContainsKey(author))
                    {
                        // Upstream's color: the author name's hash code as RGB.
                        _brushes.Add(author, new SolidColorBrush(Color.FromUInt32((uint)author.GetHashCode() | 0xFF000000)).ToImmutable());
                    }

                    y += height + 2;
                }

                maxHeight = Math.Max(maxHeight, y);
                weeks.Add((new Point(x + BlockHalfWidth, y), week.ToShortDateString()));
                x += BlockWidth + TransitionWidth;
            }

            _drawOrder = [.. _authorStack];
        }

        double heightFactor = maxHeight > 0 ? 0.9 * Bounds.Height / maxHeight : 1.0;

        _weekLabels.Clear();
        foreach ((Point point, string date) in weeks)
        {
            FormattedText text = Text(date, WeekFontSize, Brushes.Gray);
            _weekLabels.Add((new Point(point.X - (text.Width / 2), (point.Y * heightFactor) + (text.Height / 2)), text));
        }

        _paths.Clear();
        _blocks.Clear();
        _lineLabels.Clear();
        foreach ((string author, List<(Rect Block, int ChangedLines)> blocks) in blocksByAuthor)
        {
            List<Rect> scaled = [];
            foreach ((Rect block, int changedLines) in blocks)
            {
                Rect rect = new(block.Left, (int)(block.Top * heightFactor), block.Width,
                    Math.Max(1, (int)(block.Height * heightFactor)));
                scaled.Add(rect);
                if (rect.Height > LinesFontSize * 1.5)
                {
                    FormattedText text = Text(changedLines.ToString(CultureInfo.CurrentCulture), LinesFontSize, Brushes.White);
                    _lineLabels.Add((new Point(rect.Left + BlockHalfWidth - (text.Width / 2),
                        rect.Top + (rect.Height / 2) - (text.Height / 2)), text));
                }
            }

            _paths.Add(author, AuthorPath(scaled));
            _blocks.Add(author, scaled);
        }

        InvalidateVisual();
        GraphChanged?.Invoke(this, EventArgs.Empty);
    }

    // The author's band: up the first block's left side, along the tops with a curve between weeks, down the last block's
    // right side, and back along the bottoms.
    private static StreamGeometry AuthorPath(List<Rect> blocks)
    {
        StreamGeometry geometry = new();
        using StreamGeometryContext path = geometry.Open();
        Rect first = blocks[0];
        path.BeginFigure(first.BottomLeft, isFilled: true);
        path.LineTo(first.TopLeft);
        for (int i = 0; i < blocks.Count; i++)
        {
            Rect rect = blocks[i];
            path.LineTo(rect.TopLeft);
            path.LineTo(rect.TopRight);
            if (i < blocks.Count - 1)
            {
                Rect next = blocks[i + 1];
                path.CubicBezierTo(new Point(rect.Right + TransitionHalfWidth, rect.Top),
                    new Point(rect.Right + TransitionHalfWidth, next.Top), next.TopLeft);
            }
        }

        path.LineTo(blocks[^1].BottomRight);
        for (int i = blocks.Count - 1; i >= 0; i--)
        {
            Rect rect = blocks[i];
            path.LineTo(rect.BottomRight);
            path.LineTo(rect.BottomLeft);
            if (i > 0)
            {
                Rect previous = blocks[i - 1];
                path.CubicBezierTo(new Point(rect.Left - TransitionHalfWidth, rect.Bottom),
                    new Point(rect.Left - TransitionHalfWidth, previous.Bottom), previous.BottomRight);
            }
        }

        path.EndFigure(isClosed: true);
        return geometry;
    }

    // Upstream asks the GraphicsPath whether it contains the point. This answers from the blocks and the same curves, so it
    // does not depend on the platform's geometry (the headless test platform has none): inside a block, or between two
    // weeks' blocks below the top curve and above the bottom one.
    private static bool Contains(List<Rect> blocks, Point point)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            Rect rect = blocks[i];
            if (rect.Contains(point))
            {
                return true;
            }

            if (i < blocks.Count - 1 && point.X > rect.Right && point.X < blocks[i + 1].Left)
            {
                Rect next = blocks[i + 1];
                double t = CurveParameterAt(point.X, rect.Right);
                return point.Y >= CurveY(t, rect.Top, next.Top) && point.Y <= CurveY(t, rect.Bottom, next.Bottom);
            }
        }

        return false;
    }

    // The curves between two weeks have their control points half way along the transition, so x grows with t; t is found by
    // halving the interval.
    private static double CurveParameterAt(double x, double start)
    {
        double low = 0;
        double high = 1;
        for (int step = 0; step < 30; step++)
        {
            double t = (low + high) / 2;
            if (CurveX(t, start) < x)
            {
                low = t;
            }
            else
            {
                high = t;
            }
        }

        return (low + high) / 2;
    }

    private static double CurveX(double t, double start)
        => Bezier(t, start, start + TransitionHalfWidth, start + TransitionHalfWidth, start + TransitionWidth);

    private static double CurveY(double t, double from, double to) => Bezier(t, from, from, to, to);

    private static double Bezier(double t, double p0, double p1, double p2, double p3)
    {
        double u = 1 - t;
        return (u * u * u * p0) + (3 * u * u * t * p1) + (3 * u * t * t * p2) + (t * t * t * p3);
    }

    private static FormattedText Text(string text, double size, IBrush brush)
        => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _labelFont, size, brush);

    // Upstream paints on the window color and outlines in the window text color; the theme's are used here.
    private IBrush Background()
        => this.TryFindResource("SystemControlBackgroundAltHighBrush", ActualThemeVariant, out object? brush)
           && brush is IBrush found
            ? found
            : Brushes.White;

    private IBrush Foreground()
        => this.TryFindResource("SystemControlForegroundBaseHighBrush", ActualThemeVariant, out object? brush)
           && brush is IBrush found
            ? found
            : Brushes.Black;
}
