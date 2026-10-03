namespace GitExtensions.Xplat.Core.Diff;

public enum DiffLineKind
{
    Context,
    Added,
    Removed,
    Hunk,
    Header,
}

public sealed record DiffLine(string Text, DiffLineKind Kind);

/// <summary>
///  Classifies the lines of a unified diff so a view can color them.
/// </summary>
public static class DiffParser
{
    private static readonly string[] _headerPrefixes =
    [
        "diff ", "index ", "new file", "deleted file", "old mode", "new mode", "similarity index", "rename ", "copy ",
    ];

    public static IReadOnlyList<DiffLine> Parse(string diff)
    {
        List<DiffLine> lines = [];
        foreach (string text in diff.Split('\n'))
        {
            string line = text.TrimEnd('\r');
            lines.Add(new DiffLine(line, Classify(line)));
        }

        if (lines.Count > 0 && lines[^1].Text.Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
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
