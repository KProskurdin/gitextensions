using System.Text.RegularExpressions;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The kind of a lost object, as upstream's <c>FormVerify.LostObjectType</c>.
/// </summary>
public enum LostObjectType
{
    Commit,
    Blob,
    Tree,
    Tag,
    Other
}

/// <summary>
///  One object <c>git fsck-objects</c> reported, as upstream's <c>FormVerify.LostObject</c>: its id and diagnostics, and for
///  a commit or an annotated tag its author, subject, date and parent.
/// </summary>
public sealed partial class LostObject
{
    [GeneratedRegex(
        @"^(?<rawtype>(dangling|missing|unreachable) (?<objecttype>commit|blob|tree|tag)|warning in tree) (?<objectid>[a-f\d]{40})(.)*$",
        RegexOptions.ExplicitCapture)]
    private static partial Regex RawDataRegex { get; }

    [GeneratedRegex(@"^(?<author>[^\u001F]+)\u001F(?<subject>.*)\u001F(?<date>\d+)\u001F(?<first_parent>[^ ]+)?( .+)?$",
        RegexOptions.Singleline | RegexOptions.ExplicitCapture)]
    private static partial Regex LogRegex { get; }

    [GeneratedRegex(
        @"^object (?<parent>.+)\ntype commit\ntag (?<tagname>.+)\ntagger (?<author>.+) <.*> (?<date>.+) .*\n\n(?<subject>.*)\n",
        RegexOptions.Multiline | RegexOptions.ExplicitCapture)]
    private static partial Regex TagRegex { get; }

    // Upstream's _showCommandArgumentsFormat: {0} is the commits' hashes; author name, subject, committer date and parents.
    private static readonly string _showCommandArgumentsFormat = (ArgumentString)new GitArgumentBuilder("show")
    {
        "--quiet", "--pretty=format:\"%aN\u001F%s\u001F%ct\u001F%P\" {0}"
    };

    private static readonly string _tagCommandArgumentsFormat =
        (ArgumentString)new GitArgumentBuilder("cat-file") { "-p", "{0}" };

    private LostObject(LostObjectType objectType, string rawType, ObjectId objectId)
    {
        ObjectType = objectType;
        RawType = rawType;
        ObjectId = objectId;
    }

    public LostObjectType ObjectType { get; }

    /// <summary>
    ///  The lost object's id.
    /// </summary>
    public ObjectId ObjectId { get; }

    /// <summary>
    ///  The first parent of a commit, or the commit an annotated tag points at; zero for other objects.
    /// </summary>
    public ObjectId Parent { get; private set; }

    /// <summary>
    ///  The diagnostics and the object type, as fsck printed them (upstream's Type column).
    /// </summary>
    public string RawType { get; set; }

    public string? Author { get; private set; }

    public string? Subject { get; private set; }

    public DateTime? Date { get; private set; }

    /// <summary>
    ///  The tag name of an annotated tag.
    /// </summary>
    public string? TagName { get; private set; }

    /// <summary>
    ///  Commits and annotated tags, which upstream's "Show commits and annotated tags" lists; blobs, trees and the rest
    ///  are under "Show blobs and trees".
    /// </summary>
    public bool IsCommitOrTag => ObjectType is LostObjectType.Commit or LostObjectType.Tag;

    /// <summary>
    ///  Upstream's <c>GetCommitsMetadata</c>: one <c>git show</c> line per commit, in the order of <paramref name="commitIds"/>.
    /// </summary>
    public static string[] GetCommitsMetadata(IGitModule module, IEnumerable<string> commitIds)
        => module.GitExecutable
            .GetOutput(string.Format(_showCommandArgumentsFormat, string.Join(" ", commitIds)),
                outputEncoding: GitModule.LosslessEncoding)
            .Split('\n');

    /// <summary>
    ///  Upstream's <c>FillCommitData</c>: the author, subject, date and first parent from a <c>git show</c> line.
    /// </summary>
    public void FillCommitData(IGitModule module, string commitLog)
    {
        Match logPatternMatch = LogRegex.Match(commitLog);
        if (!logPatternMatch.Success)
        {
            return;
        }

        Author = module.ReEncodeStringFromLossless(logPatternMatch.Groups["author"].Value);
        Subject = module.ReEncodeCommitMessage(logPatternMatch.Groups["subject"].Value);
        Date = DateTimeUtils.ParseUnixTime(logPatternMatch.Groups["date"].Value);
        string firstParent = logPatternMatch.Groups["first_parent"].Value;
        if (!string.IsNullOrEmpty(firstParent))
        {
            Parent = ObjectId.Parse(firstParent);
        }
    }

