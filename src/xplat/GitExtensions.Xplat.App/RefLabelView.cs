using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Settings;
using GitExtUtils.GitUI.Theming;
using DrawingColor = System.Drawing.Color;

namespace GitExtensions.Xplat.App;

/// <summary>
///  A ref label of the revision grid as upstream's <c>RevisionGridRefRenderer.DrawRefEx</c> draws it: the outline of its
///  shape in a light shade of the ref's color (filled when <c>FillRefLabels</c> is on, or with the window color on a
///  selected row), the text in a darker shade, the checked-out branch bold after a filled arrow, and the label nestled
///  against it (dashed when it only shows ahead and behind arrows). A bisect mark is upstream's image instead. The label
///  under the pointer gets upstream's highlight frame and its tooltip (<see cref="RefLabelTooltips"/>). In a submodule a
///  label the superproject has is dashed, and the superproject's own labels take no frame, as upstream draws them without
///  a hit box.
/// </summary>
public sealed class RefLabelView : Control
{
    public static readonly StyledProperty<GridLabel?> LabelProperty =
        AvaloniaProperty.Register<RefLabelView, GridLabel?>(nameof(Label));

    public static readonly StyledProperty<bool> IsRowSelectedProperty =
        AvaloniaProperty.Register<RefLabelView, bool>(nameof(IsRowSelected));

    public static readonly StyledProperty<bool> FillProperty =
        AvaloniaProperty.Register<RefLabelView, bool>(nameof(Fill));

    /// <summary>
    ///  Upstream's <c>RevisionDataGridView.RemoteColors</c>: the colors set on remotes, by name, which the labels of their
    ///  branches take. Set on the grid and inherited by its labels.
    /// </summary>
    public static readonly AttachedProperty<IReadOnlyDictionary<string, DrawingColor>?> RemoteColorsProperty =
        AvaloniaProperty.RegisterAttached<RefLabelView, Control, IReadOnlyDictionary<string, DrawingColor>?>(
            "RemoteColors", inherits: true);

    /// <summary>
    ///  Upstream's <c>ShowRevisionGridTooltips</c>, set on the grid and inherited by its labels: while it is off the grid's
    ///  tooltips are off, but a label still shows how its branch tracks or is tracked, as upstream's.
    /// </summary>
    public static readonly AttachedProperty<bool> ShowRevisionTooltipsProperty =
        AvaloniaProperty.RegisterAttached<RefLabelView, Control, bool>("ShowRevisionTooltips", true, inherits: true);

    // Upstream's sizes at 100 % (RevisionGridRefRenderer and MessageColumnProvider).
    private const double PaddingTopBottom = 2;
    private const double CornerRadius = 2.5;
    private const double MarginRight = 5;
    private const double ImageMargin = 4;
    private static readonly double[] _dashPattern = [4, 4];

    // The part under the pointer (the label or the one nestled against it), which upstream frames in its color.
    private GridLabel? _hovered;

