using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using GitExtensions.Xplat.Core.Spelling;
using GitExtUtils.GitUI.Theming;
using DrawingColor = System.Drawing.Color;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>SpellCheckEditControl</c> over a text box: a red wave under each misspelled word and, with "Mark ill formed
///  lines", a translucent yellow mark over the text past upstream's line lengths and over an empty first line. It is an
///  adorner of the box's text presenter, clipped to the box, and draws where the presenter's text layout puts the ranges.
/// </summary>
public sealed class SpellingUnderlines : Control
{
    // Upstream's DrawWave: 4 pixels from one wave to the next, half of it high, with a 1-pixel pen.
    private const double WaveWidth = 4;
    private const double PenWidth = 1;

    private readonly TextPresenter _presenter;
    private IReadOnlyList<TextRange> _misspelled = [];
    private IReadOnlyList<TextRange> _illFormed = [];
    private bool _marksEmptySubject;

    private SpellingUnderlines(TextPresenter presenter)
    {
        _presenter = presenter;
        IsHitTestVisible = false;
        presenter.LayoutUpdated += (_, _) => InvalidateVisual();
    }

    /// <summary>
    ///  The underlines of <paramref name="box"/>, attached once its template has its text presenter.
    /// </summary>
    public static void Attach(TextBox box, Action<SpellingUnderlines> attached)
    {
        box.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<TextPresenter>("PART_TextPresenter") is not { } presenter)
            {
                return;
            }

            SpellingUnderlines underlines = new(presenter);
            presenter.AttachedToVisualTree += (_, _) =>
            {
                if (AdornerLayer.GetAdornerLayer(presenter) is { } layer && !layer.Children.Contains(underlines))
                {
                    AdornerLayer.SetAdornedElement(underlines, presenter);
                    AdornerLayer.SetIsClipEnabled(underlines, true);
                    layer.Children.Add(underlines);
                }
            };
            attached(underlines);
        };
    }

    /// <summary>
    ///  The misspelled words, for the tests.
    /// </summary>
    public IReadOnlyList<TextRange> Misspelled => _misspelled;

    /// <summary>
    ///  The ill formed parts of lines, for the tests.
    /// </summary>
    public IReadOnlyList<TextRange> IllFormed => _illFormed;

    public void Show(IReadOnlyList<TextRange> misspelled, IReadOnlyList<TextRange> illFormed, bool marksEmptySubject)
    {
        _misspelled = misspelled;
        _illFormed = illFormed;
        _marksEmptySubject = marksEmptySubject;
        InvalidateVisual();
    }

    public void Clear() => Show([], [], marksEmptySubject: false);

    /// <summary>
    ///  The place in the text under the pointer of <paramref name="e"/>, or null without a text layout.
    /// </summary>
    public int? TextPositionAt(Avalonia.Input.PointerEventArgs e)
        => _presenter.TextLayout is { } layout ? layout.HitTestPoint(e.GetPosition(_presenter)).TextPosition : null;

    public override void Render(DrawingContext context)
    {
        if (_presenter.TextLayout is not { } layout)
        {
            return;
        }

        DrawingColor window = ThemeBrushes.Current.Colors.GetNonEmpty(System.Drawing.KnownColor.Window);

        // Upstream marks first, then draws the waves over them.
        IBrush mark = new SolidColorBrush(ToColor(DrawingColor.FromArgb(120, 255, 255, 0).AdaptBackColor()));
        if (_marksEmptySubject && layout.TextLines.Count > 0)
        {
            context.FillRectangle(mark, new Rect(0, 0, Bounds.Width, layout.TextLines[0].Height));
        }

        foreach (Rect rect in _illFormed.SelectMany(range => layout.HitTestTextRange(range.Start, range.Length)))
        {
            context.FillRectangle(mark, rect);
        }

        Pen wave = new(new SolidColorBrush(ToColor(DrawingColor.Red.AdaptForeColor(window))), PenWidth);
        foreach (Rect rect in _misspelled.SelectMany(range => layout.HitTestTextRange(range.Start, range.Length)))
        {
            DrawWave(context, wave, rect.Left, rect.Right, rect.Bottom - (WaveWidth / 2));
        }
    }

    // Upstream's DrawWave: a zigzag of half a wave's height, or a straight line under a word too short for one.
    private static void DrawWave(DrawingContext context, Pen pen, double start, double end, double y)
    {
        const double half = WaveWidth / 2;
        if (end - start <= WaveWidth)
        {
            context.DrawLine(pen, new Point(start, y), new Point(end, y));
            return;
        }

        StreamGeometry geometry = new();
        using (StreamGeometryContext path = geometry.Open())
        {
            path.BeginFigure(new Point(start, y), isFilled: false);
            for (double x = start; x <= end - half; x += WaveWidth)
            {
                path.LineTo(new Point(x, y));
                path.LineTo(new Point(x + half, y + half));
            }

            path.EndFigure(isClosed: false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    private static Color ToColor(DrawingColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);
}
