using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using GitExtensions.Xplat.Core.CommitHistory;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Draws one row of the revision graph: the shapes upstream's renderer draws for it (<see cref="GraphPainter"/>), in the
///  theme's lane colors. Every row of a graph is as wide as its widest row, so the columns after it line up.
/// </summary>
public sealed class GraphCell : Control
{
    // Upstream draws the checked-out commit's outline 2 pixels wide, 1 pixel outside the node.
    private const double OutlineThickness = 2;

    public static readonly StyledProperty<GraphRowRef?> GraphProperty =
        AvaloniaProperty.Register<GraphCell, GraphRowRef?>(nameof(Graph));

    static GraphCell()
    {
        AffectsMeasure<GraphCell>(GraphProperty);
        AffectsRender<GraphCell>(GraphProperty);
    }

    public GraphRowRef? Graph
    {
        get => GetValue(GraphProperty);
        set => SetValue(GraphProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
        => new((Graph?.Graph.LaneCount ?? 0) * GraphPainter.LaneWidth, 0);

    public override void Render(DrawingContext context)
    {
        if (Graph is not { } row || Bounds.Height <= 0)
        {
            return;
        }

        IReadOnlyList<GraphShape> shapes =
            GraphPainter.Paint(row.Graph, row.Index, (int)Bounds.Height, row.HasRefs, row.IsHead);

        // Upstream's segments start and end in the rows above and below; the cell shows its own part.
        using DrawingContext.PushedState clip = context.PushClip(new Rect(Bounds.Size));
        foreach (GraphShape shape in shapes)
        {
            IBrush brush = BrushFor(shape.Color);
            switch (shape)
            {
                case GraphLine line:
                    context.DrawLine(new Pen(brush, GraphPainter.LaneLineWidth), ToPoint(line.From), ToPoint(line.To));
                    break;
                case GraphBezier curve:
                    StreamGeometry geometry = new();
                    using (StreamGeometryContext path = geometry.Open())
                    {
                        path.BeginFigure(ToPoint(curve.Start), isFilled: false);
                        path.CubicBezierTo(ToPoint(curve.Control1), ToPoint(curve.Control2), ToPoint(curve.End));
                        path.EndFigure(isClosed: false);
                    }

                    context.DrawGeometry(null, new Pen(brush, GraphPainter.LaneLineWidth), geometry);
                    break;
                case GraphNode node:
                    DrawNode(context, node, brush);
                    break;
            }
        }
    }

    private static void DrawNode(DrawingContext context, GraphNode node, IBrush brush)
    {
        Rect bounds = new(node.Bounds.X, node.Bounds.Y, node.Bounds.Width, node.Bounds.Height);
        Pen? outline = node.Outline ? new Pen(ThemeBrushes.Current.Context, OutlineThickness) : null;
        Rect outlineBounds = bounds.Inflate(1);
        if (node.Square)
        {
            context.DrawRectangle(brush, null, bounds);
            if (outline is not null)
            {
                context.DrawRectangle(null, outline, outlineBounds);
            }
        }
        else
        {
            context.DrawEllipse(brush, null, bounds);
            if (outline is not null)
            {
                context.DrawEllipse(null, outline, outlineBounds);
            }
        }
    }

    private static IBrush BrushFor(int color)
    {
        IReadOnlyList<IBrush> lanes = ThemeBrushes.Current.Lanes;
        return color == GraphPainter.NonRelativeColor
            ? ThemeBrushes.Current.NonRelativeLane
            : lanes[color % lanes.Count];
    }

    private static Point ToPoint(System.Drawing.PointF point) => new(point.X, point.Y);
}
