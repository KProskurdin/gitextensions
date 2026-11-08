using System.Text;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  The editing half of upstream's <c>FindAndReplaceForm</c>, and how its <c>ShowFor</c> reads the editor's selection.
/// </summary>
public static class TextReplace
{
    /// <summary>
    ///  Upstream's <c>btnReplace_Click</c>: the selection is replaced only when it is the text looked for, whatever
    ///  <c>Match case</c> says.
    /// </summary>
    public static bool MatchesSelection(string selected, string lookFor)
        => string.Equals(selected, lookFor, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///  Upstream's <c>btnReplaceAll_Click</c>: each occurrence once, so a replacement holding the text looked for ("red" to
    ///  "very red") is not replaced again.
    /// </summary>
    public static (string Text, int Count) ReplaceAll(string text, TextSearch search, string replacement)
    {
        IReadOnlyList<(int Offset, int Length)> found = search.FindAll();
        StringBuilder result = new(text.Length);
        int copied = 0;
        foreach ((int offset, int length) in found)
        {
            result.Append(text, copied, offset - copied).Append(replacement);
            copied = offset + length;
        }

        result.Append(text, copied, text.Length - copied);
        return (result.ToString(), found.Count);
    }

    /// <summary>
    ///  Upstream's <c>ShowFor</c>: a selection within one line is the text to look for, while one over several lines is the
    ///  region the search keeps to; without a selection the word at the caret is looked for. Empty when it finds neither, and
    ///  the last text looked for stays.
    /// </summary>
    public static (string LookFor, (int Offset, int Length)? ScanRegion) ReadSelection(string text, int selectionStart, int selectionEnd)
    {
        int start = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
        int end = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);
        if (start == end)
        {
            return (WordAt(text, start), null);
        }

        string selected = text[start..end];
        return selected.Contains('\n') ? ("", (start, end - start)) : (selected, null);
    }

    /// <summary>
    ///  The 1-based line number at <paramref name="offset"/>.
    /// </summary>
    public static int LineOf(string text, int offset)
    {
        int line = 1;
        for (int i = 0; i < offset && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    /// <summary>
    ///  The offset where 1-based <paramref name="line"/> starts, the end of the text when it has fewer lines.
    /// </summary>
    public static int OffsetOfLine(string text, int line)
    {
        int offset = 0;
        for (int current = 1; current < line; current++)
        {
            int newLine = text.IndexOf('\n', offset);
            if (newLine < 0)
            {
                return text.Length;
            }

            offset = newLine + 1;
        }

        return offset;
    }

    private static string WordAt(string text, int caret)
    {
        int start = caret;
        while (start > 0 && IsWordPart(text[start - 1]))
        {
            start--;
        }

        int end = caret;
        while (end < text.Length && IsWordPart(text[end]))
        {
            end++;
        }

        return text[start..end];
    }

    private static bool IsWordPart(char c) => char.IsLetterOrDigit(c) || c == '_';
}
