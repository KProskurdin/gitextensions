using System.Reactive;
using GitCommands;
using GitCommands.ExternalLinks;
using GitCommands.Git;
using GitCommands.Remotes;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;
using GitExtUtils;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Reads commits with the shared git engine. Each call runs on the thread pool.
/// </summary>
public sealed class GitCommitHistory : ICommitHistory
{
    private const string DateFormat = "yyyy-MM-dd HH:mm";

    private readonly Func<CommitDateStyle> _dateStyle;
    private readonly Func<RevisionSortOrder> _sortOrder;
    private readonly IRevisionLinkStore? _links;

    /// <summary>
    ///  Reads commits; the grid's dates follow <paramref name="dateStyle"/> and the order <paramref name="sortOrder"/>
    ///  (upstream's <c>RevisionSortOrder</c>), asked at each read so a changed setting applies to the next reload
    ///  (upstream's defaults when none is given). The details list the revision links defined in <paramref name="links"/>.
    /// </summary>
    public GitCommitHistory(Func<CommitDateStyle>? dateStyle = null, Func<RevisionSortOrder>? sortOrder = null,
        IRevisionLinkStore? links = null)
    {
        _links = links;
        _dateStyle = dateStyle ?? (() => new CommitDateStyle());
        _sortOrder = sortOrder ?? (() => RevisionSortOrder.GitDefault);
    }

    public Task<CommitPage> LoadPageAsync(string repositoryPath, int limit, RevisionFilter? filter = null)
    {
        CommitDateStyle style = _dateStyle();
        string revision = Sorted(filter?.ToRevisionArguments() ?? "HEAD");
        return Task.Run(() => ReadPage(repositoryPath, revision,
            pathFilter: filter is { PathFilter.Length: > 0 } ? filter.PathFilter.Quote() : "", limit, markHead: true,
            style));
    }

    public Task<CommitDetails> LoadDetailsAsync(string repositoryPath, string hash)
        => Task.Run(() => LoadDetails(repositoryPath, hash));

    public Task<IReadOnlyList<CommitFile>> LoadFilesAsync(string repositoryPath, string hash)
        => Task.Run(() => LoadFiles(repositoryPath, hash));

    public Task<IReadOnlyList<string>> LoadTreeAsync(string repositoryPath, string hash)
        => Task.Run(() => LoadTree(repositoryPath, hash));

    public Task<CommitPage> LoadFileHistoryAsync(string repositoryPath, string hash, string filePath, int limit)
    {
        CommitDateStyle style = _dateStyle();
        string revision = Sorted(hash);
        return Task.Run(() => ReadPage(repositoryPath, revision, pathFilter: filePath.Quote(), limit, markHead: false,
            style));
    }

    public Task<CommitPage> SearchAsync(string repositoryPath, string text, int limit)
    {
        CommitDateStyle style = _dateStyle();
        string sorting = RevisionSorting.Argument(_sortOrder());
        return Task.Run(() => ReadSearch(repositoryPath, text, limit, style, sorting));
    }

    // Upstream's revision reader adds the sort flag of upstream's own setting before the revision; the preference's flag
    // goes after it, and git uses the last one, so the preference applies (the same value in the app).
    private string Sorted(string revision)
        => RevisionSorting.Argument(_sortOrder()) is { Length: > 0 } flag ? $"{flag} {revision}" : revision;

    // git's %at and %ct: seconds since the epoch, shown in local time as the revision reader gives its dates.
    private static DateTime FromUnixSeconds(string seconds)
        => long.TryParse(seconds, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out long value)
            ? DateTimeOffset.FromUnixTimeSeconds(value).LocalDateTime
            : DateTime.MinValue;

