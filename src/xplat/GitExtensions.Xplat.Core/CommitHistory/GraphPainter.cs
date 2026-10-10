using GitExtensions.Extensibility.Git;
using GitUI.UserControls.RevisionGrid.Graph;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  A shape of a graph cell. <see cref="Color"/> is the lane's color index (see <c>RevisionGraphLaneColor</c>), or
///  <see cref="GraphPainter.NonRelativeColor"/> for a commit with no lane.
/// </summary>
/// <summary>
///  Upstream's <c>RevisionGraphDrawStyle</c>.
/// </summary>
public enum GraphDrawStyle
{
    Normal,
    DrawNonRelativesGray,
    HighlightSelected,
}

public abstract record GraphShape(int Color);

public sealed record GraphLine(PointF From, PointF To, int Color) : GraphShape(Color);

public sealed record GraphBezier(PointF Start, PointF Control1, PointF Control2, PointF End, int Color) : GraphShape(Color);

/// <summary>
///  A commit's node: a square for a commit with refs, a circle otherwise, outlined when it is checked out.
/// </summary>
public sealed record GraphNode(RectangleF Bounds, bool Square, bool Outline, int Color) : GraphShape(Color);

/// <summary>
///  Upstream's revision graph drawing (<c>GraphRenderer</c> and <c>SegmentRenderer</c>), ported from WinForms' Graphics to
///  shapes the view draws: the same lanes, curves, diagonals and nodes, in device-independent pixels, with upstream's
///  draw styles (non-relatives gray, a highlighted branch, the hover highlight of a ref's ancestry).
/// </summary>
public static class GraphPainter
{
    public const int LaneLineWidth = 2;
    public const int LaneWidth = 16;
    public const int NodeDimension = 10;
    public const int NonRelativeColor = -1;

    private const int MaxLanes = RevisionGraph.MaxLanes;
    private const int NoLane = -10;

    /// <summary>
    ///  Upstream's <c>GraphRenderer.DrawItem</c> for the row at <paramref name="index"/> of <paramref name="graph"/>, in a
    ///  cell <paramref name="rowHeight"/> high.
    /// </summary>
    public static IReadOnlyList<GraphShape> Paint(CommitGraph graph, int index, int rowHeight, bool hasRefs, bool isHead)
    {
        List<GraphShape> shapes = [];
        IRevisionGraphRow? currentRow = graph.SegmentsFor(index);
        if (currentRow is null)
        {
            return shapes;
        }

        RevisionGraphConfig config = graph.Config;
        IRevisionGraphRow? previousRow = graph.SegmentsFor(index - 1);
        IRevisionGraphRow? nextRow = graph.SegmentsFor(index + 1);

        Point center = new(0, rowHeight / 2);
        Point start = new(0, center.Y - rowHeight);
        Point end = new(0, center.Y + rowHeight);

        LaneInfo? currentRowRevisionLaneInfo = null;

        GraphDrawStyle style = graph.DrawStyle;
        IReadOnlySet<ObjectId>? hovered = graph.HoverHighlighted;

        // Upstream's normal draw style skips the secondary segments of an entirely shared lane.
        bool skipSecondarySharedSegments = style == GraphDrawStyle.Normal;
        foreach (RevisionGraphSegment segment in currentRow.Segments.Reverse()
                     .OrderBy(s => s.Child.IsRelative)
                     .ThenBy(s => hovered?.Contains(s.Child.Objectid) is true || hovered?.Contains(s.Parent.Objectid) is true))
        {
            SegmentLanesInfo lanes = GetLanesInfo(segment, previousRow, currentRow, nextRow, skipSecondarySharedSegments,
                config.MergeGraphLanesHavingCommonParent, setLaneInfo: laneInfo => currentRowRevisionLaneInfo = laneInfo);
            if (!lanes.DrawFromStart && !lanes.DrawToEnd)
            {
                continue;
            }

            start.X = (int)((lanes.StartLane + 0.5) * LaneWidth);
            center.X = (int)((lanes.CenterLane + 0.5) * LaneWidth);
            end.X = (int)((lanes.EndLane + 0.5) * LaneWidth);

            SegmentRenderer renderer = new(config, shapes,
                ColorOf(segment.LaneInfo, segment.Child.IsRelative, style, hovered?.Contains(segment.Child.Objectid)),
                new Size(LaneWidth, rowHeight));
            if (config.RenderGraphWithDiagonals)
            {
                Lazy<DiagonalSegmentInfo> previousSegmentInfo = new(() => GetDiagonalSegmentInfo(
                    GetLanesInfo(segment, graph.SegmentsFor(index - 2), previousRow!, currentRow,
                        skipSecondarySharedSegments, config.MergeGraphLanesHavingCommonParent),
                    config.MergeGraphLanesHavingCommonParent));
                Lazy<DiagonalSegmentInfo> nextSegmentInfo = new(() => GetDiagonalSegmentInfo(
                    GetLanesInfo(segment, currentRow, nextRow!, graph.SegmentsFor(index + 2),
                        skipSecondarySharedSegments, config.MergeGraphLanesHavingCommonParent),
                    config.MergeGraphLanesHavingCommonParent));
                DiagonalSegmentInfo currentSegmentInfo =
                    GetDiagonalSegmentInfo(lanes, config.MergeGraphLanesHavingCommonParent);

                DrawSegmentWithDiagonals(ref renderer, start, center, end, previousSegmentInfo, currentSegmentInfo,
                    nextSegmentInfo);
            }
            else
            {
                DrawSegmentCurvy(ref renderer, start, center, end, lanes);
            }
        }

        int revisionLane = currentRow.GetCurrentRevisionLane();
        if (revisionLane < MaxLanes)
        {
            int centerX = (int)((revisionLane + 0.5) * LaneWidth);
            shapes.Add(new GraphNode(
                new RectangleF(centerX - (NodeDimension / 2), center.Y - (NodeDimension / 2), NodeDimension, NodeDimension),
                Square: hasRefs, Outline: isHead,
                ColorOf(currentRowRevisionLaneInfo, currentRow.Revision.IsRelative, style,
                    hovered?.Contains(currentRow.Revision.Objectid))));
        }

        return shapes;
    }

