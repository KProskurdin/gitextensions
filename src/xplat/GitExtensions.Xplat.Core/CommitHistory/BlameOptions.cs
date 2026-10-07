using System.Globalization;
using System.Text;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Upstream's blame settings (the Blame viewer page and the blame menu of its file history window), with upstream's
///  defaults: how git blames (<c>IgnoreWhitespaceOnBlame</c>, <c>DetectCopyInFileOnBlame</c>, <c>DetectCopyInAllOnBlame</c>)
///  and what each commit's line in the gutter shows (<c>Blame.*</c>).
/// </summary>
public sealed record BlameOptions(
    bool IgnoreWhitespace = true,
    bool DetectMoveInFile = false,
    bool DetectMoveInAllFiles = false,
    bool DisplayAuthorFirst = false,
    bool ShowAuthor = true,
    bool ShowAuthorDate = true,
    bool ShowAuthorTime = true,
    bool ShowLineNumbers = false,
    bool ShowOriginalFilePath = true)
{
    /// <summary>
    ///  The <c>git blame</c> flags upstream's <c>GitModule.Blame</c> adds: <c>-M</c>, <c>-C</c> and <c>-w</c>.
    /// </summary>
    public IReadOnlyList<string> Arguments
        =>
        [
            .. DetectMoveInFile ? ["-M"] : Array.Empty<string>(),
            .. DetectMoveInAllFiles ? ["-C"] : Array.Empty<string>(),
            .. IgnoreWhitespace ? ["-w"] : Array.Empty<string>(),
        ];

    // Upstream's menu rules: the line always shows the author or the date, and the time only goes with the date.
    public BlameOptions ToggleShowAuthor()
        => this with { ShowAuthor = !ShowAuthor, ShowAuthorDate = ShowAuthorDate || ShowAuthor };

    public BlameOptions ToggleShowAuthorDate()
        => this with { ShowAuthorDate = !ShowAuthorDate, ShowAuthor = ShowAuthor || ShowAuthorDate };

    /// <summary>
    ///  Upstream's <c>BlameControl.BuildAuthorLine</c>, without its padding: author and date in the chosen order, then the
    ///  file the line came from when it is another file. The date is the culture's short date, with its short time when
    ///  chosen.
    /// </summary>
    public string AuthorLine(BlameLine line, string blamedFile, CultureInfo culture)
    {
        string dateFormat = ShowAuthorTime
            ? culture.DateTimeFormat.ShortDatePattern + " " + culture.DateTimeFormat.ShortTimePattern
            : culture.DateTimeFormat.ShortDatePattern;
        StringBuilder text = new();
        if (ShowAuthor && DisplayAuthorFirst)
        {
            text.Append(line.Author);
            if (ShowAuthorDate)
            {
                text.Append(" - ");
            }
        }

        if (ShowAuthorDate)
        {
            text.Append(line.AuthorTime.ToString(dateFormat, culture));
        }

        if (ShowAuthor && !DisplayAuthorFirst)
        {
            if (ShowAuthorDate)
            {
                text.Append(" - ");
            }

            text.Append(line.Author);
        }

        if (ShowOriginalFilePath && line.FileName.Length > 0 && blamedFile != line.FileName)
        {
            text.Append(" - ").Append(line.FileName);
        }

        return text.ToString();
    }

    /// <summary>
    ///  The blame view's rows: as upstream's gutter, a commit's line and hash are shown on the first line of each run of
    ///  lines from that commit, and left empty on the rest.
    /// </summary>
    public IReadOnlyList<BlameRow> Rows(IReadOnlyList<BlameLine> lines, string blamedFile, CultureInfo culture)
    {
        List<BlameRow> rows = new(lines.Count);
        string? previous = null;
        foreach (BlameLine line in lines)
        {
            bool first = line.Hash != previous;
            rows.Add(new BlameRow(line, first ? line.Hash : "", first ? AuthorLine(line, blamedFile, culture) : "",
                ShowLineNumbers ? line.LineNumber.ToString(CultureInfo.InvariantCulture) : ""));
            previous = line.Hash;
        }

        return rows;
    }
}

/// <summary>
///  A row of the blame view: the line, the commit hash and author line shown beside it (empty when the line above is from
///  the same commit), and its number when line numbers are shown.
/// </summary>
public sealed record BlameRow(BlameLine Line, string Hash, string AuthorLine, string LineNumberText)
{
    public string Content => Line.Content;
}
