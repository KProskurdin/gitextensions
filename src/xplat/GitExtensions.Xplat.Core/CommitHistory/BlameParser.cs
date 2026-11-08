using System.Globalization;
using System.Text;
using GitCommands;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  A blamed line: its number, the commit (short hash), author and day that last changed it, its text, and the author's
///  time and the path the line had in that commit (git's "filename"; another path when the file was renamed or the line
///  moved from another file).
/// </summary>
public sealed record BlameLine(int LineNumber, string Hash, string Author, string Date, string Content,
    DateTime AuthorTime = default, string FileName = "", string AuthorEmail = "")
{
    /// <summary>
    ///  Upstream's <c>GitBlameLine.Commit</c>: the whole commit, with the full hash <see cref="Hash"/> shortens; null for a
    ///  line that was not parsed from git.
    /// </summary>
    public GitBlameCommit? Commit { get; init; }

    /// <summary>
    ///  Upstream's <c>GitBlameLine.OriginLineNumber</c>: the line's number in <see cref="Commit"/>, which is where blaming
    ///  that commit shows it.
    /// </summary>
    public int OriginLineNumber { get; init; }
}

/// <summary>
///  Reads the output of <c>git blame --line-porcelain</c>, where every line carries its own commit headers.
/// </summary>
public static class BlameParser
{
    private const string DateFormat = "yyyy-MM-dd";
    private const int FullHashLength = 40;
    private const int ShortHashLength = 8;

    /// <summary>
    ///  Reads <paramref name="porcelain"/>. With encodings it is git's output read losslessly (<see cref="GitModule.LosslessEncoding"/>),
    ///  as upstream reads blame: a file's lines are in the file's encoding and the commit headers in the log's, which can differ.
    /// </summary>
    public static IReadOnlyList<BlameLine> Parse(string porcelain, Encoding? filesEncoding = null, Encoding? logEncoding = null)
    {
        List<BlameLine> lines = [];
        ObjectId? objectId = null;
        string hash = "";
        string author = "";
        string email = "";
        string rawEmail = "";
        string authorTimeZone = "";
        string committer = "";
        string committerEmail = "";
        string committerTimeZone = "";
        string summary = "";
        string date = "";
        DateTime time = default;
        DateTime committerTime = default;
        string fileName = "";
        int lineNumber = 0;
        int originLineNumber = 0;

        foreach (string raw in porcelain.Split('\n'))
        {
            string line = Decode(raw.TrimEnd('\r'), raw.StartsWith('\t') ? filesEncoding : logEncoding);
            if (line.StartsWith('\t'))
            {
                lines.Add(new BlameLine(lineNumber, hash, author, date, line[1..], time, fileName, email)
                {
                    Commit = objectId is { } commitId
                        ? new GitBlameCommit(commitId, author, rawEmail, time, authorTimeZone, committer,
                            committerEmail, committerTime, committerTimeZone, summary, fileName)
                        : null,
                    OriginLineNumber = originLineNumber,
                });
            }
            else if (line.StartsWith("author ", StringComparison.Ordinal))
            {
                author = line["author ".Length..];
            }
            else if (line.StartsWith("author-mail ", StringComparison.Ordinal))
            {
                rawEmail = line["author-mail ".Length..];
                email = rawEmail.Trim('<', '>');
            }
            else if (line.StartsWith("author-time ", StringComparison.Ordinal))
            {
                long seconds = long.Parse(line["author-time ".Length..], CultureInfo.InvariantCulture);
                time = DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime;
                date = time.ToString(DateFormat, CultureInfo.InvariantCulture);
            }
            else if (line.StartsWith("author-tz ", StringComparison.Ordinal))
            {
                authorTimeZone = line["author-tz ".Length..];
            }
            else if (line.StartsWith("committer ", StringComparison.Ordinal))
            {
                committer = line["committer ".Length..];
            }
            else if (line.StartsWith("committer-mail ", StringComparison.Ordinal))
            {
                committerEmail = line["committer-mail ".Length..];
            }
            else if (line.StartsWith("committer-time ", StringComparison.Ordinal))
            {
                long seconds = long.Parse(line["committer-time ".Length..], CultureInfo.InvariantCulture);
                committerTime = DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime;
            }
            else if (line.StartsWith("committer-tz ", StringComparison.Ordinal))
            {
                committerTimeZone = line["committer-tz ".Length..];
            }
            else if (line.StartsWith("summary ", StringComparison.Ordinal))
            {
                summary = line["summary ".Length..];
            }
            else if (line.StartsWith("filename ", StringComparison.Ordinal))
            {
                fileName = line["filename ".Length..];
            }
            else if (IsGroupHeader(line))
            {
                string[] fields = line.Split(' ');
                objectId = ObjectId.TryParse(fields[0], out ObjectId id) ? id : null;
                hash = fields[0][..ShortHashLength];
                originLineNumber = int.Parse(fields[1], CultureInfo.InvariantCulture);
                lineNumber = int.Parse(fields[2], CultureInfo.InvariantCulture);
            }
        }

        return lines;
    }

    private static bool IsGroupHeader(string line)
        => line.Length > FullHashLength && line[FullHashLength] == ' ' && line[..FullHashLength].All(Uri.IsHexDigit);

    private static string Decode(string line, Encoding? encoding)
        => encoding is null ? line : encoding.GetString(GitModule.LosslessEncoding.GetBytes(line));
}
