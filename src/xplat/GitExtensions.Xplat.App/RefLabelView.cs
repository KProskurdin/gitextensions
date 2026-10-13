using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Settings;
using GitExtUtils.GitUI.Theming;
using DrawingColor = System.Drawing.Color;

namespace GitExtensions.Xplat.App;

/// <summary>
///  A ref label of the revision grid as upstream's <c>RevisionGridRefRenderer.DrawRefEx</c> draws it: the outline of its
///  shape in a light shade of the ref's color (filled when <c>FillRefLabels</c> is on, or with the window color on a
///  selected row), the text in a darker shade, the checked-out branch bold after a filled arrow, and the label nestled
///  against it (dashed when it only shows ahead and behind arrows). A bisect mark is upstream's image instead.
/// </summary>
public sealed class RefLabelView : Control
{
    public static readonly StyledProperty<GridLabel?> LabelProperty =
        AvaloniaProperty.Register<RefLabelView, GridLabel?>(nameof(Label));

    public static readonly StyledProperty<bool> IsRowSelectedProperty =
        AvaloniaProperty.Register<RefLabelView, bool>(nameof(IsRowSelected));

    public static readonly StyledProperty<bool> FillProperty =
        AvaloniaProperty.Register<RefLabelView, bool>(nameof(Fill));

    // Upstream's sizes at 100 % (RevisionGridRefRenderer and MessageColumnProvider).
    private const double PaddingTopBottom = 2;
    private const double CornerRadius = 2.5;
    private const double MarginRight = 5;
    private const double ImageMargin = 4;
    private static readonly double[] _dashPattern = [4, 4];

    static RefLabelView()
    {
        AffectsMeasure<RefLabelView>(LabelProperty, TextElement.FontSizeProperty, TextElement.FontFamilyProperty);
        AffectsRender<RefLabelView>(LabelProperty, IsRowSelectedProperty, FillProperty);
    }

    public GridLabel? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public bool IsRowSelected
    {
        get => GetValue(IsRowSelectedProperty);
        set => SetValue(IsRowSelectedProperty, value);
    }

