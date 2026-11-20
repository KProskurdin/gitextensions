using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The diff viewer's text: upstream's FileViewer is an editor, so its lines are an AvaloniaEdit editor (read only) with
///  upstream's two line number columns, each line's background and colors (<see cref="DiffLineItem"/>), and real text
///  selection. The viewer's line-based work (line patching, copy, find's line, go to line) reads the lines the selection
///  covers.
/// </summary>
public sealed class DiffTextView : UserControl
{
    private const double NumberColumnWidth = 44;
    private const double NumberOpacity = 0.55;

    // AvaloniaEdit splits a line's elements for each part it colors and walks them to find each part again, so a line's time
    // grows with the square of its parts: a minified JSON file of one 400 KB line held the viewer for over two minutes, and
    // marking 6,667 occurrences in a 20 KB line for over three. Past this many parts a line keeps its own color, as editors
    // stop coloring very long lines (VS Code's editor.maxTokenizationLineLength); upstream's editor colors them all.
    private const int MaxColoredPartsPerLine = 1000;

    private IReadOnlyList<DiffLineItem> _items = [];
    private IReadOnlyList<(int Offset, int Length)> _occurrences = [];

    // Moving the caret to an occurrence clears the editor's selection; upstream keeps the marks then.
    private bool _movingToOccurrence;

