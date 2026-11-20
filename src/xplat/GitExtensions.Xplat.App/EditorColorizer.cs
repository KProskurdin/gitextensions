using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Editing;
using GitExtUtils.GitUI.Theming;
using DrawingColor = System.Drawing.Color;
using KnownColor = System.Drawing.KnownColor;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's highlighting in its editor (<c>SetHighlightingForFile</c>): git's files (<see cref="EditorHighlighting"/>)
///  with upstream's colors (each rebase command in its color and bold, the commit message's summary bold, comments green)
///  and a commit message's red waves (<see cref="Marks"/>); any other file in the syntax of its extension, as the file
///  viewer shows it.
/// </summary>
public sealed class EditorColorizer(EditorSyntax syntax, char commentChar, SyntaxDefinition? definition = null)
    : DocumentColorizingTransformer
{
    private IReadOnlyList<IReadOnlyList<EditorToken>> _tokens = [];
    private IReadOnlyList<IReadOnlyList<SyntaxRun>> _syntaxRuns = [];

    /// <summary>
    ///  A commit message's red waves (upstream's <c>TextMarker</c>s), with their tooltips.
    /// </summary>
    public IReadOnlyList<EditorMark> Marks { get; private set; } = [];

    /// <summary>
    ///  Draws <see cref="Marks"/> in the text view.
    /// </summary>
    public IBackgroundRenderer WaveRenderer => new Waves(this);

    /// <summary>
    ///  Reads every line again after each change: a commit message's summary and marks depend on the lines before, and so
    ///  does a syntax's state (a comment over several lines).
    /// </summary>
    public void Update(TextDocument document)
    {
        IReadOnlyList<string> lines = [.. document.Lines.Select(line => document.GetText(line))];
        _tokens = EditorHighlighting.Tokens(syntax, lines, commentChar);
        Marks = syntax == EditorSyntax.CommitMessage ? EditorHighlighting.CommitMessageMarks(lines, commentChar) : [];
        if (syntax == EditorSyntax.File && definition is not null)
        {
            SyntaxHighlighter highlighter = new(definition);
            _syntaxRuns = [.. lines.Select(highlighter.HighlightLine)];
        }
    }

    /// <summary>
    ///  The tooltip of the mark at <paramref name="offset"/>, or null.
    /// </summary>
    public string? ToolTipAt(TextDocument document, int offset)
    {
        DocumentLine line = document.GetLineByOffset(offset);
        int column = offset - line.Offset;
        return Marks.FirstOrDefault(mark => mark.Line == line.LineNumber - 1 && column >= mark.Start
                                            && column <= mark.Start + mark.Length)?.ToolTip;
    }

    /// <summary>
    ///  Upstream's color of a kind: fixed light and dark colors for the commands it does not adapt, the theme's text and
    ///  gray text for pick and exec, and its adapted dark green for comments.
    /// </summary>
    public static DrawingColor ColorOf(EditorTokenKind kind, bool dark) => kind switch
    {
        EditorTokenKind.Pick or EditorTokenKind.Summary => Theme(KnownColor.WindowText),
        EditorTokenKind.Reword => dark ? DrawingColor.MediumPurple : DrawingColor.Purple,
        EditorTokenKind.Edit => dark ? DrawingColor.LightGray : DrawingColor.DarkGray,
        EditorTokenKind.Squash => dark ? DrawingColor.CornflowerBlue : DrawingColor.DarkBlue,
        EditorTokenKind.Fixup => dark ? DrawingColor.Coral : DrawingColor.LightCoral,
        EditorTokenKind.Exec => Theme(KnownColor.GrayText),
        EditorTokenKind.Drop => dark ? DrawingColor.IndianRed : DrawingColor.Red,
        _ => DrawingColor.DarkGreen.AdaptForeColor(Theme(KnownColor.Window)),
    };

    protected override void ColorizeLine(DocumentLine line)
    {
        if (syntax == EditorSyntax.File)
        {
            ColorizeSyntax(line);
            return;
        }

        if (line.LineNumber > _tokens.Count)
        {
            return;
        }

        bool dark = ThemeBrushes.Current.IsDark;
        foreach (EditorToken token in _tokens[line.LineNumber - 1])
        {
            int start = line.Offset + Math.Min(token.Start, line.Length);
            int end = line.Offset + Math.Min(token.Start + token.Length, line.Length);
            if (end <= start)
            {
                continue;
            }

            DrawingColor color = ColorOf(token.Kind, dark);
            ChangeLinePart(start, end, element =>
            {
                element.TextRunProperties.SetForegroundBrush(
                    new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B)));
                if (token.Kind != EditorTokenKind.Comment)
                {
                    element.TextRunProperties.SetTypeface(new Typeface(element.TextRunProperties.Typeface.FontFamily,
                        element.TextRunProperties.Typeface.Style, FontWeight.Bold));
                }
            });
        }
    }

    // The file viewer's syntax colors (ThemeBrushes.Syntax), bold and italic.
    private void ColorizeSyntax(DocumentLine line)
    {
        if (line.LineNumber > _syntaxRuns.Count)
        {
            return;
        }

        foreach (SyntaxRun run in _syntaxRuns[line.LineNumber - 1])
        {
            int start = line.Offset + Math.Min(run.Start, line.Length);
            int end = line.Offset + Math.Min(run.Start + run.Length, line.Length);
            if (end <= start || run.Style is not { } style)
            {
                continue;
            }

            ChangeLinePart(start, end, element =>
            {
                if (style.Color is { } color && ThemeBrushes.Syntax(color) is { } brush)
                {
                    element.TextRunProperties.SetForegroundBrush(brush);
                }

                if (style.Bold || style.Italic)
                {
                    Typeface typeface = element.TextRunProperties.Typeface;
                    element.TextRunProperties.SetTypeface(new Typeface(typeface.FontFamily,
                        style.Italic ? FontStyle.Italic : typeface.Style, style.Bold ? FontWeight.Bold : typeface.Weight));
                }
            });
        }
    }

    private static DrawingColor Theme(KnownColor color) => ThemeBrushes.Current.Colors.GetNonEmpty(color);

    // Upstream's TextMarkerType.WaveLine in Color.Red.AdaptBackColor(), under each mark.
    private sealed class Waves(EditorColorizer owner) : IBackgroundRenderer
    {
        private const double Amplitude = 1.5;
        private const double Step = 2;

        public KnownLayer Layer => KnownLayer.Selection;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (owner.Marks.Count == 0 || textView.Document is not { } document || !textView.VisualLinesValid)
            {
                return;
            }

            DrawingColor red = DrawingColor.Red.AdaptBackColor();
            Pen pen = new(new SolidColorBrush(Color.FromArgb(red.A, red.R, red.G, red.B)), 1);
            foreach (EditorMark mark in owner.Marks)
            {
                if (mark.Line >= document.LineCount)
                {
                    continue;
                }

                DocumentLine line = document.GetLineByNumber(mark.Line + 1);
                int start = line.Offset + Math.Min(mark.Start, line.Length);
                int length = Math.Min(mark.Length, line.EndOffset - start);
                if (length <= 0)
                {
                    continue;
                }

                foreach (Avalonia.Rect rect in BackgroundGeometryBuilder.GetRectsForSegment(textView,
                             new TextSegment { StartOffset = start, Length = length }))
                {
                    StreamGeometry wave = new();
                    using (StreamGeometryContext context = wave.Open())
                    {
                        double y = rect.Bottom - Amplitude;
                        context.BeginFigure(new Avalonia.Point(rect.Left, y), isFilled: false);
                        bool up = true;
                        for (double x = rect.Left + Step; x <= rect.Right; x += Step)
                        {
                            context.LineTo(new Avalonia.Point(x, up ? y - Amplitude : y + Amplitude));
                            up = !up;
                        }

                        context.EndFigure(isClosed: false);
                    }

                    drawingContext.DrawGeometry(null, pen, wave);
                }
            }
        }
    }
}