    // Upstream's GetBrushForLaneInfo: the lane's color when the commit is a relative, while hovered, or in the normal style;
    // gray otherwise and for a commit without a lane.
    private static int ColorOf(LaneInfo? laneInfo, bool isRelative, GraphDrawStyle style, bool? isHoverHighlighted)
        => laneInfo is not null && isHoverHighlighted is not false
           && (isHoverHighlighted is true || isRelative || style == GraphDrawStyle.Normal)
            ? laneInfo.Color
            : NonRelativeColor;

    private static SegmentLanesInfo GetLanesInfo(RevisionGraphSegment revisionGraphSegment,
        IRevisionGraphRow? previousRow,
        IRevisionGraphRow currentRow,
        IRevisionGraphRow? nextRow,
        bool skipSecondarySharedSegments,
        bool mergeGraphLanesHavingCommonParent,
        Action<LaneInfo?>? setLaneInfo = null)
    {
        Lane currentLane = currentRow.GetLaneForSegment(revisionGraphSegment);

        int startLane = NoLane;
        int centerLane = NoLane;
        int endLane = NoLane;

        // Avoid drawing the same curve twice (caused aliasing artifacts, particularly when in different colors)
        if (skipSecondarySharedSegments && currentLane.Sharing == LaneSharing.Entire)
        {
            return new SegmentLanesInfo(startLane, centerLane, endLane, PrimaryEndLane: endLane, IsTheRevisionLane: false,
                DrawFromStart: false, DrawToEnd: false);
        }

        centerLane = currentLane.Index;
        bool isTheRevisionLane = true;
        if (revisionGraphSegment.Parent == currentRow.Revision)
        {
            // This lane ends here
            startLane = GetLaneForRow(previousRow, revisionGraphSegment);
            setLaneInfo?.Invoke(revisionGraphSegment.LaneInfo);
        }
        else if (revisionGraphSegment.Child == currentRow.Revision)
        {
            // This lane starts here
            endLane = GetLaneForRow(nextRow, revisionGraphSegment);
            setLaneInfo?.Invoke(revisionGraphSegment.LaneInfo);
        }
        else
        {
            // This lane crosses
            startLane = GetLaneForRow(previousRow, revisionGraphSegment);
            endLane = GetLaneForRow(nextRow, revisionGraphSegment);
            isTheRevisionLane = false;
        }

        // Upstream throws on the two inconsistent cases below; a cell that cannot be drawn is left out instead.
        int primaryEndLane = endLane;
        switch (currentLane.Sharing)
        {
            case LaneSharing.DifferentStart:
                if (mergeGraphLanesHavingCommonParent)
                {
                    if (skipSecondarySharedSegments)
                    {
                        endLane = NoLane;
                    }
                }
                else if (endLane != NoLane)
                {
                    return new SegmentLanesInfo(NoLane, NoLane, NoLane, NoLane, false, false, false);
                }

                break;

            case LaneSharing.DifferentEnd:
                if (startLane != NoLane)
                {
                    return new SegmentLanesInfo(NoLane, NoLane, NoLane, NoLane, false, false, false);
                }

                break;
        }

        return new SegmentLanesInfo(startLane, centerLane, endLane, primaryEndLane, isTheRevisionLane,
            DrawFromStart: startLane >= 0 && centerLane >= 0 && (startLane <= MaxLanes || centerLane <= MaxLanes),
            DrawToEnd: endLane >= 0 && centerLane >= 0 && (endLane <= MaxLanes || centerLane <= MaxLanes));
    }

