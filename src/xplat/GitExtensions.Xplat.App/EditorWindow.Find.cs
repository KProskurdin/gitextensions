using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using GitExtUtils.GitUI.Theming;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

// Upstream's FormEditor is a FileViewer that can be edited, so it has the viewer's find, replace and go to line. The window here
// is AvaloniaEdit's editor, which has a real selection, so the search works on it as upstream's does on its editor. "Find &
// highlight all" marks every occurrence as upstream's markers do; the marks move with the text as it is edited.
public partial class EditorWindow
{
    // Upstream's FindAndReplaceForm texts and caption.
    private const string NoOccurrencesFoundText = "No occurrences found.";
    private const string ReplacedOccurrencesText = "Replaced {0} occurrences.";
    private const string InformationCaption = "Information";
    private const string NoCaption = " ";
    private const string FindMenuText = "_Find...";
    private const string ReplaceMenuText = "_Replace...";
    private const string GoToLineMenuText = "Go to line...";
    private const string CutMenuText = "Cut";
    private const string CopyMenuText = "Copy";
    private const string PasteMenuText = "Paste";

    private FindWindow? _findWindow;
    private TextSegmentCollection<TextSegment>? _highlighted;
    private (int Offset, int Length)? _scanRegion;
    private bool _lastSearchWasBackward;

    /// <summary>
    ///  The find window, once it was asked for.
    /// </summary>
    public FindWindow? FindWindow => _findWindow;

    /// <summary>
    ///  The occurrences "Find & highlight all" marked, where they are now.
    /// </summary>
    public IReadOnlyList<(int Offset, int Length)> Highlighted
        => [.. (_highlighted ?? []).Select(segment => (segment.StartOffset, segment.Length))];

    // Upstream's viewer menu, in the order it has them, with the box's editing items.
    private void AddEditorMenu()
    {
        ContentBox.ContextFlyout = new MenuFlyout
        {
            Items =
            {
                MenuEntry(CutMenuText, new KeyGesture(Key.X, Hotkeys.CommandModifier), ContentBox.Cut),
                MenuEntry(CopyMenuText, new KeyGesture(Key.C, Hotkeys.CommandModifier), ContentBox.Copy),
                MenuEntry(PasteMenuText, new KeyGesture(Key.V, Hotkeys.CommandModifier), ContentBox.Paste),
                new Separator(),
                MenuEntry(FindMenuText, Hotkeys.Diff.GestureFor(DiffCommand.Find), () => ShowFind(replace: false)),
                MenuEntry(ReplaceMenuText, Hotkeys.Diff.GestureFor(DiffCommand.Replace), () => ShowFind(replace: true)),
                MenuEntry(GoToLineMenuText, Hotkeys.Diff.GestureFor(DiffCommand.GoToLine), () => Run(GoToLineAsync)),
            },
        };
    }

    private static MenuItem MenuEntry(string text, KeyGesture? gesture, Action action)
    {
        MenuItem item = new() { Header = text, InputGesture = gesture };
        item.Click += (_, _) => action();
        return item;
    }

    // The viewer's hotkeys, as upstream's editor has them. True when the key was one of them.
    private bool RunFindHotkey(KeyEventArgs e)
    {
        switch (Hotkeys.Diff.Match(e))
        {
            case DiffCommand.Find:
                ShowFind(replace: false);
                return true;
            case DiffCommand.Replace:
                ShowFind(replace: true);
                return true;
            case DiffCommand.FindNextOrOpenWithDifftool when _findWindow is { LookFor.Length: > 0 }:
                Run(() => FindNextAsync(searchBackward: false));
                return true;
            case DiffCommand.FindPrevious when _findWindow is { LookFor.Length: > 0 }:
                Run(() => FindNextAsync(searchBackward: true));
                return true;
            case DiffCommand.GoToLine:
                Run(GoToLineAsync);
                return true;
            default:
                return false;
        }
    }

    private void ShowFind(bool replace)
    {
        FindWindow window = _findWindow ??= NewFindWindow();
        (string lookFor, _scanRegion) = TextReplace.ReadSelection(ContentBox.Text ?? "", ContentBox.SelectionStart,
            ContentBox.SelectionStart + ContentBox.SelectionLength);
        if (lookFor.Length > 0)
        {
            window.LookFor = lookFor;
        }

        window.ReplaceMode = replace;
        window.ShowFor(this, _path, _scanRegion is not null);
    }

    private FindWindow NewFindWindow()
    {
        FindWindow window = new();
        window.HighlightAllRequested += (_, _) => Run(() => HighlightAllAsync(window));
        window.FindNextRequested += (_, backward) => Run(() => FindNextAsync(backward));
        window.ReplaceRequested += (_, _) => Run(ReplaceAsync);
        window.ReplaceAllRequested += (_, _) => Run(ReplaceAllAsync);

        // Upstream's FormClosing discards the search region.
        window.Hidden += (_, _) => _scanRegion = null;
        return window;
    }

