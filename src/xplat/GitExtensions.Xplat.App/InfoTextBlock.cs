using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using GitExtensions.Xplat.Core.CommitHistory;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's commit info rich text boxes: selectable text whose links (commits, branches, tags, addresses) are drawn in the
///  link color, underlined, and raise <see cref="LinkClicked"/> when clicked without selecting text.
/// </summary>
public sealed class InfoTextBlock : SelectableTextBlock
{
    private const string LinkBrushKey = "SystemControlHyperlinkTextBrush";

    private readonly List<(int Start, int End, string Link)> _links = [];
    private IReadOnlyList<IReadOnlyList<InfoSpan>> _lines = [];

    public InfoTextBlock()
    {
        // A transparent background makes the whole text, not only its glyphs, take the pointer, so links are easy to click.
        Background = Brushes.Transparent;
    }

    protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

    /// <summary>
    ///  Raised with the link's address when a link is clicked.
    /// </summary>
    public event EventHandler<string>? LinkClicked;

    public IReadOnlyList<IReadOnlyList<InfoSpan>> Lines
    {
        get => _lines;
        set
        {
            _lines = value;
            Build();
        }
    }

    /// <summary>
    ///  The text without links, lines separated by line feeds.
    /// </summary>
    public string PlainText => string.Join("\n", _lines.Select(CommitInfoText.PlainText));

    /// <summary>
    ///  The link under <paramref name="point"/> (in this control's coordinates), or null.
    /// </summary>
    public string? LinkAt(Point point)
    {
        if (_links.Count == 0)
        {
            return null;
        }

        TextHitTestResult hit = TextLayout.HitTestPoint(point - new Point(Padding.Left, Padding.Top));
        if (!hit.IsInside)
        {
            return null;
        }

        int position = hit.TextPosition;
        return _links.FirstOrDefault(link => position >= link.Start && position < link.End).Link;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Cursor = LinkAt(e.GetPosition(this)) is null ? new Cursor(StandardCursorType.Ibeam) : new Cursor(StandardCursorType.Hand);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && SelectionStart == SelectionEnd
            && LinkAt(e.GetPosition(this)) is { } link)
        {
            LinkClicked?.Invoke(this, link);
        }
    }

    private void Build()
    {
        _links.Clear();
        InlineCollection inlines = [];
        int position = 0;
        for (int i = 0; i < _lines.Count; i++)
        {
            if (i > 0)
            {
                Add(new Run("\n"), "\n");
            }

            foreach (InfoSpan span in _lines[i])
            {
                Run run = new(span.Text);
                if (span.Link is { } link)
                {
                    run[!TextElement.ForegroundProperty] = this.GetResourceObservable(LinkBrushKey).ToBinding();
                    run.TextDecorations = Avalonia.Media.TextDecorations.Underline;
                    _links.Add((position, position + span.Text.Length, link));
                }
                else if (span.Underline)
                {
                    run.TextDecorations = Avalonia.Media.TextDecorations.Underline;
                }

                Add(run, span.Text);
            }
        }

        Inlines = inlines;
        return;

        void Add(Run run, string text)
        {
            inlines.Add(run);
            position += text.Length;
        }
    }
}