    /// <summary>
    ///  Upstream's <c>TryParse</c>: a line of fsck's output, with an annotated tag's data read at once and a blob's date
    ///  from its loose object file; null for a line in another format. A commit's data is read later in batches
    ///  (<see cref="FillCommitData"/>).
    /// </summary>
    public static LostObject? TryParse(IGitModule module, string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            throw new ArgumentException("Raw source must be non-empty string", nameof(raw));
        }

        Match patternMatch = RawDataRegex.Match(raw);
        if (!patternMatch.Success)
        {
            return null;
        }

        LostObjectType objectType = GetObjectType(patternMatch.Groups["objecttype"]);
        ObjectId objectId = ObjectId.Parse(raw, patternMatch.Groups["objectid"]);
        LostObject result = new(objectType, patternMatch.Groups["rawtype"].Value, objectId);

        if (objectType == LostObjectType.Tag)
        {
            string tagData = module.GitExecutable.GetOutput(string.Format(_tagCommandArgumentsFormat, objectId),
                outputEncoding: GitModule.LosslessEncoding);
            Match tagPatternMatch = TagRegex.Match(tagData);
            if (tagPatternMatch.Success)
            {
                result.Parent = ObjectId.Parse(tagData, tagPatternMatch.Groups["parent"]);
                result.Author = module.ReEncodeStringFromLossless(tagPatternMatch.Groups["author"].Value);
                result.TagName = tagPatternMatch.Groups["tagname"].Value;
                result.Subject = $"{result.TagName}:{tagPatternMatch.Groups["subject"].ValueSpan}";
                result.Date = DateTimeUtils.ParseUnixTime(tagPatternMatch.Groups["date"].Value);
            }
        }
        else if (objectType == LostObjectType.Blob)
        {
            ReadOnlySpan<char> hash = objectId.ToString().AsSpan();
            string blobPath = Path.Join(module.WorkingDirGitDir, "objects", hash[..2], hash[2..ObjectId.Sha1CharCount]);
            result.Date = new FileInfo(blobPath).CreationTime;
        }

        return result;

        static LostObjectType GetObjectType(Group matchedGroup)
            => !matchedGroup.Success
                ? LostObjectType.Other
                : matchedGroup.ValueSpan switch
                {
                    "commit" => LostObjectType.Commit,
                    "blob" => LostObjectType.Blob,
                    "tree" => LostObjectType.Tree,
                    "tag" => LostObjectType.Tag,
                    _ => LostObjectType.Other
                };
    }
}

/// <summary>
///  Upstream's <c>FormVerify</c> without its window: the fsck options of its check boxes, the lost objects they find, the
///  guess of a blob's file type, and the LOST_FOUND_ tags that recover objects.
/// </summary>
public static class VerifyDatabase
{
    /// <summary>
    ///  The prefix of the tags that recover lost objects (upstream's <c>_restoredObjectsTagPrefix</c>).
    /// </summary>
    public const string RestoredObjectsTagPrefix = "LOST_FOUND_";

    // Upstream's texts.
    public const string RemoveDanglingObjectsCaption = "Remove";
    public const string RemoveDanglingObjectsQuestion = "Are you sure you want to delete all dangling objects?";
    public const string XTagsCreated = "{0} Tags created.\n\nDo not forget to delete these tags when finished.";
    public const string TagsCreatedCaption = "Tags created";
    public const string SelectLostObjectsToRestoreMessage = "Select objects to restore.";
    public const string SelectLostObjectsToRestoreCaption = "Restore lost objects";
    public const string Seemingly = "seemingly";
    public const string CommitPatchFileName = "commit.patch";
    public const string OtherObjectFileName = "file.txt";

    // Upstream's batch size: within the longest command line, with a margin, a hash and a space each.
    private const int _metadataBatchSize = 30_000 / (ObjectId.Sha1CharCount + 1);

