using System.Diagnostics;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitExtensions.Plugins.FindLargeFiles;

/// <summary>
///  Finds the blobs at least as big as a threshold in every commit of HEAD's history, with the date of the newest commit that
///  has each one and its size in the pack files: upstream <c>FindLargeFilesForm.FindLargeFilesFunction</c> and
///  <c>GetLargeFiles</c>, with the same git commands, run off the UI thread. Each change is reported through
///  <see cref="IProgress{T}"/>, so it reaches the list on the UI thread.
/// </summary>
public sealed class LargeFileFinder(IGitModule module, float threshold)
{
    private readonly Dictionary<string, GitObject> _found = [];

    /// <summary>
    ///  The commits of HEAD, newest first; read by <see cref="ReadRevisions"/>.
    /// </summary>
    public IReadOnlyList<string> Revisions { get; private set; } = [];

    /// <summary>
    ///  The progress bar's end: one step per commit and a tenth more for the pack files, as upstream.
    /// </summary>
    public int ProgressMaximum => (int)(Revisions.Count * 1.1f);

    public void ReadRevisions()
    {
        GitArgumentBuilder args = new("rev-list") { "HEAD" };
        Revisions = module.GitExecutable.GetOutput(args).Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    ///  Scans <see cref="Revisions"/>. <paramref name="added"/> gets each new file, <paramref name="changed"/> a file seen
    ///  again in an older commit or given its packed size, and <paramref name="progress"/> the progress bar value.
    /// </summary>
    public void Scan(IProgress<GitObject> added, IProgress<GitObject> changed, IProgress<int> progress,
        CancellationToken cancellationToken)
    {
        Dictionary<string, DateTime> commitDates = [];
        foreach (GitObject file in GetLargeFiles(progress, cancellationToken))
        {
            string commit = file.Commit.First();
            if (!commitDates.TryGetValue(commit, out DateTime date))
            {
                date = CommitDate(commit);
                commitDates.Add(commit, date);
            }

            if (!_found.TryGetValue(file.SHA, out GitObject? known))
            {
                file.LastCommitDate = date;
                _found.Add(file.SHA, file);
                added.Report(file);
            }
            else if (!known.Commit.Contains(commit))
            {
                if (known.LastCommitDate < date)
                {
                    known.LastCommitDate = date;
                }

                known.Commit.Add(commit);
                changed.Report(known);
            }
        }

        ReadPackedSizes(changed, progress, cancellationToken);
    }

    private IEnumerable<GitObject> GetLargeFiles(IProgress<int> progress, CancellationToken cancellationToken)
    {
        int thresholdSize = (int)(threshold * 1024 * 1024);
        for (int i = 0; i < Revisions.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(i);
            string revision = Revisions[i];
            GitArgumentBuilder args = new("ls-tree") { "-zrl", revision.Quote() };
            string[] objects = module.GitExecutable.GetOutput(args).Split('\0', StringSplitOptions.RemoveEmptyEntries);
            foreach (string objectData in objects)
            {
                // "100644 blob b17a497cdc6140aa3b9a681344522f44768165ac 2120195\tBin/Dictionaries/de-DE.dic"
                string[] pathAndData = objectData.Split('\t');
                string[] data = pathAndData[0].Split(' ', count: 4, StringSplitOptions.RemoveEmptyEntries);
                if (data[1] == "blob" && int.TryParse(data[3], out int size) && size >= thresholdSize)
                {
                    yield return new GitObject(data[2], pathAndData[1], size, revision);
                }
            }
        }
    }

    private DateTime CommitDate(string commit)
    {
        GitArgumentBuilder args = new("show") { "-s", commit, "--format=\"%ci\"" };
        string text = module.GitExecutable.GetOutput(args);
        if (DateTime.TryParse(text, out DateTime date))
        {
            return date;
        }

        Trace.WriteLine($"Could not parse date '{text}' for commit '{commit}'");
        return DateTime.MinValue;
    }

    private void ReadPackedSizes(IProgress<GitObject> changed, IProgress<int> progress,
        CancellationToken cancellationToken)
    {
        string packDirectory = module.ResolveGitInternalPath("objects/pack/");
        if (!Directory.Exists(packDirectory))
        {
            progress.Report(ProgressMaximum);
            return;
        }

        string[] packFiles = Directory.GetFiles(packDirectory, "pack-*.idx");
        int value = Revisions.Count;
        foreach (string pack in packFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GitArgumentBuilder args = new("verify-pack") { "-v", pack };
            string[] objects = module.GitExecutable.GetOutput(args).Split('\n');
            value += (int)(Revisions.Count * 0.1f / packFiles.Length);
            progress.Report(value);
            foreach (string line in objects.Where(line => line.Contains(" blob ")))
            {
                string[] fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (_found.TryGetValue(fields[0], out GitObject? known) && int.TryParse(fields[3], out int compressed))
                {
                    known.CompressedSizeInBytes = compressed;
                    changed.Report(known);
                }
            }
        }
    }
}