    static RefLabelView()
    {
        AffectsMeasure<RefLabelView>(LabelProperty, TextElement.FontSizeProperty, TextElement.FontFamilyProperty);
        AffectsRender<RefLabelView>(LabelProperty, IsRowSelectedProperty, FillProperty, RemoteColorsProperty);
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

    public static void SetRemoteColors(Control control, IReadOnlyDictionary<string, DrawingColor>? colors)
        => control.SetValue(RemoteColorsProperty, colors);

    public static void SetShowRevisionTooltips(Control control, bool show)
        => control.SetValue(ShowRevisionTooltipsProperty, show);

    /// <summary>
    ///  Upstream's tooltip of the label at <paramref name="point"/> (<see cref="LabelAt"/>): null outside the labels, where
    ///  the row's message tooltip shows, and where upstream shows none.
    /// </summary>
    public string? TooltipAt(Point point)
        => LabelAt(point)?.Ref is { } label
            ? RefLabelTooltips.For(label,
                (this.FindAncestorOfType<ListBoxItem>()?.DataContext as CommitListItem)?.MessageTooltip,
                GetValue(ShowRevisionTooltipsProperty))
            : null;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Point point = e.GetPosition(this);
        SetHovered(Parts(Bounds.Height).OfType<LabelPart>()
            .LastOrDefault(part => part.Rect.Contains(point) && part.Label is not { InSuperproject: true, Ref: null })?.Label);
        string? tooltip = TooltipAt(point);
        ToolTip.SetTip(this, tooltip);

        // While the grid's tooltips are off, a label's tracking text still shows, as upstream's.
        if (tooltip is not null && !GetValue(ShowRevisionTooltipsProperty))
        {
            ToolTip.SetServiceEnabled(this, true);
        }
        else
        {
            ClearValue(ToolTip.ServiceEnabledProperty);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHovered(null);
        ClearValue(ToolTip.TipProperty);
        ClearValue(ToolTip.ServiceEnabledProperty);
    }

    private void SetHovered(GridLabel? label)
    {
        if (!ReferenceEquals(label, _hovered))
        {
            _hovered = label;
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double cellHeight = double.IsFinite(availableSize.Height) ? availableSize.Height : TextHeight() + 6;
        return new Size(Parts(cellHeight).Select(part => part.End).DefaultIfEmpty(0).Max(), cellHeight);
    }

    /// <summary>
    ///  The label at <paramref name="point"/>, as upstream's grid finds the ref under a right-click: the label nestled
    ///  against this one when it stands for a ref, otherwise this label (also for upstream's virtual ahead and behind
    ///  label); null outside both and for a bisect mark.
    /// </summary>
    public GridLabel? LabelAt(Point point)
    {
        GridLabel? hit = null;
        foreach (Part part in Parts(Bounds.Height))
        {
            if (part is LabelPart labelPart && labelPart.Rect.Contains(point))
            {
                hit = labelPart.Label.Ref is null ? Label : labelPart.Label;
            }
        }

        return hit;
    }

    public override void Render(DrawingContext context)
    {
        List<Part> parts = Parts(Bounds.Height);
        foreach (Part part in parts)
        {
            // An unfilled label is only a frame and text, which the pointer would pass through; the transparent fill
            // makes the whole label take the pointer for its tooltip and frame.
            if (part is LabelPart label)
            {
                context.FillRectangle(Brushes.Transparent, label.Rect);
            }

            part.Draw(context);
        }

        // Upstream draws the frame after the label nestled against it.
        foreach (LabelPart part in parts.OfType<LabelPart>().Where(part => ReferenceEquals(part.Label, _hovered)))
        {
            part.DrawHighlight(context);
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

        LabelPart first = Shape(label, 0, cellHeight, dashed: label.InSuperproject);
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
        double iconWidth = label.IsCurrent || label.IsMergeSource ? cellHeight / 2 : 0;
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
            ColorOf(label), dashed, Fill, IsRowSelected)
        {
            End = rect.Right + MarginRight,
        };
    }

    // Upstream's DrawRef: the color set on the label's remote, else its kind's; DrawSuperprojectInfo's orange red adapted to
    // the background.
    private DrawingColor ColorOf(GridLabel label)
        => label.IsSuperprojectCommit
            ? DrawingColor.OrangeRed.AdaptForeColor(ThemeBrushes.Current.Colors.GetNonEmpty(System.Drawing.KnownColor.Window))
            : label.Remote is { } remote && GetValue(RemoteColorsProperty) is { } colors
              && colors.TryGetValue(remote, out DrawingColor color)
                ? color
                : ColorOf(label.Kind);

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
        public GridLabel Label { get; } = label;

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

            if (label.IsCurrent || label.IsMergeSource)
            {
                // Upstream's DrawArrow: a triangle before the text, filled for the checked-out branch and outlined for
                // the remote branch it tracks.
                double height = Rect.Height - 6;
                double x = Rect.X + iconOffset + 4;
                double y = Rect.Y + 3;
                StreamGeometry arrow = new();
                using (StreamGeometryContext path = arrow.Open())
                {
                    path.BeginFigure(new Point(x, y), isFilled: label.IsCurrent);
                    path.LineTo(new Point(x + (height / 2), y + (height / 2)));
                    path.LineTo(new Point(x, y + height));
                    path.EndFigure(isClosed: true);
                }

                SolidColorBrush arrowBrush = new(ToColor(color));
                context.DrawGeometry(label.IsCurrent ? arrowBrush : null, label.IsCurrent ? null : new Pen(arrowBrush, 1), arrow);
            }

            text.SetForegroundBrush(new SolidColorBrush(ToColor(fill ? color : ColorHelper.Lerp(color, DrawingColor.Black, 0.25f))));
            context.DrawText(text, textOrigin);
        }

        // Upstream's highlight frame of a hovered label (RefLabelHighlightWidth): the outline in the label's color.
        public void DrawHighlight(DrawingContext context)
            => context.DrawGeometry(null, new Pen(new SolidColorBrush(ToColor(color)), 1, dashed ? new DashStyle(_dashPattern, 0) : null),
                Outline(label.Shape, Rect, PointWidth));

        private static DrawingColor WindowColor()
            => ThemeBrushes.Current.Colors.GetNonEmpty(System.Drawing.KnownColor.Window);

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