    // Upstream's _languagesStartOfFile (https://en.wikipedia.org/wiki/List_of_file_signatures), in its order: the first
    // start that matches wins.
    private static readonly (string Start, string Type)[] _languagesStartOfFile =
    [
        (@"{\rtf", "rtf"),
        ("{", "json"),
        ("#include", "cpp"),
        ("import {", "js"),
        ("import * as", "js"),
        ("import \"", "js"),
        ("export ", "js"),
        ("import ", "java"),
        ("from", "py"),
        ("package", "go"),
        ("namespace ", "fs"),
        ("#!", "sh"),
        ("[", "ini"),
        ("using ", "cs"),
        ("# ", "md"),
        ("##", "md"),
        ("<!doctype html", "html"),
        ("<html", "html"),
        ("<?xml", "xml"),
        ("use ", "rs"),
        ("%PDF", "pdf"),
        ("PK", "zip"),
        ("MZ", "exe"),
        (@"\document", "tex"),
        ("\u0089PNG", "png"),
        ("ÿØÿQ", "jp2"),
        ("ÿØÿ", "jpg"),
        ("ÿ\n", "jxl"),
        ("RIFF", "webp"),
        ("<svg", "svg"),
        ("BM", "bmp"),
        ("7z", "7z"),
        ("GIF", "gif"),
        ("ÐÏ\u0011à¡±\u001Aá", "doc"),
        ("qoif", "qoi"),
        ("Rar!", "rar"),
        ("%!PS", "ps"),
        ("OggS", "ogg"),
        ("8BPS", "psf"),
        ("ID3", "mp3"),
        ("CD001", "iso"),
        ("fLaC", "flac"),
        ("FLIF", "flif"),
        ("␚Eß£", "mkv"),
        ("<", "xml"),
    ];

    // Upstream's _fileTypesEquivalences: the other extensions offered when saving a blob of a guessed type.
    private static readonly Dictionary<string, string[]> _fileTypesEquivalences = new()
    {
        { "js", ["ts", "jsx", "tsx"] },
        { "html", ["php", "cshtml"] },
        { "cpp", ["c"] },
        { "xml", ["config", "settings", "csproj", "xlf", "props"] },
        { "zip", ["docx", "xlsx", "pptx", "odt", "ods", "odp", "epub", "jar", "msix"] },
        { "exe", ["dll"] },
        { "doc", ["xls", "ppt", "msi"] },
        { "md", ["sh", "yml"] },
        { "txt", ["csv", "css", "md", "yml"] },
    };

    /// <summary>
    ///  Upstream's <c>GetOptions</c>: the fsck options of the check boxes, each with a leading space.
    /// </summary>
    public static string Options(bool unreachable, bool fullCheck, bool noReflogs)
        => (unreachable ? " --unreachable" : "") + (fullCheck ? " --full" : "") + (noReflogs ? " --no-reflogs" : "");

    /// <summary>
    ///  The command of upstream's "Save objects to .git/lost-found".
    /// </summary>
    public static string SaveObjectsArguments(string options) => $"fsck-objects --lost-found{options}";

    /// <summary>
    ///  The command of upstream's "Remove all dangling objects".
    /// </summary>
    public const string RemoveArguments = "prune";

    /// <summary>
    ///  Upstream's <c>UpdateLostObjects</c>: <c>git fsck-objects</c> with <paramref name="options"/>, its lines parsed,
    ///  newest first, and the commits' data read in batches.
    /// </summary>
    public static IReadOnlyList<LostObject> Load(IGitModule module, string options)
    {
        string output = module.GitExecutable.GetOutput($"fsck-objects{options}");
        List<LostObject> lostObjects =
        [
            .. output.Split(Delimiters.LineFeedAndCarriageReturn)
                .Where(line => !string.IsNullOrEmpty(line))
                .Select(line => LostObject.TryParse(module, line))
                .WhereNotNull()
                .OrderByDescending(lostObject => lostObject.Date)
        ];

        LostObject[] commits = [.. lostObjects.Where(o => o.ObjectType == LostObjectType.Commit)];
        List<string> metadata = new(commits.Length);
        for (int batch = 0; batch * _metadataBatchSize < commits.Length; ++batch)
        {
            metadata.AddRange(LostObject.GetCommitsMetadata(module,
                commits.Skip(batch * _metadataBatchSize).Take(_metadataBatchSize).Select(c => c.ObjectId.ToString())));
        }

        for (int i = 0; i < commits.Length && i < metadata.Count; i++)
        {
            commits[i].FillCommitData(module, metadata[i]);
        }

        return lostObjects;
    }

    /// <summary>
    ///  Upstream's <c>IsMatchToFilter</c>: whether the two "Show ..." check boxes let the object through.
    /// </summary>
    public static bool IsShown(LostObject lostObject, bool showCommitsAndTags, bool showOtherObjects)
        => lostObject.IsCommitOrTag ? showCommitsAndTags : showOtherObjects;

