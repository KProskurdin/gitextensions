using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Ui;
using GitExtUtils.GitUI.Theming;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream FileViewer's find (<c>FindAndReplaceForm</c> with <c>TextEditorSearcher</c>, core <see cref="TextSearch"/>)
///  and go to line (<c>FormGoToLine</c>) over the lines the viewer shows. The viewer selects whole lines, not text in a
///  line, so a found occurrence selects its line and is marked in it, and the next search starts after it.
/// </summary>
public partial class DiffView
{
    // Upstream's texts (FindAndReplaceForm and FileViewerInternal).
    public const string TextNotFoundText = "Text not found";
    public const string NoSearchText = "No string specified to look for!";
    public const string SearchTextNotFoundText = "Search text not found.";
    public const string NotFoundText = "Not found";

    // Upstream's message caption of a search from the viewer.
    private const string NoCaption = " ";

    private FindWindow? _findWindow;

    // Upstream's scan region: the lines selected when Find opened, which the search keeps to.
    private (int Offset, int Length)? _scanRegion;

    // The occurrence found last (upstream's selection, with the caret at its end) and upstream's "highlight all" markers,
    // as offsets in the shown text; the lines as shown before they were marked.
    private (int Offset, int Length)? _found;
    private IReadOnlyList<(int Offset, int Length)> _highlighted = [];
    private IReadOnlyList<DiffLineItem> _unmarkedLines = [];

    // Completed when the next lines show (or the next error), for the search that moved to another file.
    private TaskCompletionSource? _linesShown;

    /// <summary>
    ///  Upstream's find window of this viewer, once Find opened it; for tests.
    /// </summary>
    public FindWindow? FindWindow => _findWindow;

    /// <summary>
    ///  Upstream's <c>SetFileLoader</c> (only RevisionDiffControl sets it): selects the list's next file (the previous one
    ///  searching backward), around at the ends, whose diff the viewer then shows; the file's list item, or null when there
    ///  is none.
    /// </summary>
    public Func<bool, object?>? NextFile { get; set; }

    private Task NextLinesShownAsync()
    {
        _linesShown?.TrySetResult();
        TaskCompletionSource shown = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _linesShown = shown;
        return shown.Task;
    }

    /// <summary>
    ///  The viewer a key went to, when the focus is in one.
    /// </summary>
    public static DiffView? Focused(Avalonia.Input.KeyEventArgs e)
        => (e.Source as Avalonia.Visual)?.FindAncestorOfType<DiffView>(includeSelf: true);

    private void WireFind()
    {
        FindMenuItem.Click += (_, _) => Find();
        GoToLineMenuItem.Click += (_, _) => UiActions.Run(GoToLineAsync, ex => ShowError(ex.Message));
    }