    private static CommitPage ReadSearch(string path, string text, int limit, CommitDateStyle style,
        string sorting)
    {
        ExecutionResult result = CreateModule(path).GitExecutable.Execute(
            new GitArgumentBuilder("log")
            {
                "-i",
                { sorting.Length > 0, sorting },
                ("--grep=" + text).Quote(),
                "-n",
                (limit + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--format=%H%x09%h%x09%an%x09%at%x09%ct%x09%s",
            },
            throwOnErrorExit: false);
        if (!result.ExitedSuccessfully)
        {
            throw new GitOperationException(result.StandardError.Trim());
        }

        DateTime now = DateTime.Now;
        List<CommitRow> rows = result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\t', 6))
            .Where(parts => parts.Length == 6)
            .Select(parts => new CommitRow(parts[0], parts[1], parts[5], parts[2],
                style.Format(FromUnixSeconds(parts[3]), FromUnixSeconds(parts[4]), now)))
            .ToList();

        return new CommitPage(rows.Take(limit).ToList(), HasMore: rows.Count > limit);
    }

    public Task<IReadOnlyList<BlameLine>> LoadBlameAsync(string repositoryPath, string hash, string filePath,
        BlameOptions? options = null)
    {
        IReadOnlyList<string> flags = (options ?? new BlameOptions()).Arguments;
        return Task.Run(() => LoadBlame(repositoryPath, hash, filePath, flags));
    }

    public Task<string?> LoadFileTextAsync(string repositoryPath, string hash, string filePath)
        => Task.Run(() => LoadFileText(repositoryPath, hash, filePath));

    // A NUL character marks a binary file, as git itself decides for diffs.
    private static string? LoadFileText(string path, string hash, string filePath)
    {
        ExecutionResult result = CreateModule(path).GitExecutable.Execute(
            new GitArgumentBuilder("show") { $"{hash}:{filePath}".Quote() }, throwOnErrorExit: false);
        if (!result.ExitedSuccessfully)
        {
            throw new GitOperationException(result.StandardError.Trim());
        }

        return result.StandardOutput.Contains('\0') ? null : result.StandardOutput;
    }

    private static IReadOnlyList<BlameLine> LoadBlame(string path, string hash, string filePath,
        IReadOnlyList<string> flags)
    {
        ExecutionResult result = CreateModule(path).GitExecutable.Execute(
            new GitArgumentBuilder("blame") { "--line-porcelain", flags, hash, "--", filePath.Quote() },
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
    private static CommitPage ReadPage(string path, string revision, string pathFilter, int limit, bool markHead,
        CommitDateStyle style)
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

        Dictionary<string, List<RefLabel>> labels = LoadRefLabels(module);
        string? head = markHead ? HeadHash(module) : null;
        DateTime now = DateTime.Now;
        IReadOnlyList<CommitRow> rows = revisions
            .Take(limit)
            .Select(r =>
            {
                List<RefLabel> refs = labels.TryGetValue(r.ObjectId.ToString(), out List<RefLabel>? found)
                    ? [.. found]
                    : [];
                if (r.ObjectId.ToString() == head)
                {
                    refs.Insert(0, new RefLabel("HEAD", RefKind.Head));
                }

                return new CommitRow(r.ObjectId.ToString(), r.ObjectId.ToShortString(), r.Subject, r.Author ?? "",
                    style.Format(r.AuthorDate, r.CommitDate, now),
                    r.ParentIds?.Select(id => id.ToString()).ToList() ?? [],
                    [.. refs.Where(label => !label.IsBisect).Select(label => label.Name)], refs, RevisionTooltips.For(r, refs));
            })
            .ToList();

        return new CommitPage(rows, HasMore: revisions.Count > limit);
    }

    // The commit HEAD points at, which gets the HEAD label wherever it is in the list; null before the first commit.
    private static string? HeadHash(GitModule module)
    {
        ObjectId head = module.GetCurrentCheckout();
        return head.IsZero ? null : head.ToString();
    }

    // Branch and tag labels by the commit they point at; an annotated tag points at the commit it was made on. Other refs
    // (the stash, notes) are not labels.
    private static Dictionary<string, List<RefLabel>> LoadRefLabels(GitModule module)
    {
        ExecutionResult result = module.GitExecutable.Execute(
            new GitArgumentBuilder("for-each-ref") { "--format=%(refname) %(objectname) %(*objectname)".Quote() },
            throwOnErrorExit: false);
        Dictionary<string, List<RefLabel>> labels = new();
        if (!result.ExitedSuccessfully)
        {
            return labels;
        }

        foreach (string line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.TrimEnd('\r').Split(' ');
            if (parts.Length < 2 || RefLabel.FromRefName(parts[0]) is not { } label)
            {
                continue;
            }

            // origin/HEAD only repeats the remote's default branch.
            if (label.Kind == RefKind.RemoteBranch && label.Name.EndsWith("/HEAD", StringComparison.Ordinal))
            {
                continue;
            }

            string commit = parts.Length > 2 && parts[2].Length > 0 ? parts[2] : parts[1];
            if (!labels.TryGetValue(commit, out List<RefLabel>? names))
            {
                labels[commit] = names = [];
            }

            names.Add(label);
        }

        return labels;
    }

    private CommitDetails LoadDetails(string path, string hash)
    {
        GitModule module = CreateModule(path);
        GitRevision revision = new RevisionReader(module)
                                   .GetRevision(hash, hasNotes: false, throwOnError: true,
                                       cancellationToken: CancellationToken.None)
                               ?? throw new InvalidOperationException($"Commit not found: {hash}");

        return new CommitDetails(
            Hash: revision.ObjectId.ToString(),
            Author: $"{revision.Author} <{revision.AuthorEmail}>",
            AuthorDate: revision.AuthorDate.ToString(DateFormat),
            CommitDate: revision.CommitDate.ToString(DateFormat),
            Parents: string.Join(", ", (revision.ParentIds ?? []).Select(parent => parent.ToShortString())),
            Message: (revision.Body ?? revision.Subject).TrimEnd(),
            Links: LoadLinks(module, revision, path));
    }

    // Upstream's CommitInfo "Related links": the definitions in effect for the repository, applied by upstream's parser to the
    // commit's message and to the branches that point at it, each address once.
    private IReadOnlyList<RevisionLink> LoadLinks(GitModule module, GitRevision revision, string path)
    {
        if (_links is null)
        {
            return [];
        }

        DistributedSettings settings = _links.Open(path);
        ConfiguredLinkDefinitionsProvider definitions = new(new ExternalLinksStorage());
        if (!definitions.Get(settings).Any(definition => definition.Enabled))
        {
            return [];
        }

        revision.Refs = [.. module.GetRefs(RefsFilter.Heads | RefsFilter.Remotes)
            .Where(reference => reference.ObjectId == revision.ObjectId)];
        GitRevisionExternalLinksParser parser = new(definitions,
            new ExternalLinkRevisionParser(new ConfigFileRemoteSettingsManager(() => module)));
        return [.. parser.Parse(revision, settings).Distinct().Select(link => new RevisionLink(link.Caption ?? link.Uri, link.Uri))];
    }

    private static GitModule CreateModule(string path)
        => new(new GitExecutorProvider(new GitDirectoryResolver()), path);
}