    /// <summary>
    ///  Upstream's <c>GetLostObjectContent</c>: <c>git show</c> of the object, a blob's bytes as they are.
    /// </summary>
    public static string Content(IGitModule module, LostObject lostObject)
        => module.ShowObject(lostObject.ObjectId, returnRaw: lostObject.ObjectType == LostObjectType.Blob) ?? "";

    /// <summary>
    ///  Upstream's <c>Warnings_SelectionChanged</c> file names: a patch for a commit or tag, a guessed name for a blob, and
    ///  a text file for the rest. The viewer picks its highlighting by it, and "Save as..." proposes it.
    /// </summary>
    public static string DefaultFileName(LostObject lostObject, string content)
        => lostObject.ObjectType switch
        {
            LostObjectType.Commit or LostObjectType.Tag => CommitPatchFileName,
            LostObjectType.Blob => GuessFileName(content, lostObject.ObjectId.ToString()),
            _ => OtherObjectFileName
        };

    /// <summary>
    ///  Upstream's <c>GuessFileTypeWithContent</c>: the extension whose known start the content has, else txt.
    /// </summary>
    public static string GuessFileType(string content)
        => _languagesStartOfFile
            .FirstOrDefault(pair => content.StartsWith(pair.Start, StringComparison.OrdinalIgnoreCase))
            .Type ?? "txt";

    /// <summary>
    ///  Upstream's <c>GuessFileNameWithContent</c>.
    /// </summary>
    public static string GuessFileName(string content, string hash)
        => $"{RestoredObjectsTagPrefix}{hash}.{GuessFileType(content)}";

    /// <summary>
    ///  Upstream's type detection when blobs are shown first: " (seemingly: type)" after a blob's diagnostics.
    /// </summary>
    public static string WithSeemingType(string rawType, string content)
        => $"{rawType} ({Seemingly}: {GuessFileType(content)})";

    /// <summary>
    ///  Upstream's <c>saveAsToolStripMenuItem_Click</c> file name: the guessed one, else the hash with a text extension.
    /// </summary>
    public static string SaveAsFileName(LostObject lostObject, string? defaultFileName)
        => defaultFileName ?? lostObject.ObjectId + "_LOST_FOUND.txt";

    /// <summary>
    ///  The extensions upstream's "Save as..." offers for a file name: its own, then the equivalent ones.
    /// </summary>
    public static IReadOnlyList<string> SaveAsExtensions(string fileName)
    {
        string extension = Path.GetExtension(fileName).TrimStart('.');
        return _fileTypesEquivalences.TryGetValue(extension, out string[]? types) ? [extension, .. types] : [extension];
    }

    /// <summary>
    ///  The name of the tag that recovers the <paramref name="number"/>th selected object: an annotated tag's own name, else
    ///  the number (upstream's <c>CreateLostFoundTags</c>).
    /// </summary>
    public static string LostFoundTagName(LostObject lostObject, int number)
        => RestoredObjectsTagPrefix + (lostObject.ObjectType == LostObjectType.Tag && lostObject.TagName is { } tagName
            ? tagName
            : number.ToString());

    /// <summary>
    ///  Upstream's <c>CreateLostFoundTags</c>: a lightweight LOST_FOUND_ tag on each selected object; the count created.
    /// </summary>
    public static int CreateLostFoundTags(GitModule module, IReadOnlyList<LostObject> selected)
    {
        int number = 0;
        foreach (LostObject lostObject in selected)
        {
            number++;
            GitCreateTagArgs args = new(LostFoundTagName(lostObject, number), lostObject.ObjectId);
            _ = module.GitExecutable.RunCommand(
                Commands.CreateTag(args, tagMessageFileName: null, module.GetPathForGitExecution).Arguments);
        }

        return number;
    }

    /// <summary>
    ///  Upstream's <c>DeleteLostFoundTags</c>: every tag whose name starts with the LOST_FOUND_ prefix.
    /// </summary>
    public static void DeleteLostFoundTags(IGitModule module)
    {
        foreach (IGitRef head in module.GetRefs(RefsFilter.Tags))
        {
            if (head.Name.StartsWith(RestoredObjectsTagPrefix, StringComparison.Ordinal))
            {
                module.DeleteTag(head.Name);
            }
        }
    }
}
