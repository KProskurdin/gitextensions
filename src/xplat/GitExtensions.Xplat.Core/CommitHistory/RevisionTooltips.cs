using System.Text;
using GitCommands;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  The grid's tooltips for a commit, one per column, as upstream's column providers write them; null where upstream shows
///  none.
/// </summary>
public sealed record RevisionTooltip(string? Message, string Author, string Date, string Hash);

/// <summary>
///  Upstream's revision grid tooltips (<c>MessageColumnProvider</c>, <c>AuthorNameColumnProvider</c>,
///  <c>DateColumnProvider</c>, <c>CommitIdColumnProvider</c>, with upstream's <c>ShowRevisionGridTooltips</c> on).
/// </summary>
public static class RevisionTooltips
{
    public const string Authored = "authored";
    public const string Committed = "committed";

    /// <summary>
    ///  Upstream's <c>TranslatedStrings.MarkBisectAsGood</c> and <c>MarkBisectAsBad</c>.
    /// </summary>
    public const string MarkedGood = "Marked as good in bisect";
    public const string MarkedBad = "Marked as bad in bisect";
    public const string AuthoredAndCommitted = "authored and committed";
    public const string BodyNotLoaded =
        "\n\nFull message text is not present in older commits.\nSelect this commit to populate the full message.";

    public static RevisionTooltip For(GitRevision revision, IReadOnlyList<RefLabel> labels)
        => new(Message(revision, labels), Author(revision), Date(revision), revision.Guid);

    // Upstream's AuthorNameColumnProvider.GetAuthorAndCommiterToolTip.
    private static string Author(GitRevision revision)
        => revision.Author == revision.Committer && revision.AuthorEmail == revision.CommitterEmail
            ? $"{revision.Author} <{revision.AuthorEmail}> {AuthoredAndCommitted}"
            : $"{revision.Author} <{revision.AuthorEmail}> {Authored}\n" +
              $"{revision.Committer} <{revision.CommitterEmail}> {Committed}";

    private static string Date(GitRevision revision)
        => revision.Author == revision.Committer && revision.AuthorDate == revision.CommitDate
            ? $"{revision.AuthorDate:g} {revision.Author} {AuthoredAndCommitted}"
            : $"{revision.AuthorDate:g} {revision.Author} {Authored}\n" +
              $"{revision.CommitDate:g} {revision.Committer} {Committed}";

    // Upstream's MessageColumnProvider: the message's summary when it has more than one line or the commit has refs, then
    // the refs in brackets, bisect marks first (as upstream's texts), then local branches before remote ones before tags. The shell's HEAD label is not a ref upstream
    // lists, and ahead/behind counts are not known here.
    private static string? Message(GitRevision revision, IReadOnlyList<RefLabel> labels)
    {
        List<RefLabel> refs =
        [
            .. labels.Where(label => label.Kind != RefKind.Head)
                .OrderBy(label => label.Kind switch
                {
                    RefKind.BisectGood or RefKind.BisectBad => 0,
                    RefKind.Branch => 1,
                    RefKind.RemoteBranch => 2,
                    _ => 3,
                })
                .ThenBy(label => label.Name, StringComparer.Ordinal),
        ];
        if (!revision.HasMultiLineMessage && refs.Count == 0)
        {
            return null;
        }

        StringBuilder text = new(new GitRevisionSummaryBuilder().BuildSummary(revision.Body)
                                 ?? revision.Subject + (revision.HasMultiLineMessage ? BodyNotLoaded : ""));
        if (refs.Count != 0)
        {
            if (text.Length != 0)
            {
                text.AppendLine().AppendLine();
            }

            foreach (RefLabel label in refs)
            {
                text.AppendLine(label.Kind switch
                {
                    RefKind.BisectGood => MarkedGood,
                    RefKind.BisectBad => MarkedBad,
                    _ => $"[{label.Name}]",
                });
            }
        }

        return text.ToString();
    }
}
