using GitExtensions.Extensibility.Git;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Upstream's <see cref="GitRevision"/> of a grid row, for the code that takes upstream's model, such as plugins through
///  <c>IBrowseRepo</c>.
/// </summary>
public static class GitRevisions
{
    /// <summary>
    ///  The revision of <paramref name="row"/> with what the grid read: parents, subject and body, author, dates, notes and
    ///  a stash's reflog selector. The refs are not filled in, as the grid keeps them as labels.
    /// </summary>
    public static GitRevision Of(CommitRow row) => new(ObjectId.Parse(row.Hash))
    {
        ParentIds = [.. (row.ParentHashes ?? []).Select(ObjectId.Parse)],
        Subject = row.Subject,
        Body = row.Body,
        HasMultiLineMessage = row.HasMultiLineMessage,
        Author = row.Author,
        AuthorEmail = row.AuthorEmail,
        AuthorUnixTime = UnixTime(row.AuthorDateValue),
        CommitUnixTime = UnixTime(row.CommitDateValue),
        Notes = row.Notes,
        ReflogSelector = row.ReflogSelector,
    };

    private static long UnixTime(DateTime? date) => date is { } value ? new DateTimeOffset(value).ToUnixTimeSeconds() : 0;
}