    private static DiagonalSegmentInfo GetDiagonalSegmentInfo(SegmentLanesInfo currentLanes,
        bool mergeGraphLanesHavingCommonParent)
    {
        bool drawFromStart = currentLanes.DrawFromStart;
        bool drawToEnd = currentLanes.DrawToEnd;
        bool isTheRevisionLane = currentLanes.IsTheRevisionLane;

        int startShift = currentLanes.CenterLane - currentLanes.StartLane;
        int endShift = currentLanes.EndLane - currentLanes.CenterLane;
        bool startIsDiagonal = Math.Abs(startShift) == 1;
        bool endIsDiagonal = Math.Abs(endShift) == 1;
        bool isBowOfDiagonals = startIsDiagonal && endIsDiagonal && -Math.Sign(startShift) == Math.Sign(endShift);
        int bowOffset = LaneWidth / 6;
        int junctionBowOffset = mergeGraphLanesHavingCommonParent ? LaneLineWidth : bowOffset;
        int horizontalOffset = isBowOfDiagonals ? -Math.Sign(startShift) * junctionBowOffset : 0;

        // Go perpendicularly through the center in order to avoid crossing independend nodes
        bool drawCenterToStartPerpendicularly = drawFromStart && (startShift == 0 || (!startIsDiagonal && !isTheRevisionLane));
        bool drawCenterToEndPerpendicularly = drawToEnd && (endShift == 0 || (!endIsDiagonal && !isTheRevisionLane));
        bool drawCenterPerpendicularly = isBowOfDiagonals;
        bool drawCenter = drawCenterPerpendicularly
            || !drawFromStart
            || !drawToEnd
            || (!drawCenterToStartPerpendicularly && !drawCenterToEndPerpendicularly);

        // handle non-straight junctions
        if (currentLanes.EndLane < 0 && currentLanes.PrimaryEndLane >= 0 && startShift != 0)
        {
            endShift = currentLanes.PrimaryEndLane - currentLanes.CenterLane;
            bool sameDirection = Math.Sign(endShift) == Math.Sign(startShift);
            if (startIsDiagonal)
            {
                int endDelta = Math.Abs(endShift);
                if (!sameDirection || endDelta > 1)
                {
                    drawCenterToEndPerpendicularly = true;
                    drawCenter = false;
                    horizontalOffset = -Math.Sign(startShift) * (endDelta != 1 || sameDirection ? LaneLineWidth / 3 : bowOffset);
                }
            }
            else if (Math.Abs(endShift) == 1)
            {
                // multi-lane crossing continued by a diagonal
                drawCenterToStartPerpendicularly = false;
                if (!sameDirection)
                {
                    // bow
                    horizontalOffset = -Math.Sign(startShift) * LaneLineWidth * 2 / 3;
                }
            }
            else
            {
                // multi-lane crossing continued by a straight or a multi-lane crossing
                drawCenterToStartPerpendicularly = false;
            }
        }

        return new DiagonalSegmentInfo(drawFromStart, drawToEnd,
            drawCenterToStartPerpendicularly, drawCenter, drawCenterPerpendicularly, drawCenterToEndPerpendicularly,
            horizontalOffset);
    }

