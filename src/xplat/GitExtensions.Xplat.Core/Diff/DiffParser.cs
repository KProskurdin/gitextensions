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
public sealed record DiffLine(string Text, DiffLineKind Kind, int? OldNumber = null, int? NewNumber = null);

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
                        lines.Add(new DiffLine(line, DiffLineKind.Added, NewNumber: newNumber++));
                        newLeft--;
                        continue;
                    case '-':
                        lines.Add(new DiffLine(line, DiffLineKind.Removed, OldNumber: oldNumber++));
                        oldLeft--;
                        continue;
                    case ' ':
                        lines.Add(new DiffLine(line, DiffLineKind.Context, oldNumber++, newNumber++));
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
    ///  A file's content as numbered context lines, so a diff view can show a whole file.
    /// </summary>
    public static IReadOnlyList<DiffLine> ParseText(string content)
    {
        string[] texts = content.Split('\n');
        int count = texts.Length > 0 && texts[^1].Length == 0 ? texts.Length - 1 : texts.Length;
        List<DiffLine> lines = new(count);
        for (int i = 0; i < count; i++)
        {
            lines.Add(new DiffLine(texts[i].TrimEnd('\r'), DiffLineKind.Context, NewNumber: i + 1));
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
