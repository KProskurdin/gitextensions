using System.Globalization;
using System.Text.RegularExpressions;

namespace GitExtensions.Xplat.Core.Diff;

public enum DiffLineKind
{
    Context,
    Added,
    Removed,
    Hunk,
    Header,
}

/// <summary>
///  One line of a diff. <see cref="OldNumber"/> and <see cref="NewNumber"/> are the line's numbers in the old and new file;
///  an added line has no old number, a removed line no new number, and header lines have neither.
/// </summary>
public sealed record DiffLine(string Text, DiffLineKind Kind, int? OldNumber = null, int? NewNumber = null)
{
    /// <summary>
    ///  How the line ended in git's text, which <see cref="Text"/> leaves out, for the viewer's non-printing marks.
    /// </summary>
    public LineEnding Ending { get; init; } = LineEnding.LineFeed;

    /// <summary>
    ///  The line's parts in git's colors (<see cref="GitColoring"/>); null when git's colors were not asked for, and then the
    ///  viewer colors the line by its kind.
    /// </summary>
    public IReadOnlyList<DiffColorRun>? ColorRuns { get; init; }

    /// <summary>
    ///  The parts this changed line shares with the one it replaces, in upstream's dimmed colors (<see cref="InlineDifferences"/>).
    /// </summary>
    public IReadOnlyList<DiffColorRun>? DimRuns { get; init; }

    /// <summary>
    ///  Upstream's <c>IsMovedLine</c>: git's colors say the line was moved, so it has no line that it replaces.
    /// </summary>
    public bool IsMovedLine { get; init; }

    /// <summary>
    ///  <see cref="Ending"/> of the raw line <paramref name="raw"/> that a newline followed.
    /// </summary>
    public static LineEnding EndingOf(string raw)
        => raw.EndsWith('\r') ? LineEnding.CarriageReturnLineFeed : LineEnding.LineFeed;
}

/// <summary>
///  How a line of text ended (upstream's text editor's <c>EolMarker</c>).
/// </summary>
public enum LineEnding
{
    LineFeed,
    CarriageReturnLineFeed,
    None,
}

/// <summary>
///  Classifies the lines of a unified diff so a view can color them, and numbers the lines of each hunk.
/// </summary>
public static partial class DiffParser
{
    private static readonly string[] _headerPrefixes =
    [
        "diff ", "index ", "new file", "deleted file", "old mode", "new mode", "similarity index", "rename ", "copy ",
    ];

    // "@@ -12,7 +12,8 @@ optional section heading"; a count of 1 may be left out.
    [GeneratedRegex(@"^@@ -(?<oldStart>\d+)(,(?<oldCount>\d+))? \+(?<newStart>\d+)(,(?<newCount>\d+))? @@",
        RegexOptions.ExplicitCapture)]
    private static partial Regex HunkHeaderRegex { get; }

    // Git's "\ No newline at end of file" line.
    private const char NoNewlineMarker = '\\';

    // The merge's range of a combined diff's "@@@ -1,2 -1,3 +1,4 @@@".
    [GeneratedRegex(@"^@@@+ (-\d+(,\d+)? )+\+(?<start>\d+)", RegexOptions.ExplicitCapture)]
    private static partial Regex CombinedRangeRegex { get; }

    public static IReadOnlyList<DiffLine> Parse(string diff)
    {
        List<DiffLine> lines = [];
        int oldNumber = 0;
        int newNumber = 0;
        int oldLeft = 0;
        int newLeft = 0;

        foreach (string text in diff.Split('\n'))
        {
            string line = text.TrimEnd('\r');

            // Inside a hunk the first character alone decides, so a removed "-- comment" is not taken for a "--- " header.
            if (oldLeft > 0 || newLeft > 0)
            {
                switch (line.Length == 0 ? ' ' : line[0])
                {
                    case '+':
                        lines.Add(new DiffLine(line, DiffLineKind.Added, NewNumber: newNumber++) { Ending = DiffLine.EndingOf(text) });
                        newLeft--;
                        continue;
                    case '-':
                        lines.Add(new DiffLine(line, DiffLineKind.Removed, OldNumber: oldNumber++) { Ending = DiffLine.EndingOf(text) });
                        oldLeft--;
                        continue;
                    case ' ':
                        lines.Add(new DiffLine(line, DiffLineKind.Context, oldNumber++, newNumber++) { Ending = DiffLine.EndingOf(text) });
                        oldLeft--;
                        newLeft--;
                        continue;
                    case '\\':
                        // "\ No newline at end of file" belongs to the line before it.
                        lines.Add(new DiffLine(line, DiffLineKind.Header));
                        continue;
                }

                // Anything else ends a hunk whose counts did not match its lines.
                oldLeft = newLeft = 0;
            }

            DiffLineKind kind = Classify(line);
            if (kind == DiffLineKind.Hunk && HunkHeaderRegex.Match(line) is { Success: true } hunk)
            {
                oldNumber = int.Parse(hunk.Groups["oldStart"].Value, CultureInfo.InvariantCulture);
                newNumber = int.Parse(hunk.Groups["newStart"].Value, CultureInfo.InvariantCulture);
                oldLeft = Count(hunk.Groups["oldCount"]);
                newLeft = Count(hunk.Groups["newCount"]);
            }

            lines.Add(new DiffLine(line, kind));
        }

        if (lines.Count > 0 && lines[^1].Text.Length == 0 &&
            lines[^1].Kind is DiffLineKind.Context or DiffLineKind.Header)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;

        static int Count(Group group) => group.Success ? int.Parse(group.Value, CultureInfo.InvariantCulture) : 1;
    }