    private static void DrawSegmentCurvy(ref SegmentRenderer renderer, Point start, Point center, Point end,
        SegmentLanesInfo lanes)
    {
        if (lanes.DrawFromStart)
        {
            renderer.DrawTo(start);
        }

        renderer.DrawTo(center);

        if (lanes.DrawToEnd)
        {
            renderer.DrawTo(end);
        }
    }

    private static void DrawSegmentWithDiagonals(ref SegmentRenderer renderer, Point start, Point center, Point end,
        Lazy<DiagonalSegmentInfo> previousSegmentInfo,
        DiagonalSegmentInfo current,
        Lazy<DiagonalSegmentInfo> nextSegmentInfo)
    {
        int halfPerpendicularHeight = renderer.RowHeight / 6;

        if (current.DrawFromStart)
        {
            DiagonalSegmentInfo previous = previousSegmentInfo.Value;
            int startX = start.X + previous.HorizontalOffset;
            if (previous.DrawCenterToEndPerpendicularly)
            {
                renderer.DrawTo(startX, start.Y + halfPerpendicularHeight);
            }
            else if (previous.DrawCenter)
            {
                renderer.DrawTo(startX, start.Y, previous.DrawCenterPerpendicularly);
            }
            else
            {
                renderer.DrawTo(startX, start.Y - halfPerpendicularHeight);
            }
        }

        int centerX = center.X + current.HorizontalOffset;

        if (current.DrawCenterToStartPerpendicularly)
        {
            renderer.DrawTo(centerX, center.Y - halfPerpendicularHeight);
        }

        if (current.DrawCenter)
        {
            renderer.DrawTo(centerX, center.Y, current.DrawCenterPerpendicularly);
        }

        if (current.DrawCenterToEndPerpendicularly)
        {
            renderer.DrawTo(centerX, center.Y + halfPerpendicularHeight);
        }

        if (current.DrawToEnd)
        {
            DiagonalSegmentInfo next = nextSegmentInfo.Value;
            int endX = end.X + next.HorizontalOffset;
            if (next.DrawCenterToStartPerpendicularly)
            {
                renderer.DrawTo(endX, end.Y - halfPerpendicularHeight);
            }
            else if (next.DrawCenter)
            {
                renderer.DrawTo(endX, end.Y, next.DrawCenterPerpendicularly);
            }
            else
            {
                renderer.DrawTo(endX, end.Y + halfPerpendicularHeight);
            }
        }
    }

    private static int GetLaneForRow(IRevisionGraphRow? row, RevisionGraphSegment revisionGraphRevision)
    {
        if (row is not null)
        {
            int lane = row.GetLaneForSegment(revisionGraphRevision).Index;
            if (lane >= 0)
            {
                return lane;
            }
        }

        return NoLane;
    }

    private readonly record struct SegmentLanesInfo(int StartLane, int CenterLane, int EndLane, int PrimaryEndLane,
        bool IsTheRevisionLane, bool DrawFromStart, bool DrawToEnd);

    private readonly record struct DiagonalSegmentInfo(bool DrawFromStart, bool DrawToEnd,
        bool DrawCenterToStartPerpendicularly, bool DrawCenter, bool DrawCenterPerpendicularly,
        bool DrawCenterToEndPerpendicularly, int HorizontalOffset);

    // Upstream's SegmentRenderer: joins the points of one segment with straight lines, diagonals and Bezier curves.
    // Upstream shifts anti-aliased lines by 1/8 px to compensate GDI+; Avalonia needs no such shift.
    private struct SegmentRenderer(RevisionGraphConfig config, List<GraphShape> shapes, int color, Size cellSize)
    {
        private bool _fromPerpendicularly = true;
        private Point? _fromPoint = null;

        public readonly int RowHeight => cellSize.Height;

        public void DrawTo(int x, int y, bool toPerpendicularly = true) => DrawTo(new Point(x, y), toPerpendicularly);

