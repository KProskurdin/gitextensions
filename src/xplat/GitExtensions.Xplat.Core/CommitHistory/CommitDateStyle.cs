using ResourceManager;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  How the grid shows a commit's date, as upstream's <c>DateColumnProvider</c>: the author date or the commit date
///  (upstream's <c>showauthordate</c>), relative ("3 days ago", upstream's <c>relativedate</c>) or in full.
/// </summary>
public sealed record CommitDateStyle(bool Relative = true, bool AuthorDate = true)
{
    /// <summary>
    ///  Upstream's text: <see cref="LocalizationHelpers.GetRelativeDateString"/> without weeks, or the culture's general date
    ///  and time ("G"); empty for an unknown date.
    /// </summary>
    public string Format(DateTime authorDate, DateTime commitDate, DateTime now)
    {
        DateTime date = AuthorDate ? authorDate : commitDate;
        if (date == DateTime.MinValue || date == DateTime.MaxValue)
        {
            return "";
        }

        return Relative ? LocalizationHelpers.GetRelativeDateString(now, date, displayWeeks: false) : date.ToString("G");
    }
}
