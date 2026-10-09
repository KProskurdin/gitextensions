using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Color = Avalonia.Media.Color;
using Control = Avalonia.Controls.Control;
using Pen = Avalonia.Media.Pen;
using Point = Avalonia.Point;
using Size = Avalonia.Size;
using ToolTip = Avalonia.Controls.ToolTip;

namespace GitExtensions.Plugins.GitStatistics;

/// <summary>
///  The statistics window's pie: one slice per value in upstream's colors, edged a little darker than the slice (upstream's
///  <c>EdgeColorType.DarkerThanSurface</c>), with the slice's text as the tooltip under the pointer. Upstream's
///  <c>PieChartControl</c> draws a 3D pie with shadows through System.Drawing; this draws it flat.
/// </summary>
public sealed class PieChart : Control
{
    private const double Margin = 10;
    private const double EdgeDarkening = 0.7;
    private const double StartAngle = -90;

    // Upstream's DecentColors.
    private static readonly Color[] _colors =
    [
        Colors.Red, Colors.Yellow, Colors.DodgerBlue, Colors.LightGreen, Colors.Coral, Colors.Goldenrod, Colors.YellowGreen,
        Colors.MediumPurple, Colors.LightGray, Colors.Brown, Colors.Pink, Colors.DarkBlue, Colors.Purple,
    ];

    private decimal[] _values = [];

    /// <summary>
    ///  One text per slice, in the order of the values.
    /// </summary>
    public IReadOnlyList<string> ToolTips { get; set; } = [];

    public IReadOnlyList<decimal> Values => _values;

    public void SetValues(decimal[] values)
    {
        _values = values;
        InvalidateVisual();
    }

    /// <summary>
    ///  The slice at <paramref name="point"/> (in the control's coordinates), or -1 outside the pie.
    /// </summary>
    public int SliceAt(Point point)
    {
        (Point center, double radius) = Circle();
        Vector offset = point - center;
        decimal total = _values.Sum();
        if (total <= 0 || offset.Length > radius)
        {
            return -1;
        }

        double angle = (Math.Atan2(offset.Y, offset.X) * 180 / Math.PI) - StartAngle;
        angle = ((angle % 360) + 360) % 360;
        double end = 0;
        for (int i = 0; i < _values.Length; i++)
        {
            end += (double)(_values[i] / total) * 360;
            if (angle <= end)
            {
                return i;
            }
        }

        return _values.Length - 1;
    }

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        int slice = SliceAt(e.GetPosition(this));
        ToolTip.SetTip(this, slice >= 0 && slice < ToolTips.Count ? ToolTips[slice] : null);
    }

    public override void Render(DrawingContext context)
    {
        decimal total = _values.Sum();
        if (total <= 0)
        {
            return;
        }

        (Point center, double radius) = Circle();
        double start = StartAngle;
        for (int i = 0; i < _values.Length; i++)
        {
            double sweep = (double)(_values[i] / total) * 360;
            Color color = _colors[i % _colors.Length];
            IBrush fill = new SolidColorBrush(color);
            Pen edge = new(new SolidColorBrush(Darker(color)));
            if (sweep >= 360)
            {
                context.DrawEllipse(fill, edge, center, radius, radius);
            }
            else if (sweep > 0)
            {
                context.DrawGeometry(fill, edge, Slice(center, radius, start, sweep));
            }

            start += sweep;
        }
    }

    private (Point Center, double Radius) Circle()
    {
        double side = Math.Max(0, Math.Min(Bounds.Width, Bounds.Height) - (2 * Margin));
        return (new Point(Bounds.Width / 2, Bounds.Height / 2), side / 2);
    }

    private static StreamGeometry Slice(Point center, double radius, double startAngle, double sweep)
    {
        StreamGeometry geometry = new();
        using StreamGeometryContext path = geometry.Open();
        path.BeginFigure(center, isFilled: true);
        path.LineTo(PointAt(center, radius, startAngle));
        path.ArcTo(PointAt(center, radius, startAngle + sweep), new Size(radius, radius), 0, sweep > 180,
            SweepDirection.Clockwise);
        path.EndFigure(isClosed: true);
        return geometry;
    }

    private static Point PointAt(Point center, double radius, double angle)
    {
        double radians = angle * Math.PI / 180;
        return new Point(center.X + (radius * Math.Cos(radians)), center.Y + (radius * Math.Sin(radians)));
    }

    private static Color Darker(Color color)
        => Color.FromArgb(color.A, (byte)(color.R * EdgeDarkening), (byte)(color.G * EdgeDarkening),
            (byte)(color.B * EdgeDarkening));
}
