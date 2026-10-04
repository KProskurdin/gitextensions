using System.Reactive;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Xplat.Core.Operations;
using GitExtUtils;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Reads commits with the shared git engine. Each call runs on the thread pool.
/// </summary>
public sealed class GitCommitHistory : ICommitHistory
{
    private const string DateFormat = "yyyy-MM-dd HH:mm";

    public Task<CommitPage> LoadPageAsync(string repositoryPath, int limit)
        => Task.Run(() => ReadPage(repositoryPath, "HEAD", pathFilter: "", limit));

    public Task<CommitDetails> LoadDetailsAsync(string repositoryPath, string hash)
        => Task.Run(() => LoadDetails(repositoryPath, hash));

    public Task<IReadOnlyList<CommitFile>> LoadFilesAsync(string repositoryPath, string hash)
        => Task.Run(() => LoadFiles(repositoryPath, hash));

    public Task<IReadOnlyList<string>> LoadTreeAsync(string repositoryPath, string hash)
        => Task.Run(() => LoadTree(repositoryPath, hash));

    public Task<CommitPage> LoadFileHistoryAsync(string repositoryPath, string hash, string filePath, int limit)
        => Task.Run(() => ReadPage(repositoryPath, hash, pathFilter: filePath.Quote(), limit));

    public Task<IReadOnlyList<BlameLine>> LoadBlameAsync(string repositoryPath, string hash, string filePath)
        => Task.Run(() => LoadBlame(repositoryPath, hash, filePath));

    private static IReadOnlyList<BlameLine> LoadBlame(string path, string hash, string filePath)
    {
        ExecutionResult result = CreateModule(path).GitExecutable.Execute(
            new GitArgumentBuilder("blame") { "--line-porcelain", hash, "--", filePath.Quote() },
            throwOnErrorExit: false);
        if (!result.ExitedSuccessfully)
        {
            throw new GitOperationException(result.StandardError.Trim());
        }

        return BlameParser.Parse(result.StandardOutput);
    }

    private static IReadOnlyList<CommitFile> LoadFiles(string path, string hash)
    {
        ExecutionResult result = CreateModule(path).GitExecutable.Execute(
            new GitArgumentBuilder("show") { "--format=", "--name-status", "--no-color", hash },
            throwOnErrorExit: false);
        if (!result.ExitedSuccessfully)
        {
            throw new GitOperationException(result.StandardError.Trim());
        }

        return result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\t'))
            .Where(fields => fields.Length >= 2)
            .Select(fields => new CommitFile(fields[0][..1], fields[^1].TrimEnd('\r')))
            .ToList();
    }

    private static IReadOnlyList<string> LoadTree(string path, string hash)
    {
        ExecutionResult result = CreateModule(path).GitExecutable.Execute(
            new GitArgumentBuilder("ls-tree") { "-r", "--name-only", "-z", hash },
            throwOnErrorExit: false);
        if (!result.ExitedSuccessfully)
        {
            throw new GitOperationException(result.StandardError.Trim());
        }

        return result.StandardOutput
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    ///  Reads up to <paramref name="limit"/> commits reachable from <paramref name="revision"/>, optionally only those
    ///  that touched <paramref name="pathFilter"/>. Reading stops once one commit beyond the limit is seen, so a long
    ///  history is not loaded in full.
    /// </summary>
    private static CommitPage ReadPage(string path, string revision, string pathFilter, int limit)
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
            new RevisionReader(module).GetLog(observer, revisionFilter: revision, pathFilter: pathFilter,
                hasNotes: false,
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