    // Upstream's btnHighlightAll_Click: every occurrence marked, or the marks cleared for an empty text; the window closes
    // when it marked some, else it says none was found.
    private async Task HighlightAllAsync(FindWindow window)
    {
        TextDocument document = ContentBox.Document;
        if (_highlighted is null)
        {
            _highlighted = new TextSegmentCollection<TextSegment>(document);
            ContentBox.TextArea.TextView.BackgroundRenderers.Add(new HighlightMarks(this));
        }

        _highlighted.Clear();
        if (window.LookFor.Length > 0)
        {
            foreach ((int offset, int length) in NewSearch(window, ContentBox.Text ?? "").FindAll())
            {
                _highlighted.Add(new TextSegment { StartOffset = offset, Length = length });
            }
        }

        ContentBox.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
        if (window.LookFor.Length == 0)
        {
            return;
        }

        if (_highlighted.Count == 0)
        {
            await ShowMessageAsync(window, UpstreamTranslation.ByText(DiffView.SearchTextNotFoundText),
                UpstreamTranslation.ByText(DiffView.NotFoundText));
        }
        else
        {
            window.Close();
        }
    }

    private TextSearch NewSearch(FindWindow window, string text)
        => new(text)
        {
            LookFor = window.LookFor,
            MatchCase = window.MatchCase,
            MatchWholeWordOnly = window.MatchWholeWord,
            ScanRegion = _scanRegion,
        };

    // Upstream's FindNextAsync, from the caret, which stands after a selection.
    private async Task FindNextAsync(bool searchBackward)
    {
        FindWindow window = _findWindow ??= NewFindWindow();
        _lastSearchWasBackward = searchBackward;
        if (window.LookFor.Length == 0)
        {
            await ShowMessageAsync(window, UpstreamTranslation.ByText(DiffView.NoSearchText), window.Title ?? "");
            return;
        }

        string text = ContentBox.Text ?? "";
        int caret = Math.Max(ContentBox.CaretOffset, ContentBox.SelectionStart + ContentBox.SelectionLength);
        TextSearch search = NewSearch(window, text);
        int startFrom = caret - (searchBackward ? 1 : 0);
        if (startFrom == -1)
        {
            startFrom = search.EndOffset;
        }

        if (search.FindNext(startFrom, searchBackward, out _) is { } found)
        {
            ContentBox.Select(found.Offset, found.Length);
            ContentBox.TextArea.Caret.BringCaretToView();
        }
        else
        {
            await ShowMessageAsync(window, UpstreamTranslation.ByText(DiffView.TextNotFoundText), NoCaption);
        }
    }

    // Upstream's btnReplace_Click: what is selected, when it is the text looked for, then on to the next one.
    private async Task ReplaceAsync()
    {
        FindWindow window = _findWindow ??= NewFindWindow();
        if (TextReplace.MatchesSelection(ContentBox.SelectedText ?? "", window.LookFor))
        {
            int change = window.ReplaceWith.Length - window.LookFor.Length;

            // The editor's own input keeps its undo.
            ContentBox.SelectedText = window.ReplaceWith;
            _scanRegion = _scanRegion is { } region ? (region.Offset, region.Length + change) : null;
        }

        await FindNextAsync(_lastSearchWasBackward);
    }

    // Upstream's btnReplaceAll_Click; the one input keeps its undo as one step, as upstream's undo group does.
    private async Task ReplaceAllAsync()
    {
        FindWindow window = _findWindow ??= NewFindWindow();
        string text = ContentBox.Text ?? "";
        (string replaced, int count) = TextReplace.ReplaceAll(text, NewSearch(window, text), window.ReplaceWith);
        if (count == 0)
        {
            await ShowMessageAsync(window, UpstreamTranslation.ByText(NoOccurrencesFoundText), InformationCaption);
            return;
        }

        ContentBox.Document.Replace(0, text.Length, replaced);
        await ShowMessageAsync(window, string.Format(UpstreamTranslation.ByText(ReplacedOccurrencesText), count), InformationCaption);
        window.Close();
    }

    private async Task GoToLineAsync()
    {
        string text = ContentBox.Text ?? "";
        GoToLineWindow window = new();
        window.SetMaxLineNumber(TextReplace.LineOf(text, text.Length));
        if (await window.ShowAsync(this) is { } line)
        {
            ContentBox.CaretOffset = TextReplace.OffsetOfLine(text, line);
            ContentBox.TextArea.Caret.BringCaretToView();
            ContentBox.Focus();
        }
    }

    private static async Task ShowMessageAsync(Window owner, string text, string caption)
        => await new MessageBoxWindow(text, caption, MessageBoxButtons.OK).ShowDialog(owner);

    // Upstream's highlight markers, in its color (Color.Yellow.AdaptBackColor()), under the text.
    private sealed class HighlightMarks(EditorWindow owner) : IBackgroundRenderer
    {
        public KnownLayer Layer => KnownLayer.Selection;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (owner._highlighted is not { Count: > 0 } marks || !textView.VisualLinesValid)
            {
                return;
            }

            System.Drawing.Color yellow = System.Drawing.Color.Yellow.AdaptBackColor();
            IBrush brush = new SolidColorBrush(Color.FromArgb(yellow.A, yellow.R, yellow.G, yellow.B));
            foreach (TextSegment segment in marks)
            {
                foreach (Rect rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                {
                    drawingContext.FillRectangle(brush, rect);
                }
            }
        }
    }
}
