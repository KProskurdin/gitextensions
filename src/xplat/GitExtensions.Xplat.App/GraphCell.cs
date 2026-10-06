using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using GitExtensions.Xplat.Core.CommitHistory;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Draws one row of the revision graph: the lanes that pass through, the commit's node, and the lines to its parents.
///  Each lane is drawn in its own color from the theme's lane palette.
/// </summary>
public sealed class GraphCell : Control
{
    private const double LaneWidth = 12;
    private const double EdgeMargin = 8;
    private const double NodeRadius = 4;
    private const double LineThickness = 2;

    public static readonly StyledProperty<GraphRow?> GraphProperty =
        AvaloniaProperty.Register<GraphCell, GraphRow?>(nameof(Graph));

    static GraphCell()
    {
        AffectsMeasure<GraphCell>(GraphProperty);
        AffectsRender<GraphCell>(GraphProperty);
    }

    public GraphRow? Graph
    {
        get => GetValue(GraphProperty);
        set => SetValue(GraphProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(LaneX(Graph?.Width ?? 0) + EdgeMargin, 0);

    public override void Render(DrawingContext context)
    {
        if (Graph is not { } graph)
        {
            return;
        }

        IReadOnlyList<IBrush> palette = ThemeBrushes.Current.Lanes;
        double top = 0;
        double middle = Bounds.Height / 2;
        double bottom = Bounds.Height;

        for (int i = 0; i < graph.ActiveColumns.Count; i++)
        {
            int lane = graph.ActiveColumns[i];
            IBrush brush = palette[graph.ActiveColorAt(i) % palette.Count];
            if (lane == graph.Column)
            {
                DrawLine(context, brush, lane, top, lane, middle);
            }
            else
            {
                DrawLine(context, brush, lane, top, lane, bottom);
            }
        }

        for (int i = 0; i < graph.ParentColumns.Count; i++)
        {
            DrawLine(context, palette[graph.ParentColorAt(i) % palette.Count], graph.Column, middle, graph.ParentColumns[i], bottom);
        }

        context.DrawEllipse(palette[graph.Color % palette.Count], null, new Point(LaneX(graph.Column), middle), NodeRadius, NodeRadius);
    }

    private static double LaneX(int lane) => EdgeMargin + (lane * LaneWidth) + (LaneWidth / 2);

    private static void DrawLine(DrawingContext context, IBrush brush, int fromLane, double fromY, int toLane, double toY)
        => context.DrawLine(new Pen(brush, LineThickness), new Point(LaneX(fromLane), fromY), new Point(LaneX(toLane), toY));
}
