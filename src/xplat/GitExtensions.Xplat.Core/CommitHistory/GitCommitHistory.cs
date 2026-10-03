using System.Reactive;
using GitCommands;
using GitCommands.Git;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Reads commits with the shared git engine. Each call runs on the thread pool.
/// </summary>
public sealed class GitCommitHistory : ICommitHistory
{
    private const string DateFormat = "yyyy-MM-dd HH:mm";

    public Task<CommitPage> LoadPageAsync(string repositoryPath, int limit)
        => Task.Run(() => LoadPage(repositoryPath, limit));

    public Task<CommitDetails> LoadDetailsAsync(string repositoryPath, string hash)
        => Task.Run(() => LoadDetails(repositoryPath, hash));

    private static CommitPage LoadPage(string path, int limit)
    {
        GitModule module = CreateModule(path);
        if (!module.IsValidGitWorkingDir())
        {
            throw new InvalidOperationException($"Not a git repository: {path}");
        }

        // Reading stops once one commit beyond the limit is seen, so a long history is not loaded in full.
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

    private static CommitDetails LoadDetails(string path, string hash)
    {
        GitRevision revision = new RevisionReader(CreateModule(path))
            .GetRevision(hash, hasNotes: false, throwOnError: true, cancellationToken: CancellationToken.None)
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