    /// <summary>
    ///  Classifies the lines of git's combined diff of a merge (<c>diff-tree --cc</c>): after the header, each line starts
    ///  with one column per parent, as upstream's <c>DiffLineNumAnalyzer</c> reads them (any '+' adds, else any '-'
    ///  removes); the merge's lines are numbered from each "@@@" hunk header's last range.
    /// </summary>
    public static IReadOnlyList<DiffLine> ParseCombined(string diff)
    {
        List<DiffLine> lines = [];
        int columns = 0;
        int newNumber = 0;
        foreach (string text in diff.Split('\n'))
        {
            string line = text.TrimEnd('\r');
            if (line.StartsWith("@@@", StringComparison.Ordinal))
            {
                // "@@@ -a,b -c,d +e,f @@@": as many '@' as parents and one, the merge's range last.
                int ats = line.TakeWhile(c => c == '@').Count();
                columns = ats - 1;
                Match range = CombinedRangeRegex.Match(line);
                newNumber = range.Success ? int.Parse(range.Groups["start"].Value, CultureInfo.InvariantCulture) : 1;
                lines.Add(new DiffLine(line, DiffLineKind.Hunk));
                continue;
            }

            if (columns == 0 || line.Length < columns || line.StartsWith(NoNewlineMarker))
            {
                lines.Add(new DiffLine(line, columns == 0 ? DiffLineKind.Header : DiffLineKind.Context));
                continue;
            }

            ReadOnlySpan<char> markers = line.AsSpan(0, columns);
            if (markers.Contains('+'))
            {
                lines.Add(new DiffLine(line, DiffLineKind.Added, NewNumber: newNumber++));
            }
            else if (markers.Contains('-'))
            {
                lines.Add(new DiffLine(line, DiffLineKind.Removed));
            }
            else
            {
                lines.Add(new DiffLine(line, DiffLineKind.Context, NewNumber: newNumber++));
            }
        }

        if (lines.Count > 0 && lines[^1].Text.Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    /// <summary>
    ///  A file's content as numbered context lines, so a diff view can show a whole file.
    /// </summary>
    public static IReadOnlyList<DiffLine> ParseText(string content)
    {
        string[] texts = content.Split('\n');
        int count = texts.Length > 0 && texts[^1].Length == 0 ? texts.Length - 1 : texts.Length;
        List<DiffLine> lines = new(count);
        for (int i = 0; i < count; i++)
        {
            lines.Add(new DiffLine(texts[i].TrimEnd('\r'), DiffLineKind.Context, NewNumber: i + 1)
            {
                // The text after the last newline, when there is any, ends with nothing.
                Ending = i == texts.Length - 1 ? LineEnding.None : DiffLine.EndingOf(texts[i])
            });
        }

        return lines;
    }

    private static DiffLineKind Classify(string line)
    {
        if (line.StartsWith("+++ ", StringComparison.Ordinal) || line.StartsWith("--- ", StringComparison.Ordinal))
        {
            return DiffLineKind.Header;
        }

        if (line.StartsWith("@@", StringComparison.Ordinal))
        {
            return DiffLineKind.Hunk;
        }

        if (line.StartsWith('+'))
        {
            return DiffLineKind.Added;
        }

        if (line.StartsWith('-'))
        {
            return DiffLineKind.Removed;
        }

        return _headerPrefixes.Any(prefix => line.StartsWith(prefix, StringComparison.Ordinal))
            ? DiffLineKind.Header
            : DiffLineKind.Context;
    }
}

/// <summary>
///  Moves between the changes of a diff, as upstream's FileViewer "next change" and "previous change" do: a change is a run
///  of added or removed lines, and the move lands on its first line.
/// </summary>
public static class DiffNavigation
{
    /// <summary>
    ///  The first line of the next change after <paramref name="from"/> (<paramref name="forward"/>) or of the change before
    ///  the one <paramref name="from"/> is in; null when there is none. <paramref name="from"/> is -1 when nothing is selected.
    /// </summary>
    public static int? FindChange(IReadOnlyList<DiffLine> lines, int from, bool forward)
    {
        if (forward)
        {
            for (int i = Math.Max(from + 1, 0); i < lines.Count; i++)
            {
                if (IsChangeStart(lines, i))
                {
                    return i;
                }
            }

            return null;
        }

        int start = from < 0 ? lines.Count : from;
        for (int i = Math.Min(start, lines.Count) - 1; i >= 0; i--)
        {
            if (IsChangeStart(lines, i))
            {
                return i;
            }
        }

        return null;
    }

    private static bool IsChangeStart(IReadOnlyList<DiffLine> lines, int index)
        => IsChange(lines[index]) && (index == 0 || !IsChange(lines[index - 1]));

    private static bool IsChange(DiffLine line) => line.Kind is DiffLineKind.Added or DiffLineKind.Removed;
}
