using System.Reactive;
using GitCommands;
using GitCommands.Git;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Reads commits with the shared git engine. Methods block; callers run them off the UI thread.
/// </summary>
public static class CommitHistory
{
    public const int PageSize = 500;

    private const string DateFormat = "yyyy-MM-dd HH:mm";

    /// <summary>
    ///  Reads the first <paramref name="limit"/> commits reachable from HEAD. Reading stops as soon as one more commit is seen,
    ///  so a long history is not loaded in full. <see cref="CommitPage.HasMore"/> tells whether the history goes further.
    /// </summary>
    public static CommitPage LoadPage(string path, int limit)
    {
        GitModule module = CreateModule(path);
        if (!module.IsValidGitWorkingDir())
        {
            throw new InvalidOperationException($"Not a git repository: {path}");
        }

        CancellationTokenSource stopReading = new();
        List<GitRevision> revisions = [];
        IObserver<IReadOnlyList<GitRevision>> observer = Observer.Create<IReadOnlyList<GitRevision>>(batch =>
        {
            revisions.AddRange(batch);
            if (revisions.Count > limit)
            {
                stopReading.Cancel();
            }
        });

        try
        {
            new RevisionReader(module).GetLog(observer, revisionFilter: "HEAD", pathFilter: "", hasNotes: false,
                autostashLabel: "", cancellationToken: stopReading.Token);
        }
        catch (OperationCanceledException) when (stopReading.IsCancellationRequested)
        {
            // Expected: the page is full and the rest of the history was not read.
        }

        IReadOnlyList<CommitRow> rows = revisions
            .Take(limit)
            .Select(r => new CommitRow(r.ObjectId.ToString(), r.ObjectId.ToShortString(), r.Subject, r.Author ?? "",
                r.CommitDate.ToString(DateFormat)))
            .ToList();

        return new CommitPage(rows, HasMore: revisions.Count > limit);
    }

    public static CommitDetails LoadDetails(string path, string hash)
    {
        GitRevision revision = new RevisionReader(CreateModule(path))
                                   .GetRevision(hash, hasNotes: false, throwOnError: true,
                                       cancellationToken: CancellationToken.None)
                               ?? throw new InvalidOperationException($"Commit not found: {hash}");

        return new CommitDetails(
            Hash: revision.ObjectId.ToString(),
            Author: $"{revision.Author} <{revision.AuthorEmail}>",
            AuthorDate: revision.AuthorDate.ToString(DateFormat),
            CommitDate: revision.CommitDate.ToString(DateFormat),
            Parents: string.Join(", ", (revision.ParentIds ?? []).Select(parent => parent.ToShortString())),
            Message: (revision.Body ?? revision.Subject).TrimEnd());
    }

    private static GitModule CreateModule(string path)
        => new(new GitExecutorProvider(new GitDirectoryResolver()), path);
}

public sealed record CommitPage(IReadOnlyList<CommitRow> Rows, bool HasMore);

public sealed record CommitRow(string Hash, string ShortHash, string Subject, string Author, string Date);

public sealed record CommitDetails(
    string Hash,
    string Author,
    string AuthorDate,
    string CommitDate,
    string Parents,
    string Message);