        public void DrawTo(Point toPoint, bool toPerpendicularly = true)
        {
            if (_fromPoint is { } fromPoint)
            {
                DrawTo(fromPoint, toPoint, _fromPerpendicularly, toPerpendicularly);
            }

            _fromPoint = toPoint;
            _fromPerpendicularly = toPerpendicularly;
        }

        private readonly void DrawTo(Point fromPoint, Point toPoint, bool fromPerpendicularly, bool toPerpendicularly)
        {
            List<GraphShape> output = shapes;
            int lineColor = color;
            if (fromPoint.X == toPoint.X)
            {
                DrawLine(fromPoint, toPoint);
                return;
            }

            PointF e0 = fromPoint;
            PointF e1 = toPoint;

            int height = toPoint.Y - fromPoint.Y;
            int width = toPoint.X - fromPoint.X;
            bool singleLane = Math.Abs(width) <= cellSize.Width;
            Size cellShift = new(Math.Sign(width) * cellSize.Width, cellSize.Height);

            if (!fromPerpendicularly && !toPerpendicularly && singleLane)
            {
                // Direct line with anti-aliasing
                DrawLine(e0, e1);
                return;
            }

            // Control points for Bezier curve
            PointF c0 = e0;
            PointF c1 = e1;

            const float diagonalFractionCurve = 1f / 4f;
            const float perpendicularFraction = diagonalFractionCurve;
            float perpendicularOffset = perpendicularFraction * cellShift.Height;

            if (fromPerpendicularly && toPerpendicularly)
            {
                if (config.RenderGraphWithDiagonals && singleLane)
                {
                    c0.Y += perpendicularOffset;
                    c1.Y -= perpendicularOffset;

                    PointF mid = new(1f / 2f * (e0.X + e1.X), 1f / 2f * (e0.Y + e1.Y));
                    SizeF shift = diagonalFractionCurve * cellShift;
                    DrawBezier(e0, c0, mid - shift, mid);
                    DrawBezier(e1, c1, mid + shift, mid);
                    return;
                }

                c0.Y = c1.Y = 1f / 2f * (fromPoint.Y + toPoint.Y);
            }
            else
            {
                // Is the end of a diagonal
                if (singleLane)
                {
                    float diagonalFractionStraight = height < cellShift.Height ? 2f / 5f : 1f / 2f;

                    if (fromPerpendicularly)
                    {
                        MoveDrawDiagonallyFrom(ref e1, out _, -diagonalFractionStraight);

                        // Prepare remaining curve
                        c1 = e1 - (diagonalFractionCurve * cellShift);
                        c0.Y += perpendicularOffset;
                    }
                    else
                    {
                        MoveDrawDiagonallyFrom(ref e0, out _, +diagonalFractionStraight);

                        // Prepare remaining curve
                        c0 = e0 + (diagonalFractionCurve * cellShift);
                        c1.Y -= perpendicularOffset;
                    }
                }

                // Is a multi-lane crossing
                else
                {
                    const float diagonalFractionStraight = 1f / 6f;

                    if (fromPerpendicularly)
                    {
                        c0.Y += perpendicularOffset;
                    }
                    else
                    {
                        MoveDrawDiagonallyFrom(ref e0, out c0, +diagonalFractionStraight);
                    }

                    if (toPerpendicularly)
                    {
                        c1.Y -= perpendicularOffset;
                    }
                    else
                    {
                        MoveDrawDiagonallyFrom(ref e1, out c1, -diagonalFractionStraight);
                    }
                }
            }

            DrawBezier(e0, c0, c1, e1);

            return;

            void DrawBezier(PointF b0, PointF b1, PointF b2, PointF b3)
                => output.Add(new GraphBezier(b0, b1, b2, b3, lineColor));

            void DrawLine(PointF from, PointF to) => output.Add(new GraphLine(from, to, lineColor));

            void MoveDrawDiagonallyFrom(ref PointF lineStart, out PointF bezierCenter, float fractionOfCell)
            {
                SizeF shift = fractionOfCell * cellShift;
                PointF lineEnd = lineStart + shift;
                DrawLine(lineStart, lineEnd);

                lineStart = lineEnd;
                bezierCenter = lineEnd + shift;
            }
        }
    }
}
