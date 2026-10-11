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
    bool ShowOriginalFilePath = true,
    bool ShowAuthorAvatar = true)
{
    /// <summary>
    ///  The number of upstream's age colors (<c>BlameControl.GetAgeBucketGradientColors</c>), newest last.
    /// </summary>
    public const int AgeBucketCount = 7;

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
    public IReadOnlyList<BlameRow> Rows(IReadOnlyList<BlameLine> lines, string blamedFile, CultureInfo culture,
        DateTime? now = null)
    {
        List<BlameRow> rows = new(lines.Count);
        IReadOnlyList<int> ages = AgeBuckets(lines, now ?? DateTime.Now);
        string? previous = null;
        for (int i = 0; i < lines.Count; i++)
        {
            BlameLine line = lines[i];
            bool first = line.Hash != previous;
            rows.Add(new BlameRow(line, first ? line.Hash : "", first ? AuthorLine(line, blamedFile, culture) : "",
                ShowLineNumbers ? line.LineNumber.ToString(CultureInfo.InvariantCulture) : "", ages[i],
                AvatarColumn: ShowAuthorAvatar, ShowAvatar: ShowAuthorAvatar && first));
            previous = line.Hash;
        }

        return rows;
    }

    /// <summary>
    ///  Upstream's <c>BlameControl.CalculateBlameGutterData</c>: the time from the oldest line (at least three years back)
    ///  to now is cut into <see cref="AgeBucketCount"/> equal parts, and each line gets the part its author time falls in.
    /// </summary>
    public static IReadOnlyList<int> AgeBuckets(IReadOnlyList<BlameLine> lines, DateTime now)
    {
        DateTime artificialOldBoundary = now.AddYears(-3);
        long mostRecent = now.Ticks;
        long lessRecent = Math.Min(artificialOldBoundary.Ticks,
            lines.Select(line => line.AuthorTime).Where(time => time != DateTime.MinValue)
                .DefaultIfEmpty(artificialOldBoundary).Min().Ticks);
        long intervalSize = (mostRecent - lessRecent + 1) / AgeBucketCount;
        return
        [
            .. lines.Select(line => Math.Min((int)(Math.Max(0, line.AuthorTime.Ticks - lessRecent) / intervalSize),
                AgeBucketCount - 1))
        ];
    }
}

/// <summary>
///  A row of the blame view: the line, the commit hash and author line shown beside it (empty when the line above is from
///  the same commit), its number when line numbers are shown, its age color (upstream's age bucket, 0 the oldest) and,
///  as upstream's gutter, the author's avatar on the first line of a commit's run when avatars are shown.
/// </summary>
public sealed record BlameRow(BlameLine Line, string Hash, string AuthorLine, string LineNumberText, int AgeBucket = 0,
    bool AvatarColumn = false, bool ShowAvatar = false)
{
    public string Content => Line.Content;
}
