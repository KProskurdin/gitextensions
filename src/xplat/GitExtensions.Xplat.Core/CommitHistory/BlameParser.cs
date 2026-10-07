using System.Globalization;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  A blamed line: its number, the commit (short hash), author and day that last changed it, its text, and the author's
///  time and the path the line had in that commit (git's "filename"; another path when the file was renamed or the line
///  moved from another file).
/// </summary>
public sealed record BlameLine(int LineNumber, string Hash, string Author, string Date, string Content,
    DateTime AuthorTime = default, string FileName = "");

/// <summary>
///  Reads the output of <c>git blame --line-porcelain</c>, where every line carries its own commit headers.
/// </summary>
public static class BlameParser
{
    private const string DateFormat = "yyyy-MM-dd";
    private const int FullHashLength = 40;
    private const int ShortHashLength = 8;

    public static IReadOnlyList<BlameLine> Parse(string porcelain)
    {
        List<BlameLine> lines = [];
        string hash = "";
        string author = "";
        string date = "";
        DateTime time = default;
        string fileName = "";
        int lineNumber = 0;

        foreach (string raw in porcelain.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.StartsWith('\t'))
            {
                lines.Add(new BlameLine(lineNumber, hash, author, date, line[1..], time, fileName));
            }
            else if (line.StartsWith("author ", StringComparison.Ordinal))
            {
                author = line["author ".Length..];
            }
            else if (line.StartsWith("author-time ", StringComparison.Ordinal))
            {
                long seconds = long.Parse(line["author-time ".Length..], CultureInfo.InvariantCulture);
                time = DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime;
                date = time.ToString(DateFormat, CultureInfo.InvariantCulture);
            }
            else if (line.StartsWith("filename ", StringComparison.Ordinal))
            {
                fileName = line["filename ".Length..];
            }
            else if (IsGroupHeader(line))
            {
                string[] fields = line.Split(' ');
                hash = fields[0][..ShortHashLength];
                lineNumber = int.Parse(fields[2], CultureInfo.InvariantCulture);
            }
        }

        return lines;
    }

    private static bool IsGroupHeader(string line)
        => line.Length > FullHashLength && line[FullHashLength] == ' ' && line[..FullHashLength].All(Uri.IsHexDigit);
}
