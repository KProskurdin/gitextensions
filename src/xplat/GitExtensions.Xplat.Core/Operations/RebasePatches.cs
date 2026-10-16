using System.Net.Mail;
using System.Text.RegularExpressions;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  A row of upstream's rebase commit list (<c>PatchGrid</c>'s <c>PatchFile</c>): a commit of an interactive rebase's todo
///  and done lists, or a patch file of an apply based rebase.
/// </summary>
public sealed class RebasePatch
{
    // Upstream's status texts.
    public const string SkippedText = "Skipped";
    public const string AppliedText = "Applied";
    public const string ApplyingText = "Applying...";

    public string? FullName { get; init; }

    public string? Action { get; init; }

    public string? Name { get; init; }

    public ObjectId ObjectId { get; init; }

    public string? Author { get; set; }

    public string? Subject { get; set; }

    public string? Date { get; set; }

    public bool IsNext { get; init; }

    public bool IsSkipped { get; set; }

    public bool IsApplied { get; init; }

    /// <summary>
    ///  Upstream's short hash column.
    /// </summary>
    public string ShortHash => ObjectId.IsZero ? "" : ObjectId.ToShortString();

    public string Status
        => IsSkipped ? SkippedText
            : IsApplied ? AppliedText
            : IsNext ? ApplyingText
            : !string.IsNullOrEmpty(FullName) && !File.Exists(FullName) ? AppliedText
            : "";
}

/// <summary>
///  Upstream's <c>PatchGrid</c> reading of a rebase in progress (<c>IsManagingRebase</c>): the commits of the todo and
///  done lists of an interactive (merge based) rebase, or the numbered patch files of an apply based one, with the one
///  being applied and the ones applied or skipped.
/// </summary>
public static partial class RebasePatches
{
    private const string StoppedShaFile = "stopped-sha";
    private const string DoneFile = "done";
    private const string TodoFile = "git-rebase-todo";
    private const string NextFile = "next";
    private const int MinimumTodoColumns = 3;
    private static readonly TimeSpan _rangeTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    ///  Upstream's static <c>Skipped</c> list of FormRebase: the commits skipped in the rebase in progress, kept across the
    ///  dialog's openings and cleared when a rebase starts or is aborted.
    /// </summary>
    public static List<RebasePatch> Skipped { get; } = [];

    [GeneratedRegex(@"^(?<header_key>[-A-Za-z0-9]+)(?::[ \t]*)(?<header_value>.*)$", RegexOptions.ExplicitCapture)]
    private static partial Regex HeadersRegex { get; }

    [GeneratedRegex(@"=\?(?<qr1>[\w-]+)\?q\?(<qr2>.*)\?=$", RegexOptions.ExplicitCapture)]
    private static partial Regex QuotedRegex { get; }

    /// <summary>
    ///  Upstream's <c>GetPatches</c>: the rows, with the ones before the current one that were skipped marked.
    /// </summary>
    public static IReadOnlyList<RebasePatch> Read(GitModule module, IReadOnlyCollection<RebasePatch> skipped)
    {
        IReadOnlyList<RebasePatch> patches = File.Exists(module.GetRebaseDir() + TodoFile)
            ? InteractiveRebasePatches(module)
            : RebasePatchFiles(module.GetRebaseDir());
        foreach (RebasePatch patch in patches.TakeWhile(patch => !patch.IsNext)
                     .Where(patch => skipped.Any(s => patch.ObjectId == s.ObjectId && patch.Name == s.Name)))
        {
            patch.IsSkipped = true;
        }

        return patches;
    }