    /// <summary>
    ///  Upstream's <c>Find</c> (<c>FindAndReplaceForm.ShowFor</c>): the find window over this viewer's window. Lines selected
    ///  over more than one line are the region the search keeps to; upstream's text selected in one line or the word at
    ///  the caret has no counterpart here, so the last text to find stays.
    /// </summary>
    public void Find()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        FindWindow window = _findWindow ??= NewFindWindow();
        IReadOnlyList<int> selected = [.. DiffList.Selection.SelectedIndexes.Order()];
        _scanRegion = selected is [var first, .., var last] && last > first
            ? (LineStart(first), LineStart(last) + _unmarkedLines[last].Text.Length - LineStart(first))
            : null;
        window.ShowFor(owner, _syntaxPath, _scanRegion is not null);
    }

    /// <summary>
    ///  Upstream's <c>FindNextAsync</c>: the next (or previous) occurrence from the caret, wrapping around; the message when
    ///  none is found (or when there is nothing to look for). True when one was found.
    /// </summary>
    public async Task<bool> FindNextAsync(bool viaF3, bool searchBackward, string? messageIfNotFound)
    {
        FindWindow window = _findWindow ??= NewFindWindow();
        if (window.LookFor.Length == 0)
        {
            await ShowMessageAsync(window, UpstreamTranslation.ByText(NoSearchText), window.Title ?? "");
            return false;
        }

        // Upstream's loop: with a file loader (the Diff tab's list) and no region to keep to, a search that wraps around
        // while the window shows goes on in the list's next file, until it is back at the first file it moved to.
        object? startItem = null;
        object? currentItem = null;
        (int Offset, int Length)? found;
        do
        {
            int caret = Caret();

            // A search with F3 outside the region the search kept to leaves the region.
            if (viaF3 && _scanRegion is { } region && (caret < region.Offset || caret > region.Offset + region.Length))
            {
                _scanRegion = null;
                window.SetSelectionOnly(false);
            }

            TextSearch search = NewSearch(window);
            int startFrom = caret - (searchBackward ? 1 : 0);
            if (startFrom == -1)
            {
                startFrom = search.EndOffset;
            }

            bool isMultiFileSearch = NextFile is not null && _scanRegion is null;
            found = search.FindNext(startFrom, searchBackward, out bool loopedAround);
            if (found is { } range && (!loopedAround || !isMultiFileSearch))
            {
                SelectFound(range);
            }
            else if (isMultiFileSearch)
            {
                found = null;
                if (!window.IsVisible)
                {
                    break;
                }

                if (currentItem is not null && startItem is null)
                {
                    startItem = currentItem;
                }

                Task shown = NextLinesShownAsync();
                if (NextFile!(searchBackward) is not { } selectedItem)
                {
                    break;
                }

                currentItem = selectedItem;
                await shown;
            }
            else
            {
                break;
            }
        }
        while (found is null && !Equals(startItem, currentItem) && currentItem is not null);

        if (found is not null)
        {
            return true;
        }

        if (messageIfNotFound is not null)
        {
            await ShowMessageAsync(window, UpstreamTranslation.ByText(messageIfNotFound), NoCaption);
        }

        return false;
    }

    /// <summary>
    ///  Upstream's <c>GoToLine</c>: the first line at or after <paramref name="lineNumber"/> of the file (the new file's
    ///  number in a diff), selected and scrolled to.
    /// </summary>
    public void GoToLine(int lineNumber)
    {
        if (_unmarkedLines.Count == 0)
        {
            return;
        }

        int index = 0;
        if (!_textIsDiff)
        {
            index = Math.Clamp(lineNumber - 1, 0, _unmarkedLines.Count - 1);
        }
        else
        {
            for (int i = 0; i < _unmarkedLines.Count; i++)
            {
                if (Number(_unmarkedLines[i].NewNumber) is { } number && number >= lineNumber)
                {
                    index = i;
                    break;
                }
            }
        }

        DiffList.SelectedIndex = index;
        DiffList.ScrollIntoView(index);
    }

    /// <summary>
    ///  Upstream's <c>MaxLineNumber</c>: the number of lines of a file's text, the highest line number of a diff.
    /// </summary>
    public int MaxLineNumber
        => !_textIsDiff
            ? _unmarkedLines.Count
            : _unmarkedLines.SelectMany(line => new[] { Number(line.OldNumber), Number(line.NewNumber) })
                .Max(number => number ?? 0);

    // Upstream's goToLineToolStripMenuItem_Click: FormGoToLine with the last line number, then the line.
    private async Task GoToLineAsync()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        GoToLineWindow window = new();
        window.SetMaxLineNumber(MaxLineNumber);
        if (await window.ShowAsync(owner) is { } lineNumber)
        {
            GoToLine(lineNumber);
        }
    }

    // A new view of lines drops the find's marks, as upstream's markers belong to the document shown.
    private void ResetFind()
    {
        _unmarkedLines = Lines;
        _found = null;
        _highlighted = [];
        _linesShown?.TrySetResult();
    }

    private FindWindow NewFindWindow()
    {
        FindWindow window = new();
        window.FindNextRequested += (_, backward) => UiActions.Run(
            () => FindNextAsync(viaF3: false, backward, TextNotFoundText), ex => ShowError(ex.Message));
        window.HighlightAllRequested += (_, _) => UiActions.Run(() => HighlightAllAsync(window),
            ex => ShowError(ex.Message));

        // Upstream's FormClosing discards the search region.
        window.Hidden += (_, _) => _scanRegion = null;
        return window;
    }

    // Upstream's btnHighlightAll_Click: every occurrence marked, or the marks cleared for an empty text; the window closes
    // when there are some.
    private async Task HighlightAllAsync(FindWindow window)
    {
        if (window.LookFor.Length == 0)
        {
            _highlighted = [];
            ShowMarks();
            return;
        }

        _highlighted = NewSearch(window).FindAll();
        ShowMarks();
        if (_highlighted.Count == 0)
        {
            await ShowMessageAsync(window, UpstreamTranslation.ByText(SearchTextNotFoundText),
                UpstreamTranslation.ByText(NotFoundText));
        }
        else
        {
            window.Close();
        }
    }

    private TextSearch NewSearch(FindWindow window)
        => new(string.Join('\n', _unmarkedLines.Select(line => line.Text)))
        {
            LookFor = window.LookFor,
            MatchCase = window.MatchCase,
            MatchWholeWordOnly = window.MatchWholeWord,
            ScanRegion = _scanRegion,
        };

    // Upstream's caret: after the occurrence found last while its line is still the selected one, else at the start of
    // the selected line (the top without one).
    private int Caret()
    {
        int selected = DiffList.SelectedIndex;
        if (_found is { } found && LineOf(found.Offset) == selected)
        {
            return found.Offset + found.Length;
        }

        return selected >= 0 && selected < _unmarkedLines.Count ? LineStart(selected) : 0;
    }

    // Upstream's SelectResult: the occurrence selected and scrolled to; here its line is selected and it is marked.
    private void SelectFound((int Offset, int Length) found)
    {
        _found = found;
        ShowMarks();
        int line = LineOf(found.Offset);
        DiffList.SelectedIndex = line;
        DiffList.ScrollIntoView(line);
    }

    // The lines with the occurrences marked in upstream's highlight color (Color.Yellow.AdaptBackColor()).
    private void ShowMarks()
    {
        int selected = DiffList.SelectedIndex;
        IBrush brush = new SolidColorBrush(ToAvalonia(System.Drawing.Color.Yellow.AdaptBackColor()));
        List<(int Offset, int Length)> marks = [.. _highlighted];
        if (_found is { } found)
        {
            marks.Add(found);
        }

        List<DiffLineItem> lines = [.. _unmarkedLines];
        int start = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            int end = start + lines[i].Text.Length;
            List<(int Start, int Length)> inLine = [];
            foreach ((int offset, int length) in marks)
            {
                int from = Math.Max(offset, start);
                int to = Math.Min(offset + length, end);
                if (to > from)
                {
                    inLine.Add((from - start, to - from));
                }
            }

            if (inLine.Count > 0)
            {
                lines[i] = Marked(lines[i], inLine, brush);
            }

            start = end + 1;
        }

        Lines = lines;
        DiffList.ItemsSource = Lines;
        DiffList.SelectedIndex = selected;
    }

    // The line's runs (its syntax colors, else the whole text) split at the marks, the marked parts on the brush.
    private static DiffLineItem Marked(DiffLineItem line, IReadOnlyList<(int Start, int Length)> marks, IBrush brush)
    {
        bool[] marked = new bool[line.Text.Length];
        foreach ((int start, int length) in marks)
        {
            Array.Fill(marked, true, start, length);
        }

        IReadOnlyList<DiffTextRun> runs =
            line.Runs is { } own && string.Concat(own.Select(run => run.Text)) == line.Text
                ? own
                : [new DiffTextRun(line.Text, null, false, false)];
        List<DiffTextRun> result = [];
        int position = 0;
        foreach (DiffTextRun run in runs)
        {
            int partStart = 0;
            for (int i = 1; i <= run.Text.Length; i++)
            {
                if (i == run.Text.Length || marked[position + i] != marked[position + partStart])
                {
                    result.Add(run with
                    {
                        Text = run.Text[partStart..i],
                        Background = marked[position + partStart] ? brush : run.Background
                    });
                    partStart = i;
                }
            }

            position += run.Text.Length;
        }

        return line with { Runs = result };
    }

    private int LineStart(int index)
    {
        int start = 0;
        for (int i = 0; i < index; i++)
        {
            start += _unmarkedLines[i].Text.Length + 1;
        }

        return start;
    }

    private int LineOf(int offset)
    {
        int start = 0;
        for (int i = 0; i < _unmarkedLines.Count; i++)
        {
            int next = start + _unmarkedLines[i].Text.Length + 1;
            if (offset < next)
            {
                return i;
            }

            start = next;
        }

        return Math.Max(0, _unmarkedLines.Count - 1);
    }

    private static int? Number(string text)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number : null;

    private static Color ToAvalonia(System.Drawing.Color color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    // Upstream's MessageBoxes.Show over the find window when it shows, else over the viewer's window.
    private async Task ShowMessageAsync(FindWindow window, string text, string caption)
    {
        Window? owner = window.IsVisible ? window : TopLevel.GetTopLevel(this) as Window;
        if (owner is not null)
        {
            await new MessageBoxWindow(text, caption, MessageBoxButtons.OK).ShowDialog(owner);
        }
    }
}