    public DiffTextView()
    {
        Editor = new TextEditor
        {
            IsReadOnly = true,
            WordWrap = false,
            ShowLineNumbers = false,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
        Editor.Options.EnableHyperlinks = false;
        Editor.Options.EnableEmailHyperlinks = false;
        Editor.Options.EnableTextDragDrop = false;
        Editor.Options.HighlightCurrentLine = false;
        Editor.TextArea.LeftMargins.Insert(0, new NumberMargin(this));
        Editor.TextArea.TextView.BackgroundRenderers.Add(new LineBackgrounds(this));
        Editor.TextArea.TextView.BackgroundRenderers.Add(new OccurrenceMarks(this));
        Editor.TextArea.TextView.BackgroundRenderers.Add(new InsertionAnchors(this));
        Editor.TextArea.TextView.LineTransformers.Add(new LineColors(this));
        Editor.TextArea.TextView.LineTransformers.Add(new OccurrenceTextColors(this));
        Editor.TextArea.SelectionChanged += (_, _) =>
        {
            MarkOccurrences();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        };
        Editor.TextArea.Caret.PositionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        Editor.TextArea.TextView.ScrollOffsetChanged += (_, _) => ScrollChanged?.Invoke(this, EventArgs.Empty);
        Editor.Background = Brushes.Transparent;
        Content = Editor;

        // Upstream's Selection color, as the list had it.
        AttachedToVisualTree += (_, _) => ShowSelectionColor();
        ActualThemeVariantChanged += (_, _) => ShowSelectionColor();
    }

    /// <summary>
    ///  Raised when the selection or the caret moves.
    /// </summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    ///  Raised when the text scrolls.
    /// </summary>
    public event EventHandler? ScrollChanged;

    /// <summary>
    ///  The editor, for the viewer's own text selection (find's occurrence).
    /// </summary>
    public TextEditor Editor { get; }

    /// <summary>
    ///  The lines shown. Lines with the same text keep the selection and the scroll (find's marks change only colors).
    /// </summary>
    public IReadOnlyList<DiffLineItem> ItemsSource
    {
        get => _items;
        set
        {
            string text = string.Join('\n', value.Select(item => item.Text));
            _items = value;
            if (text != Editor.Text)
            {
                Editor.Text = text;
                Editor.TextArea.ClearSelection();
                Editor.CaretOffset = 0;
                Editor.ScrollToHome();
            }

            Editor.TextArea.TextView.Redraw();
            foreach (NumberMargin margin in Editor.TextArea.LeftMargins.OfType<NumberMargin>())
            {
                margin.InvalidateVisual();
            }
        }
    }

    public int ItemCount => _items.Count;

    /// <summary>
    ///  The caret's line, -1 without lines. Setting it selects that whole line, as a list selects its row.
    /// </summary>
    public int SelectedIndex
    {
        get => _items.Count == 0 ? -1 : Editor.TextArea.Caret.Line - 1;
        set
        {
            if (value < 0 || value >= _items.Count)
            {
                Editor.TextArea.ClearSelection();
                return;
            }

            SelectLines(value, value);
        }
    }

    /// <summary>
    ///  The lines the selection covers, in order; the caret's line when nothing is selected.
    /// </summary>
    public IReadOnlyList<int> SelectedIndexes
    {
        get
        {
            if (_items.Count == 0)
            {
                return [];
            }

            if (Editor.SelectionLength == 0)
            {
                return [SelectedIndex];
            }

            TextDocument document = Editor.Document;
            int first = document.GetLineByOffset(Editor.SelectionStart).LineNumber - 1;
            int end = Editor.SelectionStart + Editor.SelectionLength;
            DocumentLine lastLine = document.GetLineByOffset(end);

            // A selection that ends at the start of a line does not take that line.
            int last = lastLine.LineNumber - 1 - (end == lastLine.Offset && lastLine.LineNumber - 1 > first ? 1 : 0);
            return [.. Enumerable.Range(first, last - first + 1)];
        }
    }

    public string SelectedText => Editor.SelectedText;

    /// <summary>
    ///  Upstream's marks of every occurrence of the selected text (<c>SelectionManagerSelectionChanged</c>).
    /// </summary>
    public IReadOnlyList<(int Offset, int Length)> Occurrences => _occurrences;

    /// <summary>
    ///  Upstream's marks' colors: the background <c>HighlightAllOccurences</c>, the text the theme's window text adapted
    ///  to it (<c>GetTextColor</c>).
    /// </summary>
    public static (System.Drawing.Color Background, System.Drawing.Color Text) OccurrenceMarkColors()
    {
        System.Drawing.Color background =
            ThemeBrushes.Current.Colors.Get(GitExtUtils.GitUI.Theming.AppColor.HighlightAllOccurences);
        System.Drawing.Color text = GitExtUtils.GitUI.Theming.ColorHelper.AdaptForeColor(
            ThemeBrushes.Current.Colors.GetNonEmpty(System.Drawing.KnownColor.WindowText), background);
        return (background, text);
    }

    /// <summary>
    ///  Upstream's <c>GoToNextOccurrence</c>: the caret to the next marked occurrence; false when there is none after it.
    /// </summary>
    public bool GoToNextOccurrence()
        => MoveCaretTo(_occurrences.Where(found => found.Offset > Editor.CaretOffset).Select(found => found.Offset)
            .DefaultIfEmpty(-1).First());

    /// <summary>
    ///  Upstream's <c>GoToPreviousOccurrence</c>: the caret to the previous marked occurrence.
    /// </summary>
    public bool GoToPreviousOccurrence()
        => MoveCaretTo(_occurrences.Where(found => found.Offset < Editor.CaretOffset).Select(found => found.Offset)
            .DefaultIfEmpty(-1).Last());

    /// <summary>
    ///  The vertical scroll: the offset, the height of all lines and the height shown.
    /// </summary>
    public (double Offset, double Extent, double Viewport) VerticalScroll
        => (Editor.VerticalOffset, Editor.ExtentHeight, Editor.ViewportHeight);

    public double HorizontalOffset => Editor.HorizontalOffset;

    /// <summary>
    ///  The width the line numbers take on the left.
    /// </summary>
    public static double NumbersWidth => 2 * NumberColumnWidth;

    /// <summary>
    ///  Selects the lines <paramref name="first"/> to <paramref name="last"/>, whole, and scrolls the first into view.
    /// </summary>
    public void SelectLines(int first, int last)
    {
        TextDocument document = Editor.Document;
        DocumentLine start = document.GetLineByNumber(first + 1);
        DocumentLine end = document.GetLineByNumber(last + 1);
        Editor.Select(start.Offset, end.EndOffset - start.Offset);
        Editor.TextArea.Caret.Offset = end.EndOffset;
        ScrollIntoView(first);
    }

    public void ScrollIntoView(int index)
    {
        if (index >= 0 && index < _items.Count)
        {
            Editor.ScrollToLine(index + 1);
        }
    }

    public void ScrollToTop() => Editor.ScrollToHome();

    public void ScrollToBottom() => Editor.ScrollToEnd();

    private bool MoveCaretTo(int offset)
    {
        if (offset < 0)
        {
            return false;
        }

        _movingToOccurrence = true;
        try
        {
            Editor.TextArea.ClearSelection();
            Editor.TextArea.Caret.Offset = offset;
        }
        finally
        {
            _movingToOccurrence = false;
        }

        Editor.TextArea.Caret.BringCaretToView();
        return true;
    }

    // Upstream's GetTextMarkersMatchingWord: every match of the selected text, ignoring case; none for white space.
    private void MarkOccurrences()
    {
        if (_movingToOccurrence)
        {
            return;
        }

        string word = Editor.SelectedText;
        List<(int Offset, int Length)> found = [];
        if (!string.IsNullOrWhiteSpace(word))
        {
            string text = Editor.Text;
            for (int index = text.IndexOf(word, StringComparison.OrdinalIgnoreCase); index >= 0;
                 index = index + 1 < text.Length ? text.IndexOf(word, index + 1, StringComparison.OrdinalIgnoreCase) : -1)
            {
                found.Add((index, word.Length));
            }
        }

        _occurrences = found;

        // The marks' text colors are in the lines, so the lines are built again.
        Editor.TextArea.TextView.Redraw();
    }

    private void ShowSelectionColor()
    {
        if (this.FindResource("SelectionBackgroundBrush") is IBrush selection)
        {
            Editor.TextArea.SelectionBrush = selection;
        }
    }

    private DiffLineItem? ItemOf(DocumentLine line)
        => line.LineNumber - 1 < _items.Count ? _items[line.LineNumber - 1] : null;

    // Upstream's two line number columns, old and new, on the line number background.
    private sealed class NumberMargin(DiffTextView owner) : AbstractMargin
    {
        protected override Size MeasureOverride(Size availableSize) => new(NumbersWidth, 0);

        public override void Render(DrawingContext context)
        {
            TextView? view = TextView;
            if (view is null || !view.VisualLinesValid)
            {
                return;
            }

            IBrush background = owner.FindResource("LineNumberBackgroundBrush") as IBrush ?? Brushes.Transparent;
            context.FillRectangle(background, new Rect(Bounds.Size));
            Typeface typeface = new(owner.FontFamily, owner.FontStyle, owner.FontWeight);
            foreach (VisualLine line in view.VisualLines)
            {
                if (owner.ItemOf(line.FirstDocumentLine) is not { } item)
                {
                    continue;
                }

                double y = line.VisualTop - view.VerticalOffset;
                IBrush foreground = WithOpacity(item.Foreground);
                Draw(context, item.OldNumber, typeface, foreground, NumberColumnWidth - 6, y);
                Draw(context, item.NewNumber, typeface, foreground, (2 * NumberColumnWidth) - 8, y);
            }
        }

        private void Draw(DrawingContext context, string number, Typeface typeface, IBrush brush, double right, double y)
        {
            if (number.Length == 0)
            {
                return;
            }

            FormattedText text = new(number, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface,
                owner.FontSize, brush);
            context.DrawText(text, new Point(right - text.Width, y));
        }

        private static IBrush WithOpacity(IBrush brush)
            => brush is ISolidColorBrush solid
                ? new SolidColorBrush(solid.Color, NumberOpacity)
                : brush;
    }

    // Each line's background (added, removed, hunk), across the whole width as upstream's.
    private sealed class LineBackgrounds(DiffTextView owner) : IBackgroundRenderer
    {
        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            foreach (VisualLine line in textView.VisualLines)
            {
                if (owner.ItemOf(line.FirstDocumentLine)?.Background is { } background)
                {
                    drawingContext.FillRectangle(background, new Rect(0, line.VisualTop - textView.VerticalOffset,
                        textView.Bounds.Width, line.Height));
                }
            }
        }
    }