    // Upstream's GetInteractiveRebasePatchFiles.
    private static List<RebasePatch> InteractiveRebasePatches(GitModule module)
    {
        string rebaseDir = module.GetRebaseDir();
        string commentChar = module.GetEffectiveSetting("core.commentchar", defaultValue: "#");

        // Upstream keeps the lines that are not comments and have the action, the hash and a subject.
        string[][] commitsInfos =
        [
            .. ReadLines(rebaseDir + DoneFile).Concat(ReadLines(rebaseDir + TodoFile))
                .Where(line => !line.StartsWith(commentChar))
                .Select(line => line.Split(Delimiters.Space))
                .Where(parts => parts.Length >= MinimumTodoColumns)
        ];
        List<RebasePatch> patches = [];
        if (commitsInfos.Length == 0)
        {
            return patches;
        }

        CommitDataManager commitDataManager = new(() => module);
        Dictionary<string, GitRevision> revisions;
        try
        {
            using CancellationTokenSource cancellation = new(_rangeTimeout);
            revisions = new RevisionReader(module, allBodies: true)
                .GetRevisionsFromRange(commitsInfos[0][1], commitsInfos[^1][1], cancellation.Token)
                .ToDictionary(revision => revision.Guid, revision => revision);
        }
        catch (OperationCanceledException)
        {
            // As upstream, the commits are read one by one when the range cannot be.
            revisions = [];
        }

        string stoppedShaPath = rebaseDir + StoppedShaFile;
        string? currentShortHash = File.Exists(stoppedShaPath) ? File.ReadAllText(stoppedShaPath).Trim() : null;
        bool isCurrentFound = false;
        foreach (string[] parts in commitsInfos)
        {
            string hash = parts[1];
            CommitData? data = revisions.TryGetValue(hash, out GitRevision? revision)
                ? commitDataManager.CreateFromRevision(revision, null)
                : commitDataManager.GetCommitData(hash);
            bool isApplying = currentShortHash is not null && hash.StartsWith(currentShortHash);
            isCurrentFound |= isApplying;

            ObjectId objectId;
            if (data is not null)
            {
                objectId = data.ObjectId;
            }
            else if (!ObjectId.TryParse(hash, out objectId))
            {
                continue;
            }

            // As upstream, the subject column shows the commit's body.
            patches.Add(new RebasePatch
            {
                Action = parts[0],
                ObjectId = objectId,
                Subject = data?.Body ?? string.Join(' ', parts.Skip(2)),
                Author = data?.Author,
                Date = data?.CommitDate.LocalDateTime.ToString(),
                IsNext = isApplying,
                IsApplied = !isCurrentFound,
            });
        }

        return patches;

        static string[] ReadLines(string path)
            => File.Exists(path)
                ? File.ReadAllText(path).Trim().Split(Delimiters.LineFeedAndCarriageReturn,
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                : [];
    }

    // Upstream's GetRebasePatchFiles: the numbered files in the rebase folder with their mail headers.
    private static List<RebasePatch> RebasePatchFiles(string rebaseDir)
    {
        string nextPath = rebaseDir + NextFile;
        if (!int.TryParse(File.Exists(nextPath) ? File.ReadAllText(nextPath).Trim() : "", out int next))
        {
            next = 0;
        }

        List<RebasePatch> patches = [];
        foreach (string fullFileName in Directory.Exists(rebaseDir) ? Directory.GetFiles(rebaseDir) : [])
        {
            string file = PathUtil.GetFileName(fullFileName);
            if (!int.TryParse(file, out int number))
            {
                continue;
            }

            RebasePatch patch = new()
            {
                Name = file,
                FullName = fullFileName,
                IsApplied = number < next,
                IsNext = number == next,
            };
            ReadHeaders(rebaseDir + file, patch);
            patches.Add(patch);
        }

        return patches;
    }

    private static void ReadHeaders(string path, RebasePatch patch)
    {
        if (!File.Exists(path))
        {
            return;
        }

        string? key = null;
        string value = "";
        foreach (string line in File.ReadLines(path))
        {
            Match match = HeadersRegex.Match(line);
            if (key is null)
            {
                if (!string.IsNullOrWhiteSpace(line) && !match.Success)
                {
                    continue;
                }
            }
            else if (string.IsNullOrWhiteSpace(line) || match.Success)
            {
                // Upstream decodes quoted printable text with .NET's mail decoder.
                value = Attachment.CreateAttachmentFromString("", value).Name!;
                switch (key)
                {
                    case "From":
                        if (value.IndexOf('<') > 0 && value.IndexOf('<') < value.Length)
                        {
                            string author = RFC2047Decoder.Parse(value);
                            patch.Author = author[..author.IndexOf('<')].Trim();
                        }
                        else
                        {
                            patch.Author = value;
                        }

                        break;
                    case "Date":
                        patch.Date = value.IndexOf('+') > 0 && value.IndexOf('<') < value.Length
                            ? value[..value.IndexOf('+')].Trim()
                            : value;
                        break;
                    case "Subject":
                        patch.Subject = value;
                        break;
                }
            }

            if (match.Success)
            {
                key = match.Groups["header_key"].Value;
                value = match.Groups["header_value"].Value;
            }
            else if (!string.IsNullOrEmpty(line))
            {
                value = AppendQuotedString(value, line.Trim());
            }

            if (string.IsNullOrEmpty(line)
                || (!string.IsNullOrEmpty(patch.Author) && !string.IsNullOrEmpty(patch.Date)
                                                        && !string.IsNullOrEmpty(patch.Subject)))
            {
                break;
            }
        }

        static string AppendQuotedString(string str1, string str2)
        {
            Match m1 = QuotedRegex.Match(str1);
            Match m2 = QuotedRegex.Match(str2);
            return !m1.Success || !m2.Success ? str1 + str2 : $"{str1.AsSpan()[..^2]}{m2.Groups["qr2"].ValueSpan}?=";
        }
    }
}