    public bool Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double cellHeight = double.IsFinite(availableSize.Height) ? availableSize.Height : TextHeight() + 6;
        return new Size(Parts(cellHeight).Select(part => part.End).DefaultIfEmpty(0).Max(), cellHeight);
    }

    public override void Render(DrawingContext context)
    {
        foreach (Part part in Parts(Bounds.Height))
        {
            part.Draw(context);
        }
    }

    private double TextHeight() => Text(" ", bold: false).Height;

    private FormattedText Text(string text, bool bold)
        => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(TextElement.GetFontFamily(this), FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal),
            TextElement.GetFontSize(this), Brushes.Black);

    // The label and the one nestled against it, laid out from the left as upstream's OnCellPainting advances its offset.
    private List<Part> Parts(double cellHeight)
    {
        List<Part> parts = [];
        if (Label is not { } label)
        {
            return parts;
        }

        if (label.Kind is RefKind.BisectGood or RefKind.BisectBad)
        {
            if (UpstreamImages.Named(label.Kind == RefKind.BisectGood ? "BisectGood" : "BisectBad") is { } image)
            {
                parts.Add(new ImagePart(image, cellHeight));
            }

            return parts;
        }

        LabelPart first = Shape(label, 0, cellHeight, dashed: false);
        parts.Add(first);
        if (label.Nestled is { } nested)
        {
            // Upstream aligns the nestled label's notch or point with the first one's, cancelling the margin between them.
            double x = Math.Max(0, first.Rect.Right - first.PointWidth + 1);
            LabelPart second = Shape(nested, x, cellHeight, dashed: nested.Ref is null);
            parts.Add(second);
        }

        return parts;
    }

    private LabelPart Shape(GridLabel label, double x, double cellHeight, bool dashed)
    {
        FormattedText text = Text(label.Text, label.IsCurrent);
        double paddingLeftRight = label.Text.Length == 0 ? 1 : 4;
        double iconWidth = label.IsCurrent ? cellHeight / 2 : 0;
        double height = text.Height + PaddingTopBottom + PaddingTopBottom - 1;
        double pointWidth = height / 2;
        double extraWidth = label.Shape switch
        {
            GridLabelShape.NotchLeft or GridLabelShape.NotchRight => pointWidth,
            GridLabelShape.PointLeft or GridLabelShape.PointRight => pointWidth / 2,
            _ => 0,
        };
        Rect rect = new(x, (cellHeight - height) / 2, text.Width + iconWidth + paddingLeftRight + paddingLeftRight + extraWidth - 1, height);
        double iconOffset = label.Shape is GridLabelShape.NotchLeft or GridLabelShape.PointLeft ? pointWidth : 0;
        double textX = rect.X + iconOffset + iconWidth + paddingLeftRight
                       - (label.Shape == GridLabelShape.PointLeft ? pointWidth / 2 : 0);
        return new LabelPart(label, rect, pointWidth, iconOffset, text, new Point(textX, rect.Y + PaddingTopBottom - 1),
            ColorOf(label.Kind), dashed, Fill, IsRowSelected)
        {
            End = rect.Right + MarginRight,
        };
    }

    // Upstream's GetHeadColor; an artificial row's label (HEAD here) has OtherTag's color.
    private static DrawingColor ColorOf(RefKind kind)
    {
        AppThemeColors colors = ThemeBrushes.Current.Colors;
        return colors.Get(kind switch
        {
            RefKind.Branch => AppColor.Branch,
            RefKind.RemoteBranch => AppColor.RemoteBranch,
            RefKind.Tag => AppColor.Tag,
            _ => AppColor.OtherTag,
        });
    }

    private abstract class Part
    {
        // Where the next label starts: after this part and upstream's margin.
        public double End { get; init; }

        public abstract void Draw(DrawingContext context);
    }

    private sealed class ImagePart : Part
    {
        private readonly Bitmap _image;
        private readonly Rect _rect;

        public ImagePart(Bitmap image, double cellHeight)
        {
            _image = image;
            double size = ToolStripContent.IconSize;
            _rect = new Rect(0, (cellHeight - size) / 2, size, size);
            End = size + ImageMargin;
        }

        public override void Draw(DrawingContext context) => context.DrawImage(_image, _rect);
    }

    private sealed class LabelPart(
        GridLabel label,
        Rect rect,
        double pointWidth,
        double iconOffset,
        FormattedText text,
        Point textOrigin,
        DrawingColor color,
        bool dashed,
        bool fill,
        bool rowSelected) : Part
    {
        public Rect Rect { get; } = rect;

        public double PointWidth { get; } = pointWidth;

        public override void Draw(DrawingContext context)
        {
            DrawingColor window = WindowColor();
            Geometry outline = Outline(label.Shape, Rect, PointWidth);
            IBrush? background = fill
                ? new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(ToColor(ColorHelper.Lerp(color, window, 0.92f)), 0),
                        new GradientStop(ToColor(ColorHelper.Lerp(ColorHelper.Lerp(color, window, 0.92f), window, 0.9f)), 1),
                    },
                }
                : rowSelected ? new SolidColorBrush(ToColor(window)) : null;
            Pen frame = new(new SolidColorBrush(ToColor(ColorHelper.Lerp(color, window, fill ? 0.83f : 0.5f))), 1,
                dashed ? new DashStyle(_dashPattern, 0) : null);
            context.DrawGeometry(background, frame, outline);

            if (label.IsCurrent)
            {
                // Upstream's DrawArrow: a filled triangle before the text of the checked-out branch.
                double height = Rect.Height - 6;
                double x = Rect.X + iconOffset + 4;
                double y = Rect.Y + 3;
                StreamGeometry arrow = new();
                using (StreamGeometryContext path = arrow.Open())
                {
                    path.BeginFigure(new Point(x, y), isFilled: true);
                    path.LineTo(new Point(x + (height / 2), y + (height / 2)));
                    path.LineTo(new Point(x, y + height));
                    path.EndFigure(isClosed: true);
                }

                context.DrawGeometry(new SolidColorBrush(ToColor(color)), null, arrow);
            }

            text.SetForegroundBrush(new SolidColorBrush(ToColor(fill ? color : ColorHelper.Lerp(color, DrawingColor.Black, 0.25f))));
            context.DrawText(text, textOrigin);
        }

        private static DrawingColor WindowColor()
            => ThemeBrushes.Current.Colors.Get(System.Drawing.KnownColor.Window) is { IsEmpty: false } themed
                ? themed
                : ThemeBrushes.Current.IsDark ? DrawingColor.FromArgb(0x1E, 0x1E, 0x1E) : DrawingColor.White;

        private static Color ToColor(DrawingColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);

        // Upstream's Create*RoundRectPath: GDI arcs of the radius as diameter, so the corners round by half of it.
        private static Geometry Outline(GridLabelShape shape, Rect rect, double pointWidth)
        {
            double left = rect.Left;
            double top = rect.Top;
            double right = rect.Right;
            double bottom = rect.Bottom;
            double middle = top + (rect.Height / 2);
            double r = CornerRadius;
            Size corner = new(r, r);
            StreamGeometry geometry = new();
            using StreamGeometryContext path = geometry.Open();
            switch (shape)
            {
                case GridLabelShape.PointRight:
                    path.BeginFigure(new Point(left + r, top), isFilled: true);
                    path.LineTo(new Point(right - pointWidth, top));
                    path.LineTo(new Point(right, middle));
                    path.LineTo(new Point(right - pointWidth, bottom));
                    LeftSide(path);
                    break;
                case GridLabelShape.NotchRight:
                    path.BeginFigure(new Point(left + r, top), isFilled: true);
                    path.LineTo(new Point(right, top));
                    path.LineTo(new Point(right - pointWidth, middle));
                    path.LineTo(new Point(right, bottom));
                    LeftSide(path);
                    break;
                case GridLabelShape.PointLeft:
                    path.BeginFigure(new Point(left, middle), isFilled: true);
                    path.LineTo(new Point(left + pointWidth, top));
                    RightSide(path);
                    path.LineTo(new Point(left + pointWidth, bottom));
                    break;
                case GridLabelShape.NotchLeft:
                    path.BeginFigure(new Point(left, top), isFilled: true);
                    path.LineTo(new Point(left + pointWidth, middle));
                    path.LineTo(new Point(left, bottom));
                    path.LineTo(new Point(right - r, bottom));
                    path.ArcTo(new Point(right, bottom - r), corner, 0, false, SweepDirection.CounterClockwise);
                    path.LineTo(new Point(right, top + r));
                    path.ArcTo(new Point(right - r, top), corner, 0, false, SweepDirection.CounterClockwise);
                    break;
                default:
                    path.BeginFigure(new Point(left + r, top), isFilled: true);
                    RightSide(path);
                    LeftSide(path);
                    break;
            }

            path.EndFigure(isClosed: true);
            return geometry;

            void RightSide(StreamGeometryContext context)
            {
                context.LineTo(new Point(right - r, top));
                context.ArcTo(new Point(right, top + r), corner, 0, false, SweepDirection.Clockwise);
                context.LineTo(new Point(right, bottom - r));
                context.ArcTo(new Point(right - r, bottom), corner, 0, false, SweepDirection.Clockwise);
            }

            void LeftSide(StreamGeometryContext context)
            {
                context.LineTo(new Point(left + r, bottom));
                context.ArcTo(new Point(left, bottom - r), corner, 0, false, SweepDirection.Clockwise);
                context.LineTo(new Point(left, top + r));
                context.ArcTo(new Point(left + r, top), corner, 0, false, SweepDirection.Clockwise);
            }
        }
    }
}