    // Upstream's HighlightAllOccurences color behind each occurrence of the selected text.
    private sealed class OccurrenceMarks(DiffTextView owner) : IBackgroundRenderer
    {
        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (owner._occurrences.Count == 0 || !textView.VisualLinesValid)
            {
                return;
            }

            System.Drawing.Color color = DiffTextView.OccurrenceMarkColors().Background;
            IBrush brush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));

            // Only the occurrences in the columns shown: each one measures its line, and a long line can hold thousands. They
            // are in order and of one length (the selected text's), and one across a line break is drawn once.
            IReadOnlyList<(int Offset, int Length)> occurrences = owner._occurrences;
            int length = occurrences[0].Length;
            SortedSet<int> shown = [];
            foreach (VisualLine line in textView.VisualLines)
            {
                int first = OffsetAt(line, textView.HorizontalOffset);
                int last = OffsetAt(line, textView.HorizontalOffset + textView.Bounds.Width);
                for (int index = FirstEndingAtOrAfter(occurrences, first, length);
                     index < occurrences.Count && occurrences[index].Offset <= last; index++)
                {
                    shown.Add(index);
                }
            }

            foreach (int index in shown)
            {
                (int offset, int _) = occurrences[index];
                foreach (Rect rect in BackgroundGeometryBuilder.GetRectsForSegment(textView,
                             new TextSegment { StartOffset = offset, Length = length }))
                {
                    drawingContext.FillRectangle(brush, rect);
                }
            }

            return;

            static int FirstEndingAtOrAfter(IReadOnlyList<(int Offset, int Length)> occurrences, int offset, int length)
            {
                int low = 0;
                int high = occurrences.Count;
                while (low < high)
                {
                    int middle = (low + high) / 2;
                    if (occurrences[middle].Offset + length < offset)
                    {
                        low = middle + 1;
                    }
                    else
                    {
                        high = middle;
                    }
                }

                return low;
            }

            static int OffsetAt(VisualLine line, double x)
                => line.FirstDocumentLine.Offset
                   + line.GetRelativeOffset(line.GetVisualColumnFloor(new Point(x, line.VisualTop)));
        }
    }

    // Upstream's TextMarkerType.InterChar: a line along the bottom, half a character to each side of the point, and a tick up a
    // quarter of the line's height.
    private sealed class InsertionAnchors(DiffTextView owner) : IBackgroundRenderer
    {
        public KnownLayer Layer => KnownLayer.Caret;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (!textView.VisualLinesValid || textView.Document is not { } document)
            {
                return;
            }

            double width = textView.WideSpaceWidth;
            foreach (VisualLine line in textView.VisualLines)
            {
                if (owner.ItemOf(line.FirstDocumentLine)?.Anchors is not { Count: > 0 } anchors)
                {
                    continue;
                }

                DocumentLine documentLine = line.FirstDocumentLine;
                foreach ((int column, IBrush brush) in anchors)
                {
                    int offset = documentLine.Offset + Math.Min(column, documentLine.Length);
                    Point bottom = textView.GetVisualPosition(new TextViewPosition(document.GetLocation(offset)),
                        VisualYPosition.LineBottom) - textView.ScrollOffset;
                    double tick = line.Height * 0.25;
                    Pen pen = new(brush, 1);
                    Point baseLine = new(bottom.X, bottom.Y - 1);
                    drawingContext.DrawLine(pen, baseLine - new Point(width / 2, 0), baseLine + new Point(width / 2, 0));
                    drawingContext.DrawLine(pen, baseLine - new Point(0, tick), baseLine);
                }
            }
        }
    }

    // The marked occurrences' text in upstream's GetTextColor, over the line's own colors.
    private sealed class OccurrenceTextColors(DiffTextView owner) : DocumentColorizingTransformer
    {
        protected override void ColorizeLine(DocumentLine line)
        {
            if (owner._occurrences.Count == 0)
            {
                return;
            }

            List<(int Start, int End)> parts = [];
            foreach ((int offset, int length) in owner._occurrences)
            {
                int start = Math.Max(offset, line.Offset);
                int end = Math.Min(offset + length, line.EndOffset);
                if (end > start)
                {
                    parts.Add((start, end));
                }
            }

            // The marks behind them still show where they are.
            if (parts.Count > MaxColoredPartsPerLine)
            {
                return;
            }

            System.Drawing.Color color = DiffTextView.OccurrenceMarkColors().Text;
            IBrush brush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
            foreach ((int start, int end) in parts)
            {
                ChangeLinePart(start, end, element => element.TextRunProperties.SetForegroundBrush(brush));
            }
        }
    }

    // Each line's color, or its runs' colors, bold, italic and backgrounds (syntax, git's colors, find's marks).
    private sealed class LineColors(DiffTextView owner) : DocumentColorizingTransformer
    {
        protected override void ColorizeLine(DocumentLine line)
        {
            if (owner.ItemOf(line) is not { } item || line.Length == 0)
            {
                return;
            }

            if (item.Runs is not { Count: <= MaxColoredPartsPerLine } runs)
            {
                ChangeLinePart(line.Offset, line.EndOffset,
                    element => element.TextRunProperties.SetForegroundBrush(item.Foreground));
                return;
            }

            int start = line.Offset;
            foreach (DiffTextRun run in runs)
            {
                int end = Math.Min(start + run.Text.Length, line.EndOffset);
                if (end > start)
                {
                    ChangeLinePart(start, end, element =>
                    {
                        element.TextRunProperties.SetForegroundBrush(run.Foreground ?? item.Foreground);
                        if (run.Background is { } background)
                        {
                            element.TextRunProperties.SetBackgroundBrush(background);
                        }

                        if (run.Bold || run.Italic)
                        {
                            Typeface typeface = element.TextRunProperties.Typeface;
                            element.TextRunProperties.SetTypeface(new Typeface(typeface.FontFamily,
                                run.Italic ? FontStyle.Italic : typeface.Style,
                                run.Bold ? FontWeight.Bold : typeface.Weight));
                        }
                    });
                }

                start = end;
            }
        }
    }
}
